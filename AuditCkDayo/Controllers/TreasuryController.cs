using AuditCkDayo.Data;
using AuditCkDayo.ViewModels;
using AuditCkDayo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        public async Task<IActionResult> ReleasePcf()
        {
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = DateTime.Today
            };

            await PopulateReleasePcfLookupsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleasePcf(PcfReleaseViewModel model)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            model.ReleaseDate = model.ReleaseDate.Date;

            if (model.Amount <= 0m)
            {
                ModelState.AddModelError(nameof(PcfReleaseViewModel.Amount), "Amount must be greater than zero.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateReleasePcfLookupsAsync(model);
                return View(model);
            }

            var release = new PcfRelease
            {
                ReleasedByTreasuryUserId = currentUserId,
                ReceiverUserId = model.ReceiverUserId,
                ReceiverName = model.ReceiverName,
                EstablishmentId = model.EstablishmentId,
                Amount = model.Amount,
                ReleaseDate = model.ReleaseDate,
                Purpose = model.Purpose,
                Status = PcfReleaseStatus.Released
            };

            var flow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                .FirstOrDefaultAsync(f => f.CashFlowDate == model.ReleaseDate);

            if (flow == null)
            {
                flow = new TreasuryCashFlow
                {
                    TreasuryUserId = currentUserId,
                    CashFlowDate = model.ReleaseDate,
                    StartingBalance = 0m,
                    Status = TreasuryCashFlowStatus.Open
                };

                _context.TreasuryCashFlows.Add(flow);
            }

            var entry = new CashFlowEntry
            {
                TreasuryCashFlow = flow,
                Direction = CashFlowDirection.Out,
                Category = CashFlowCategory.PcfRelease,
                EstablishmentId = model.EstablishmentId,
                RelatedUserId = model.ReceiverUserId,
                Amount = model.Amount,
                Notes = model.Purpose,
                CreatedByUserId = currentUserId,
                ConfirmedByUserId = currentUserId
            };

            flow.Entries.Add(entry);
            flow.RecomputeTotals();
            _context.PcfReleases.Add(release);

            await _context.SaveChangesAsync();

            release.CashFlowEntryId = entry.Id;
            await _context.SaveChangesAsync();

            TempData["Message"] = "PCF release saved.";
            return RedirectToAction(nameof(Index), "Treasury", new { date = model.ReleaseDate });
        }

        private async Task PopulateReleasePcfLookupsAsync(PcfReleaseViewModel model)
        {
            var receiverUsers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();

            var establishments = await _context.Establishments
                .AsNoTracking()
                .Where(e => e.IsOperatingBranch && e.IsActive)
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.ReceiverUsers = new SelectList(receiverUsers, "Id", "Name", model.ReceiverUserId);
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
        }

        [HttpGet]
        public IActionResult Settlement()
        {
            return View(new AuditSettlementViewModel());
        }
    }
}
