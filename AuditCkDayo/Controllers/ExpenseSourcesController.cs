using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ExpenseSourcesController : Controller
    {
        private readonly AuditDbContext _context;

        public ExpenseSourcesController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var sources = await _context.ExpenseSources
                .AsNoTracking()
                .OrderBy(source => source.Name)
                .ToListAsync();

            return View(sources);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name)
        {
            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["Error"] = "Store / source name is required.";
                return RedirectToAction(nameof(Index));
            }

            var exists = await _context.ExpenseSources.AnyAsync(source => source.Name == normalizedName);
            if (exists)
            {
                TempData["Error"] = "That store / source already exists.";
                return RedirectToAction(nameof(Index));
            }

            _context.ExpenseSources.Add(new ExpenseSource
            {
                Name = normalizedName,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            TempData["Message"] = "Store / source registered.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var source = await _context.ExpenseSources.FirstOrDefaultAsync(s => s.Id == id);
            if (source == null)
            {
                return NotFound();
            }

            source.IsActive = !source.IsActive;
            await _context.SaveChangesAsync();

            TempData["Message"] = source.IsActive ? "Store / source activated." : "Store / source hidden from audit dropdowns.";
            return RedirectToAction(nameof(Index));
        }
    }
}
