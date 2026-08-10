using AuditCkDayo.Data;
using AuditCkDayo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager,Admin")]
    public class TreasuryController : Controller
    {
        private readonly AuditDbContext _context;

        public TreasuryController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(DateTime? date = null)
        {
            var flowDate = date?.Date ?? DateTime.Today;
            var model = new TreasuryCashFlowViewModel { CashFlowDate = flowDate };
            return View(model);
        }

        [HttpGet]
        public IActionResult ReleasePcf()
        {
            return View(new PcfReleaseViewModel());
        }

        [HttpGet]
        public IActionResult Settlement()
        {
            return View(new AuditSettlementViewModel());
        }
    }
}
