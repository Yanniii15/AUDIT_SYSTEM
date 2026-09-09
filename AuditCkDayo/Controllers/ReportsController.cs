using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;
using ClosedXML.Excel;

namespace AuditCkDayo.Controllers;

[Authorize(Roles = "Owner,Manager,Admin")]
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

        if (role == "Manager" && !filter.TreasuryHandlerId.HasValue)
        {
            filter.TreasuryHandlerId = userId;
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
        var pnlSalesReports = await salesReportQuery
            .Include(s => s.Establishment)
            .Include(s => s.Lines)
            .ToListAsync();

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
            var managedReleaseBuyerIds = await _context.PcfReleases
                .AsNoTracking()
                .Where(r => r.ReleasedByTreasuryUserId == userId)
                .Select(r => r.ReceiverUserId)
                .Distinct()
                .ToListAsync();

            buyerIdsQuery = buyerIdsQuery.Where(u => u.ManagerId == userId || managedReleaseBuyerIds.Contains(u.Id));
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
        model.BuyerAudits = await LoadBuyerAuditsAsync(filter, role, userId, currentUser);
        model.BuyerAudit = model.BuyerAudits.FirstOrDefault() ?? new BuyerAuditReportViewModel();
        model.PcfMatrix = await BuildPcfMatrixAsync(filter, model.BuyerAudits, role, userId);

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
            .Include(ad => ad.ExpenseSource)
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

        model.BranchAudit.Expenses = (await branchExpensesQuery
                .OrderBy(ad => ad.AuditItem.EntryDate)
                .ToListAsync())
            .Select(ad => new BranchExpenseLine
            {
                Date = ad.AuditItem.EntryDate,
                Description = ad.ItemName,
                Amount = ad.Total,
                Allocation = ResolveExpenseAllocation(ad)
            })
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
            query = query.Where(flow => flow.TreasuryUserId == userId
                || flow.Entries.Any(e => e.ReportedByUserId == userId));
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
            var handlerId = filter.TreasuryHandlerId.Value;
            query = query.Where(flow => flow.TreasuryUserId == handlerId
                || flow.Entries.Any(e => e.ReportedByUserId == handlerId));
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

    [HttpGet]
    public async Task<IActionResult> ExportPcfMatrixExcel(ReportsFilterViewModel filter)
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

        var buyerAudits = await LoadBuyerAuditsAsync(filter, role, userId, currentUser);
        var matrix = await BuildPcfMatrixAsync(filter, buyerAudits, role, userId);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("PCF Matrix");

        // Header Row: DATE, then each Custodian
        worksheet.Cell(1, 1).Value = "DATE";
        for (int c = 0; c < matrix.Custodians.Count; c++)
        {
            worksheet.Cell(1, c + 2).Value = matrix.Custodians[c];
        }

        var totalCol = matrix.Custodians.Count + 1;
        worksheet.Range(1, 1, 1, totalCol).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, totalCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int rowIdx = 2;
        foreach (var row in matrix.Rows)
        {
            worksheet.Cell(rowIdx, 1).Value = row.Date.ToString("MMMM dd");
            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int c = 0; c < matrix.Custodians.Count; c++)
            {
                var cust = matrix.Custodians[c];
                var amt = row.AmountsByCustodian.GetValueOrDefault(cust, 0m);
                var cell = worksheet.Cell(rowIdx, c + 2);
                if (amt > 0m)
                {
                    cell.Value = amt;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    var note = row.NotesByCustodian.GetValueOrDefault(cust, "");
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        cell.CreateComment().AddText(note);
                    }
                }
                else
                {
                    cell.Value = string.Empty;
                }
            }
            rowIdx++;
        }

        // Yellow Total Row (matching Image #1)
        int totalRowIdx = rowIdx + 1;
        worksheet.Cell(totalRowIdx, 1).Value = "TOTAL";
        worksheet.Cell(totalRowIdx, 1).Style.Font.Bold = true;
        for (int c = 0; c < matrix.Custodians.Count; c++)
        {
            var cust = matrix.Custodians[c];
            var tot = matrix.ColumnTotals.GetValueOrDefault(cust, 0m);
            var cell = worksheet.Cell(totalRowIdx, c + 2);
            cell.Value = tot;
            cell.Style.Font.Bold = true;
            cell.Style.NumberFormat.Format = "₱#,##0.00";
        }
        worksheet.Range(totalRowIdx, 1, totalRowIdx, totalCol).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFF00");

        // Bottom-Left Summary Box (matching Image #1)
        int summaryRow = totalRowIdx + 2;
        worksheet.Cell(summaryRow, 1).Value = "TOTAL PC:";
        worksheet.Cell(summaryRow, 2).Value = matrix.TotalPc;
        worksheet.Cell(summaryRow, 2).Style.NumberFormat.Format = "₱#,##0.00";
        worksheet.Range(summaryRow, 1, summaryRow, 2).Style.Font.Bold = true;

        worksheet.Cell(summaryRow + 1, 1).Value = "TOTAL EXP:";
        worksheet.Cell(summaryRow + 1, 2).Value = matrix.TotalExpenses;
        worksheet.Cell(summaryRow + 1, 2).Style.NumberFormat.Format = "₱#,##0.00";
        worksheet.Range(summaryRow + 1, 1, summaryRow + 1, 2).Style.Font.Bold = true;

        worksheet.Cell(summaryRow + 2, 1).Value = "CHANGE:";
        worksheet.Cell(summaryRow + 2, 2).Value = matrix.ExpectedChange;
        worksheet.Cell(summaryRow + 2, 2).Style.NumberFormat.Format = "₱#,##0.00";
        worksheet.Range(summaryRow + 2, 1, summaryRow + 2, 2).Style.Font.Bold = true;

        worksheet.Cell(summaryRow + 3, 1).Value = "A. CHANGE:";
        worksheet.Cell(summaryRow + 3, 2).Value = matrix.ActualChangeReturned;
        worksheet.Cell(summaryRow + 3, 2).Style.NumberFormat.Format = "₱#,##0.00";
        worksheet.Range(summaryRow + 3, 1, summaryRow + 3, 2).Style.Font.Bold = true;

        worksheet.Cell(summaryRow + 4, 1).Value = "SHORT/OVER:";
        worksheet.Cell(summaryRow + 4, 2).Value = matrix.ShortOverAmount;
        worksheet.Cell(summaryRow + 4, 2).Style.NumberFormat.Format = "₱#,##0.00";
        worksheet.Range(summaryRow + 4, 1, summaryRow + 4, 2).Style.Font.Bold = true;

        worksheet.Columns().AdjustToContents();
        worksheet.Column(1).Width = 16;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        var startStr = matrix.StartDate.ToString("yyyy-MM-dd");
        var endStr = matrix.EndDate.ToString("yyyy-MM-dd");
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PCF_Release_Matrix_{startStr}_to_{endStr}.xlsx");
    }
    public async Task<IActionResult> ExportBuyerAuditExcel(ReportsFilterViewModel filter)
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

        var buyerIdsQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Buyer && !u.IsDeleted);
        if (role == "Manager")
        {
            var managedReleaseBuyerIds = await _context.PcfReleases
                .AsNoTracking()
                .Where(r => r.ReleasedByTreasuryUserId == userId)
                .Select(r => r.ReceiverUserId)
                .Distinct()
                .ToListAsync();
            buyerIdsQuery = buyerIdsQuery.Where(u => u.ManagerId == userId || managedReleaseBuyerIds.Contains(u.Id));
        }
        else if (role == "Buyer")
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.Id == userId);
        }
        if (filter.BuyerId.HasValue)
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.Id == filter.BuyerId.Value);
        }

        var buyerIds = await buyerIdsQuery.Select(u => u.Id).ToListAsync();
        var detailsQuery = _context.AuditItemDetails
            .AsNoTracking()
            .Include(detail => detail.AuditItem)
                .ThenInclude(audit => audit.Establishment)
            .Include(detail => detail.AssignedEstablishment)
            .Include(detail => detail.CostCenter)
            .Include(detail => detail.ExpenseSource)
            .Where(detail => buyerIds.Contains(detail.AuditItem.BuyerId)
                && detail.AuditItem.Status != AuditStatus.Cancelled
                && detail.AuditItem.Status != AuditStatus.Rejected);

        if (filter.StartDate.HasValue)
        {
            detailsQuery = detailsQuery.Where(detail => detail.AuditItem.EntryDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            detailsQuery = detailsQuery.Where(detail => detail.AuditItem.EntryDate < filter.EndDate.Value.AddDays(1));
        }
        if (filter.EstablishmentId.HasValue)
        {
            detailsQuery = detailsQuery.Where(detail => (detail.AssignedEstablishmentId ?? detail.AuditItem.EstablishmentId) == filter.EstablishmentId.Value);
        }

        var details = await detailsQuery
            .OrderBy(detail => detail.AuditItem.EntryDate)
            .ThenBy(detail => detail.Id)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Buyer Audit");
        worksheet.Cell("A1").Value = "DATE";
        worksheet.Cell("B1").Value = "DESCRIPTION";
        worksheet.Cell("C1").Value = "ITEM";
        worksheet.Cell("D1").Value = "AMOUNT";
        worksheet.Cell("E1").Value = "ALLOCATION";
        worksheet.Range("A1:E1").Style.Font.Bold = true;
        worksheet.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

        var row = 2;
        DateTime? previousDate = null;
        foreach (var detail in details)
        {
            var currentDate = detail.AuditItem.EntryDate.Date;
            worksheet.Cell(row, 1).Value = currentDate == previousDate ? string.Empty : currentDate.ToString("M/d");
            worksheet.Cell(row, 2).Value = ResolveExpenseDescription(detail).ToUpperInvariant();
            worksheet.Cell(row, 3).Value = detail.ItemName;
            worksheet.Cell(row, 4).Value = detail.Total;
            worksheet.Cell(row, 5).Value = ResolveExpenseAllocation(detail);
            previousDate = currentDate;
            row++;
        }

        worksheet.Column("A").Width = 12;
        worksheet.Column("B").Width = 28;
        worksheet.Column("C").Width = 28;
        worksheet.Column("D").Width = 14;
        worksheet.Column("E").Width = 28;
        worksheet.Column("D").Style.NumberFormat.Format = "#,##0.00";

        using var stream = new System.IO.MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Buyer_Audit_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPnlExcel(ReportsFilterViewModel filter)
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

        var salesReportQuery = BuildScopedSalesReportQuery(role, userId, currentUser);
        salesReportQuery = ApplySalesReportFilters(salesReportQuery, filter);

        var auditQuery = BuildScopedAuditQuery(role, userId, currentUser);
        auditQuery = ApplyAuditFilters(auditQuery, filter);

        var pnlSalesReports = await salesReportQuery.ToListAsync();
        var pnlAudits = await auditQuery
            .Include(a => a.Details)
            .ThenInclude(detail => detail.AssignedEstablishment)
            .Include(a => a.Establishment)
            .ToListAsync();

        var pnlReport = PnlReportViewModel.Build(
            pnlAudits,
            pnlSalesReports,
            filter.StartDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            filter.EndDate ?? DateTime.Today,
            filter.EstablishmentId);

        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("P&L Report");

            worksheet.Column("A").Width = 25;
            worksheet.Column("B").Width = 30;
            worksheet.Column("C").Width = 18;
            worksheet.Column("D").Width = 10;
            worksheet.Column("E").Width = 18;
            worksheet.Column("F").Width = 10;

            var titleRange = worksheet.Range("A1:F1");
            titleRange.Merge().Value = $"SALES REPORT - {pnlReport.StartDate:MMMM yyyy}".ToUpper();
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 14;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

            worksheet.Cell("A2").Value = "DATE";
            worksheet.Cell("B2").Value = "DESCRIPTION";
            worksheet.Cell("C2").Value = "MAIN";
            worksheet.Cell("D2").Value = "%";
            worksheet.Cell("E2").Value = "BRANCH 4";
            worksheet.Cell("F2").Value = "%";

            var headerRange = worksheet.Range("A2:F2");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            var mainSalesReports = pnlSalesReports.Where(s => s.EstablishmentId == 2).ToList();
            var b4SalesReports = pnlSalesReports.Where(s => s.EstablishmentId == 4).ToList();

            decimal mainDailyGrossSales = mainSalesReports.Sum(s => s.TotalGrossSales);
            decimal b4DailyGrossSales = b4SalesReports.Sum(s => s.TotalGrossSales);

            decimal mainHardSales = mainSalesReports.Sum(s => s.HardSales);
            decimal b4HardSales = b4SalesReports.Sum(s => s.HardSales);

            decimal mainBeerSales = mainSalesReports.Sum(s => s.BeerSales);
            decimal b4BeerSales = b4SalesReports.Sum(s => s.BeerSales);

            decimal mainOtherSales = mainSalesReports.Sum(s => s.OtherSales);
            decimal b4OtherSales = b4SalesReports.Sum(s => s.OtherSales);

            worksheet.Cell("B3").Value = "DAILY GROSS SALES";
            worksheet.Cell("C3").Value = mainDailyGrossSales;
            worksheet.Cell("E3").Value = b4DailyGrossSales;

            worksheet.Cell("B4").Value = "KALBO";
            worksheet.Cell("C4").Value = mainHardSales;
            worksheet.Cell("E4").Value = b4HardSales;

            worksheet.Cell("B5").Value = "SAN JOSE CRAFT BEER";
            worksheet.Cell("C5").Value = mainBeerSales;
            worksheet.Cell("E5").Value = b4BeerSales;

            worksheet.Cell("B6").Value = "WILLOW'S CAFE";
            worksheet.Cell("C6").Value = mainOtherSales;
            worksheet.Cell("E6").Value = b4OtherSales;

            worksheet.Cell("B7").Value = "TOTAL SALES";
            worksheet.Cell("B7").Style.Font.Bold = true;
            worksheet.Cell("C7").FormulaA1 = "=SUM(C3:C6)";
            worksheet.Cell("C7").Style.Font.Bold = true;
            worksheet.Cell("E7").FormulaA1 = "=SUM(E3:E6)";
            worksheet.Cell("E7").Style.Font.Bold = true;

            worksheet.Range("C3:C7").Style.NumberFormat.Format = "₱#,##0.00";
            worksheet.Range("E3:E7").Style.NumberFormat.Format = "₱#,##0.00";

            worksheet.Cell("A9").Value = "EXPENSES: COGS";
            worksheet.Cell("A9").Style.Font.Bold = true;
            worksheet.Cell("A9").Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");

            var approvedDetails = pnlAudits
                .Where(a => a.Status == AuditStatus.Approved)
                .SelectMany(a => a.Details)
                .ToList();

            var mainDetails = approvedDetails.Where(d => (d.AssignedEstablishmentId ?? d.AuditItemId) == 2 || d.AssignedEstablishment?.Name == "CKR Main" || d.AuditItem?.EstablishmentId == 2).ToList();
            var b4Details = approvedDetails.Where(d => (d.AssignedEstablishmentId ?? d.AuditItemId) == 4 || d.AssignedEstablishment?.Name == "CKR Branch 4" || d.AuditItem?.EstablishmentId == 4).ToList();

            string[] marketKeys = { "market" };
            string[] groceryKeys = { "grocery" };
            string[] oilKeys = { "oil" };
            string[] iceKeys = { "ice" };
            string[] beerKeys = { "beer", "smb", "pilsen" };
            string[] beverageKeys = { "beverage", "beverages", "coke" };
            string[] hardKeys = { "hard", "whiskey" };
            string[] balutKeys = { "balut" };
            string[] restockedKeys = { "restocked", "stock" };
            string[] selectaKeys = { "selecta", "ice cream" };
            string[] winstonKeys = { "winston", "cigarette" };

            int rowIdx = 10;
            void WriteCogsRow(string label, string[] keys) {
                worksheet.Cell(rowIdx, 2).Value = label;
                worksheet.Cell(rowIdx, 3).Value = GetExpenseAmount(mainDetails, keys);
                worksheet.Cell(rowIdx, 4).FormulaA1 = $"=IF(C$7>0, C{rowIdx}/C$7, 0)";
                worksheet.Cell(rowIdx, 4).Style.NumberFormat.Format = "0.00%";
                worksheet.Cell(rowIdx, 4).Style.Font.FontColor = XLColor.Red;
                worksheet.Cell(rowIdx, 5).Value = GetExpenseAmount(b4Details, keys);
                worksheet.Cell(rowIdx, 6).FormulaA1 = $"=IF(E$7>0, E{rowIdx}/E$7, 0)";
                worksheet.Cell(rowIdx, 6).Style.NumberFormat.Format = "0.00%";
                worksheet.Cell(rowIdx, 6).Style.Font.FontColor = XLColor.Red;
                rowIdx++;
            }

            WriteCogsRow("MARKET", marketKeys);
            WriteCogsRow("GROCERY", groceryKeys);
            WriteCogsRow("OIL", oilKeys);
            WriteCogsRow("ICE", iceKeys);
            WriteCogsRow("BEER", beerKeys);
            WriteCogsRow("BEVERAGES", beverageKeys);
            WriteCogsRow("HARD DRINKS", hardKeys);
            WriteCogsRow("BALUT", balutKeys);
            WriteCogsRow("RESTOCKED", restockedKeys);
            WriteCogsRow("SELECTA", selectaKeys);
            WriteCogsRow("WINSTON", winstonKeys);

            worksheet.Cell(rowIdx, 2).Value = "TOTAL EXPENSES";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=SUM(C10:C{rowIdx-1})";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=SUM(E10:E{rowIdx-1})";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            rowIdx++;

            int gpRow = rowIdx;
            worksheet.Cell(rowIdx, 2).Value = "GROSS PROFIT";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=C7-C{rowIdx-1}";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=E7-E{rowIdx-1}";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            rowIdx += 2;

            worksheet.Cell(rowIdx, 1).Value = "EXPENSES: OPEX";
            worksheet.Cell(rowIdx, 1).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            int opexStartRow = rowIdx + 1;
            rowIdx++;

            string[] counterKeys = { "counter" };
            string[] packagingKeys = { "packaging", "pack" };
            string[] petronKeys = { "petron", "gas", "fuel" };
            string[] diningKeys = { "dining", "plates" };
            string[] kitchenKeys = { "kitchen", "knife", "stove" };
            string[] posKeys = { "pos" };
            string[] payrollKeys = { "payroll", "sahod", "salary" };
            string[] rentKeys = { "rent" };
            string[] maintenanceKeys = { "maintenance", "repair" };
            string[] electricKeys = { "electric", "meralco", "power" };
            string[] waterKeys = { "water" };
            string[] transpoKeys = { "transportation", "transpo", "fare" };
            string[] adegKeys = { "adeg" };
            string[] benefitsKeys = { "benefits", "sss", "philhealth" };

            void WriteOpexRow(string label, string[] keys) {
                worksheet.Cell(rowIdx, 2).Value = label;
                worksheet.Cell(rowIdx, 3).Value = GetExpenseAmount(mainDetails, keys);
                worksheet.Cell(rowIdx, 4).FormulaA1 = $"=IF(C$7>0, C{rowIdx}/C$7, 0)";
                worksheet.Cell(rowIdx, 4).Style.NumberFormat.Format = "0.00%";
                worksheet.Cell(rowIdx, 4).Style.Font.FontColor = XLColor.Red;
                worksheet.Cell(rowIdx, 5).Value = GetExpenseAmount(b4Details, keys);
                worksheet.Cell(rowIdx, 6).FormulaA1 = $"=IF(E$7>0, E{rowIdx}/E$7, 0)";
                worksheet.Cell(rowIdx, 6).Style.NumberFormat.Format = "0.00%";
                worksheet.Cell(rowIdx, 6).Style.Font.FontColor = XLColor.Red;
                rowIdx++;
            }

            WriteOpexRow("COUNTER SUPPLIES", counterKeys);
            WriteOpexRow("PACKAGING SUPPLIES", packagingKeys);
            WriteOpexRow("PETRON", petronKeys);
            WriteOpexRow("DINNING TOOLS", diningKeys);
            WriteOpexRow("KITCHEN TOOLS", kitchenKeys);
            WriteOpexRow("POS", posKeys);
            WriteOpexRow("PAYROLL", payrollKeys);
            WriteOpexRow("RENT", rentKeys);
            WriteOpexRow("MAINTENANCE", maintenanceKeys);
            WriteOpexRow("ELECTRIC BILL", electricKeys);
            WriteOpexRow("WATER BILL", waterKeys);
            WriteOpexRow("TRANSPORTATION", transpoKeys);
            WriteOpexRow("ADEG", adegKeys);
            WriteOpexRow("BENEFITS", benefitsKeys);

            int opexEndRow = rowIdx - 1;

            worksheet.Cell(rowIdx, 2).Value = "TOTAL EXPENSES";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=SUM(C{opexStartRow}:C{opexEndRow})";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=SUM(E{opexStartRow}:E{opexEndRow})";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            rowIdx++;

            worksheet.Cell(rowIdx, 2).Value = "NET PROFIT";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=C{gpRow}-C{rowIdx-1}";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=E{gpRow}-E{rowIdx-1}";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE599");
            rowIdx++;

            worksheet.Cell(rowIdx, 2).Value = "PERCENTAGE";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=IF(C$7>0, C{rowIdx-1}/C$7, 0)";
            worksheet.Cell(rowIdx, 3).Style.NumberFormat.Format = "0%";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=IF(E$7>0, E{rowIdx-1}/E$7, 0)";
            worksheet.Cell(rowIdx, 5).Style.NumberFormat.Format = "0%";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            rowIdx += 2;

            worksheet.Range($"C9:C{rowIdx}").Style.NumberFormat.Format = "₱#,##0.00";
            worksheet.Range($"E9:E{rowIdx}").Style.NumberFormat.Format = "₱#,##0.00";

            worksheet.Cell(rowIdx, 1).Value = "MONTH END INVENTORY";
            worksheet.Cell(rowIdx, 1).Style.Font.Bold = true;
            rowIdx++;

            void WriteInventoryRow(string label) {
                worksheet.Cell(rowIdx, 2).Value = label;
                worksheet.Cell(rowIdx, 3).Value = 0.0;
                worksheet.Cell(rowIdx, 5).Value = 0.0;
                rowIdx++;
            }

            WriteInventoryRow("FROZEN STOCKS");
            WriteInventoryRow("BEVERAGES");
            WriteInventoryRow("BEER");
            WriteInventoryRow("HARD DRINKS");
            WriteInventoryRow("GROCERY/MARKET");
            WriteInventoryRow("PACKAGING");
            WriteInventoryRow("WINSTON");
            WriteInventoryRow("CLEANING MATERIALS");
            WriteInventoryRow("OTHER SUPPLIES");

            worksheet.Cell(rowIdx, 2).Value = "TOTAL";
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2EFDA");
            worksheet.Cell(rowIdx, 3).FormulaA1 = $"=SUM(C{rowIdx-9}:C{rowIdx-1})";
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2EFDA");
            worksheet.Cell(rowIdx, 5).FormulaA1 = $"=SUM(E{rowIdx-9}:E{rowIdx-1})";
            worksheet.Cell(rowIdx, 5).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2EFDA");

            worksheet.Range($"C{rowIdx-9}:C{rowIdx}").Style.NumberFormat.Format = "₱#,##0.00";
            worksheet.Range($"E{rowIdx-9}:E{rowIdx}").Style.NumberFormat.Format = "₱#,##0.00";

            workbook.RecalculateAllFormulas();

            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PnL_Report_{pnlReport.StartDate:yyyyMM}.xlsx");
            }
        }
    }

    private async Task<List<BuyerAuditReportViewModel>> LoadBuyerAuditsAsync(ReportsFilterViewModel filter, string role, int userId, User? currentUser)
    {
        var buyerIdsQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Buyer && !u.IsDeleted);

        if (role == "Manager")
        {
            var managedReleaseBuyerIds = await _context.PcfReleases
                .AsNoTracking()
                .Where(r => r.ReleasedByTreasuryUserId == userId)
                .Select(r => r.ReceiverUserId)
                .Distinct()
                .ToListAsync();

            buyerIdsQuery = buyerIdsQuery.Where(u => u.ManagerId == userId || managedReleaseBuyerIds.Contains(u.Id));
        }
        else if (role == "Buyer")
        {
            buyerIdsQuery = buyerIdsQuery.Where(u => u.Id == userId);
        }
        else if (role == "BranchStaff" && currentUser != null)
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

        var buyerAudits = new List<BuyerAuditReportViewModel>();

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
                .Include(ad => ad.ExpenseSource)
                .Where(ad => ad.AuditItem.BuyerId == buyerId && ad.AuditItem.Status != AuditStatus.Cancelled && ad.AuditItem.Status != AuditStatus.Rejected);

            if (filter.StartDate.HasValue)
            {
                expensesQuery = expensesQuery.Where(ad => ad.AuditItem.EntryDate >= filter.StartDate.Value);
            }
            if (filter.EndDate.HasValue)
            {
                expensesQuery = expensesQuery.Where(ad => ad.AuditItem.EntryDate < filter.EndDate.Value.AddDays(1));
            }

            var detailsList = await expensesQuery
                .OrderBy(ad => ad.AuditItem.EntryDate)
                .ThenBy(ad => ad.AuditItemId)
                .ThenBy(ad => ad.Id)
                .ToListAsync();

            buyerReport.Expenses = detailsList
                .Select(ad => new BuyerExpenseLine
                {
                    AuditItemId = ad.AuditItemId,
                    Date = ad.AuditItem.EntryDate,
                    Description = ResolveExpenseDescription(ad),
                    Item = ad.ItemName,
                    Amount = ad.Total,
                    Allocation = ResolveExpenseAllocation(ad),
                    HasReceipt = ad.ReceiptStatus != ReceiptLineStatus.NoReceipt
                })
                .ToList();

            var releaseIds = await releasesQuery.Select(r => r.Id).ToListAsync();
            buyerReport.ActualChangeReturned = await _context.AuditSettlements
                .AsNoTracking()
                .Where(s => s.PcfReleaseId.HasValue && releaseIds.Contains(s.PcfReleaseId.Value))
                .SumAsync(s => s.ActualChangeReturned);

            if (buyerReport.Releases.Any() || buyerReport.Expenses.Any() || buyerReport.ActualChangeReturned != 0m)
            {
                buyerAudits.Add(buyerReport);
            }
        }

        return buyerAudits;
    }

    private async Task<PcfMatrixViewModel> BuildPcfMatrixAsync(ReportsFilterViewModel filter, List<BuyerAuditReportViewModel> buyerAudits, string role, int userId)
    {
        var matrix = new PcfMatrixViewModel();
        var releasesQuery = _context.PcfReleases
            .AsNoTracking()
            .Include(r => r.ReceiverUser)
            .Include(r => r.ReleasedByTreasuryUser)
            .Include(r => r.Establishment)
            .AsQueryable();

        if (role == "Manager")
        {
            releasesQuery = releasesQuery.Where(r => r.ReleasedByTreasuryUserId == userId || (r.ReceiverUser != null && r.ReceiverUser.ManagerId == userId));
        }
        else if (role == "Buyer")
        {
            releasesQuery = releasesQuery.Where(r => r.ReceiverUserId == userId);
        }

        if (filter.BuyerId.HasValue)
        {
            releasesQuery = releasesQuery.Where(r => r.ReceiverUserId == filter.BuyerId.Value);
        }

        if (filter.StartDate.HasValue)
        {
            releasesQuery = releasesQuery.Where(r => r.ReleaseDate >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            releasesQuery = releasesQuery.Where(r => r.ReleaseDate < filter.EndDate.Value.AddDays(1));
        }

        var allReleases = await releasesQuery
            .OrderBy(r => r.ReleaseDate)
            .ThenBy(r => r.Id)
            .ToListAsync();

        if (!allReleases.Any() && !buyerAudits.Any())
        {
            return matrix;
        }

        var minDate = filter.StartDate?.Date ?? (allReleases.Any() ? allReleases.Min(r => r.ReleaseDate.Date) : DateTime.Today);
        var maxDate = filter.EndDate?.Date ?? (allReleases.Any() ? allReleases.Max(r => r.ReleaseDate.Date) : DateTime.Today);

        matrix.StartDate = minDate;
        matrix.EndDate = maxDate;

        var custodianNames = new List<string>();
        foreach (var r in allReleases)
        {
            var name = ResolveReleaserName(r);
            if (!custodianNames.Contains(name))
            {
                custodianNames.Add(name);
            }
        }

        if (!custodianNames.Any())
        {
            custodianNames.Add("OTHERS");
        }

        matrix.Custodians = custodianNames;

        for (var dt = minDate; dt <= maxDate; dt = dt.AddDays(1))
        {
            var dateRow = new PcfMatrixDateRow { Date = dt };
            var dayReleases = allReleases.Where(r => r.ReleaseDate.Date == dt).ToList();

            foreach (var cust in custodianNames)
            {
                var custReleases = dayReleases.Where(r =>
                {
                    var name = ResolveReleaserName(r);
                    return string.Equals(name, cust, StringComparison.OrdinalIgnoreCase);
                }).ToList();
                var totalDayAmt = custReleases.Sum(r => r.Amount);
                dateRow.AmountsByCustodian[cust] = totalDayAmt;

                var notes = string.Join(", ", custReleases.Select(r => r.Purpose).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    dateRow.NotesByCustodian[cust] = notes;
                }
            }

            matrix.Rows.Add(dateRow);
        }

        foreach (var cust in custodianNames)
        {
            matrix.ColumnTotals[cust] = matrix.Rows.Sum(r => r.AmountsByCustodian.GetValueOrDefault(cust, 0m));
        }

        matrix.TotalPc = matrix.ColumnTotals.Values.Sum();
        matrix.TotalExpenses = buyerAudits.Sum(b => b.TotalExpenses);
        matrix.ActualChangeReturned = buyerAudits.Sum(b => b.ActualChangeReturned);

        return matrix;
    }

    private static string ResolveReleaserName(PcfRelease r)
    {
        if (r.ReleasedByTreasuryUser != null && !string.IsNullOrWhiteSpace(r.ReleasedByTreasuryUser.Name))
        {
            return r.ReleasedByTreasuryUser.Name.Trim().ToUpperInvariant();
        }
        if (r.Establishment != null && !string.IsNullOrWhiteSpace(r.Establishment.Name))
        {
            return r.Establishment.Name.Trim().ToUpperInvariant();
        }
        return "OTHERS";
    }
    private static string ResolveExpenseDescription(AuditItemDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.ExpenseSourceName))
        {
            return detail.ExpenseSourceName.Trim();
        }

        if (detail.ExpenseSource != null && !string.IsNullOrWhiteSpace(detail.ExpenseSource.Name))
        {
            return detail.ExpenseSource.Name.Trim();
        }
        return "NO RECEIPT";
    }

    private static List<BuyerExpenseLine> BuildBuyerExpenseLines(IEnumerable<AuditItemDetail> details)
    {
        return details
            .GroupBy(ad => new
            {
                ad.AuditItemId,
                Date = ad.AuditItem.EntryDate.Date,
                Allocation = ResolveExpenseAllocation(ad)
            })
            .OrderBy(g => g.Key.Date)
            .ThenBy(g => g.Key.AuditItemId)
            .Select(g =>
            {
                var desc = ResolveGroupExpenseDescription(g);
                var items = string.Join(", ", g.Select(d => d.ItemName).Where(n => !string.IsNullOrWhiteSpace(n) && n != "Item" && n != "General Expense").Distinct());
                var hasReceipt = g.All(d => d.ReceiptStatus != ReceiptLineStatus.NoReceipt);
                return new BuyerExpenseLine
                {
                    Date = g.Key.Date,
                    Description = desc,
                    Item = string.IsNullOrWhiteSpace(items) ? desc : items,
                    Amount = g.Sum(d => d.Total),
                    Allocation = g.Key.Allocation,
                    HasReceipt = hasReceipt
                };
            })
            .ToList();
    }

    private static string ResolveGroupExpenseDescription(IEnumerable<AuditItemDetail> details)
    {
        var list = details.ToList();
        var firstWithSource = list.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.ExpenseSourceName));
        if (firstWithSource != null)
        {
            return firstWithSource.ExpenseSourceName!.Trim().ToUpperInvariant();
        }

        var firstWithEntity = list.FirstOrDefault(d => d.ExpenseSource != null && !string.IsNullOrWhiteSpace(d.ExpenseSource.Name));
        if (firstWithEntity != null)
        {
            return firstWithEntity.ExpenseSource!.Name.Trim().ToUpperInvariant();
        }

        var distinctItemNames = list.Select(d => d.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (distinctItemNames.Count == 1 && distinctItemNames[0] != "Item" && distinctItemNames[0] != "General Expense")
        {
            return distinctItemNames[0].Trim().ToUpperInvariant();
        }
        if (distinctItemNames.Count > 0)
        {
            return distinctItemNames[0].Trim().ToUpperInvariant();
        }

        return "EXPENSE";
    }

    private static string ResolveExpenseAllocation(AuditItemDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.AllocationNotes))
        {
            return detail.AllocationNotes.Trim();
        }

        if (detail.AssignedEstablishment != null)
        {
            return detail.AssignedEstablishment.Name;
        }

        if (detail.CostCenter != null)
        {
            return detail.CostCenter.Name;
        }

        return detail.AuditItem.Establishment?.Name ?? "OTHERS";
    }

    private static decimal GetExpenseAmount(IEnumerable<AuditItemDetail> details, params string[] keywords)
    {
        return details
            .Where(d => keywords.Any(k => d.ItemName.Contains(k, StringComparison.OrdinalIgnoreCase) 
                                       || (d.PnlCategoryName != null && d.PnlCategoryName.Contains(k, StringComparison.OrdinalIgnoreCase))))
            .Sum(d => d.Total);
    }
}
