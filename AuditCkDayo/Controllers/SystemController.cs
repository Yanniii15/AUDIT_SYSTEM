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
    }
}
