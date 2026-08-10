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
        public async Task<IActionResult> Upload(int establishmentId, DateTime businessDate, DateTime handoverDate, string? cashierName, IFormFile? reportImage)
        {
            await PopulateEstablishments(establishmentId);

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

            if (reportImage == null || reportImage.Length == 0)
            {
                ModelState.AddModelError(nameof(reportImage), "Please upload a non-empty sales report image.");
            }

            var extension = reportImage == null ? string.Empty : Path.GetExtension(reportImage.FileName).ToLowerInvariant();
            if (reportImage != null && !AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(reportImage), "Please upload a PNG, JPG, JPEG, or WEBP image.");
            }

            if (!ModelState.IsValid || reportImage == null)
            {
                return View();
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Challenge();
            }

            var uploadsFolder = GetUploadsFolder();
            Directory.CreateDirectory(uploadsFolder);

            var generatedFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, generatedFileName);

            await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
            {
                await reportImage.CopyToAsync(fileStream);
            }

            SalesReportOcrResult? ocrResult = null;
            var ocrStatus = OcrStatus.Failed;
            string? ocrRawJson = null;

            try
            {
                await using var ocrStream = System.IO.File.OpenRead(filePath);
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
                ImageUrl = $"/SalesReports/Image/{generatedFileName}",
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
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
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
                .FirstOrDefaultAsync(r => r.Id == model.SalesReportId.Value && r.DocumentRecordId == model.DocumentRecordId);

            if (report == null)
            {
                return NotFound();
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

            ApplyReviewModel(report, model);

            if (string.Equals(actionType, "Confirm", StringComparison.OrdinalIgnoreCase))
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
                ImageUrl = report.DocumentRecord.ImageUrl
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

        private async Task<bool> IsValidOperatingBranchAsync(int establishmentId)
        {
            return await _context.Establishments
                .AsNoTracking()
                .AnyAsync(e => e.Id == establishmentId && e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous);
        }

        private async Task PopulateEstablishments(int? selectedId = null)
        {
            var establishments = await _context.Establishments
                .AsNoTracking()
                .Where(e => e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous)
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
