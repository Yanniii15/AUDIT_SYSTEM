using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditCkDayo.Controllers
{
    [AllowAnonymous]
    public class AppDownloadController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public AppDownloadController(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        [HttpGet("/download/android")]
        [HttpGet("/apk")]
        [HttpGet("/app/android")]
        public IActionResult DownloadAndroid()
        {
            var apkPath = Path.Combine(_env.WebRootPath, "app", "makbie-companies.apk");
            if (System.IO.File.Exists(apkPath))
            {
                return PhysicalFile(apkPath, "application/vnd.android.package-archive", "makbie-companies.apk", enableRangeProcessing: true);
            }

            var externalUrl = _config["MobileApp:AndroidDownloadUrl"];
            if (!string.IsNullOrWhiteSpace(externalUrl))
            {
                return Redirect(externalUrl);
            }

            return Content(
                "<!DOCTYPE html><html><head><meta charset='utf-8'/><meta name='viewport' content='width=device-width,initial-scale=1'/><title>Makbie Companies - Android App</title><style>body{font-family:system-ui,-apple-system,sans-serif;background:#041632;color:#ffffff;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;padding:20px;text-align:center}.card{background:#ffffff;color:#0f172a;padding:36px 28px;border-radius:24px;max-width:420px;box-shadow:0 12px 36px rgba(0,0,0,0.35)}img{height:56px;margin-bottom:18px}h1{font-size:22px;font-weight:800;color:#041632;margin:0 0 10px}p{font-size:14px;color:#64748b;line-height:1.5;margin:0 0 20px}.badge{display:inline-block;padding:4px 12px;border-radius:20px;background:#e0f2fe;color:#0369a1;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:14px}</style></head><body><div class='card'><img src='/images/logo.png' alt='Makbie Logo'/><div class='badge'>Android Package</div><h1>Makbie Companies App</h1><p>The Android installer (.apk) is ready to be linked. Once the build finishes, downloading will start automatically here.</p></div></body></html>",
                "text/html"
            );
        }
    }
}
