using System;
using System.Collections.Generic;
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
    [Route("api/audits")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AuditsApiController : ControllerBase
    {
        private readonly AuditDbContext _context;

        public AuditsApiController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet("by-date")]
        public async Task<IActionResult> GetAuditsByDate([FromQuery] DateTime? date, [FromQuery] int? buyerId, [FromQuery] int? establishmentId)
        {
            DateTime targetDate;
            if (date.HasValue)
            {
                targetDate = date.Value.Date;
            }
            else
            {
                var hasAuditsToday = await _context.AuditItems.AnyAsync(a => a.EntryDate.Date == DateTime.Today);
                if (hasAuditsToday)
                {
                    targetDate = DateTime.Today;
                }
                else
                {
                    var recentWithAudits = await _context.AuditItems
                        .Where(a => a.EntryDate <= DateTime.Today)
                        .Select(a => (DateTime?)a.EntryDate)
                        .MaxAsync();
                    targetDate = recentWithAudits?.Date ?? await _context.AuditItems.Select(a => (DateTime?)a.EntryDate).MaxAsync() ?? DateTime.Today;
                }
            }

            var query = _context.AuditItems
                .AsNoTracking()
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Images)
                .Include(a => a.Details)
                    .ThenInclude(d => d.ExpenseSource)
                .Include(a => a.Details)
                    .ThenInclude(d => d.AssignedEstablishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.CostCenter)
                .Where(a => a.EntryDate.Date == targetDate);

            if (buyerId.HasValue && buyerId.Value > 0)
            {
                query = query.Where(a => a.BuyerId == buyerId.Value);
            }

            if (establishmentId.HasValue && establishmentId.Value > 0)
            {
                query = query.Where(a => a.EstablishmentId == establishmentId.Value);
            }

            var audits = await query
                .OrderByDescending(a => a.SubmittedAt ?? a.EntryDate)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            var recentDates = await _context.AuditItems
                .AsNoTracking()
                .Where(a => a.EntryDate <= DateTime.Today)
                .GroupBy(a => a.EntryDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Date)
                .Take(6)
                .ToListAsync();

            var totalReceiptPhotos = audits.Sum(a => a.Images.Count > 0 ? a.Images.Count : (!string.IsNullOrEmpty(a.ReceiptImageUrl) ? 1 : 0));

            return Ok(new
            {
                selectedDate = targetDate.ToString("yyyy-MM-dd"),
                stats = new
                {
                    totalAmount = audits.Sum(a => a.Amount),
                    totalCount = audits.Count,
                    totalReceiptPhotos,
                    totalLineItems = audits.Sum(a => a.Details.Count),
                    approvedCount = audits.Count(a => a.Status == AuditStatus.Approved),
                    pendingCount = audits.Count(a => a.Status != AuditStatus.Approved && a.Status != AuditStatus.Rejected && a.Status != AuditStatus.Cancelled)
                },
                recentActiveDates = recentDates.Select(rd => new
                {
                    date = rd.Date.ToString("yyyy-MM-dd"),
                    label = rd.Date.ToString("MMM d"),
                    count = rd.Count,
                    total = rd.Total
                }),
                audits = audits.Select(a => new
                {
                    a.Id,
                    a.Amount,
                    a.Description,
                    EntryDate = a.EntryDate.ToString("yyyy-MM-dd"),
                    SubmittedAt = a.SubmittedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Status = a.Status.ToString(),
                    a.Notes,
                    Buyer = new { a.Buyer.Id, a.Buyer.Name, a.Buyer.Email },
                    Establishment = new { a.Establishment.Id, a.Establishment.Name },
                    Images = a.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                    ReceiptImageUrl = a.ReceiptImageUrl,
                    Details = a.Details.Select(d => new
                    {
                        d.Id,
                        d.ItemName,
                        d.Quantity,
                        d.Price,
                        d.Total,
                        Source = !string.IsNullOrWhiteSpace(d.ExpenseSourceName) ? d.ExpenseSourceName : (d.ExpenseSource != null ? d.ExpenseSource.Name : "—"),
                        ExpenseSourceName = d.ExpenseSourceName,
                        ExpenseSourceId = d.ExpenseSourceId,
                        Allocation = !string.IsNullOrWhiteSpace(d.AllocationNotes) ? d.AllocationNotes : (d.AssignedEstablishment != null ? d.AssignedEstablishment.Name : (d.CostCenter != null ? d.CostCenter.Name : a.Establishment.Name)),
                        HasReceipt = d.ReceiptStatus != ReceiptLineStatus.NoReceipt,
                        Status = d.ReceiptStatus.ToString()
                    })
                })
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAuditById(int id)
        {
            var audit = await _context.AuditItems
                .AsNoTracking()
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Images)
                .Include(a => a.Details)
                    .ThenInclude(d => d.ExpenseSource)
                .Include(a => a.Details)
                    .ThenInclude(d => d.AssignedEstablishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.CostCenter)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound(new { message = $"Audit #{id} not found." });
            }

            return Ok(new
            {
                audit.Id,
                audit.Amount,
                audit.Description,
                EntryDate = audit.EntryDate.ToString("yyyy-MM-dd"),
                Status = audit.Status.ToString(),
                audit.Notes,
                Buyer = new { audit.Buyer.Id, audit.Buyer.Name },
                Establishment = new { audit.Establishment.Id, audit.Establishment.Name },
                Images = audit.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                ReceiptImageUrl = audit.ReceiptImageUrl,
                Details = audit.Details.Select(d => new
                {
                    d.Id,
                    d.ItemName,
                    d.Quantity,
                    d.Price,
                    d.Total,
                    d.ExpenseSourceId,
                    d.ExpenseSourceName,
                    d.AssignedEstablishmentId,
                    d.CostCenterId,
                    d.AllocationNotes,
                    d.ReceiptStatus
                })
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAudit(int id, [FromBody] UpdateAuditRequest request)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Details)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound(new { message = $"Audit #{id} not found." });
            }

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                audit.Description = request.Description.Trim();
            }
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                audit.Notes = request.Notes.Trim();
            }

            if (request.Details != null && request.Details.Count > 0)
            {
                foreach (var item in request.Details)
                {
                    var detail = audit.Details.FirstOrDefault(d => d.Id == item.Id);
                    if (detail != null)
                    {
                        if (!string.IsNullOrWhiteSpace(item.ItemName)) detail.ItemName = item.ItemName.Trim();
                        if (item.Quantity > 0) detail.Quantity = item.Quantity;
                        if (item.Price >= 0) detail.Price = item.Price;
                        detail.Total = detail.Quantity * detail.Price;
                        if (!string.IsNullOrWhiteSpace(item.ExpenseSourceName)) detail.ExpenseSourceName = item.ExpenseSourceName.Trim();
                        if (item.AssignedEstablishmentId.HasValue) detail.AssignedEstablishmentId = item.AssignedEstablishmentId.Value;
                        if (!string.IsNullOrWhiteSpace(item.AllocationNotes)) detail.AllocationNotes = item.AllocationNotes.Trim();
                    }
                }

                audit.Amount = audit.Details.Sum(d => d.Total);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Audit #{id} updated successfully.", audit.Amount });
        }
    }

    public class UpdateAuditRequest
    {
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public List<UpdateDetailItem>? Details { get; set; }
    }

    public class UpdateDetailItem
    {
        public int Id { get; set; }
        public string? ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string? ExpenseSourceName { get; set; }
        public int? AssignedEstablishmentId { get; set; }
        public string? AllocationNotes { get; set; }
    }
}
