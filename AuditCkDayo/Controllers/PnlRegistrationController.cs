using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PnlRegistrationController : Controller
    {
        private readonly AuditDbContext _context;

        public PnlRegistrationController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _context.PnlCategories
                .AsNoTracking()
                .OrderBy(category => category.Section)
                .ThenBy(category => category.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, PnlExpenseSection section)
        {
            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction(nameof(Index));
            }

            if (section != PnlExpenseSection.COGS && section != PnlExpenseSection.OPEX)
            {
                TempData["Error"] = "P&L category section must be COGS or OPEX.";
                return RedirectToAction(nameof(Index));
            }

            var exists = await _context.PnlCategories.AnyAsync(category => category.Section == section && category.Name == normalizedName);
            if (exists)
            {
                TempData["Error"] = "That P&L category already exists in this section.";
                return RedirectToAction(nameof(Index));
            }

            _context.PnlCategories.Add(new PnlCategory
            {
                Name = normalizedName,
                Section = section,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            TempData["Message"] = "P&L category registered.";
            return RedirectToAction(nameof(Index));
        }
    }
}
