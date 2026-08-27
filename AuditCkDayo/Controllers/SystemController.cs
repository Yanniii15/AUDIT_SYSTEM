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
        public async Task<IActionResult> ImportChelseaTreasury([FromServices] AuditCkDayo.Data.AuditDbContext context)
        {
            try
            {
                await AuditCkDayo.Scripts.ProductionChelseaImport.Run(context);
                return Ok("Success: Imported all 16 of Chelsea's daily cash flow sheets (August 1 to August 26) successfully!");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner Exception: {ex.InnerException.Message}" : "";
                return StatusCode(500, $"Error during Chelsea import: {ex.Message}{inner} \n {ex.StackTrace}");
            }
        }
    }
}
