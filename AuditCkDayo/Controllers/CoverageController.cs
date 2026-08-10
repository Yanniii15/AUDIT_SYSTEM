using AuditCkDayo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var coverages = await _context.ManagerCoverages
                .AsNoTracking()
                .Include(c => c.CoveredManager)
                .Include(c => c.CoveringManager)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return View(coverages);
        }
    }
}
