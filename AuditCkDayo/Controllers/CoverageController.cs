using System.ComponentModel.DataAnnotations;
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
        public async Task<IActionResult> Create(CoverageCreateForm form)
        {
            var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(currentUserIdValue, out var currentUserId))
            {
                ModelState.AddModelError(string.Empty, "Current user is required to create coverage.");
                TempData["Error"] = "Current user is required to create coverage.";
            }

            if (form.CoveredManagerId == form.CoveringManagerId)
            {
                ModelState.AddModelError(nameof(CoverageCreateForm.CoveringManagerId), "Covered and covering manager must be different.");
                TempData["Error"] = "Covered and covering manager must be different.";
            }

            if (form.EndDate.Date < form.StartDate.Date)
            {
                ModelState.AddModelError(nameof(CoverageCreateForm.EndDate), "End date must be on or after start date.");
                TempData["Error"] = "End date must be on or after start date.";
            }

            var requestedManagerIds = new[] { form.CoveredManagerId, form.CoveringManagerId }
                .Distinct()
                .ToList();

            var activeManagerIds = await _context.Users
                .AsNoTracking()
                .Where(u => requestedManagerIds.Contains(u.Id) && u.Role == UserRole.Manager && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            if (!activeManagerIds.Contains(form.CoveredManagerId))
            {
                ModelState.AddModelError(nameof(CoverageCreateForm.CoveredManagerId), "Covered manager must be an active manager.");
                TempData["Error"] = "Covered and covering managers must be active managers.";
            }

            if (!activeManagerIds.Contains(form.CoveringManagerId))
            {
                ModelState.AddModelError(nameof(CoverageCreateForm.CoveringManagerId), "Covering manager must be an active manager.");
                TempData["Error"] = "Covered and covering managers must be active managers.";
            }

            if (!ModelState.IsValid)
            {
                await LoadManagerOptionsAsync();
                return View("Index", await GetCoveragesAsync());
            }

            var coverage = new ManagerCoverage
            {
                CoveredManagerId = form.CoveredManagerId,
                CoveringManagerId = form.CoveringManagerId,
                StartDate = form.StartDate.Date,
                EndDate = form.EndDate.Date,
                Scope = form.Scope,
                Reason = form.Reason,
                IsActive = form.IsActive,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

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

    public class CoverageCreateForm
    {
        [Required]
        public int CoveredManagerId { get; set; }

        [Required]
        public int CoveringManagerId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public CoverageScope Scope { get; set; } = CoverageScope.All;

        [MaxLength(255)]
        public string? Reason { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
