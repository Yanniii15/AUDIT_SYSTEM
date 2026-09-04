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

namespace AuditCkDayo.Controllers.Api
{
    [ApiController]
    [Route("api/branch")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BranchApiController : ControllerBase
    {
        private readonly AuditDbContext _context;

        public BranchApiController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet("deliveries")]
        public async Task<IActionResult> GetPendingDeliveries()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.EstablishmentId.HasValue)
            {
                return BadRequest(new { message = "User is not assigned to any establishment." });
            }

            var branchId = user.EstablishmentId.Value;

            var pendingDetails = await _context.AuditItemDetails
                .AsNoTracking()
                .Include(d => d.AuditItem)
                    .ThenInclude(a => a.Buyer)
                .Include(d => d.ExpenseSource)
                .Where(d => (d.AssignedEstablishmentId == branchId || (!d.AssignedEstablishmentId.HasValue && d.AuditItem.EstablishmentId == branchId))
                    && d.BranchVerificationStatus == BranchVerificationStatus.Pending
                    && d.AuditItem.Status != AuditStatus.Cancelled
                    && d.AuditItem.Status != AuditStatus.Rejected)
                .OrderByDescending(d => d.AuditItem.EntryDate)
                .ToListAsync();

            return Ok(pendingDetails.Select(d => new
            {
                d.Id,
                AuditItemId = d.AuditItemId,
                d.ItemName,
                d.Quantity,
                d.Price,
                d.Total,
                Source = !string.IsNullOrWhiteSpace(d.ExpenseSourceName) ? d.ExpenseSourceName : (d.ExpenseSource != null ? d.ExpenseSource.Name : "—"),
                Date = d.AuditItem.EntryDate.ToString("yyyy-MM-dd"),
                Buyer = new { d.AuditItem.Buyer.Id, d.AuditItem.Buyer.Name },
                HasReceipt = d.ReceiptStatus != ReceiptLineStatus.NoReceipt,
                ReceiptImageUrl = d.AuditItem.ReceiptImageUrl
            }));
        }

        [HttpPost("deliveries/{detailId}/verify")]
        public async Task<IActionResult> VerifyDelivery(int detailId)
        {
            var detail = await _context.AuditItemDetails
                .Include(d => d.AuditItem)
                .FirstOrDefaultAsync(d => d.Id == detailId);

            if (detail == null)
            {
                return NotFound(new { message = $"Delivery detail #{detailId} not found." });
            }

            detail.BranchVerificationStatus = BranchVerificationStatus.Verified;

            // Check if all details in this audit are now verified
            var allVerified = await _context.AuditItemDetails
                .Where(d => d.AuditItemId == detail.AuditItemId && d.Id != detailId)
                .AllAsync(d => d.BranchVerificationStatus == BranchVerificationStatus.Verified);

            if (allVerified && detail.AuditItem.Status == AuditStatus.AwaitingBranchVerification)
            {
                detail.AuditItem.Status = AuditStatus.AwaitingManagerApproval;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Detail #{detailId} verified successfully.", allVerified });
        }
    }
}
