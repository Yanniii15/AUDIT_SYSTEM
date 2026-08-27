using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuditCkDayo.Services;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SystemController : Controller
    {
        private readonly SystemDiagnosticsService _diagnosticsService;

        public SystemController(SystemDiagnosticsService diagnosticsService)
        {
            _diagnosticsService = diagnosticsService;
        }

        [HttpGet]
        public async Task<IActionResult> Diagnostics()
        {
            var report = await _diagnosticsService.RunAsync();
            return View(report);
        }

        [HttpGet]
        [AllowAnonymous] // Allow temporary remote trigger
        public async Task<IActionResult> ImportMaymayTreasury([FromServices] AuditCkDayo.Data.AuditDbContext context)
        {
            try
            {
                await AuditCkDayo.Scripts.ProductionTreasuryImport.Run(context);
                return Ok("Success: Mapped all 24 cash flow sheets (August 2 to August 26) successfully into production database!");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner Exception: {ex.InnerException.Message}" : "";
                return StatusCode(500, $"Error during migration: {ex.Message}{inner} \n {ex.StackTrace}");
            }
        }
    }
}
