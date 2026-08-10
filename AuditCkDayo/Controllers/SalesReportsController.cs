using AuditCkDayo.Data;
using AuditCkDayo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager,BranchStaff,Admin")]
    public class SalesReportsController : Controller
    {
        private readonly AuditDbContext _context;
        private readonly IOcrService _ocrService;

        public SalesReportsController(AuditDbContext context, IOcrService ocrService)
        {
            _context = context;
            _ocrService = ocrService;
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            await PopulateEstablishments();
            return View();
        }

        private async Task PopulateEstablishments(int? selectedId = null)
        {
            var establishments = await _context.Establishments
                .AsNoTracking()
                .Where(e => e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous)
                .OrderBy(e => e.Name)
                .ToListAsync();
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name", selectedId);
        }
    }
}
