using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager,Admin")]
    public class EstablishmentsController : Controller
    {
        private readonly AuditDbContext _context;

        public EstablishmentsController(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var establishments = await _context.Establishments.ToListAsync();
            return View(establishments);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Establishment establishment)
        {
            if (ModelState.IsValid)
            {
                var exists = await _context.Establishments.AnyAsync(e => e.Name == establishment.Name);
                if (exists)
                {
                    ModelState.AddModelError("Name", "Establishment name already exists.");
                    return View(establishment);
                }

                _context.Establishments.Add(establishment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(establishment);
        }
    }
}
