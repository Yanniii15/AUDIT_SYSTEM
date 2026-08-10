using AuditCkDayo.Data;
using AuditCkDayo.ViewModels;
using Microsoft.EntityFrameworkCore;
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
            var selectedDate = date?.Date ?? DateTime.Today;
            var flow = _context.TreasuryCashFlows
                .AsNoTracking()
                .Include(f => f.Entries)
                    .ThenInclude(e => e.Establishment)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.CostCenter)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.RelatedUser)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.SourceDocument)
                .FirstOrDefault(f => f.CashFlowDate == selectedDate);

            if (flow == null)
            {
                return View(new TreasuryCashFlowViewModel { SelectedDate = selectedDate });
            }

            flow.RecomputeTotals();

            var model = new TreasuryCashFlowViewModel
            {
                SelectedDate = selectedDate,
                FlowId = flow.Id,
                Status = flow.Status,
                StartingBalance = flow.StartingBalance,
                TotalCashIn = flow.TotalCashIn,
                TotalCashOut = flow.TotalCashOut,
                NetCashFlow = flow.NetCashFlow,
                ClosingBalance = flow.ClosingBalance,
                Entries = flow.Entries.OrderBy(e => e.Id).ToList()
            };

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
