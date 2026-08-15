using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AuditDbContext _context;
    private readonly Services.CoverageService? _coverageService;

    public HomeController(ILogger<HomeController> logger, AuditDbContext context) : this(logger, context, null)
    {
    }

    public HomeController(ILogger<HomeController> logger, AuditDbContext context, Services.CoverageService? coverageService)
    {
        _logger = logger;
        _context = context;
        _coverageService = coverageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DashboardViewModel model)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            return Challenge();
        }
        
        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (currentUser == null)
        {
            return Challenge();
        }

        ViewBag.CurrentUser = currentUser;

        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        var coveredManagerIds = _coverageService != null 
            ? await _coverageService.GetCoveredManagerIdsAsync(userId, DateTime.Today, CoverageScope.BuyerAudits)
            : new List<int>();

        var coveredManagerSalesIds = _coverageService != null
            ? await _coverageService.GetCoveredManagerIdsAsync(userId, DateTime.Today, CoverageScope.SalesReports)
            : new List<int>();

        var coveredManagerIdsAll = _coverageService != null
            ? await _coverageService.GetCoveredManagerIdsAsync(userId, DateTime.Today, CoverageScope.All)
            : new List<int>();
        IQueryable<AuditItem> query = _context.AuditItems
            .AsNoTracking()
            .Include(a => a.Buyer)
            .Include(a => a.Establishment)
            .Include(a => a.Details);

        // Apply role-based filtering
        if (role == "Manager")
        {
            query = query.Where(a => a.BuyerId == userId 
                || a.AssignedReviewerId == userId 
                || a.Buyer.ManagerId == userId
                || (coveredManagerIds.Any() && a.Buyer.ManagerId.HasValue && coveredManagerIds.Contains(a.Buyer.ManagerId.Value)));
        }
        else if (role == "Buyer")
        {
            query = query.Where(a => a.BuyerId == userId);
        }
        else if (role == "BranchStaff")
        {
            if (currentUser != null && currentUser.EstablishmentId.HasValue)
            {
                query = query.Where(a => a.EstablishmentId == currentUser.EstablishmentId.Value);
            }
            else
            {
                query = query.Where(a => false);
            }
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        model.TodayAudits = await query
            .Where(a => (a.SubmittedAt ?? a.EntryDate) >= today && (a.SubmittedAt ?? a.EntryDate) < tomorrow)
            .OrderByDescending(a => a.SubmittedAt ?? a.EntryDate)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        IQueryable<SalesReport> pendingSalesQuery = _context.SalesReports
            .AsNoTracking()
            .Include(r => r.DocumentRecord)
            .Include(r => r.Establishment)
            .Where(r => r.Status == SalesReportStatus.PendingManagerVerification
                || r.DocumentRecord.ReviewStatus == DocumentReviewStatus.PendingManagerVerification);

        if (role == "Manager")
        {
            pendingSalesQuery = pendingSalesQuery.Where(r => _context.Users
                .Any(u => !u.IsDeleted
                    && u.Role == UserRole.BranchStaff
                    && (u.ManagerId == userId || (coveredManagerSalesIds.Any() && u.ManagerId.HasValue && coveredManagerSalesIds.Contains(u.ManagerId.Value)))
                    && u.EstablishmentId == r.EstablishmentId));
        }
        else if (role == "BranchStaff")
        {
            if (currentUser.EstablishmentId.HasValue)
            {
                pendingSalesQuery = pendingSalesQuery.Where(r => r.EstablishmentId == currentUser.EstablishmentId.Value);
            }
            else
            {
                pendingSalesQuery = pendingSalesQuery.Where(r => false);
            }
        }
        else if (role == "Buyer")
        {
            pendingSalesQuery = pendingSalesQuery.Where(r => false);
        }

        model.PendingSalesReports = await pendingSalesQuery
            .OrderBy(r => r.BusinessDate)
            .ThenBy(r => r.Establishment.Name)
            .ThenBy(r => r.Id)
            .ToListAsync();

        IQueryable<SalesReport> historicalSalesQuery = _context.SalesReports
            .AsNoTracking()
            .Include(r => r.DocumentRecord)
            .Include(r => r.Establishment);

        if (role == "Manager")
        {
            historicalSalesQuery = historicalSalesQuery.Where(r => _context.Users
                .Any(u => !u.IsDeleted
                    && u.Role == UserRole.BranchStaff
                    && (u.ManagerId == userId || (coveredManagerSalesIds.Any() && u.ManagerId.HasValue && coveredManagerSalesIds.Contains(u.ManagerId.Value)))
                    && u.EstablishmentId == r.EstablishmentId));
        }
        else if (role == "BranchStaff")
        {
            historicalSalesQuery = currentUser.EstablishmentId.HasValue
                ? historicalSalesQuery.Where(r => r.EstablishmentId == currentUser.EstablishmentId.Value)
                : historicalSalesQuery.Where(r => false);
        }
        else if (role == "Buyer")
        {
            historicalSalesQuery = historicalSalesQuery.Where(r => false);
        }

        // Apply search filters
        if (model.StartDate.HasValue)
        {
            query = query.Where(a => a.EntryDate >= model.StartDate.Value);
            historicalSalesQuery = historicalSalesQuery.Where(r => r.BusinessDate >= model.StartDate.Value);
        }
        if (model.EndDate.HasValue)
        {
            query = query.Where(a => a.EntryDate < model.EndDate.Value.AddDays(1));
            historicalSalesQuery = historicalSalesQuery.Where(r => r.BusinessDate < model.EndDate.Value.AddDays(1));
        }
        if (model.Status.HasValue)
        {
            query = query.Where(a => a.Status == model.Status.Value);
        }
        if (model.EstablishmentId.HasValue)
        {
            query = query.Where(a => a.EstablishmentId == model.EstablishmentId.Value);
            historicalSalesQuery = historicalSalesQuery.Where(r => r.EstablishmentId == model.EstablishmentId.Value);
        }
        if (model.BuyerId.HasValue)
        {
            query = query.Where(a => a.BuyerId == model.BuyerId.Value);
        }

        if (model.RecordType == DashboardRecordType.Audits)
        {
            historicalSalesQuery = historicalSalesQuery.Where(r => false);
        }
        else if (model.RecordType == DashboardRecordType.DailySales)
        {
            query = query.Where(a => false);
        }

        // Calculate total amount from filtered items
        model.TotalAmount = await query.SumAsync(a => a.Amount);

        // Fetch matching items
        model.Audits = await query
            .OrderByDescending(a => a.EntryDate)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        model.HistoricalSalesReports = await historicalSalesQuery
            .OrderByDescending(r => r.BusinessDate)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        if (role == "Owner" || role == "Admin")
        {
            model.CashOnHandUsers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && (u.Role == UserRole.Buyer || u.Role == UserRole.BranchStaff || u.Role == UserRole.Manager))
                .Include(u => u.Establishment)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();
        }
        else if (role == "Manager")
        {
            model.CashOnHandUsers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && (u.Id == userId || u.ManagerId == userId || (coveredManagerIdsAll.Any() && u.ManagerId.HasValue && coveredManagerIdsAll.Contains(u.ManagerId.Value))))
                .Include(u => u.Establishment)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();
        }

        // Populate dropdowns based on role
        var establishments = await _context.Establishments.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
        ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);

        if (role == "Owner" || role == "Admin")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else if (role == "Manager")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer && u.ManagerId == userId && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else if (role == "BranchStaff")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else // Buyer
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }

        // Status filter select list. AuditStatus keeps legacy aliases for compatibility; the UI shows each numeric value once.
        ViewBag.Statuses = System.Enum.GetValues(typeof(AuditStatus))
            .Cast<AuditStatus>()
            .GroupBy(status => Convert.ToInt32(status))
            .Select(group => group.First())
            .Select(status => new SelectListItem
            {
                Value = status.ToString(),
                Text = status.ToString(),
                Selected = model.Status.HasValue && model.Status.Value == status
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportHistorical(DashboardViewModel model)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            return Challenge();
        }

        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (currentUser == null)
        {
            return Challenge();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        IQueryable<AuditItem> query = _context.AuditItems
            .AsNoTracking()
            .Include(a => a.Buyer)
            .Include(a => a.Establishment)
            .Include(a => a.Details);

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

        if (model.StartDate.HasValue)
        {
            query = query.Where(a => a.EntryDate >= model.StartDate.Value);
        }
        if (model.EndDate.HasValue)
        {
            query = query.Where(a => a.EntryDate < model.EndDate.Value.AddDays(1));
        }
        if (model.Status.HasValue)
        {
            query = query.Where(a => a.Status == model.Status.Value);
        }
        if (model.EstablishmentId.HasValue)
        {
            query = query.Where(a => a.EstablishmentId == model.EstablishmentId.Value);
        }
        if (model.BuyerId.HasValue)
        {
            query = query.Where(a => a.BuyerId == model.BuyerId.Value);
        }

        var audits = await query
            .OrderBy(a => a.Establishment.Name)
            .ThenByDescending(a => a.EntryDate)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        var csv = new StringBuilder();
        
        var groupedAudits = audits.GroupBy(a => a.Establishment.Name).OrderBy(g => g.Key);
        
        foreach (var group in groupedAudits)
        {
            var establishmentName = group.Key;
            
            // Add section header row
            csv.AppendLine($"ESTABLISHMENT: {EscapeCsv(establishmentName)},,,,,,,,");
            
            // Add table column headers for this section
            csv.AppendLine("ID,Buyer,Description,Date,Audit Status,Item Name,Qty,Unit Price,Line Total");
            
            foreach (var audit in group)
            {
                if (audit.Details != null && audit.Details.Any())
                {
                    foreach (var detail in audit.Details)
                    {
                        csv.AppendLine(string.Join(",", new[]
                        {
                            EscapeCsv($"AUD-{audit.Id}"),
                            EscapeCsv(audit.Buyer.Name),
                            EscapeCsv(audit.Description),
                            EscapeCsv(audit.EntryDate.ToString("yyyy-MM-dd")),
                            EscapeCsv(audit.Status.ToString()),
                            EscapeCsv(detail.ItemName),
                            EscapeCsv(detail.Quantity.ToString()),
                            EscapeCsv(detail.Price.ToString("F2")),
                            EscapeCsv(detail.Total.ToString("F2"))
                        }));
                    }
                }
                else
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        EscapeCsv($"AUD-{audit.Id}"),
                        EscapeCsv(audit.Buyer.Name),
                        EscapeCsv(audit.Description),
                        EscapeCsv(audit.EntryDate.ToString("yyyy-MM-dd")),
                        EscapeCsv(audit.Status.ToString()),
                        EscapeCsv("No items listed"),
                        EscapeCsv("1"),
                        EscapeCsv(audit.Amount.ToString("F2")),
                        EscapeCsv(audit.Amount.ToString("F2"))
                    }));
                }
            }
            
            // Add an empty line between different establishments
            csv.AppendLine(",,,,,,,,");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"historical-audits-{DateTime.Today:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
