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
        private readonly Services.SharedPcfFundService _pcfFund;
        private readonly Services.ITreasuryAudioExportService _audioExport;
        public TreasuryController(
            AuditDbContext context,
            Services.SharedPcfFundService? pcfFund = null,
            Services.ITreasuryAudioExportService? audioExport = null)
        {
            _context = context;
            _pcfFund = pcfFund ?? new Services.SharedPcfFundService(context);
            _audioExport = audioExport ?? new Services.UnconfiguredTreasuryAudioExportService();
        }

        [HttpGet]
        public IActionResult Index(DateTime? date = null)
        {
            var selectedDate = date?.Date ?? GetToday();
            var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = int.TryParse(currentUserIdValue, out var id) ? id : 0;

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
                .Include(f => f.Entries)
                    .ThenInclude(e => e.SalesReport)
                .FirstOrDefault(f => f.CashFlowDate == selectedDate && f.TreasuryUserId == currentUserId);

            if (flow != null)
            {
                var missingSalesEntries = flow.Entries
                    .Where(e => e.Category == CashFlowCategory.Sales && e.SalesReport == null && e.SourceDocumentId.HasValue)
                    .ToList();
                if (missingSalesEntries.Any())
                {
                    var docIds = missingSalesEntries.Select(e => e.SourceDocumentId!.Value).ToList();
                    var reports = _context.SalesReports
                        .AsNoTracking()
                        .Where(r => docIds.Contains(r.DocumentRecordId))
                        .ToDictionary(r => r.DocumentRecordId);

                    foreach (var entry in missingSalesEntries)
                    {
                        if (reports.TryGetValue(entry.SourceDocumentId!.Value, out var rep))
                        {
                            entry.SalesReport = rep;
                            entry.SalesReportId = rep.Id;
                        }
                    }
                }
            }

            if (flow == null)
            {
                var startingBalance = GetCarryForwardStartingBalance(selectedDate, currentUserId);

                PopulateManualCashFlowLookups();
                ViewBag.SpokenSummary = string.Empty;
                return View(new TreasuryCashFlowViewModel
                {
                    SelectedDate = selectedDate,
                    StartingBalance = startingBalance,
                    TotalCashIn = 0m,
                    TotalCashOut = 0m,
                    NetCashFlow = startingBalance,
                    ClosingBalance = startingBalance
                });
            }

            flow.RecomputeTotals();
            ViewBag.SpokenSummary = Services.TreasuryAudioExportService.BuildSpokenSummary(flow);

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

            PopulateManualCashFlowLookups();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAudioSummary(DateTime? date = null)
        {
            var selectedDate = date?.Date ?? GetToday();
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            var flow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                    .ThenInclude(e => e.Establishment)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.RelatedUser)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.CostCenter)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.SalesReport)
                .FirstOrDefaultAsync(f => f.CashFlowDate == selectedDate && f.TreasuryUserId == currentUserId);

            if (flow == null)
            {
                TempData["Error"] = "Treasury cash flow not found for the selected day.";
                return RedirectToAction(nameof(Index), new { date = selectedDate });
            }

            try
            {
                var audio = await _audioExport.GenerateSpeechAsync(flow, HttpContext.RequestAborted);
                bool isWav = audio.Length >= 4 && audio[0] == (byte)'R' && audio[1] == (byte)'I' && audio[2] == (byte)'F' && audio[3] == (byte)'F';
                string contentType = isWav ? "audio/wav" : "audio/mpeg";
                string extension = isWav ? "wav" : "mp3";
                return File(audio, contentType, $"Treasury_CashFlow_{selectedDate:yyyy-MM-dd}.{extension}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Treasury audio generation failed: {ex.Message}");
                TempData["Error"] = "Audio summary could not be generated. Please check the speech API key/configuration and try again.";
                return RedirectToAction(nameof(Index), new { date = selectedDate });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReleasePcf()
        {
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = GetToday()
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
                .FirstOrDefaultAsync(f => f.CashFlowDate == model.ReleaseDate && f.TreasuryUserId == currentUserId);

            if (flow == null)
            {
                var startingBalance = await GetCarryForwardStartingBalanceAsync(model.ReleaseDate, currentUserId);

                flow = new TreasuryCashFlow
                {
                    TreasuryUserId = currentUserId,
                    CashFlowDate = model.ReleaseDate,
                    StartingBalance = startingBalance,
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

            var releaser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId && !u.IsDeleted);
            if (releaser != null && (releaser.PcfBalance > 0m || releaser.DailyStartingFloat > 0m))
            {
                await _pcfFund.DebitAsync(releaser, model.Amount, adjustStartingFloat: true);
                _context.PettyCashLedgers.Add(new PettyCashLedger
                {
                    UserId = releaser.Id,
                    TransactionType = LedgerTransactionType.ExpenseDeduction,
                    Amount = -model.Amount,
                    ResultingBalance = await _pcfFund.GetAvailableBalanceAsync(releaser),
                    Timestamp = DateTime.Now,
                    AssociatedRecordId = release.Id,
                    CounterpartyUserId = model.ReceiverUserId,
                    Notes = $"PCF released from treasury. Notes: {model.Purpose ?? "Funding"}"
                });
            }
            flow.RecomputeTotals();
            if (model.ReceiverUserId.HasValue)
            {
                var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.ReceiverUserId.Value && !u.IsDeleted);
                if (receiver != null)
                {
                    await _pcfFund.CreditAsync(receiver, model.Amount, adjustStartingFloat: true);
                    var releaseBalance = await _pcfFund.GetAvailableBalanceAsync(receiver);

                    var ledger = new PettyCashLedger
                    {
                        UserId = receiver.Id,
                        TransactionType = LedgerTransactionType.VaultFunding,
                        Amount = model.Amount,
                        ResultingBalance = releaseBalance,
                        Timestamp = DateTime.Now,
                        Notes = $"PCF release from vault: {model.Purpose ?? "Funding"}"
                    };
                    _context.PettyCashLedgers.Add(ledger);

                    if (receiver.IsTreasury && receiver.Id != currentUserId)
                    {
                        var receivingFlow = await _context.TreasuryCashFlows
                            .Include(f => f.Entries)
                            .FirstOrDefaultAsync(f => f.CashFlowDate == model.ReleaseDate && f.TreasuryUserId == receiver.Id);

                        if (receivingFlow == null)
                        {
                            var receivingStartingBalance = await GetCarryForwardStartingBalanceAsync(model.ReleaseDate, receiver.Id);
                            receivingFlow = new TreasuryCashFlow
                            {
                                TreasuryUserId = receiver.Id,
                                CashFlowDate = model.ReleaseDate,
                                StartingBalance = receivingStartingBalance,
                                Status = TreasuryCashFlowStatus.Open
                            };
                            _context.TreasuryCashFlows.Add(receivingFlow);
                        }

                        receivingFlow.Entries.Add(new CashFlowEntry
                        {
                            TreasuryCashFlow = receivingFlow,
                            Direction = CashFlowDirection.In,
                            Category = CashFlowCategory.PcfRelease,
                            EstablishmentId = model.EstablishmentId,
                            RelatedUserId = currentUserId,
                            Amount = model.Amount,
                            Notes = model.Purpose,
                            CreatedByUserId = currentUserId,
                            ConfirmedByUserId = receiver.Id
                        });
                        receivingFlow.RecomputeTotals();
                    }
                }
            }

            _context.PcfReleases.Add(release);

            await _context.SaveChangesAsync();

            release.CashFlowEntryId = entry.Id;
            await _context.SaveChangesAsync();

            TempData["Message"] = "PCF release saved.";
            return RedirectToAction(nameof(Index), "Treasury", new { date = model.ReleaseDate });
        }

        [HttpGet]
        public async Task<IActionResult> EditEntry(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            var entry = await _context.CashFlowEntries
                .Include(e => e.TreasuryCashFlow)
                .Include(e => e.SourceDocument)
                .Include(e => e.SalesReport)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (entry.TreasuryCashFlow.Status == TreasuryCashFlowStatus.Closed)
            {
                TempData["Error"] = "This entry belongs to a closed/locked treasury day and cannot be edited.";
                return RedirectToAction(nameof(Index), new { date = entry.TreasuryCashFlow.CashFlowDate });
            }

            PopulateManualCashFlowLookups();
            ViewBag.FlowDate = entry.TreasuryCashFlow.CashFlowDate;
            ViewBag.CanUnconfirmSalesReport = false;
            ViewBag.LinkedSalesReportId = null;

            if (IsSalesReportCashIn(entry))
            {
                var linkedReport = await _context.SalesReports
                    .AsNoTracking()
                    .Where(r => r.DocumentRecordId == entry.SourceDocumentId!.Value)
                    .Select(r => new
                    {
                        r.Id,
                        r.Status,
                        ReviewStatus = r.DocumentRecord.ReviewStatus
                    })
                    .FirstOrDefaultAsync();

                ViewBag.CanUnconfirmSalesReport = linkedReport != null
                    && entry.TreasuryCashFlow.Status != TreasuryCashFlowStatus.Closed
                    && (linkedReport.Status == SalesReportStatus.Confirmed
                        || linkedReport.ReviewStatus == DocumentReviewStatus.Confirmed);
                ViewBag.LinkedSalesReportId = linkedReport?.Id;
            }
            return View(entry);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry([FromForm] CashFlowEntry entry, DateTime date)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            var dbEntry = await _context.CashFlowEntries
                .Include(e => e.TreasuryCashFlow)
                .FirstOrDefaultAsync(e => e.Id == entry.Id);

            if (dbEntry == null)
            {
                return NotFound();
            }

            if (dbEntry.TreasuryCashFlow.Status == TreasuryCashFlowStatus.Closed)
            {
                TempData["Error"] = "Cannot modify a closed treasury day.";
                return RedirectToAction(nameof(Index), new { date });
            }

            if (entry.Amount <= 0m)
            {
                ModelState.AddModelError(nameof(CashFlowEntry.Amount), "Amount must be greater than zero.");
            }

            ModelState.Remove(nameof(CashFlowEntry.TreasuryCashFlow));
            ModelState.Remove(nameof(CashFlowEntry.CreatedByUser));

            if (!ModelState.IsValid)
            {
                PopulateManualCashFlowLookups();
                ViewBag.FlowDate = dbEntry.TreasuryCashFlow.CashFlowDate;
                return View(entry);
            }

            dbEntry.Category = entry.Category;
            dbEntry.Amount = entry.Amount;
            dbEntry.EstablishmentId = entry.EstablishmentId;
            dbEntry.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? null : entry.Notes.Trim();

            dbEntry.TreasuryCashFlow.RecomputeTotals();
            await _context.SaveChangesAsync();

            TempData["Message"] = "Treasury entry updated.";
            return RedirectToAction(nameof(Index), new { date = dbEntry.TreasuryCashFlow.CashFlowDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEntry(int id, DateTime date)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            var entry = await _context.CashFlowEntries
                .Include(e => e.TreasuryCashFlow)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (entry.TreasuryCashFlow.Status == TreasuryCashFlowStatus.Closed)
            {
                TempData["Error"] = "Cannot delete entries on a closed treasury day.";
                return RedirectToAction(nameof(Index), new { date });
            }

            // Unlink any PcfReleases referencing this entry
            var linkedReleases = await _context.PcfReleases
                .Where(r => r.CashFlowEntryId == entry.Id)
                .ToListAsync();
            foreach (var release in linkedReleases)
            {
                release.CashFlowEntryId = null;
            }
            await _context.SaveChangesAsync();

            _context.CashFlowEntries.Remove(entry);
            await _context.SaveChangesAsync();

            entry.TreasuryCashFlow.RecomputeTotals();
            await _context.SaveChangesAsync();

            TempData["Message"] = "Treasury entry deleted.";
            return RedirectToAction(nameof(Index), new { date = entry.TreasuryCashFlow.CashFlowDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnconfirmSalesReport(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var entry = await _context.CashFlowEntries
                .Include(e => e.TreasuryCashFlow)
                    .ThenInclude(f => f.Entries)
                .Include(e => e.SourceDocument)
                .Include(e => e.SalesReport)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                TempData["Error"] = "Treasury entry not found.";
                return RedirectToAction(nameof(Index));
            }

            var flowDate = entry.TreasuryCashFlow.CashFlowDate;

            if (entry.TreasuryCashFlow.Status == TreasuryCashFlowStatus.Closed)
            {
                TempData["Error"] = "This entry belongs to a closed/locked treasury day and cannot be unconfirmed.";
                return RedirectToAction(nameof(Index), new { date = flowDate });
            }

            if (!IsSalesReportCashIn(entry))
            {
                TempData["Error"] = "Only linked sales cash-in entries can be unconfirmed from Treasury.";
                return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
            }

            var report = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .FirstOrDefaultAsync(r => r.DocumentRecordId == entry.SourceDocumentId.GetValueOrDefault());

            if (report == null)
            {
                TempData["Error"] = "Linked sales report not found.";
                return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
            }

            if (report.Status != SalesReportStatus.Confirmed && report.DocumentRecord.ReviewStatus != DocumentReviewStatus.Confirmed)
            {
                TempData["Error"] = "Only confirmed sales reports can be unconfirmed.";
                return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
            }

            _context.CashFlowEntries.Remove(entry);

            report.Status = SalesReportStatus.Draft;
            report.ConfirmedByUserId = null;
            report.ConfirmedAt = null;
            report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Draft;
            report.DocumentRecord.ConfirmedByUserId = null;
            report.DocumentRecord.ConfirmedAt = null;

            entry.TreasuryCashFlow.Entries.Remove(entry);
            entry.TreasuryCashFlow.RecomputeTotals();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Message"] = "Sales report unconfirmed. Treasury cash-in was removed and the branch can edit the report again.";
            return RedirectToAction(nameof(Index), new { date = flowDate });
        }

        private static bool IsSalesReportCashIn(CashFlowEntry entry)
        {
            return entry.Direction == CashFlowDirection.In
                && entry.Category == CashFlowCategory.Sales
                && entry.SourceDocumentId.HasValue
                && entry.SourceDocument?.DocumentType == DocumentType.DailySalesReport;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordCashIn([Bind(Prefix = "ManualCashIn")] ManualCashInViewModel model)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            model.CashInDate = model.CashInDate.Date;
            model.Purpose = string.IsNullOrWhiteSpace(model.Purpose) ? null : model.Purpose.Trim();

            if (model.Amount <= 0m)
            {
                ModelState.AddModelError(nameof(ManualCashInViewModel.Amount), "Amount must be greater than zero.");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Cash-in entry was not saved. Check the input.";
                return RedirectToAction(nameof(Index), new { date = model.CashInDate });
            }

            var flow = await FindOrCreateCashFlowAsync(model.CashInDate, currentUserId);

            flow.Entries.Add(new CashFlowEntry
            {
                TreasuryCashFlow = flow,
                Direction = CashFlowDirection.In,
                Category = model.Category,
                Amount = model.Amount,
                Notes = model.Purpose,
                CreatedByUserId = currentUserId,
                ConfirmedByUserId = currentUserId
            });

            flow.RecomputeTotals();
            await _context.SaveChangesAsync();

            TempData["Message"] = "Cash-in entry saved.";
            return RedirectToAction(nameof(Index), new { date = model.CashInDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordCashOut([Bind(Prefix = "ManualCashOut")] ManualCashOutViewModel model)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            model.CashOutDate = model.CashOutDate.Date;
            model.Purpose = string.IsNullOrWhiteSpace(model.Purpose) ? null : model.Purpose.Trim();

            if (model.Category == CashFlowCategory.Others && string.IsNullOrWhiteSpace(model.Purpose))
            {
                ModelState.AddModelError(nameof(ManualCashOutViewModel.Purpose), "Purpose is required for Others.");
            }

            if (model.Purpose?.Length > 255)
            {
                ModelState.AddModelError(nameof(ManualCashOutViewModel.Purpose), "Purpose must be 255 characters or fewer.");
            }

            if (model.Amount <= 0m)
            {
                ModelState.AddModelError(nameof(ManualCashOutViewModel.Amount), "Amount must be greater than zero.");
            }

            if (!model.AppliesAcrossEstablishments && model.EstablishmentId.HasValue)
            {
                var establishmentExists = await _context.Establishments
                    .AsNoTracking()
                    .AnyAsync(e => e.Id == model.EstablishmentId.Value && e.IsActive);

                if (!establishmentExists)
                {
                    ModelState.AddModelError(nameof(ManualCashOutViewModel.EstablishmentId), "Select a valid active establishment.");
                }
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Cash-out entry was not saved. Check the input.";
                return RedirectToAction(nameof(Index), new { date = model.CashOutDate });
            }

            var flow = await FindOrCreateCashFlowAsync(model.CashOutDate, currentUserId);

            flow.Entries.Add(new CashFlowEntry
            {
                TreasuryCashFlow = flow,
                Direction = CashFlowDirection.Out,
                Category = model.Category,
                EstablishmentId = model.AppliesAcrossEstablishments ? null : model.EstablishmentId,
                ReportedByUserId = model.ReportedByUserId,
                Amount = model.Amount,
                Notes = model.Purpose,
                CreatedByUserId = currentUserId,
                ConfirmedByUserId = currentUserId
            });

            flow.RecomputeTotals();
            await _context.SaveChangesAsync();

            TempData["Message"] = "Cash-out entry saved.";
            return RedirectToAction(nameof(Index), new { date = model.CashOutDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseTreasury(DateTime date)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized();
            }

            date = date.Date;

            var flow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                .FirstOrDefaultAsync(f => f.CashFlowDate == date && f.TreasuryUserId == currentUserId);

            if (flow == null)
            {
                TempData["Error"] = "No cash flow exists for this date to close.";
                return RedirectToAction(nameof(Index), new { date });
            }

            if (flow.Status == TreasuryCashFlowStatus.Closed)
            {
                TempData["Message"] = "This treasury day is already closed.";
                return RedirectToAction(nameof(Index), new { date });
            }

            flow.RecomputeTotals();
            flow.Status = TreasuryCashFlowStatus.Closed;

            var nextFlow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                .Where(f => f.TreasuryUserId == currentUserId
                    && f.CashFlowDate > date
                    && f.Status != TreasuryCashFlowStatus.Closed)
                .OrderBy(f => f.CashFlowDate)
                .FirstOrDefaultAsync();

            if (nextFlow != null)
            {
                nextFlow.StartingBalance = flow.ClosingBalance;
                nextFlow.RecomputeTotals();
            }
            await _context.SaveChangesAsync();

            TempData["Message"] = "Treasury day closed.";
            return RedirectToAction(nameof(Index), new { date });
        }

        private async Task<TreasuryCashFlow> FindOrCreateCashFlowAsync(DateTime cashFlowDate, int treasuryUserId)
        {
            var flow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                .FirstOrDefaultAsync(f => f.CashFlowDate == cashFlowDate && f.TreasuryUserId == treasuryUserId);

            if (flow != null)
            {
                return flow;
            }

            var startingBalance = await GetCarryForwardStartingBalanceAsync(cashFlowDate, treasuryUserId);

            flow = new TreasuryCashFlow
            {
                CashFlowDate = cashFlowDate,
                StartingBalance = startingBalance,
                Status = TreasuryCashFlowStatus.Draft,
                TreasuryUserId = treasuryUserId
            };

            _context.TreasuryCashFlows.Add(flow);
            await _context.SaveChangesAsync();

            return flow;
        }

        private void PopulateManualCashFlowLookups()
        {
            var establishments = _context.Establishments
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .ToList();

            var managers = _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Manager && !u.IsDeleted)
                .OrderBy(u => u.Name)
                .ToList();

            ViewBag.CashFlowEstablishments = new SelectList(establishments, "Id", "Name");
            ViewBag.ReportedByManagers = new SelectList(managers, "Id", "Name");
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

        private decimal GetCarryForwardStartingBalance(DateTime cashFlowDate, int treasuryUserId)
        {
            return _context.TreasuryCashFlows
                .AsNoTracking()
                .Where(f => f.TreasuryUserId == treasuryUserId
                    && f.Status == TreasuryCashFlowStatus.Closed
                    && f.CashFlowDate < cashFlowDate.Date)
                .OrderByDescending(f => f.CashFlowDate)
                .Select(f => f.ClosingBalance)
                .FirstOrDefault();
        }

        private async Task<decimal> GetCarryForwardStartingBalanceAsync(DateTime cashFlowDate, int treasuryUserId)
        {
            return await _context.TreasuryCashFlows
                .AsNoTracking()
                .Where(f => f.TreasuryUserId == treasuryUserId
                    && f.Status == TreasuryCashFlowStatus.Closed
                    && f.CashFlowDate < cashFlowDate.Date)
                .OrderByDescending(f => f.CashFlowDate)
                .Select(f => f.ClosingBalance)
                .FirstOrDefaultAsync();
        }

        private static DateTime GetToday()
        {
            var utcNow = DateTime.UtcNow;
            try
            {
                var manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, manila).Date;
            }
            catch (TimeZoneNotFoundException)
            {
                var fallback = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, fallback).Date;
            }
        }
    }
}
