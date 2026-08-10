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

        public SalesReportsController(AuditDbContext context, IOcrService ocrService)
        {
            _context = context;
            _ocrService = ocrService;
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            await PopulateEstablishments();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int establishmentId, DateTime businessDate, DateTime handoverDate, string? cashierName, List<IFormFile>? reportImages)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Challenge();
            }

            await PopulateEstablishments(establishmentId);

            if (await CurrentBranchStaffCannotAccessAsync(establishmentId))
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

            // Primary image for compatibility
            var firstImagePath = Path.Combine(uploadsFolder, Path.GetFileName(savedUrls[0]));

            SalesReportOcrResult? ocrResult = null;
            var ocrStatus = OcrStatus.Failed;
            string? ocrRawJson = null;

            try
            {
                await using var ocrStream = System.IO.File.OpenRead(firstImagePath);
                ocrResult = await _ocrService.ParseSalesReportAsync(ocrStream);
                ocrStatus = OcrStatus.Parsed;
                ocrRawJson = string.IsNullOrWhiteSpace(ocrResult.RawJson)
                    ? JsonSerializer.Serialize(ocrResult)
                    : ocrResult.RawJson;
            }
            catch (Exception)
            {
                TempData["Warning"] = "OCR parsing failed. You can still review and enter the sales report values manually.";
            }

            var document = new DocumentRecord
            {
                DocumentType = DocumentType.DailySalesReport,
                UploadedByUserId = currentUserId.Value,
                UploadedAt = DateTime.UtcNow,
                ImageUrl = savedUrls[0],
                OcrRawJson = ocrRawJson,
                OcrStatus = ocrStatus,
                ReviewStatus = DocumentReviewStatus.Draft
            };

            _context.DocumentRecords.Add(document);
            await _context.SaveChangesAsync();

            var report = new SalesReport
            {
                DocumentRecordId = document.Id,
                EstablishmentId = establishmentId,
                CashierName = string.IsNullOrWhiteSpace(ocrResult?.CashierName) ? cashierName : ocrResult.CashierName,
                BusinessDate = (ocrResult?.BusinessDate ?? businessDate).Date,
                HandoverDate = handoverDate.Date,
                GrossSales = ocrResult?.GrossSales ?? 0m,
                CashOut = ocrResult?.CashOut ?? 0m,
                ConfirmedCashToHandover = ocrResult?.ConfirmedCashToHandover ?? 0m,
                GCashAmount = ocrResult?.GCashAmount ?? 0m,
                CreditAmount = ocrResult?.CreditAmount ?? 0m,
                OtherPaymentAmount = ocrResult?.OtherPaymentAmount ?? 0m,
                ReceiptNumberStart = ocrResult?.ReceiptNumberStart,
                ReceiptNumberEnd = ocrResult?.ReceiptNumberEnd,
                WitnessName = ocrResult?.WitnessName,
                Status = SalesReportStatus.Draft
            };
            report.ImageUrls = savedUrls;

            _context.SalesReports.Add(report);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Sales report uploaded. Review the values before confirming.";
            return RedirectToAction(nameof(Review), new { id = report.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var report = await _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentBranchStaffCannotAccessAsync(report.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(report.EstablishmentId);
            return View(ToReviewModel(report));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(SalesReportReviewViewModel model, string actionType)
        {
            if (!model.SalesReportId.HasValue)
            {
                return NotFound();
            }

            var report = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .FirstOrDefaultAsync(r => r.Id == model.SalesReportId.Value && r.DocumentRecordId == model.DocumentRecordId);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentBranchStaffCannotAccessAsync(report.EstablishmentId) || await CurrentBranchStaffCannotAccessAsync(model.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(model.EstablishmentId);
            model.ImageUrl = report.DocumentRecord.ImageUrl;

            if (!await IsValidOperatingBranchAsync(model.EstablishmentId))
            {
                ModelState.AddModelError(nameof(model.EstablishmentId), "Select an active operating branch.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isConfirmAction = string.Equals(actionType, "Confirm", StringComparison.OrdinalIgnoreCase);
            if (!isConfirmAction && (report.Status == SalesReportStatus.Confirmed || report.DocumentRecord.ReviewStatus == DocumentReviewStatus.Confirmed))
            {
                ModelState.AddModelError(string.Empty, "Confirmed sales reports cannot be saved as drafts.");
                TempData["Error"] = "Confirmed sales reports cannot be saved as drafts.";
                return View(ToReviewModel(report));
            }

            ApplyReviewModel(report, model);

            _context.CashBreakdownLines.RemoveRange(report.CashBreakdownLines);
            report.CashBreakdownLines.Clear();
            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    report.CashBreakdownLines.Add(new CashBreakdownLine
                    {
                        OwnerType = CashBreakdownOwnerType.SalesReport,
                        OwnerId = report.Id,
                        Denomination = item.Denomination,
                        Quantity = item.Quantity,
                        Total = item.Denomination * item.Quantity
                    });
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

                await PostConfirmedSalesReportToTreasuryAsync(report, currentUserId.Value);

                TempData["Message"] = "Sales report confirmed and posted to treasury cash-in.";
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
                        || r.DocumentRecord.ImageUrl.EndsWith(storedImageUrlWindowsSuffix)));

            if (report == null)
            {
                return NotFound();
            }

            if (CurrentBranchStaffCannotAccess(report.EstablishmentId))
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
                .FirstOrDefaultAsync(f => f.CashFlowDate == handoverDate);

            if (flow == null)
            {
                flow = new TreasuryCashFlow
                {
                    TreasuryUserId = currentUserId,
                    CashFlowDate = handoverDate,
                    StartingBalance = 0m,
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

        private static SalesReportReviewViewModel ToReviewModel(SalesReport report)
        {
            return new SalesReportReviewViewModel
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
                GCashAmount = report.GCashAmount,
                CreditAmount = report.CreditAmount,
                OtherPaymentAmount = report.OtherPaymentAmount,
                ReceiptNumberStart = report.ReceiptNumberStart,
                ReceiptNumberEnd = report.ReceiptNumberEnd,
                WitnessName = report.WitnessName,
                Notes = report.Notes,
                ImageUrl = report.DocumentRecord?.ImageUrl ?? string.Empty,
                ImageUrls = report.ImageUrls
            };
        }

        private static void ApplyReviewModel(SalesReport report, SalesReportReviewViewModel model)
        {
            report.EstablishmentId = model.EstablishmentId;
            report.CashierName = model.CashierName;
            report.BusinessDate = model.BusinessDate.Date;
            report.HandoverDate = model.HandoverDate.Date;
            report.GrossSales = model.GrossSales;
            report.CashOut = model.CashOut;
            report.ConfirmedCashToHandover = model.ConfirmedCashToHandover;
            report.GCashAmount = model.GCashAmount;
            report.CreditAmount = model.CreditAmount;
            report.OtherPaymentAmount = model.OtherPaymentAmount;
            report.ReceiptNumberStart = model.ReceiptNumberStart;
            report.ReceiptNumberEnd = model.ReceiptNumberEnd;
            report.WitnessName = model.WitnessName;
            report.Notes = model.Notes;
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
