using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers;

[Authorize(Roles = "Owner,Manager")]
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

        var salesReportQuery = BuildScopedSalesReportQuery(role, userId, currentUser);
        salesReportQuery = ApplySalesReportFilters(salesReportQuery, filter);

        var treasuryCashFlowQuery = BuildScopedTreasuryCashFlowQuery(role, userId);
        treasuryCashFlowQuery = ApplyTreasuryCashFlowFilters(treasuryCashFlowQuery, filter);
        var treasuryCashFlows = await treasuryCashFlowQuery
            .Include(flow => flow.TreasuryUser)
            .Include(flow => flow.Entries)
                .ThenInclude(entry => entry.Establishment)
            .Include(flow => flow.Entries)
                .ThenInclude(entry => entry.RelatedUser)
            .Include(flow => flow.Entries)
                .ThenInclude(entry => entry.CostCenter)
            .Include(flow => flow.Entries)
                .ThenInclude(entry => entry.ReportedByUser)
            .OrderBy(flow => flow.CashFlowDate)
            .ThenBy(flow => flow.Id)
            .ToListAsync();

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

        var pnlAudits = await auditQuery
            .Include(a => a.Details)
            .ThenInclude(detail => detail.AssignedEstablishment)
            .Include(a => a.Establishment)
            .ToListAsync();
        var pnlSalesReports = await salesReportQuery.ToListAsync();

        var model = new ReportsViewModel
        {
            Filter = filter,
            Role = role,
            ScopeLabel = BuildScopeLabel(role, currentUser),
            RecentAudits = audits,
            RecentSalesReports = await salesReportQuery
                .OrderByDescending(s => s.HandoverDate)
                .ThenByDescending(s => s.Id)
                .Take(20)
                .ToListAsync(),
            CashOnHandUsers = await cashUserQuery
                .Include(u => u.Establishment)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync(),
            SurrenderRequests = surrenders,
            LedgerEntries = await ledgerQuery
                .OrderByDescending(l => l.Timestamp)
                .Take(20)
                .ToListAsync(),
            CurrentCashBalance = await Services.SharedPcfFundService.SumSharedAwareAsync(cashUserQuery),
            TotalAuditAmount = allAuditsForSummary.Sum(a => a.Amount),
            ApprovedAuditAmount = allAuditsForSummary.Where(a => a.Status == AuditStatus.Approved).Sum(a => a.Amount),
            AuditCount = allAuditsForSummary.Count,
            ApprovedAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.Approved),
            PendingAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.AwaitingBranchVerification || a.Status == AuditStatus.AwaitingManagerApproval),
            RejectedAuditCount = allAuditsForSummary.Count(a => a.Status == AuditStatus.Rejected),
            PendingSurrenderAmount = await surrenderQuery.Where(s => s.Status == SurrenderStatus.Pending).SumAsync(s => s.DeclaredAmount),
            ConfirmedSurrenderAmount = await surrenderQuery.Where(s => s.Status == SurrenderStatus.Confirmed).SumAsync(s => s.ConfirmedAmount ?? s.DeclaredAmount),
            TreasuryAudit = TreasuryAuditReportViewModel.Build(
                treasuryCashFlows,
                filter.TreasuryHandlerId,
                filter.StartDate ?? treasuryCashFlows.Select(flow => flow.CashFlowDate.Date).DefaultIfEmpty(DateTime.Today).Min(),
                filter.EndDate ?? treasuryCashFlows.Select(flow => flow.CashFlowDate.Date).DefaultIfEmpty(DateTime.Today).Max()),
            PnlReport = PnlReportViewModel.Build(
                pnlAudits,
                pnlSalesReports,
                filter.StartDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                filter.EndDate ?? DateTime.Today,
                filter.EstablishmentId)
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

        var buyerIdsQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Buyer && !u.IsDeleted);
        if (role == "Manager")
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.ManagerId == userId);
        }
        else if (role == "Buyer")
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.Id == userId);
        }
        else if (role == "BranchStaff")
        {
            buyerIdsQuery = currentUser.EstablishmentId.HasValue
                ? buyerIdsQuery.Where(u => u.AuditItems.Any(a => a.EstablishmentId == currentUser.EstablishmentId.Value))
                : buyerIdsQuery.Where(u => false);
        }
        if (filter.BuyerId.HasValue)
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.Id == filter.BuyerId.Value);
        }

        var buyerIds = await buyerIdsQuery
            .OrderBy(u => u.Name)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var buyerId in buyerIds)
        {
            var buyer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == buyerId && !u.IsDeleted);
            if (buyer == null)
            {
                continue;
            }

            var buyerReport = new BuyerAuditReportViewModel
            {
                BuyerId = buyer.Id,
                BuyerName = buyer.Name
            };

            var releasesQuery = _context.PcfReleases
                .AsNoTracking()
                .Include(r => r.ReleasedByTreasuryUser)
                .Where(r => r.ReceiverUserId == buyerId);

            if (filter.StartDate.HasValue)
            {
                releasesQuery = releasesQuery.Where(r => r.ReleaseDate >= filter.StartDate.Value);
            }
            if (filter.EndDate.HasValue)
            {
                releasesQuery = releasesQuery.Where(r => r.ReleaseDate < filter.EndDate.Value.AddDays(1));
            }

            buyerReport.Releases = await releasesQuery
                .Select(r => new PcfReleaseLine
                {
                    Date = r.ReleaseDate,
                    Amount = r.Amount,
                    IssuedBy = r.ReleasedByTreasuryUser.Name
                })
                .OrderBy(r => r.Date)
                .ToListAsync();

            var expensesQuery = _context.AuditItemDetails
                .AsNoTracking()
                .Include(ad => ad.AuditItem)
                    .ThenInclude(a => a.Establishment)
                .Include(ad => ad.AssignedEstablishment)
                .Include(ad => ad.CostCenter)
                .Where(ad => ad.AuditItem.BuyerId == buyerId && ad.AuditItem.Status == AuditStatus.Approved);

            if (filter.StartDate.HasValue)
            {
                expensesQuery = expensesQuery.Where(ad => ad.AuditItem.EntryDate >= filter.StartDate.Value);
            }
            if (filter.EndDate.HasValue)
            {
                expensesQuery = expensesQuery.Where(ad => ad.AuditItem.EntryDate < filter.EndDate.Value.AddDays(1));
            }

            buyerReport.Expenses = await expensesQuery
                .Select(ad => new BuyerExpenseLine
                {
                    Date = ad.AuditItem.EntryDate,
                    Description = ad.ItemName,
                    Amount = ad.Quantity * ad.Price,
                    Allocation = ad.AssignedEstablishment != null ? ad.AssignedEstablishment.Name : (ad.AuditItem.Establishment != null ? ad.AuditItem.Establishment.Name : (ad.CostCenter != null ? ad.CostCenter.Name : "OTHERS"))
                })
                .OrderBy(ad => ad.Date)
                .ToListAsync();

            var releaseIds = await releasesQuery.Select(r => r.Id).ToListAsync();
            buyerReport.ActualChangeReturned = await _context.AuditSettlements
                .AsNoTracking()
                .Where(s => s.PcfReleaseId.HasValue && releaseIds.Contains(s.PcfReleaseId.Value))
                .SumAsync(s => s.ActualChangeReturned);

            if (buyerReport.Releases.Any() || buyerReport.Expenses.Any() || buyerReport.ActualChangeReturned != 0m)
            {
                model.BuyerAudits.Add(buyerReport);
            }
        }

        model.BuyerAudit = model.BuyerAudits.FirstOrDefault() ?? new BuyerAuditReportViewModel();

        // Always populate BranchAudit, but filter by establishment if set
        model.BranchAudit = new BranchAuditReportViewModel
        {
            BranchId = filter.EstablishmentId,
            BranchName = filter.EstablishmentId.HasValue
                ? (await _context.Establishments.AsNoTracking().Where(e => e.Id == filter.EstablishmentId.Value).Select(e => e.Name).FirstOrDefaultAsync() ?? "All Branches")
                : "All Branches"
        };

        var branchExpensesQuery = _context.AuditItemDetails
            .AsNoTracking()
            .Include(ad => ad.AuditItem)
                .ThenInclude(a => a.Establishment)
            .Include(ad => ad.AssignedEstablishment)
            .Include(ad => ad.CostCenter)
            .Where(ad => ad.AuditItem.Status == AuditStatus.Approved);

        if (filter.EstablishmentId.HasValue)
        {
            branchExpensesQuery = branchExpensesQuery.Where(ad => (ad.AssignedEstablishmentId ?? ad.AuditItem.EstablishmentId) == filter.EstablishmentId.Value);
        }
        if (filter.StartDate.HasValue)
        {
            branchExpensesQuery = branchExpensesQuery.Where(ad => ad.AuditItem.EntryDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            branchExpensesQuery = branchExpensesQuery.Where(ad => ad.AuditItem.EntryDate < filter.EndDate.Value.AddDays(1));
        }

        model.BranchAudit.Expenses = await branchExpensesQuery
            .Select(ad => new BranchExpenseLine
            {
                Date = ad.AuditItem.EntryDate,
                Description = ad.ItemName,
                Amount = ad.Quantity * ad.Price,
                Allocation = ad.AssignedEstablishment != null ? ad.AssignedEstablishment.Name : (ad.AuditItem.Establishment != null ? ad.AuditItem.Establishment.Name : (ad.CostCenter != null ? ad.CostCenter.Name : "OTHERS"))
            })
            .OrderBy(ad => ad.Date)
            .ToListAsync();
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
            query = query.Where(a => a.BuyerId == userId || a.AssignedReviewerId == userId || a.Buyer.ManagerId == userId);
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

    private IQueryable<SalesReport> BuildScopedSalesReportQuery(string role, int userId, User currentUser)
    {
        var query = _context.SalesReports
            .AsNoTracking()
            .Include(s => s.DocumentRecord)
            .Include(s => s.Establishment)
            .Include(s => s.CashierUser)
            .Include(s => s.ConfirmedByUser)
            .AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(s => s.DocumentRecord.UploadedByUserId == userId
                || s.CashierUserId == userId
                || s.ConfirmedByUserId == userId
                || s.DocumentRecord.ConfirmedByUserId == userId);
        }
        else if (role == "Buyer")
        {
            query = query.Where(s => s.DocumentRecord.UploadedByUserId == userId || s.CashierUserId == userId);
        }
        else if (role == "BranchStaff")
        {
            query = currentUser.EstablishmentId.HasValue
                ? query.Where(s => s.EstablishmentId == currentUser.EstablishmentId.Value)
                : query.Where(s => false);
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
            query = query.Where(s => s.AssignedReceiverId == userId || s.Buyer.ManagerId == userId);
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

    private IQueryable<TreasuryCashFlow> BuildScopedTreasuryCashFlowQuery(string role, int userId)
    {
        var query = _context.TreasuryCashFlows
            .AsNoTracking()
            .AsQueryable();

        if (role == "Manager")
        {
            query = query.Where(flow =>
                flow.Entries.Any(e => e.ReportedByUserId == userId)
                || (!flow.Entries.Any(e => e.ReportedByUserId.HasValue) && flow.TreasuryUserId == userId));
        }
        else if (role == "Buyer" || role == "BranchStaff")
        {
            query = query.Where(flow => false);
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

    private static IQueryable<SalesReport> ApplySalesReportFilters(IQueryable<SalesReport> query, ReportsFilterViewModel filter)
    {
        if (filter.StartDate.HasValue)
        {
            query = query.Where(s => s.BusinessDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(s => s.BusinessDate < filter.EndDate.Value.AddDays(1));
        }
        if (filter.EstablishmentId.HasValue)
        {
            query = query.Where(s => s.EstablishmentId == filter.EstablishmentId.Value);
        }

        return query;
    }

    private static IQueryable<TreasuryCashFlow> ApplyTreasuryCashFlowFilters(IQueryable<TreasuryCashFlow> query, ReportsFilterViewModel filter)
    {
        if (filter.StartDate.HasValue)
        {
            query = query.Where(flow => flow.CashFlowDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(flow => flow.CashFlowDate < filter.EndDate.Value.AddDays(1));
        }
        if (filter.TreasuryHandlerId.HasValue)
        {
            query = query.Where(flow => flow.TreasuryUserId == filter.TreasuryHandlerId.Value);
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

        var treasuryHandlersQuery = _context.Users.AsNoTracking().Where(u => u.IsTreasury && !u.IsDeleted);
        if (role == "Manager")
        {
            treasuryHandlersQuery = treasuryHandlersQuery.Where(u => u.Id == userId);
        }
        else if (role == "Buyer" || role == "BranchStaff")
        {
            treasuryHandlersQuery = treasuryHandlersQuery.Where(u => false);
        }
        ViewBag.TreasuryHandlers = new SelectList(await treasuryHandlersQuery.OrderBy(u => u.Name).ToListAsync(), "Id", "Name", filter.TreasuryHandlerId);
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
