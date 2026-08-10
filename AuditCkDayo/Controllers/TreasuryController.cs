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

            var hasValidEstablishment = false;
            if (model.EstablishmentId.HasValue)
            {
                hasValidEstablishment = await _context.Establishments
                    .AsNoTracking()
                    .AnyAsync(e => e.Id == model.EstablishmentId.Value
                        && e.IsOperatingBranch
                        && e.IsActive
                        && !e.IsMiscellaneous);

                if (!hasValidEstablishment)
                {
                    ModelState.AddModelError(nameof(PcfReleaseViewModel.EstablishmentId), "Select an active operating branch.");
                }
            }

            model.ReceiverName = string.IsNullOrWhiteSpace(model.ReceiverName)
                ? null
                : model.ReceiverName.Trim();

            var hasValidReceiverUser = false;
            if (model.ReceiverUserId.HasValue)
            {
                hasValidReceiverUser = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == model.ReceiverUserId.Value && !u.IsDeleted);

                if (!hasValidReceiverUser)
                {
                    ModelState.AddModelError(nameof(PcfReleaseViewModel.ReceiverUserId), "Select an active receiver user.");
                }
            }

            var hasReceiverName = !string.IsNullOrWhiteSpace(model.ReceiverName);
            if (!hasValidReceiverUser && !hasReceiverName && !hasValidEstablishment)
            {
                ModelState.AddModelError(nameof(PcfReleaseViewModel.ReceiverName), "Provide a receiver user, receiver name, or establishment.");
            }

            if (model.ReceiverName?.Length > 100)
            {
                ModelState.AddModelError(nameof(PcfReleaseViewModel.ReceiverName), "Receiver name must be 100 characters or fewer.");
            }

            if (model.Purpose?.Length > 255)
            {
                ModelState.AddModelError(nameof(PcfReleaseViewModel.Purpose), "Purpose must be 255 characters or fewer.");
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
            if (model.ReceiverUserId.HasValue)
            {
                var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.ReceiverUserId.Value && !u.IsDeleted);
                if (receiver != null)
                {
                    receiver.PcfBalance += model.Amount;
                    receiver.DailyStartingFloat += model.Amount;

                    var ledger = new PettyCashLedger
                    {
                        UserId = receiver.Id,
                        TransactionType = LedgerTransactionType.VaultFunding,
                        Amount = model.Amount,
                        ResultingBalance = receiver.PcfBalance,
                        Timestamp = DateTime.Now,
                        Notes = $"PCF release from vault: {model.Purpose ?? "Funding"}"
                    };
                    _context.PettyCashLedgers.Add(ledger);
                }
            }

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
                .Where(e => e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous)
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.ReceiverUsers = new SelectList(receiverUsers, "Id", "Name", model.ReceiverUserId);
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
        }

        [HttpGet]
        public async Task<IActionResult> Settlement()
        {
            var model = new AuditSettlementViewModel();
            await PopulateSettlementLookupsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settlement(AuditSettlementViewModel model)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            var currentUserExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == currentUserId && !u.IsDeleted);

            if (!currentUserExists)
            {
                return Forbid();
            }

            if (model.TotalPCReleased < 0m)
            {
                ModelState.AddModelError(nameof(AuditSettlementViewModel.TotalPCReleased), "Total PC released cannot be negative.");
            }

            if (model.TotalAcceptedExpenses < 0m)
            {
                ModelState.AddModelError(nameof(AuditSettlementViewModel.TotalAcceptedExpenses), "Total accepted expenses cannot be negative.");
            }

            if (model.ActualChangeReturned < 0m)
            {
                ModelState.AddModelError(nameof(AuditSettlementViewModel.ActualChangeReturned), "Actual change returned cannot be negative.");
            }

            if (model.PcfReleaseId.HasValue)
            {
                var pcfReleaseAvailable = await _context.PcfReleases
                    .AsNoTracking()
                    .AnyAsync(r => r.Id == model.PcfReleaseId.Value
                        && r.Status != PcfReleaseStatus.Settled
                        && r.Status != PcfReleaseStatus.Cancelled
                        && !_context.AuditSettlements.Any(s => s.PcfReleaseId == r.Id));

                if (!pcfReleaseAvailable)
                {
                    ModelState.AddModelError(nameof(AuditSettlementViewModel.PcfReleaseId), "Select a valid PCF release.");
                }
            }

            var responsibleManagerId = currentUserId;
            if (model.ResponsibleManagerId.HasValue)
            {
                var managerExists = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == model.ResponsibleManagerId.Value
                        && u.Role == UserRole.Manager
                        && !u.IsDeleted);

                if (managerExists)
                {
                    responsibleManagerId = model.ResponsibleManagerId.Value;
                }
                else
                {
                    ModelState.AddModelError(nameof(AuditSettlementViewModel.ResponsibleManagerId), "Select an active manager.");
                }
            }

            model.ReceiverName = model.ReceiverName?.Trim();
            if (model.ReceiverName?.Length > 100)
            {
                ModelState.AddModelError(nameof(AuditSettlementViewModel.ReceiverName), "Receiver name must be 100 characters or fewer.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSettlementLookupsAsync(model);
                return View(model);
            }

            var settlement = new AuditSettlement
            {
                PcfReleaseId = model.PcfReleaseId,
                ReceiverName = model.ReceiverName,
                ResponsibleManagerId = responsibleManagerId,
                ProcessedByUserId = currentUserId,
                TotalPCReleased = model.TotalPCReleased,
                TotalAcceptedExpenses = model.TotalAcceptedExpenses,
                ActualChangeReturned = model.ActualChangeReturned,
                Status = AuditSettlementStatus.Confirmed
            };

            settlement.Recompute();

            _context.AuditSettlements.Add(settlement);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Audit settlement saved.";
            return RedirectToAction(nameof(Settlement));
        }

        private async Task PopulateSettlementLookupsAsync(AuditSettlementViewModel model)
        {
            var managers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Manager && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToListAsync();

            var settledReleaseIds = _context.AuditSettlements
                .AsNoTracking()
                .Where(s => s.PcfReleaseId.HasValue)
                .Select(s => s.PcfReleaseId!.Value);

            var availablePcfReleases = await _context.PcfReleases
                .AsNoTracking()
                .Where(r => r.Status != PcfReleaseStatus.Settled
                    && r.Status != PcfReleaseStatus.Cancelled
                    && (r.Id == model.PcfReleaseId || !settledReleaseIds.Contains(r.Id)))
                .OrderByDescending(r => r.ReleaseDate)
                .ThenBy(r => r.Id)
                .Select(r => new
                {
                    r.Id,
                    Display = $"#{r.Id} - {(r.ReceiverName ?? r.ReceiverUser!.Name)} - {r.Amount:n2}"
                })
                .ToListAsync();

            ViewBag.ResponsibleManagers = new SelectList(managers, "Id", "Name", model.ResponsibleManagerId);
            ViewBag.PcfReleases = new SelectList(availablePcfReleases, "Id", "Display", model.PcfReleaseId);
        }
    }
}
