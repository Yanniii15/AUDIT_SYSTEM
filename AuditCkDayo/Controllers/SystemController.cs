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
        [AllowAnonymous]
        public async Task<IActionResult> ResetPcfFloats([FromServices] AuditCkDayo.Data.AuditDbContext context)
        {
            try
            {
                await AuditCkDayo.Scripts.ProductionResetPcfFloats.Run(context);
                return Ok("Success: Reset starting floats and current balances to 0 for all active buyers successfully!");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner Exception: {ex.InnerException.Message}" : "";
                return StatusCode(500, $"Error during float reset: {ex.Message}{inner} \n {ex.StackTrace}");
            }
        }
    }
}
