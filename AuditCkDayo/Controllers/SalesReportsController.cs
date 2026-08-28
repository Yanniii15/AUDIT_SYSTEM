using System.Security.Claims;
using System.Text.Json;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;
using AuditCkDayo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager,BranchStaff,Admin")]
    public class SalesReportsController : Controller
    {
        private static readonly string[] AllowedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

        private readonly AuditDbContext _context;
        private readonly IOcrService _ocrService;

        private readonly CoverageService? _coverageService;

        public SalesReportsController(AuditDbContext context, IOcrService ocrService, CoverageService? coverageService = null)
        {
            _context = context;
            _ocrService = ocrService;
            _coverageService = coverageService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var isBranchStaff = IsBranchStaff();
            if (isBranchStaff)
            {
                var staffUserId = GetCurrentUserId();
                var assignedEstablishmentId = staffUserId.HasValue
                    ? await _context.Users
                        .AsNoTracking()
                        .Where(u => u.Id == staffUserId.Value && u.Role == UserRole.BranchStaff && !u.IsDeleted)
                        .Select(u => u.EstablishmentId)
                        .FirstOrDefaultAsync()
                    : null;

                if (!assignedEstablishmentId.HasValue)
                {
                    return View(new List<SalesReport>());
                }

                var staffReports = await _context.SalesReports
                    .AsNoTracking()
                    .Include(r => r.DocumentRecord)
                    .Include(r => r.Establishment)
                    .Where(r => r.EstablishmentId == assignedEstablishmentId.Value)
                    .Where(r => r.BusinessDate >= DateTime.Today.AddDays(-30))
                    .OrderByDescending(r => r.BusinessDate)
                    .ThenByDescending(r => r.Id)
                    .ToListAsync();

                ViewBag.IsBranchStaff = true;
                return View(staffReports);
            }

            var query = _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.Establishment)
                .Where(r => r.Status == SalesReportStatus.PendingManagerVerification
                    || r.DocumentRecord.ReviewStatus == DocumentReviewStatus.PendingManagerVerification);

            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var currentUserId = GetCurrentUserId();

            if (string.Equals(currentRole, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase) && currentUserId.HasValue)
            {
                query = query.Where(r => _context.Users
                    .Any(u => !u.IsDeleted
                        && u.Role == UserRole.BranchStaff
                        && u.ManagerId == currentUserId.Value
                        && u.EstablishmentId == r.EstablishmentId));
            }

            var pendingReports = await query
                .OrderBy(r => r.HandoverDate)
                .ThenBy(r => r.Establishment.Name)
                .ThenBy(r => r.Id)
                .ToListAsync();

            return View(pendingReports);
        }

        public async Task<IActionResult> Upload(int? reportId = null, string? section = null)
        {
            if (reportId.HasValue)
            {
                var report = await _context.SalesReports
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reportId.Value);

                if (report != null)
                {
                    ViewBag.PreselectedEstablishmentId = report.EstablishmentId;
                    ViewBag.PreselectedBusinessDate = report.BusinessDate.ToString("yyyy-MM-dd");
                    ViewBag.PreselectedHandoverDate = report.HandoverDate.ToString("yyyy-MM-dd");
                    ViewBag.PreselectedCashierName = report.CashierName;
                    ViewBag.PreselectedSection = string.Equals(section, "Closing", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                }
            }

            await PopulateEstablishments(ViewBag.PreselectedEstablishmentId as int?);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int establishmentId, DateTime businessDate, DateTime handoverDate, string? cashierName, List<IFormFile>? reportImages, int reportSection = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Challenge();
            }

            await PopulateEstablishments(establishmentId);

            if (await CurrentUserCannotAccessAsync(establishmentId))
            {
                return Forbid();
            }

            if (!await IsValidOperatingBranchAsync(establishmentId))
            {
                ModelState.AddModelError(nameof(establishmentId), "Select an active operating branch.");
            }

            if (businessDate == default)
            {
                ModelState.AddModelError(nameof(businessDate), "Business date is required.");
            }

            if (handoverDate == default)
            {
                ModelState.AddModelError(nameof(handoverDate), "Handover date is required.");
            }

            if (reportImages == null || reportImages.Count == 0)
            {
                ModelState.AddModelError("", "Please upload at least one valid sales report image.");
            }
            else if (reportImages.Count > 5)
            {
                ModelState.AddModelError("", "You can upload up to 5 sales report images.");
            }
            else
            {
                foreach (var reportImage in reportImages)
                {
                    if (reportImage.Length == 0)
                    {
                        ModelState.AddModelError("", "Empty image files are not allowed.");
                    }
                    var ext = Path.GetExtension(reportImage.FileName).ToLowerInvariant();
                    if (!AllowedImageExtensions.Contains(ext))
                    {
                        ModelState.AddModelError("", $"Please upload a PNG, JPG, JPEG, or WEBP image. Invalid file: {reportImage.FileName}");
                    }
                }
            }

            if (!ModelState.IsValid || reportImages == null || reportImages.Count == 0)
            {
                return View();
            }

            var uploadsFolder = GetUploadsFolder();
            Directory.CreateDirectory(uploadsFolder);

            var savedUrls = new List<string>();
            foreach (var reportImage in reportImages)
            {
                var ext = Path.GetExtension(reportImage.FileName).ToLowerInvariant();
                var generatedFileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, generatedFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
                {
                    await reportImage.CopyToAsync(fileStream);
                }
                savedUrls.Add($"/SalesReports/Image/{generatedFileName}");
            }

            // Check if there is an existing report for this branch and date
            var existingReport = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .FirstOrDefaultAsync(r => r.EstablishmentId == establishmentId && r.BusinessDate.Date == businessDate.Date);

            if (reportSection == 0 && existingReport != null)
            {
                // Uploading closing log books onto an existing opening report
                var currentUrls = existingReport.ClosingImageUrls ?? new List<string>();
                foreach (var url in savedUrls)
                {
                    if (!currentUrls.Contains(url))
                    {
                        currentUrls.Add(url);
                    }
                }
                existingReport.ClosingImageUrls = currentUrls;
                await _context.SaveChangesAsync();

                TempData["Message"] = "Closing log books uploaded successfully. Fill in closing daily sales.";
                return RedirectToAction(nameof(Review), new { id = existingReport.Id });
            }

            var document = new DocumentRecord
            {
                DocumentType = DocumentType.DailySalesReport,
                UploadedByUserId = currentUserId.Value,
                UploadedAt = DateTime.UtcNow,
                ImageUrl = savedUrls[0],
                OcrStatus = OcrStatus.Failed,
                ReviewStatus = DocumentReviewStatus.Draft
            };

            _context.DocumentRecords.Add(document);
            await _context.SaveChangesAsync();

            var report = existingReport;
            if (report == null)
            {
                report = new SalesReport
                {
                    DocumentRecordId = document.Id,
                    EstablishmentId = establishmentId,
                    CashierName = cashierName,
                    BusinessDate = businessDate.Date,
                    HandoverDate = handoverDate.Date,
                    Status = SalesReportStatus.Draft
                };
                _context.SalesReports.Add(report);
            }
            else
            {
                report.DocumentRecordId = document.Id;
                report.HandoverDate = handoverDate.Date;
                if (!string.IsNullOrEmpty(cashierName))
                {
                    report.CashierName = cashierName;
                }
            }

            if (reportSection == 0)
            {
                report.ClosingImageUrls = savedUrls;
            }
            else
            {
                report.ImageUrls = savedUrls;
            }
            await _context.SaveChangesAsync();

            if (reportSection == 0)
            {
                TempData["Message"] = "Closing sales report uploaded. Fill in closing daily sales.";
                return RedirectToAction(nameof(Review), new { id = report.Id });
            }

            TempData["Message"] = "Opening sales report uploaded. Fill in opening daily sales.";
            return RedirectToAction(nameof(OpeningReview), new { id = report.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var report = await _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(report.EstablishmentId);
            var model = BuildReviewModel(report);
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var isManager = string.Equals(currentRole, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase)
                         || string.Equals(currentRole, UserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
                         || string.Equals(currentRole, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

            if (isManager)
            {
                return View("ReviewManager", model);
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OpeningReview(int id)
        {
            var report = await _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(report.EstablishmentId);
            var model = BuildReviewModel(report);
            model.ReportSection = SalesReportSection.Opening;
            return View("OpeningReview", model);
        }

        public async Task<IActionResult> Review(SalesReportReviewViewModel model, string actionType, List<IFormFile>? closingLogBookImages = null)
        {
            if (!model.SalesReportId.HasValue)
            {
                return NotFound();
            }

            var report = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == model.SalesReportId.Value && r.DocumentRecordId == model.DocumentRecordId);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId) || await CurrentUserCannotAccessAsync(model.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(model.EstablishmentId);
            PopulateReviewUiState(model, report);

            // Process closing log book images upload if provided
            if (closingLogBookImages != null && closingLogBookImages.Count > 0)
            {
                var uploadsFolder = GetUploadsFolder();
                Directory.CreateDirectory(uploadsFolder);
                var currentUrls = report.ImageUrls ?? new List<string>();

                foreach (var file in closingLogBookImages)
                {
                    if (file.Length > 0)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!AllowedImageExtensions.Contains(ext))
                        {
                            ModelState.AddModelError(string.Empty, $"Invalid file format: {file.FileName}. Please upload PNG, JPG, JPEG, or WEBP.");
                            continue;
                        }

                        var generatedFileName = $"{Guid.NewGuid():N}{ext}";
                        var filePath = Path.Combine(uploadsFolder, generatedFileName);

                        await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        var newUrl = $"/SalesReports/Image/{generatedFileName}";
                        if (!currentUrls.Contains(newUrl))
                        {
                            currentUrls.Add(newUrl);
                        }
                    }
                }
                report.ImageUrls = currentUrls;
            }

            if (!await IsValidOperatingBranchAsync(model.EstablishmentId))
            {
                ModelState.AddModelError(nameof(model.EstablishmentId), "Select an active operating branch.");
            }

            if (!ModelState.IsValid)
            {
                var role = User.FindFirstValue(ClaimTypes.Role);
                if (string.Equals(role, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase)
                 || string.Equals(role, UserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
                 || string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return View("ReviewManager", model);
                }
                return View(model);
            }

            var requestedConfirmAction = string.Equals(actionType, "Confirm", StringComparison.OrdinalIgnoreCase);
            var canConfirmToTreasury = CanConfirmSalesReportToTreasury();
            var isConfirmAction = requestedConfirmAction && canConfirmToTreasury;
            var isSubmitForVerificationAction = string.Equals(actionType, "SubmitForVerification", StringComparison.OrdinalIgnoreCase)
                || (requestedConfirmAction && !canConfirmToTreasury);
            if (!isConfirmAction && (report.Status == SalesReportStatus.Confirmed || report.DocumentRecord.ReviewStatus == DocumentReviewStatus.Confirmed))
            {
                ModelState.AddModelError(string.Empty, "Confirmed sales reports cannot be saved as drafts.");
                TempData["Error"] = "Confirmed sales reports cannot be saved as drafts.";
                var errorModel = BuildReviewModel(report);
                var role = User.FindFirstValue(ClaimTypes.Role);
                if (string.Equals(role, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase)
                 || string.Equals(role, UserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
                 || string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return View("ReviewManager", errorModel);
                }
                return View(errorModel);
            }

            ApplyReviewModel(report, model);

            _context.CashBreakdownLines.RemoveRange(report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Closing).ToList());
            report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Closing).ToList().ForEach(b => report.CashBreakdownLines.Remove(b));
            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    report.CashBreakdownLines.Add(new CashBreakdownLine
                    {
                        OwnerType = CashBreakdownOwnerType.SalesReport,
                        OwnerId = report.Id,
                        Section = SalesReportSection.Closing,
                        Denomination = item.Denomination,
                        Quantity = item.Quantity,
                        Total = item.Denomination * item.Quantity
                    });
                }
            }

            _context.SalesReportLines.RemoveRange(report.Lines.Where(l => l.Section == SalesReportSection.Closing).ToList());
            report.Lines.Where(l => l.Section == SalesReportSection.Closing).ToList().ForEach(l => report.Lines.Remove(l));

            int sortOrder = 0;
            if (model.GCashLines != null)
            {
                foreach (var line in model.GCashLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.GCash,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }

            if (model.BankTransferLines != null)
            {
                foreach (var line in model.BankTransferLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.BankTransfer,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }

            if (model.CardLines != null)
            {
                foreach (var line in model.CardLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.Card,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }

            if (model.CreditLines != null)
            {
                foreach (var line in model.CreditLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.Credit,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }

            if (model.RunawayCustomerLines != null)
            {
                foreach (var line in model.RunawayCustomerLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.RunawayCustomer,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }

            if (model.ExpenseFromSalesLines != null)
            {
                foreach (var line in model.ExpenseFromSalesLines)
                {
                    if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                    {
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.ExpenseFromSales,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
                    }
                }
            }


            if (isConfirmAction)
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Challenge();
                }

                report.Status = SalesReportStatus.Confirmed;
                report.ConfirmedByUserId = currentUserId.Value;
                report.ConfirmedAt = DateTime.UtcNow;
                report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Confirmed;
                report.DocumentRecord.ConfirmedByUserId = currentUserId.Value;
                report.DocumentRecord.ConfirmedAt = DateTime.UtcNow;

                if (model.ConfirmedCashToHandover > 0m)
                {
                    report.ConfirmedCashToHandover = model.ConfirmedCashToHandover;
                }

                await PostConfirmedSalesReportToTreasuryAsync(report, currentUserId.Value);
                await NotifyUploaderOfShortOverAsync(report);

                TempData["Message"] = "Sales report confirmed and posted to treasury cash-in.";
            }
            else if (isSubmitForVerificationAction)
            {
                if (report.OpeningCashSales == 0m && report.OpeningGrossSales == 0m)
                {
                    ModelState.AddModelError(string.Empty, "Add the opening sales section before submitting this daily sales report.");
                    TempData["Error"] = "Add the opening daily sales before submitting for manager verification.";
                    await _context.SaveChangesAsync();
                    var errorModel = BuildReviewModel(report);
                    var role = User.FindFirstValue(ClaimTypes.Role);
                    if (string.Equals(role, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase)
                     || string.Equals(role, UserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
                     || string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return View("ReviewManager", errorModel);
                    }
                    return View(errorModel);
                }

                report.Status = SalesReportStatus.PendingManagerVerification;
                report.DocumentRecord.ReviewStatus = DocumentReviewStatus.PendingManagerVerification;
                report.ConfirmedByUserId = null;
                report.ConfirmedAt = null;
                report.DocumentRecord.ConfirmedByUserId = null;
                report.DocumentRecord.ConfirmedAt = null;

                TempData["Message"] = "Sales report submitted for manager verification.";
            }
            else
            {
                report.Status = SalesReportStatus.Draft;
                report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Draft;
                report.ConfirmedByUserId = null;
                report.ConfirmedAt = null;
                report.DocumentRecord.ConfirmedByUserId = null;
                report.DocumentRecord.ConfirmedAt = null;

                TempData["Message"] = "Sales report draft saved.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Review), new { id = report.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpeningReview(SalesReportReviewViewModel model, string actionType)
        {
            if (!model.SalesReportId.HasValue)
            {
                return NotFound();
            }

            var report = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == model.SalesReportId.Value && r.DocumentRecordId == model.DocumentRecordId);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId) || await CurrentUserCannotAccessAsync(model.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(model.EstablishmentId);
            model.ReportSection = SalesReportSection.Opening;
            PopulateReviewUiState(model, report);

            if (!await IsValidOperatingBranchAsync(model.EstablishmentId))
            {
                ModelState.AddModelError(nameof(model.EstablishmentId), "Select an active operating branch.");
            }

            if (!ModelState.IsValid)
            {
                return View("OpeningReview", model);
            }

            var requestedConfirmAction = string.Equals(actionType, "Confirm", StringComparison.OrdinalIgnoreCase);
            var canConfirmToTreasury = CanConfirmSalesReportToTreasury();
            var isConfirmAction = requestedConfirmAction && canConfirmToTreasury;
            var isSubmitForVerificationAction = string.Equals(actionType, "SubmitForVerification", StringComparison.OrdinalIgnoreCase)
                || (requestedConfirmAction && !canConfirmToTreasury);
            if (!isConfirmAction && (report.Status == SalesReportStatus.Confirmed || report.DocumentRecord.ReviewStatus == DocumentReviewStatus.Confirmed))
            {
                ModelState.AddModelError(string.Empty, "Confirmed sales reports cannot be saved as drafts.");
                TempData["Error"] = "Confirmed sales reports cannot be saved as drafts.";
                return View("OpeningReview", BuildReviewModel(report));
            }

            ApplyOpeningModel(report, model);
            ApplyOpeningLines(report, model);

            report.Status = SalesReportStatus.Draft;
            report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Draft;
            report.ConfirmedByUserId = null;
            report.ConfirmedAt = null;
            report.DocumentRecord.ConfirmedByUserId = null;
            report.DocumentRecord.ConfirmedAt = null;

            TempData["Message"] = "Opening sales draft saved.";

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(OpeningReview), new { id = report.Id });
        }

        [HttpGet("SalesReports/Image/{fileName}")]
        public IActionResult Image(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                return NotFound();
            }

            var storedImageUrlSuffix = "/" + fileName;
            var storedImageUrlWindowsSuffix = "\\" + fileName;
            var report = _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .FirstOrDefault(r => r.DocumentRecord.DocumentType == DocumentType.DailySalesReport
                    && (r.DocumentRecord.ImageUrl == fileName
                        || r.DocumentRecord.ImageUrl.EndsWith(storedImageUrlSuffix)
                        || r.DocumentRecord.ImageUrl.EndsWith(storedImageUrlWindowsSuffix)
                        || (r.ImageUrlsJson != null && r.ImageUrlsJson.Contains(fileName))
                        || (r.ClosingImageUrlsJson != null && r.ClosingImageUrlsJson.Contains(fileName))));

            if (report == null)
            {
                return NotFound();
            }

            if (CurrentUserCannotAccess(report.EstablishmentId))
            {
                return Forbid();
            }

            var filePath = Path.Combine(GetUploadsFolder(), fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            return PhysicalFile(filePath, GetMimeType(filePath));
        }

        private async Task PostConfirmedSalesReportToTreasuryAsync(SalesReport report, int currentUserId)
        {
            var handoverDate = report.HandoverDate.Date;
            var flow = await _context.TreasuryCashFlows
                .Include(f => f.Entries)
                .FirstOrDefaultAsync(f => f.CashFlowDate == handoverDate && f.TreasuryUserId == currentUserId);

            if (flow == null)
            {
                var previousDayFlow = await _context.TreasuryCashFlows
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.CashFlowDate == handoverDate.AddDays(-1) && f.TreasuryUserId == currentUserId);
                decimal startingBalance = previousDayFlow != null && previousDayFlow.Status == TreasuryCashFlowStatus.Closed
                    ? previousDayFlow.ClosingBalance
                    : 0m;

                flow = new TreasuryCashFlow
                {
                    TreasuryUserId = currentUserId,
                    CashFlowDate = handoverDate,
                    StartingBalance = startingBalance,
                    Status = TreasuryCashFlowStatus.Open
                };

                _context.TreasuryCashFlows.Add(flow);
            }

            var entry = await _context.CashFlowEntries
                .FirstOrDefaultAsync(e => e.SourceDocumentId == report.DocumentRecordId && e.Category == CashFlowCategory.Sales);

            TreasuryCashFlow? previousFlow = null;

            if (entry == null)
            {
                entry = new CashFlowEntry
                {
                    CreatedByUserId = currentUserId,
                    SourceDocumentId = report.DocumentRecordId,
                    Category = CashFlowCategory.Sales
                };

                _context.CashFlowEntries.Add(entry);
            }
            else if (entry.TreasuryCashFlowId != flow.Id)
            {
                previousFlow = await _context.TreasuryCashFlows
                    .Include(f => f.Entries)
                    .FirstOrDefaultAsync(f => f.Id == entry.TreasuryCashFlowId);

                previousFlow?.Entries.Remove(entry);
            }

            entry.TreasuryCashFlow = flow;
            entry.TreasuryCashFlowId = flow.Id;
            entry.Direction = CashFlowDirection.In;
            entry.Category = CashFlowCategory.Sales;
            entry.EstablishmentId = report.EstablishmentId;
            entry.SourceDocumentId = report.DocumentRecordId;
            entry.Amount = report.ConfirmedCashToHandover;
            entry.Notes = $"Sales handover for {report.BusinessDate:yyyy-MM-dd}";
            entry.ConfirmedByUserId = currentUserId;

            if (!flow.Entries.Contains(entry))
            {
                flow.Entries.Add(entry);
            }

            previousFlow?.RecomputeTotals();
            flow.RecomputeTotals();
        }

        private async Task NotifyUploaderOfShortOverAsync(SalesReport report)
        {
            var expectedCashToHandover = report.GrossSales - report.GCashAmount - report.CreditAmount - report.OtherPaymentAmount;
            var shortOverAmount = report.ConfirmedCashToHandover - expectedCashToHandover;
            if (shortOverAmount == 0m)
            {
                return;
            }

            var recipientIds = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted
                    && u.Role == UserRole.BranchStaff
                    && u.EstablishmentId == report.EstablishmentId)
                .Select(u => u.Id)
                .ToListAsync();
            if (recipientIds.Count == 0)
            {
                recipientIds.Add(report.DocumentRecord.UploadedByUserId);
            }
            var branchName = await _context.Establishments
                .AsNoTracking()
                .Where(e => e.Id == report.EstablishmentId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync() ?? "the branch";
            var varianceLabel = shortOverAmount < 0m ? "SHORT" : "OVER";
            var varianceAmount = Math.Abs(shortOverAmount);

            foreach (var recipientId in recipientIds.Distinct())
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = recipientId,
                    Title = "Daily Sales Short/Over Notice",
                    Message = $"{branchName} daily sales for {report.BusinessDate:yyyy-MM-dd} was confirmed with {varianceLabel} ₱{varianceAmount:N2}. Expected cash: ₱{expectedCashToHandover:N2}; counted cash: ₱{report.ConfirmedCashToHandover:N2}.",
                    Category = "SalesReportShortOver",
                    LinkUrl = Url?.Action("Review", "SalesReports", new { id = report.Id }) ?? $"/SalesReports/Review/{report.Id}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private static SalesReportReviewViewModel ToReviewModel(SalesReport report)
        {
            var model = new SalesReportReviewViewModel
            {
                SalesReportId = report.Id,
                DocumentRecordId = report.DocumentRecordId,
                EstablishmentId = report.EstablishmentId,
                CashierName = report.CashierName,
                BusinessDate = report.BusinessDate.Date,
                HandoverDate = report.HandoverDate.Date,
                GrossSales = report.GrossSales,
                CashOut = report.CashOut,
                ConfirmedCashToHandover = report.ConfirmedCashToHandover,
                ManagerCountedTotalCash = report.ConfirmedCashToHandover + report.OpeningCashSales,
                GCashAmount = report.GCashAmount,
                CreditAmount = report.CreditAmount,
                OtherPaymentAmount = report.OtherPaymentAmount,

                ClosingGrossSales = report.ClosingGrossSales,
                FoodSales = report.FoodSales,
                BeerSales = report.BeerSales,
                BeverageSales = report.BeverageSales,
                OtherSales = report.OtherSales,
                CashSales = report.CashSales,
                SeniorDiscount = report.SeniorDiscount,
                PwdDiscount = report.PwdDiscount,
                LoyaltyCardDiscount = report.LoyaltyCardDiscount,
                GiftVoucherDiscount = report.GiftVoucherDiscount,
                EmployeeTenPercentDiscount = report.EmployeeTenPercentDiscount,
                EmployeeFivePercentDiscount = report.EmployeeFivePercentDiscount,
                EaglesDiscount = report.EaglesDiscount,
                SalesShortageAmount = report.SalesShortageAmount,
                SalesShortageReason = report.SalesShortageReason,
                SalesOverageAmount = report.SalesOverageAmount,
                SalesOverageReason = report.SalesOverageReason,
                RestoPcf = report.RestoPcf,
                PcfFromSales = report.PcfFromSales,
                ChangeAmount = report.ChangeAmount,
                OpeningGrossSales = report.OpeningGrossSales,
                OpeningCashSales = report.OpeningCashSales,
                OpeningFoodSales = report.OpeningFoodSales,
                OpeningBeerSales = report.OpeningBeerSales,
                OpeningBeverageSales = report.OpeningBeverageSales,
                OpeningOtherSales = report.OpeningOtherSales,
                OpeningSeniorDiscount = report.OpeningSeniorDiscount,
                OpeningPwdDiscount = report.OpeningPwdDiscount,
                OpeningLoyaltyCardDiscount = report.OpeningLoyaltyCardDiscount,
                OpeningGiftVoucherDiscount = report.OpeningGiftVoucherDiscount,
                OpeningEmployeeTenPercentDiscount = report.OpeningEmployeeTenPercentDiscount,
                OpeningEmployeeFivePercentDiscount = report.OpeningEmployeeFivePercentDiscount,
                OpeningEaglesDiscount = report.OpeningEaglesDiscount,
                OpeningSalesShortageAmount = report.OpeningSalesShortageAmount,
                OpeningSalesShortageReason = report.OpeningSalesShortageReason,
                OpeningSalesOverageAmount = report.OpeningSalesOverageAmount,
                OpeningSalesOverageReason = report.OpeningSalesOverageReason,
                OpeningRestoPcf = report.OpeningRestoPcf,
                OpeningPcfFromSales = report.OpeningPcfFromSales,
                OpeningChangeAmount = report.OpeningChangeAmount,
                OpeningReceiptNumberStart = report.OpeningReceiptNumberStart,
                OpeningReceiptNumberEnd = report.OpeningReceiptNumberEnd,
                OpeningWitnessName = report.OpeningWitnessName,
                OpeningNotes = report.OpeningNotes,

                ReceiptNumberStart = report.ReceiptNumberStart,
                ReceiptNumberEnd = report.ReceiptNumberEnd,
                WitnessName = report.WitnessName,
                Notes = report.Notes,
                ImageUrl = report.DocumentRecord?.ImageUrl ?? string.Empty,
                ImageUrls = report.ImageUrls,
                ClosingImageUrls = report.ClosingImageUrls,
                Status = report.Status,
                ReviewStatus = report.DocumentRecord?.ReviewStatus ?? DocumentReviewStatus.Draft,
            };

            if (report.Lines != null)
            {
                foreach (var line in report.Lines)
                {
                    var lineVm = new SalesReportLineViewModel
                    {
                        Id = line.Id,
                        LineType = line.LineType,
                        Amount = line.Amount,
                        Label = line.Label,
                        SortOrder = line.SortOrder
                    };

                    if (line.Section == SalesReportSection.Opening)
                    {
                        switch (line.LineType)
                        {
                            case SalesReportLineType.GCash:
                                model.OpeningGCashLines.Add(lineVm);
                                break;
                            case SalesReportLineType.BankTransfer:
                                model.OpeningBankTransferLines.Add(lineVm);
                                break;
                            case SalesReportLineType.Card:
                                model.OpeningCardLines.Add(lineVm);
                                break;
                            case SalesReportLineType.Credit:
                                model.OpeningCreditLines.Add(lineVm);
                                break;
                            case SalesReportLineType.RunawayCustomer:
                                model.OpeningRunawayCustomerLines.Add(lineVm);
                                break;
                            case SalesReportLineType.ExpenseFromSales:
                                model.OpeningExpenseFromSalesLines.Add(lineVm);
                                break;
                        }
                        continue;
                    }

                    switch (line.LineType)
                    {
                        case SalesReportLineType.GCash:
                            model.GCashLines.Add(lineVm);
                            break;
                        case SalesReportLineType.BankTransfer:
                            model.BankTransferLines.Add(lineVm);
                            break;
                        case SalesReportLineType.Card:
                            model.CardLines.Add(lineVm);
                            break;
                        case SalesReportLineType.Credit:
                            model.CreditLines.Add(lineVm);
                            break;
                        case SalesReportLineType.RunawayCustomer:
                            model.RunawayCustomerLines.Add(lineVm);
                            break;
                        case SalesReportLineType.ExpenseFromSales:
                            model.ExpenseFromSalesLines.Add(lineVm);
                            break;
                    }
                }
            }

            if (report.CashBreakdownLines != null)
            {
                foreach (var b in report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening))
                {
                    model.OpeningItems.Add(new CashBreakdownLineViewModel
                    {
                        Id = b.Id,
                        Denomination = b.Denomination,
                        Quantity = b.Quantity,
                        Total = b.Total
                    });
                }
            }

            return model;
        }

        private SalesReportReviewViewModel BuildReviewModel(SalesReport report)
        {
            var model = ToReviewModel(report);
            model.CanConfirmToTreasury = CanConfirmSalesReportToTreasury();
            if (model.ConfirmedCashToHandover == 0m)
            {
                model.ConfirmedCashToHandover = model.CombinedCashSales;
            }
            return model;
        }

        private void PopulateReviewUiState(SalesReportReviewViewModel model, SalesReport report)
        {
            model.ImageUrl = report.DocumentRecord?.ImageUrl ?? string.Empty;
            model.ImageUrls = report.ImageUrls;
            model.ClosingImageUrls = report.ClosingImageUrls;
            model.Status = report.Status;
            model.ReviewStatus = report.DocumentRecord?.ReviewStatus ?? DocumentReviewStatus.Draft;
            model.CanConfirmToTreasury = CanConfirmSalesReportToTreasury();
        }

        private static void ApplyReviewModel(SalesReport report, SalesReportReviewViewModel model)
        {
            report.EstablishmentId = model.EstablishmentId;
            report.CashierName = model.CashierName;
            report.BusinessDate = model.BusinessDate.Date;
            report.HandoverDate = model.HandoverDate.Date;
            report.GrossSales = model.GrossSales;

            report.ClosingGrossSales = model.ClosingGrossSales;
            report.FoodSales = model.FoodSales;
            report.BeerSales = model.BeerSales;
            report.BeverageSales = model.BeverageSales;
            report.OtherSales = model.OtherSales;
            report.CashSales = model.CashSales;
            report.SeniorDiscount = model.SeniorDiscount;
            report.PwdDiscount = model.PwdDiscount;
            report.LoyaltyCardDiscount = model.LoyaltyCardDiscount;
            report.GiftVoucherDiscount = model.GiftVoucherDiscount;
            report.EmployeeTenPercentDiscount = model.EmployeeTenPercentDiscount;
            report.EmployeeFivePercentDiscount = model.EmployeeFivePercentDiscount;
            report.EaglesDiscount = model.EaglesDiscount;
            report.SalesShortageAmount = model.SalesShortageAmount;
            report.SalesShortageReason = model.SalesShortageReason;
            report.SalesOverageAmount = model.SalesOverageAmount;
            report.SalesOverageReason = model.SalesOverageReason;
            report.RestoPcf = model.RestoPcf;
            report.PcfFromSales = model.PcfFromSales;
            report.ChangeAmount = model.ChangeAmount;

            report.GCashAmount = model.TotalGCash;
            report.CreditAmount = model.TotalCredit;

            decimal otherPayments = model.TotalBankTransfer + model.TotalCard + model.TotalRunawayCustomer + model.OtherSales;
            report.OtherPaymentAmount = otherPayments > 0m ? otherPayments : model.OtherPaymentAmount;

            report.CashOut = model.TotalExpensesFromSales > 0m ? model.TotalExpensesFromSales : model.CashOut;

            report.ConfirmedCashToHandover = model.ConfirmedCashToHandover;
            report.ReceiptNumberStart = model.ReceiptNumberStart;
            report.ReceiptNumberEnd = model.ReceiptNumberEnd;
            report.WitnessName = model.WitnessName;
            report.Notes = model.Notes;
        }

        private static void ApplyOpeningModel(SalesReport report, SalesReportReviewViewModel model)
        {
            report.EstablishmentId = model.EstablishmentId;
            report.CashierName = model.CashierName;
            report.BusinessDate = model.BusinessDate.Date;
            report.HandoverDate = model.HandoverDate.Date;
            report.OpeningGrossSales = model.OpeningGrossSales;
            report.OpeningCashSales = model.OpeningCashSales;
            report.OpeningFoodSales = model.OpeningFoodSales;
            report.OpeningBeerSales = model.OpeningBeerSales;
            report.OpeningBeverageSales = model.OpeningBeverageSales;
            report.OpeningOtherSales = model.OpeningOtherSales;
            report.OpeningSeniorDiscount = model.OpeningSeniorDiscount;
            report.OpeningPwdDiscount = model.OpeningPwdDiscount;
            report.OpeningLoyaltyCardDiscount = model.OpeningLoyaltyCardDiscount;
            report.OpeningGiftVoucherDiscount = model.OpeningGiftVoucherDiscount;
            report.OpeningEmployeeTenPercentDiscount = model.OpeningEmployeeTenPercentDiscount;
            report.OpeningEmployeeFivePercentDiscount = model.OpeningEmployeeFivePercentDiscount;
            report.OpeningEaglesDiscount = model.OpeningEaglesDiscount;
            report.OpeningSalesShortageAmount = model.OpeningSalesShortageAmount;
            report.OpeningSalesShortageReason = model.OpeningSalesShortageReason;
            report.OpeningSalesOverageAmount = model.OpeningSalesOverageAmount;
            report.OpeningSalesOverageReason = model.OpeningSalesOverageReason;
            report.OpeningRestoPcf = model.OpeningRestoPcf;
            report.OpeningPcfFromSales = model.OpeningPcfFromSales;
            report.OpeningChangeAmount = model.OpeningChangeAmount;
            report.OpeningReceiptNumberStart = model.OpeningReceiptNumberStart;
            report.OpeningReceiptNumberEnd = model.OpeningReceiptNumberEnd;
            report.OpeningWitnessName = model.OpeningWitnessName;
            report.OpeningNotes = model.OpeningNotes;
        }

        private void ApplyOpeningLines(SalesReport report, SalesReportReviewViewModel model)
        {
            _context.SalesReportLines.RemoveRange(report.Lines.Where(l => l.Section == SalesReportSection.Opening).ToList());
            report.Lines.Where(l => l.Section == SalesReportSection.Opening).ToList().ForEach(l => report.Lines.Remove(l));

            _context.CashBreakdownLines.RemoveRange(report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening).ToList());
            report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening).ToList().ForEach(b => report.CashBreakdownLines.Remove(b));

            int sortOrder = 0;
            AddOpeningLines(model.OpeningGCashLines, SalesReportLineType.GCash, report, ref sortOrder);
            AddOpeningLines(model.OpeningBankTransferLines, SalesReportLineType.BankTransfer, report, ref sortOrder);
            AddOpeningLines(model.OpeningCardLines, SalesReportLineType.Card, report, ref sortOrder);
            AddOpeningLines(model.OpeningCreditLines, SalesReportLineType.Credit, report, ref sortOrder);
            AddOpeningLines(model.OpeningRunawayCustomerLines, SalesReportLineType.RunawayCustomer, report, ref sortOrder);
            AddOpeningLines(model.OpeningExpenseFromSalesLines, SalesReportLineType.ExpenseFromSales, report, ref sortOrder);

            if (model.OpeningItems != null)
            {
                foreach (var item in model.OpeningItems)
                {
                    report.CashBreakdownLines.Add(new CashBreakdownLine
                    {
                        OwnerType = CashBreakdownOwnerType.SalesReport,
                        OwnerId = report.Id,
                        Section = SalesReportSection.Opening,
                        Denomination = item.Denomination,
                        Quantity = item.Quantity,
                        Total = item.Denomination * item.Quantity
                    });
                }
            }
        }

        private static void AddOpeningLines(List<SalesReportLineViewModel>? lines, SalesReportLineType lineType, SalesReport report, ref int sortOrder)
        {
            if (lines == null)
            {
                return;
            }
            foreach (var line in lines)
            {
                if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                {
                    report.Lines.Add(new SalesReportLine
                    {
                        LineType = lineType,
                        Section = SalesReportSection.Opening,
                        Amount = line.Amount,
                        Label = line.Label,
                        SortOrder = sortOrder++
                    });
                }
            }
        }

        private int? GetCurrentUserId()
        {
            var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(currentUserIdValue, out var currentUserId) ? currentUserId : null;
        }

        private bool IsBranchStaff()
        {
            return string.Equals(User.FindFirstValue(ClaimTypes.Role), UserRole.BranchStaff.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private bool CanConfirmSalesReportToTreasury()
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            return string.Equals(currentRole, UserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentRole, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CurrentBranchStaffCannotAccessAsync(int establishmentId)
        {
            if (!IsBranchStaff())
            {
                return false;
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return true;
            }

            var assignedEstablishmentId = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId.Value && u.Role == UserRole.BranchStaff && !u.IsDeleted)
                .Select(u => u.EstablishmentId)
                .FirstOrDefaultAsync();

            return !assignedEstablishmentId.HasValue || assignedEstablishmentId.Value != establishmentId;
        }

        private bool CurrentBranchStaffCannotAccess(int establishmentId)
        {
            if (!IsBranchStaff())
            {
                return false;
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return true;
            }

            var assignedEstablishmentId = _context.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId.Value && u.Role == UserRole.BranchStaff && !u.IsDeleted)
                .Select(u => u.EstablishmentId)
                .FirstOrDefault();

            return !assignedEstablishmentId.HasValue || assignedEstablishmentId.Value != establishmentId;
        }

        private async Task<bool> IsValidOperatingBranchAsync(int establishmentId)
        {
            return await _context.Establishments
                .AsNoTracking()
                .AnyAsync(e => e.Id == establishmentId && e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous);
        }

        private async Task PopulateEstablishments(int? selectedId = null)
        {
            var query = _context.Establishments
                .AsNoTracking()
                .Where(e => e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous);

            if (IsBranchStaff())
            {
                var currentUserId = GetCurrentUserId();
                var assignedEstablishmentId = currentUserId.HasValue
                    ? await _context.Users
                        .AsNoTracking()
                        .Where(u => u.Id == currentUserId.Value && u.Role == UserRole.BranchStaff && !u.IsDeleted)
                        .Select(u => u.EstablishmentId)
                        .FirstOrDefaultAsync()
                    : null;

                query = assignedEstablishmentId.HasValue
                    ? query.Where(e => e.Id == assignedEstablishmentId.Value)
                    : query.Where(e => false);
            }

            var establishments = await query
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.Establishments = new SelectList(establishments, "Id", "Name", selectedId);
        }

        private async Task<bool> CurrentUserCannotAccessAsync(int establishmentId)
        {
            if (await CurrentBranchStaffCannotAccessAsync(establishmentId))
            {
                return true;
            }
            if (await CurrentManagerCannotAccessAsync(establishmentId))
            {
                return true;
            }
            return false;
        }

        private bool CurrentUserCannotAccess(int establishmentId)
        {
            if (CurrentBranchStaffCannotAccess(establishmentId))
            {
                return true;
            }
            if (CurrentManagerCannotAccess(establishmentId))
            {
                return true;
            }
            return false;
        }

        private async Task<bool> CurrentManagerCannotAccessAsync(int establishmentId)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            if (!string.Equals(currentRole, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return true;
            }

            return !await _context.Users
                .AnyAsync(u => !u.IsDeleted
                    && u.Role == UserRole.BranchStaff
                    && u.EstablishmentId == establishmentId
                    && (u.ManagerId == currentUserId.Value || u.ManagerId == null));
        }

        private bool CurrentManagerCannotAccess(int establishmentId)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            if (!string.Equals(currentRole, UserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return true;
            }

            return !_context.Users
                .Any(u => !u.IsDeleted
                    && u.Role == UserRole.BranchStaff
                    && u.EstablishmentId == establishmentId
                    && (u.ManagerId == currentUserId.Value || u.ManagerId == null));
        }

        private static string GetUploadsFolder()
        {
            return Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads", "sales-reports");
        }

        private static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
