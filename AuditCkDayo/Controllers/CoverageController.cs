using System.Security.Claims;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Admin")]
    public class CoverageController : Controller
    {
        private readonly AuditDbContext _context;

        public CoverageController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await LoadManagerOptionsAsync();
            return View(await GetCoveragesAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ManagerCoverage coverage)
        {
            var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(currentUserIdValue, out var currentUserId))
            {
                ModelState.AddModelError(string.Empty, "Current user is required to create coverage.");
                TempData["Error"] = "Current user is required to create coverage.";
            }

            if (coverage.CoveredManagerId == coverage.CoveringManagerId)
            {
                ModelState.AddModelError(nameof(ManagerCoverage.CoveringManagerId), "Covered and covering manager must be different.");
                TempData["Error"] = "Covered and covering manager must be different.";
            }

            if (coverage.EndDate.Date < coverage.StartDate.Date)
            {
                ModelState.AddModelError(nameof(ManagerCoverage.EndDate), "End date must be on or after start date.");
                TempData["Error"] = "End date must be on or after start date.";
            }

            if (!ModelState.IsValid)
            {
                await LoadManagerOptionsAsync();
                return View("Index", await GetCoveragesAsync());
            }

            coverage.StartDate = coverage.StartDate.Date;
            coverage.EndDate = coverage.EndDate.Date;
            coverage.CreatedByUserId = currentUserId;
            coverage.CreatedAt = DateTime.UtcNow;

            _context.ManagerCoverages.Add(coverage);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Coverage assignment created.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<ManagerCoverage>> GetCoveragesAsync()
        {
            return await _context.ManagerCoverages
                .AsNoTracking()
                .Include(c => c.CoveredManager)
                .Include(c => c.CoveringManager)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        private async Task LoadManagerOptionsAsync()
        {
            var managers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Manager && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.ManagerUsers = new SelectList(managers, "Id", "Name");
        }
    }
}
