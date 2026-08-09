using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly AuditDbContext _context;

    public ReportsController(AuditDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ReportsFilterViewModel filter)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            return Challenge();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (currentUser == null)
        {
            return Challenge();
        }

        var auditQuery = BuildScopedAuditQuery(role, userId, currentUser);
        auditQuery = ApplyAuditFilters(auditQuery, filter);

        var surrenderQuery = BuildScopedSurrenderQuery(role, userId);
        surrenderQuery = ApplySurrenderFilters(surrenderQuery, filter);

        var ledgerQuery = BuildScopedLedgerQuery(role, userId);
        ledgerQuery = ApplyLedgerFilters(ledgerQuery, filter);

        var cashUserQuery = BuildScopedCashUserQuery(role, userId);
        cashUserQuery = ApplyCashUserFilters(cashUserQuery, filter);

        var audits = await auditQuery
            .Include(a => a.Details)
            .OrderByDescending(a => a.EntryDate)
            .ThenByDescending(a => a.Id)
            .Take(25)
            .ToListAsync();

        var allAuditsForSummary = await auditQuery.ToListAsync();
        var surrenders = await surrenderQuery
            .OrderByDescending(s => s.RequestDate)
            .Take(20)
            .ToListAsync();

        var model = new ReportsViewModel
        {
            Filter = filter,
            Role = role,
            ScopeLabel = BuildScopeLabel(role, currentUser),
            RecentAudits = audits,
            SurrenderRequests = surrenders,
            LedgerEntries = await ledgerQuery
                .OrderByDescending(l => l.Timestamp)
                .Take(20)
                .ToListAsync(),
            CurrentCashBalance = await cashUserQuery.SumAsync(u => u.PcfBalance),
            TotalAuditAmount = allAuditsForSummary.Sum(a => a.Amount),
            ApprovedAuditAmount = allAuditsForSummary.Where(a => a.Status == AuditStatus.Approved).Sum(a => a.Amount),
            AuditCount = allAuditsForSummary.Count,
            ApprovedAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.Approved),
            PendingAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.AwaitingBranchVerification || a.Status == AuditStatus.AwaitingManagerApproval),
            RejectedAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.Rejected),
            PendingSurrenderAmount = await surrenderQuery.Where(s => s.Status == SurrenderStatus.Pending).SumAsync(s => s.DeclaredAmount),
            ConfirmedSurrenderAmount = await surrenderQuery.Where(s => s.Status == SurrenderStatus.Confirmed).SumAsync(s => s.ConfirmedAmount ?? s.DeclaredAmount)
        };

        model.StatusSummaries = allAuditsForSummary
            .GroupBy(a => a.Status.ToString())
            .Select(g => new ReportStatusSummary
            {
                Status = g.Key,
                Count = g.Count(),
                Amount = g.Sum(a => a.Amount)
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        model.EstablishmentSummaries = allAuditsForSummary
            .GroupBy(a => a.Establishment?.Name ?? "Unassigned")
            .Select(g => new ReportEstablishmentSummary
            {
                Establishment = g.Key,
                Count = g.Count(),
                Amount = g.Sum(a => a.Amount)
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        await PopulateFiltersAsync(filter, role, userId, currentUser);
        return View(model);
    }

    private IQueryable<AuditItem> BuildScopedAuditQuery(string role, int userId, User currentUser)
    {
        var query = _context.AuditItems
            .AsNoTracking()
            .Include(a => a.Buyer)
            .Include(a => a.Establishment)
            .AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(a => a.Buyer.ManagerId == userId);
        }
        else if (role == "Buyer")
        {
            query = query.Where(a => a.BuyerId == userId);
        }
        else if (role == "BranchStaff")
        {
            query = currentUser.EstablishmentId.HasValue
                ? query.Where(a => a.EstablishmentId == currentUser.EstablishmentId.Value)
                : query.Where(a => false);
        }

        return query;
    }

    private IQueryable<SurrenderRequest> BuildScopedSurrenderQuery(string role, int userId)
    {
        var query = _context.SurrenderRequests
            .AsNoTracking()
            .Include(s => s.Buyer)
            .AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(s => s.Buyer.ManagerId == userId);
        }
        else if (role == "Buyer")
        {
            query = query.Where(s => s.BuyerId == userId);
        }
        else if (role == "BranchStaff")
        {
            query = query.Where(s => false);
        }

        return query;
    }

    private IQueryable<PettyCashLedger> BuildScopedLedgerQuery(string role, int userId)
    {
        var query = _context.PettyCashLedgers
            .AsNoTracking()
            .Include(l => l.User)
            .Include(l => l.CounterpartyUser)
            .AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(l => l.UserId == userId || (l.User != null && l.User.ManagerId == userId));
        }
        else if (role == "Buyer")
        {
            query = query.Where(l => l.UserId == userId);
        }
        else if (role == "BranchStaff")
        {
            query = query.Where(l => false);
        }

        return query;
    }

    private IQueryable<User> BuildScopedCashUserQuery(string role, int userId)
    {
        var query = _context.Users.AsNoTracking().Where(u => !u.IsDeleted).AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(u => u.Id == userId || u.ManagerId == userId);
        }
        else if (role == "Buyer" || role == "BranchStaff")
        {
            query = query.Where(u => u.Id == userId);
        }

        return query;
    }

    private static IQueryable<AuditItem> ApplyAuditFilters(IQueryable<AuditItem> query, ReportsFilterViewModel filter)
    {
        if (filter.StartDate.HasValue)
        {
            query = query.Where(a => a.EntryDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(a => a.EntryDate < filter.EndDate.Value.AddDays(1));
        }
        if (filter.Status.HasValue)
        {
            query = query.Where(a => a.Status == filter.Status.Value);
        }
        if (filter.EstablishmentId.HasValue)
        {
            query = query.Where(a => a.EstablishmentId == filter.EstablishmentId.Value);
        }
        if (filter.BuyerId.HasValue)
        {
            query = query.Where(a => a.BuyerId == filter.BuyerId.Value);
        }

        return query;
    }

    private static IQueryable<SurrenderRequest> ApplySurrenderFilters(IQueryable<SurrenderRequest> query, ReportsFilterViewModel filter)
    {
        if (filter.StartDate.HasValue)
        {
            query = query.Where(s => s.RequestDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(s => s.RequestDate < filter.EndDate.Value.AddDays(1));
        }
        if (filter.BuyerId.HasValue)
        {
            query = query.Where(s => s.BuyerId == filter.BuyerId.Value);
        }

        return query;
    }

    private static IQueryable<PettyCashLedger> ApplyLedgerFilters(IQueryable<PettyCashLedger> query, ReportsFilterViewModel filter)
    {
        if (filter.StartDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(l => l.Timestamp < filter.EndDate.Value.AddDays(1));
        }
        if (filter.BuyerId.HasValue)
        {
            query = query.Where(l => l.UserId == filter.BuyerId.Value);
        }
        if (filter.EstablishmentId.HasValue)
        {
            query = query.Where(l => l.User != null && l.User.AuditItems.Any(a => a.EstablishmentId == filter.EstablishmentId.Value));
        }

        return query;
    }

    private static IQueryable<User> ApplyCashUserFilters(IQueryable<User> query, ReportsFilterViewModel filter)
    {
        if (filter.BuyerId.HasValue)
        {
            query = query.Where(u => u.Id == filter.BuyerId.Value);
        }
        if (filter.EstablishmentId.HasValue)
        {
            query = query.Where(u => u.AuditItems.Any(a => a.EstablishmentId == filter.EstablishmentId.Value));
        }

        return query;
    }

    private async Task PopulateFiltersAsync(ReportsFilterViewModel filter, string role, int userId, User currentUser)
    {
        var establishmentsQuery = _context.Establishments.AsNoTracking().OrderBy(e => e.Name).AsQueryable();
        if (role == "BranchStaff" && currentUser.EstablishmentId.HasValue)
        {
            establishmentsQuery = establishmentsQuery.Where(e => e.Id == currentUser.EstablishmentId.Value);
        }
        ViewBag.Establishments = new SelectList(await establishmentsQuery.ToListAsync(), "Id", "Name", filter.EstablishmentId);

        var buyersQuery = _context.Users.AsNoTracking().Where(u => u.Role == UserRole.Buyer && !u.IsDeleted);
        if (role == "Manager")
        {
            buyersQuery = buyersQuery.Where(u => u.ManagerId == userId);
        }
        else if (role == "Buyer")
        {
            buyersQuery = buyersQuery.Where(u => u.Id == userId);
        }
        else if (role == "BranchStaff")
        {
            buyersQuery = currentUser.EstablishmentId.HasValue
                ? buyersQuery.Where(u => u.AuditItems.Any(a => a.EstablishmentId == currentUser.EstablishmentId.Value))
                : buyersQuery.Where(u => false);
        }
        ViewBag.Buyers = new SelectList(await buyersQuery.OrderBy(u => u.Name).ToListAsync(), "Id", "Name", filter.BuyerId);

        var statuses = Enum.GetValues(typeof(AuditStatus))
            .Cast<AuditStatus>()
            .Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString(),
                Selected = filter.Status.HasValue && filter.Status.Value == s
            })
            .ToList();
        ViewBag.Statuses = new SelectList(statuses, "Value", "Text", filter.Status?.ToString());
    }

    private static string BuildScopeLabel(string role, User currentUser)
    {
        return role switch
        {
            "Owner" => "All company audits, cash, and surrender activity",
            "Admin" => "All company audits, user administration, and system activity",
            "Manager" => "Assigned buyers and manager-held petty cash",
            "Buyer" => "Your submitted audits, surrender requests, and cash ledger",
            "BranchStaff" => currentUser.EstablishmentId.HasValue ? "Assigned establishment delivery audits" : "No establishment assigned",
            _ => "Role-scoped activity"
        };
    }
}
