using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
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

    public HomeController(ILogger<HomeController> logger, AuditDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DashboardViewModel model)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            return Challenge();
        }
        
        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        ViewBag.CurrentUser = currentUser;

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        IQueryable<AuditItem> query = _context.AuditItems
            .AsNoTracking()
            .Include(a => a.Buyer)
            .Include(a => a.Establishment);

        // Apply role-based filtering
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
            if (currentUser != null && currentUser.EstablishmentId.HasValue)
            {
                query = query.Where(a => a.EstablishmentId == currentUser.EstablishmentId.Value);
            }
            else
            {
                query = query.Where(a => false);
            }
        }

        // Apply search filters
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

        // Calculate total amount from filtered items
        model.TotalAmount = await query.SumAsync(a => a.Amount);

        // Fetch matching items
        model.Audits = await query
            .OrderByDescending(a => a.EntryDate)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        // Populate dropdowns based on role
        var establishments = await _context.Establishments.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
        ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);

        if (role == "Owner")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else if (role == "Manager")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer && u.ManagerId == userId)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else if (role == "BranchStaff")
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Buyer)
                .OrderBy(u => u.Name)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }
        else // Buyer
        {
            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .ToListAsync();
            ViewBag.Buyers = new SelectList(buyers, "Id", "Name", model.BuyerId);
        }

        // Status filter select list
        var statuses = System.Enum.GetValues(typeof(AuditStatus))
            .Cast<AuditStatus>()
            .Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString(),
                Selected = model.Status.HasValue && model.Status.Value == s
            }).ToList();
        ViewBag.Statuses = new SelectList(statuses, "Value", "Text", model.Status?.ToString());

        return View(model);
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
