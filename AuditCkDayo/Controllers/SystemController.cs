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
        public async Task<IActionResult> ResetPcf([FromServices] AuditCkDayo.Data.AuditDbContext context)
        {
            try
            {
                await AuditCkDayo.Scripts.ProductionResetPcf.Run(context);
                return Ok("Success: Reset PCF balance to 0 for all active buyers successfully!");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner Exception: {ex.InnerException.Message}" : "";
                return StatusCode(500, $"Error during PCF reset: {ex.Message}{inner} \n {ex.StackTrace}");
            }
        }
    }
}
