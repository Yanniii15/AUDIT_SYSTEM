using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;

namespace AuditCkDayo.Controllers.Api
{
    [ApiController]
    [Route("api/manager")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ManagerApiController : ControllerBase
    {
        private readonly AuditDbContext _context;
        private readonly SharedPcfFundService _pcfFund;

        public ManagerApiController(AuditDbContext context, SharedPcfFundService? pcfFund = null)
        {
            _context = context;
            _pcfFund = pcfFund ?? new SharedPcfFundService(context);
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Establishment)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Unauthorized();
            }

            var availablePcf = await _pcfFund.GetAvailableBalanceAsync(user);
            var startingFloat = await _pcfFund.GetStartingFloatAsync(user);

            var today = DateTime.Today;
            var cashInToday = await _context.PcfReleases
                .AsNoTracking()
                .Where(r => r.ReceiverUserId == userId && r.ReleaseDate.Date == today && r.Status == PcfReleaseStatus.Released)
                .SumAsync(r => r.Amount);

            var cashOutToday = await _context.AuditItems
                .AsNoTracking()
                .Where(a => a.BuyerId == userId && a.EntryDate.Date == today && a.Status == AuditStatus.Approved)
                .SumAsync(a => a.Amount);

            var pendingApprovalsCount = await _context.AuditItems
                .AsNoTracking()
                .CountAsync(a => a.Status == AuditStatus.AwaitingManagerApproval && (a.AssignedReviewerId == userId || a.Buyer.ManagerId == userId || User.IsInRole("Owner") || User.IsInRole("Admin")));

            var pendingSurrendersCount = await _context.SurrenderRequests
                .AsNoTracking()
                .CountAsync(s => s.Status == SurrenderStatus.Pending && (s.AssignedReceiverId == userId || s.Buyer.ManagerId == userId || User.IsInRole("Owner") || User.IsInRole("Admin")));

            return Ok(new
            {
                currentPcf = availablePcf,
                startingFloat,
                cashInToday,
                cashOutToday,
                isSafeFloat = availablePcf > (startingFloat * 0.25m),
                pendingApprovalsCount,
                pendingSurrendersCount,
                branchName = user.Establishment?.Name ?? "Main Facility"
            });
        }

        [HttpGet("audit-queue")]
        public async Task<IActionResult> GetAuditQueue()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out var userId);

            var queue = await _context.AuditItems
                .AsNoTracking()
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Images)
                .Include(a => a.Details)
                    .ThenInclude(d => d.ExpenseSource)
                .Where(a => a.Status == AuditStatus.AwaitingManagerApproval && (a.AssignedReviewerId == userId || a.Buyer.ManagerId == userId || User.IsInRole("Owner") || User.IsInRole("Admin")))
                .OrderByDescending(a => a.SubmittedAt ?? a.EntryDate)
                .ToListAsync();

            return Ok(queue.Select(a => new
            {
                a.Id,
                a.Amount,
                a.Description,
                EntryDate = a.EntryDate.ToString("yyyy-MM-dd"),
                SubmittedAt = a.SubmittedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                Buyer = new { a.Buyer.Id, a.Buyer.Name, a.Buyer.Email },
                Establishment = new { a.Establishment.Id, a.Establishment.Name },
                ImageUrls = a.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                ReceiptImageUrl = a.ReceiptImageUrl,
                ItemCount = a.Details.Count,
                Details = a.Details.Select(d => new
                {
                    d.Id,
                    d.ItemName,
                    d.Quantity,
                    d.Price,
                    d.Total,
                    Source = !string.IsNullOrWhiteSpace(d.ExpenseSourceName) ? d.ExpenseSourceName : (d.ExpenseSource != null ? d.ExpenseSource.Name : "—"),
                    Allocation = !string.IsNullOrWhiteSpace(d.AllocationNotes) ? d.AllocationNotes : (d.AssignedEstablishment != null ? d.AssignedEstablishment.Name : a.Establishment.Name),
                    HasReceipt = d.ReceiptStatus != ReceiptLineStatus.NoReceipt
                })
            }));
        }

        [HttpPost("audits/{id}/approve")]
        public async Task<IActionResult> ApproveAudit(int id)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound(new { message = $"Audit #{id} not found." });
            }

            audit.Status = AuditStatus.Approved;
            audit.VerificationDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Audit #{id} approved successfully." });
        }

        [HttpPost("audits/{id}/reject")]
        public async Task<IActionResult> RejectAudit(int id, [FromBody] RejectRequest request)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound(new { message = $"Audit #{id} not found." });
            }

            audit.Status = AuditStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                audit.Notes = string.IsNullOrWhiteSpace(audit.Notes) ? $"[Rejected]: {request.Reason.Trim()}" : $"{audit.Notes}\n[Rejected]: {request.Reason.Trim()}";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Audit #{id} rejected." });
        }
    }

    public class RejectRequest
    {
        public string? Reason { get; set; }
    }
}
