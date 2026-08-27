using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers
{
    [Authorize]
    public class AuditsController : Controller
    {
        private readonly AuditDbContext _context;
        private readonly IOcrService _ocrService;
        private readonly IWebHostEnvironment _env;
        private readonly Services.CoverageService? _coverageService;
        private readonly SharedPcfFundService _pcfFund;
        private const string PendingAuditDraftsSessionKey = "PendingAuditDrafts";

        public AuditsController(AuditDbContext context, IOcrService ocrService, IWebHostEnvironment env, Services.CoverageService? coverageService = null, Services.SharedPcfFundService? pcfFund = null)
        {
            _context = context;
            _ocrService = ocrService;
            _env = env;
            _coverageService = coverageService;
            _pcfFund = pcfFund ?? new SharedPcfFundService(context);
        }

        [HttpGet]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin,Auditor")]
        public IActionResult Upload()
        {
            return View(GetPendingAuditDrafts());
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin,Auditor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessUpload(List<IFormFile> receipts)
        {

            if (receipts == null || receipts.Count == 0)
            {
                ModelState.AddModelError("", "Please upload at least one valid receipt image.");
                return View("Upload");
            }

            if (receipts.Count > 5)
            {
                ModelState.AddModelError("", "You can upload up to 5 receipt images.");
                return View("Upload");
            }

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            foreach (var receipt in receipts)
            {
                var extension = Path.GetExtension(receipt.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("", $"Invalid file format: {receipt.FileName}. Please upload in PNG, JPG, JPEG, or WEBP format.");
                    return View("Upload");
                }
            }

            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var savedUrls = new List<string>();
            var streams = new List<Stream>();

            try
            {
                foreach (var receipt in receipts)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(receipt.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await receipt.CopyToAsync(stream);
                    }

                    savedUrls.Add("/Audits/Receipt/" + fileName);
                }

                // Clear previous session entries
                HttpContext.Session.Remove("ReceiptImageUrl");
                HttpContext.Session.Remove("ReceiptImageUrls");

                // Save list of URLs in session
                HttpContext.Session.SetString("ReceiptImageUrls", System.Text.Json.JsonSerializer.Serialize(savedUrls));
                if (savedUrls.Count > 0)
                {
                    HttpContext.Session.SetString("ReceiptImageUrl", savedUrls[0]);
                }

                // Open streams for OCR
                foreach (var url in savedUrls)
                {
                    var fileName = Path.GetFileName(url);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    streams.Add(new FileStream(filePath, FileMode.Open, FileAccess.Read));
                }

                var ocrResult = await _ocrService.ParseReceiptAsync(streams);

                HttpContext.Session.SetString("TotalAmount", ocrResult.TotalAmount.ToString("F2"));
                HttpContext.Session.SetString("TransactionDate", ocrResult.TransactionDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"));
                HttpContext.Session.SetString("OcrItems", System.Text.Json.JsonSerializer.Serialize(ocrResult.Items));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditsController] OCR processing failed safely: {ex.Message}");
                HttpContext.Session.SetString("TotalAmount", "0.00");
                HttpContext.Session.SetString("TransactionDate", DateTime.Today.ToString("yyyy-MM-dd"));
                HttpContext.Session.SetString("OcrItems", "[]");
                TempData["Warning"] = "OCR scan failed. Please enter the details manually.";
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }

            return RedirectToAction(nameof(Review));
        }

        [HttpGet]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin,Auditor")]
        public async Task<IActionResult> Review()
        {
            var imageUrlsJson = HttpContext.Session.GetString("ReceiptImageUrls");
            var imageUrls = string.IsNullOrEmpty(imageUrlsJson) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(imageUrlsJson);

            if (imageUrls == null || imageUrls.Count == 0)
            {
                return RedirectToAction(nameof(Upload));
            }

            await PopulateReviewLookupsAsync();

            var itemsJson = HttpContext.Session.GetString("OcrItems") ?? "[]";
            var items = System.Text.Json.JsonSerializer.Deserialize<List<OcrItemResult>>(itemsJson) ?? new();

            var model = new AuditSubmissionViewModel
            {
                ReceiptImageUrl = imageUrls[0],
                ReceiptImageUrls = imageUrls,
                Amount = decimal.TryParse(HttpContext.Session.GetString("TotalAmount"), out var amt) ? amt : 0.00m,
                EntryDate = DateTime.TryParse(HttpContext.Session.GetString("TransactionDate"), out var dt) ? dt : DateTime.Today,
                Items = items
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin,Auditor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAudit(AuditSubmissionViewModel model)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
            {
                return Challenge();
            }

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId && !u.IsDeleted);
            if (currentUser == null)
            {
                return Challenge();
            }

            int targetBuyerId = currentUserId;
            if (currentUser.Role == UserRole.Auditor)
            {
                if (!model.SelectedBuyerId.HasValue)
                {
                    ModelState.AddModelError("SelectedBuyerId", "Please select whose buyer this receipt is from.");
                }
                else
                {
                    targetBuyerId = model.SelectedBuyerId.Value;
                }
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetBuyerId && !u.IsDeleted);
            if (buyer == null)
            {
                ModelState.AddModelError("SelectedBuyerId", "The selected buyer does not exist.");
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            var selectedReviewerId = await ResolvePrivilegedReviewerIdAsync(model.SelectedReviewerId, buyer.Role, "SelectedReviewerId");

            var routesDirectlyToManager = model.CombinedDestinationId == "others";
            // Parse CombinedDestinationId
            if (!string.IsNullOrEmpty(model.CombinedDestinationId))
            {
                if (model.CombinedDestinationId.StartsWith("branch-"))
                {
                    model.EstablishmentId = int.Parse(model.CombinedDestinationId.Replace("branch-", ""));
                }
                else if (routesDirectlyToManager)
                {
                    model.EstablishmentId = await EnsureMiscellaneousEstablishmentAsync();
                }
            }

            if (!model.EstablishmentId.HasValue)
            {
                ModelState.AddModelError("CombinedDestinationId", "Please select a destination.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            var establishmentExists = await _context.Establishments.AnyAsync(e => e.Id == model.EstablishmentId.Value);
            if (!establishmentExists)
            {
                ModelState.AddModelError("CombinedDestinationId", "The selected destination does not exist.");
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    if (item.Quantity < 0 || item.Price < 0 || item.Total < 0)
                    {
                        ModelState.AddModelError("", "Line item quantities, prices, and totals must be non-negative.");
                        await PopulateReviewLookupsAsync();
                        return View("Review", model);
                    }
                }
            }

            bool isAuditor = currentUser.Role == UserRole.Auditor;

            if (!isAuditor && await _pcfFund.GetAvailableBalanceAsync(buyer) < model.Amount)
            {
                ModelState.AddModelError("", $"Insufficient Petty Cash Fund balance. Required: ₱{model.Amount:N2}, Available: ₱{await _pcfFund.GetAvailableBalanceAsync(buyer):N2}");
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!isAuditor)
                {
                    // Deduct from the (shared) fund immediately
                    await _pcfFund.DebitAsync(buyer, model.Amount);
                }

                var auditItem = new AuditItem
                {
                    BuyerId = targetBuyerId,
                    EstablishmentId = model.EstablishmentId.Value,
                    Amount = model.Amount,
                    Description = model.Description,
                    EntryDate = model.EntryDate,
                    SubmittedAt = DateTime.Now,
                    Notes = model.Notes,
                    ReceiptImageUrl = model.ReceiptImageUrls != null && model.ReceiptImageUrls.Count > 0 ? model.ReceiptImageUrls[0] : model.ReceiptImageUrl,
                    Status = routesDirectlyToManager ? AuditStatus.AwaitingManagerApproval : AuditStatus.AwaitingBranchVerification,
                    AssignedReviewerId = selectedReviewerId
                };

                // Save multi-images
                if (model.ReceiptImageUrls != null && model.ReceiptImageUrls.Count > 0)
                {
                    for (int i = 0; i < model.ReceiptImageUrls.Count; i++)
                    {
                        auditItem.Images.Add(new AuditItemImage
                        {
                            ImageUrl = model.ReceiptImageUrls[i],
                            DisplayOrder = i
                        });
                    }
                }

                // Save line items
                if (model.Items != null)
                {
                    foreach (var item in model.Items)
                    {
                        // Ensure the name is not empty if the user modified it
                        var itemName = string.IsNullOrWhiteSpace(item.Name) ? "Unknown Item" : item.Name;
                        int? assignedBranchId = null;
                        int? costCenterId = null;

                        if (!string.IsNullOrEmpty(item.CombinedDestinationId))
                        {
                            if (item.CombinedDestinationId.StartsWith("branch-"))
                            {
                                assignedBranchId = int.Parse(item.CombinedDestinationId.Replace("branch-", ""));
                            }
                        }

                        var pnlCategory = User.IsInRole("BranchStaff")
                            ? await ResolvePnlCategoryAsync(item.PnlCategoryId)
                            : null;
                        var detail = new AuditItemDetail
                        {
                            ItemName = itemName,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            Total = item.Total,
                            AssignedEstablishmentId = assignedBranchId,
                            CostCenterId = costCenterId,
                            BranchVerificationStatus = routesDirectlyToManager ? BranchVerificationStatus.Verified : BranchVerificationStatus.Pending,
                            AllocationNotes = item.AllocationNotes,
                            PnlCategoryId = pnlCategory?.Id,
                            PnlSection = pnlCategory?.Section ?? ResolveFallbackPnlSection(item),
                            PnlCategoryName = pnlCategory?.Name ?? NormalizePnlFallbackName(ResolveFallbackPnlSection(item))
                        };
                        auditItem.Details.Add(detail);
                    }
                }
                _context.AuditItems.Add(auditItem);
                await _context.SaveChangesAsync();

                var ledger = new PettyCashLedger
                {
                    UserId = targetBuyerId,
                    TransactionType = LedgerTransactionType.ExpenseDeduction,
                    Amount = -model.Amount,
                    ResultingBalance = await _pcfFund.GetAvailableBalanceAsync(buyer),
                    Timestamp = DateTime.Now,
                    AssociatedRecordId = auditItem.Id,
                    Notes = $"Expense deduction for submitted AuditItem ID {auditItem.Id}: {model.Description}"
                };
                _context.PettyCashLedgers.Add(ledger);
                await _context.SaveChangesAsync();

                if (routesDirectlyToManager)
                {
                    var reviewerIds = selectedReviewerId.HasValue
                        ? new List<int> { selectedReviewerId.Value }
                        : await _context.Users
                            .AsNoTracking()
                            .Where(u => !u.IsDeleted
                                && (u.Role == UserRole.Owner || (buyer.ManagerId.HasValue && u.Id == buyer.ManagerId.Value)))
                            .Select(u => u.Id)
                            .ToListAsync();

                    foreach (var reviewerId in reviewerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = reviewerId,
                            Title = "Audit Awaiting Manager Approval",
                            Message = $"A new other-destination audit of ₱{model.Amount:N2} from {buyer.Email} is awaiting manager approval.",
                            Category = "AuditVerify",
                            LinkUrl = Url.Action("VerifyList", "Audits") ?? "/Audits/VerifyList",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    var assignedBranchIds = auditItem.Details
                        .Where(d => d.AssignedEstablishmentId.HasValue)
                        .Select(d => d.AssignedEstablishmentId!.Value)
                        .Distinct()
                        .ToList();

                    if (assignedBranchIds.Count == 0)
                    {
                        assignedBranchIds.Add(model.EstablishmentId.Value);
                    }

                    var branchStaffIds = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.Role == UserRole.BranchStaff
                            && u.EstablishmentId.HasValue
                            && assignedBranchIds.Contains(u.EstablishmentId.Value)
                            && !u.IsDeleted)
                        .Select(u => u.Id)
                        .ToListAsync();

                    foreach (var branchStaffId in branchStaffIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = branchStaffId,
                            Title = "Audit Awaiting Branch Verification",
                            Message = $"A new audit of ₱{model.Amount:N2} from {buyer.Email} is awaiting branch verification.",
                            Category = "BranchVerify",
                            LinkUrl = Url.Action("BranchVerifyList", "Audits") ?? "/Audits/BranchVerifyList",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Clear session data for the upload after successful submission
            HttpContext.Session.Remove("ReceiptImageUrl");
            HttpContext.Session.Remove("ReceiptImageUrls");
            HttpContext.Session.Remove("TotalAmount");
            HttpContext.Session.Remove("TransactionDate");
            HttpContext.Session.Remove("OcrItems");
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAuditDraft(AuditSubmissionViewModel model)
        {
            await ApplyCombinedDestinationAsync(model);
            if (!model.EstablishmentId.HasValue)
            {
                ModelState.AddModelError("CombinedDestinationId", "Please select a destination.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            var drafts = GetPendingAuditDrafts();
            drafts.Add(model);
            SavePendingAuditDrafts(drafts);
            ClearCurrentUploadSession();
            return RedirectToAction(nameof(BatchReview));
        }

        [HttpGet]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        public IActionResult BatchReview()
        {
            return View(GetPendingAuditDrafts());
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAuditDraft(int index, string description, decimal amount, string entryDate, string? notes)
        {
            var drafts = GetPendingAuditDrafts();
            if (index < 0 || index >= drafts.Count)
            {
                return Json(new { success = false, message = "Invalid draft index." });
            }

            drafts[index].Description = description ?? drafts[index].Description;
            drafts[index].Amount = amount;
            if (DateTime.TryParse(entryDate, out var parsedDate))
            {
                drafts[index].EntryDate = parsedDate;
            }
            drafts[index].Notes = notes;

            // Recalculate amount from items if items exist
            if (drafts[index].Items != null && drafts[index].Items.Count > 0)
            {
                drafts[index].Amount = drafts[index].Items.Sum(item => item.Quantity * item.Price);
            }

            SavePendingAuditDrafts(drafts);
            return Json(new { success = true, newAmount = drafts[index].Amount });
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAuditDraftItems(int index, [FromBody] List<AuditCkDayo.Services.OcrItemResult> items)
        {
            var drafts = GetPendingAuditDrafts();
            if (index < 0 || index >= drafts.Count)
            {
                return Json(new { success = false, message = "Invalid draft index." });
            }

            drafts[index].Items = items ?? new List<AuditCkDayo.Services.OcrItemResult>();
            drafts[index].Amount = drafts[index].Items.Sum(item => item.Quantity * item.Price);

            SavePendingAuditDrafts(drafts);
            return Json(new { success = true, newAmount = drafts[index].Amount });
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveAuditDraft(int index)
        {
            var drafts = GetPendingAuditDrafts();
            if (index < 0 || index >= drafts.Count)
            {
                TempData["Error"] = "Invalid draft to remove.";
                return RedirectToAction(nameof(BatchReview));
            }

            drafts.RemoveAt(index);
            SavePendingAuditDrafts(drafts);
            return RedirectToAction(nameof(BatchReview));
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAuditBatch()
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId && !u.IsDeleted);
            if (buyer == null)
            {
                return Challenge();
            }

            var drafts = GetPendingAuditDrafts();
            if (drafts.Count == 0)
            {
                TempData["Error"] = "No processed audit invoices are waiting to submit.";
                return RedirectToAction(nameof(Upload));
            }

            var totalAmount = drafts.Sum(d => d.Amount);
            if (await _pcfFund.GetAvailableBalanceAsync(buyer) < totalAmount)
            {
                ModelState.AddModelError("", $"Insufficient Petty Cash Fund balance. Required: ₱{totalAmount:N2}, Available: ₱{await _pcfFund.GetAvailableBalanceAsync(buyer):N2}");
                return View(nameof(BatchReview), drafts);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _pcfFund.DebitAsync(buyer, totalAmount);

                foreach (var draft in drafts)
                {
                    var routesDirectlyToManager = draft.CombinedDestinationId == "others";
                    var selectedReviewerId = await ResolvePrivilegedReviewerIdAsync(draft.SelectedReviewerId, buyer.Role, "SelectedReviewerId");
                    await ApplyCombinedDestinationAsync(draft);
                    if (!draft.EstablishmentId.HasValue)
                    {
                        throw new InvalidOperationException("A batch audit draft is missing its destination.");
                    }

                    var auditItem = new AuditItem
                    {
                        BuyerId = buyerId,
                        EstablishmentId = draft.EstablishmentId.Value,
                        Amount = draft.Amount,
                        Description = draft.Description,
                        EntryDate = draft.EntryDate,
                        SubmittedAt = DateTime.Now,
                        Notes = draft.Notes,
                        ReceiptImageUrl = draft.ReceiptImageUrls.Count > 0 ? draft.ReceiptImageUrls[0] : draft.ReceiptImageUrl,
                        Status = routesDirectlyToManager ? AuditStatus.AwaitingManagerApproval : AuditStatus.AwaitingBranchVerification,
                        AssignedReviewerId = selectedReviewerId
                    };

                    foreach (var imageUrl in draft.ReceiptImageUrls)
                    {
                        auditItem.Images.Add(new AuditItemImage
                        {
                            ImageUrl = imageUrl,
                            DisplayOrder = auditItem.Images.Count
                        });
                    }

                    foreach (var item in draft.Items)
                    {
                        var assignedBranchId = item.CombinedDestinationId != null && item.CombinedDestinationId.StartsWith("branch-")
                            ? int.Parse(item.CombinedDestinationId.Replace("branch-", ""))
                            : (int?)null;

                        var pnlCategory = User.IsInRole("BranchStaff")
                            ? await ResolvePnlCategoryAsync(item.PnlCategoryId)
                            : null;
                        auditItem.Details.Add(new AuditItemDetail
                        {
                            ItemName = string.IsNullOrWhiteSpace(item.Name) ? "Unknown Item" : item.Name,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            Total = item.Total,
                            AssignedEstablishmentId = assignedBranchId,
                            BranchVerificationStatus = routesDirectlyToManager ? BranchVerificationStatus.Verified : BranchVerificationStatus.Pending,
                            AllocationNotes = item.AllocationNotes,
                            PnlCategoryId = pnlCategory?.Id,
                            PnlSection = pnlCategory?.Section ?? ResolveFallbackPnlSection(item),
                            PnlCategoryName = pnlCategory?.Name ?? NormalizePnlFallbackName(ResolveFallbackPnlSection(item))
                        });
                    }

                    _context.AuditItems.Add(auditItem);
                    await _context.SaveChangesAsync();

                    if (routesDirectlyToManager)
                    {
                        var reviewerIds = selectedReviewerId.HasValue
                            ? new List<int> { selectedReviewerId.Value }
                            : await _context.Users
                                .AsNoTracking()
                                .Where(u => !u.IsDeleted
                                    && (u.Role == UserRole.Owner || (buyer.ManagerId.HasValue && u.Id == buyer.ManagerId.Value)))
                                .Select(u => u.Id)
                                .ToListAsync();

                        foreach (var reviewerId in reviewerIds)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                UserId = reviewerId,
                                Title = "Audit Awaiting Manager Approval",
                                Message = $"A new other-destination audit of ₱{draft.Amount:N2} from {buyer.Email} is awaiting manager approval.",
                                Category = "AuditVerify",
                                LinkUrl = Url.Action("VerifyList", "Audits") ?? "/Audits/VerifyList",
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    _context.PettyCashLedgers.Add(new PettyCashLedger
                    {
                        UserId = buyerId,
                        TransactionType = LedgerTransactionType.ExpenseDeduction,
                        Amount = -draft.Amount,
                        ResultingBalance = await _pcfFund.GetAvailableBalanceAsync(buyer),
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = auditItem.Id,
                        Notes = $"Expense deduction for submitted AuditItem ID {auditItem.Id}: {draft.Description}"
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            HttpContext.Session.Remove(GetPendingAuditDraftsSessionKey());
            ClearCurrentUploadSession();
            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        [Authorize(Roles = "Owner,Manager")]
        public async Task<IActionResult> VerifyList()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<AuditItem> query = _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.AssignedEstablishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.CostCenter)
                .Where(a => a.Status == AuditStatus.AwaitingManagerApproval);

            if (role == "Manager")
            {
                var coveredManagerIds = _coverageService != null
                    ? await _coverageService.GetCoveredManagerIdsAsync(userId, DateTime.Today, CoverageScope.BuyerAudits)
                    : new List<int>();

                query = query.Where(a => a.AssignedReviewerId == userId 
                    || a.Buyer.ManagerId == userId
                    || (coveredManagerIds.Any() && a.Buyer.ManagerId.HasValue && coveredManagerIds.Contains(a.Buyer.ManagerId.Value)));
            }

            var pendingAudits = await query.ToListAsync();
            Console.WriteLine($"[DEBUG_QUEUE] Manager/Owner ID: {userId}, Role: {role}, Queue size: {pendingAudits.Count}");
            return View(pendingAudits);
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, [FromForm] AuditStatus action)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound();
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            // Access check
            bool isAuthorized = role == "Owner" || audit.AssignedReviewerId == userId || audit.Buyer.ManagerId == userId;
            if (!isAuthorized && role == "Manager" && _coverageService != null)
            {
                var coveredManagerIds = await _coverageService.GetCoveredManagerIdsAsync(userId, DateTime.Today, CoverageScope.BuyerAudits);
                if (audit.Buyer.ManagerId.HasValue && coveredManagerIds.Contains(audit.Buyer.ManagerId.Value))
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                return Forbid();
            }

            if (audit.Status != AuditStatus.AwaitingManagerApproval)
            {
                return BadRequest("This audit item is not awaiting manager approval.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                audit.Status = action;
                audit.VerifiedById = userId;
                audit.VerificationDate = DateTime.Now;

                if (action == AuditStatus.Rejected)
                {
                    // Refund money to the (shared) fund
                    await _pcfFund.CreditAsync(audit.Buyer, audit.Amount);
                    var refundedBalance = await _pcfFund.GetAvailableBalanceAsync(audit.Buyer);

                    var ledger = new PettyCashLedger
                    {
                        UserId = audit.BuyerId,
                        TransactionType = LedgerTransactionType.ReversalRefund,
                        Amount = audit.Amount,
                        ResultingBalance = refundedBalance,
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = audit.Id,
                        CounterpartyUserId = userId,
                        Notes = $"Audit item rejected. Refund of ₱{audit.Amount:N2} to buyer."
                    };
                    _context.PettyCashLedgers.Add(ledger);
                }

                // Notify Buyer on Manager Approve or Reject
                var verifyNotification = new Notification
                {
                    UserId = audit.BuyerId,
                    Title = action == AuditStatus.Approved ? "Audit Approved" : "Audit Rejected",
                    Message = action == AuditStatus.Approved 
                        ? $"Your audit item for ₱{audit.Amount:N2} was approved by manager." 
                        : $"Your audit item for ₱{audit.Amount:N2} was rejected by manager.",
                    Category = "AuditVerify",
                    LinkUrl = Url.Action("Index", "Home") ?? "/",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(verifyNotification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            return RedirectToAction(nameof(VerifyList));
        }

        [HttpGet]
        [Authorize(Roles = "BranchStaff")]
        public async Task<IActionResult> BranchVerifyList()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            
            if (currentUser == null || !currentUser.EstablishmentId.HasValue) 
            {
                return Challenge();
            }

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE AuditItems SET Status = {0} WHERE Status = '' OR Status IS NULL",
                AuditStatus.AwaitingBranchVerification.ToString());

            var branchId = currentUser.EstablishmentId.Value;
            var pendingAudits = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.AssignedEstablishment)
                .Include(a => a.Details)
                    .ThenInclude(d => d.CostCenter)
                .AsNoTracking()
                .Where(a => a.Status == AuditStatus.AwaitingBranchVerification
                    && ((!a.Details.Any() && a.EstablishmentId == branchId)
                        || a.Details.Any(d => d.BranchVerificationStatus == BranchVerificationStatus.Pending
                            && (d.AssignedEstablishmentId == branchId
                                || (!d.AssignedEstablishmentId.HasValue && a.EstablishmentId == branchId)))))
                .ToListAsync();

            foreach (var audit in pendingAudits)
            {
                audit.Details = audit.Details
                    .Where(d => d.BranchVerificationStatus == BranchVerificationStatus.Pending
                        && DetailBelongsToBranch(d, audit.EstablishmentId, branchId))
                    .ToList();
            }

            Console.WriteLine($"[DEBUG_QUEUE] BranchStaff ID: {userId}, Establishment ID: {currentUser.EstablishmentId.Value}, Queue size: {pendingAudits.Count}");
            return View(pendingAudits);
        }

        [HttpPost]
        [Authorize(Roles = "BranchStaff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchVerify(int id, string actionType)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Details)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return NotFound();

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (currentUser == null || !currentUser.EstablishmentId.HasValue) return Forbid();
            var branchId = currentUser.EstablishmentId.Value;
            var branchDetails = audit.Details
                .Where(d => d.BranchVerificationStatus == BranchVerificationStatus.Pending
                    && DetailBelongsToBranch(d, audit.EstablishmentId, branchId))
                .ToList();
            var legacyAuditWithoutDetailsForBranch = !audit.Details.Any() && audit.EstablishmentId == branchId;
            if (branchDetails.Count == 0 && !legacyAuditWithoutDetailsForBranch) return Forbid();
            if (audit.Status != AuditStatus.AwaitingBranchVerification) return BadRequest("This item is not awaiting branch verification.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (actionType == "Verify")
                {
                    foreach (var detail in branchDetails)
                    {
                        detail.BranchVerificationStatus = BranchVerificationStatus.Verified;
                    }

                    if (!HasPendingBranchVerification(audit))
                    {
                        audit.Status = AuditStatus.AwaitingManagerApproval;
                    }
                }
                else if (actionType == "Reject")
                {
                    foreach (var detail in branchDetails)
                    {
                        detail.BranchVerificationStatus = BranchVerificationStatus.Rejected;
                    }

                    audit.Status = AuditStatus.Rejected;
                    // Refund immediately
                    await _pcfFund.CreditAsync(audit.Buyer, audit.Amount);
                    var branchRefundBalance = await _pcfFund.GetAvailableBalanceAsync(audit.Buyer);

                    var ledger = new PettyCashLedger
                    {
                        UserId = audit.BuyerId,
                        TransactionType = LedgerTransactionType.ReversalRefund,
                        Amount = audit.Amount,
                        ResultingBalance = branchRefundBalance,
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = audit.Id,
                        CounterpartyUserId = userId,
                        Notes = $"Audit item rejected by branch staff. Refund of ₱{audit.Amount:N2} to buyer."
                    };
                    _context.PettyCashLedgers.Add(ledger);
                }

                // BranchVerify notifications:
                if (actionType == "Verify" && audit.Status == AuditStatus.AwaitingManagerApproval)
                {
                    // On Verify, notify the Buyer and the assigned Manager that the audit has passed branch verification.
                    var notifyBuyer = new Notification
                    {
                        UserId = audit.BuyerId,
                        Title = "Audit Passed Branch Verification",
                        Message = $"Your audit item for ₱{audit.Amount:N2} has passed branch verification and is awaiting manager approval.",
                        Category = "BranchVerify",
                        LinkUrl = Url.Action("Index", "Home") ?? "/",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notifyBuyer);

                    // Notify assigned manager (or Owner if no manager)
                    int managerNotifyId;
                    if (audit.AssignedReviewerId.HasValue)
                    {
                        managerNotifyId = audit.AssignedReviewerId.Value;
                    }
                    else if (audit.Buyer.ManagerId.HasValue)
                    {
                        managerNotifyId = audit.Buyer.ManagerId.Value;
                    }
                    else
                    {
                        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Owner);
                        managerNotifyId = owner?.Id ?? audit.BuyerId;
                    }

                    var notifyManager = new Notification
                    {
                        UserId = managerNotifyId,
                        Title = "Audit Passed Branch Verification",
                        Message = $"Audit item for ₱{audit.Amount:N2} by {audit.Buyer.Email} has passed branch verification and is awaiting your approval.",
                        Category = "BranchVerify",
                        LinkUrl = Url.Action("VerifyList", "Audits") ?? "/Audits/VerifyList",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notifyManager);
                }
                else if (actionType == "Reject")
                {
                    // On Reject, notify the Buyer.
                    var notifyBuyerReject = new Notification
                    {
                        UserId = audit.BuyerId,
                        Title = "Audit Rejected by Branch Staff",
                        Message = $"Your audit item for ₱{audit.Amount:N2} was rejected by branch staff.",
                        Category = "BranchReject",
                        LinkUrl = Url.Action("Index", "Home") ?? "/",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notifyBuyerReject);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            return RedirectToAction(nameof(BranchVerifyList));
        }

        private static bool DetailBelongsToBranch(AuditItemDetail detail, int auditEstablishmentId, int branchId)
        {
            return detail.AssignedEstablishmentId == branchId
                || (!detail.AssignedEstablishmentId.HasValue && auditEstablishmentId == branchId);
        }

        private static bool HasPendingBranchVerification(AuditItem audit)
        {
            return audit.Details.Any(detail =>
                detail.BranchVerificationStatus == BranchVerificationStatus.Pending
                && (detail.AssignedEstablishmentId.HasValue
                    || (!detail.AssignedEstablishmentId.HasValue && audit.EstablishmentId != 0)));
        }

        [HttpGet]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Images)
                .Include(a => a.Details)
                .ThenInclude(d => d.PnlCategory)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound();
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!IsPendingAuditStatus(audit.Status))
            {
                return BadRequest("Only pending audits can be edited.");
            }

            if (!CanCorrectPendingAudit(audit, userId, role))
            {
                return Forbid();
            }

            await PopulateReviewLookupsAsync();

            var imageUrls = audit.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.ImageUrl)
                .ToList();
            if (imageUrls.Count == 0 && !string.IsNullOrEmpty(audit.ReceiptImageUrl))
            {
                imageUrls.Add(audit.ReceiptImageUrl);
            }

            var model = new AuditSubmissionViewModel
            {
                AuditId = audit.Id,
                EstablishmentId = audit.EstablishmentId,
                CombinedDestinationId = audit.Establishment.IsMiscellaneous ? "others" : $"branch-{audit.EstablishmentId}",
                Amount = audit.Amount,
                Description = audit.Description,
                EntryDate = audit.EntryDate,
                Notes = audit.Notes,
                ReceiptImageUrl = audit.ReceiptImageUrl,
                ReceiptImageUrls = imageUrls,
                Items = audit.Details.Select(d => new OcrItemResult
                {
                    Name = d.ItemName,
                    Quantity = d.Quantity,
                    Price = d.Price,
                    Total = d.Total,
                    AssignedEstablishmentId = d.AssignedEstablishmentId,
                    CostCenterId = d.CostCenterId,
                    CombinedDestinationId = d.AssignedEstablishmentId.HasValue ? $"branch-{d.AssignedEstablishmentId.Value}" :
                        (d.CostCenterId.HasValue ? $"cc-{d.CostCenterId.Value}" : null),
                    AllocationNotes = d.AllocationNotes,
                    PnlCategoryId = d.PnlCategoryId ?? (d.PnlSection == PnlExpenseSection.COGS ? -1 : -2),
                    PnlSection = d.PnlCategory?.Section ?? d.PnlSection,
                    PnlCategoryName = d.PnlCategory?.Name ?? d.PnlCategoryName
                }).ToList()
            };

            return View("Review", model);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AuditSubmissionViewModel model)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Details)
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound();
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!IsPendingAuditStatus(audit.Status))
            {
                return BadRequest("Only pending audits can be edited.");
            }

            if (!CanCorrectPendingAudit(audit, userId, role))
            {
                return Forbid();
            }

            model.AuditId = id;
            await ApplyCombinedDestinationAsync(model);

            if (!model.EstablishmentId.HasValue)
            {
                ModelState.AddModelError("CombinedDestinationId", "Please select a destination.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            var establishmentId = model.EstablishmentId.Value;
            var establishmentExists = await _context.Establishments.AnyAsync(e => e.Id == establishmentId);
            if (!establishmentExists)
            {
                ModelState.AddModelError("CombinedDestinationId", "The selected destination does not exist.");
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    if (item.Quantity < 0 || item.Price < 0 || item.Total < 0)
                    {
                        ModelState.AddModelError("", "Line item quantities, prices, and totals must be non-negative.");
                        await PopulateReviewLookupsAsync();
                        return View("Review", model);
                    }
                }
            }

            var amountDelta = model.Amount - audit.Amount;
            if (amountDelta > 0 && await _pcfFund.GetAvailableBalanceAsync(audit.Buyer) < amountDelta)
            {
                ModelState.AddModelError("", $"Insufficient Petty Cash Fund balance. Required adjustment: ₱{amountDelta:N2}, Available: ₱{await _pcfFund.GetAvailableBalanceAsync(audit.Buyer):N2}");
                await PopulateReviewLookupsAsync();
                return View("Review", model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _pcfFund.DebitAsync(audit.Buyer, amountDelta);
                var oldAmount = audit.Amount;

                audit.EstablishmentId = establishmentId;
                audit.Amount = model.Amount;
                audit.Description = model.Description;
                audit.EntryDate = model.EntryDate;
                audit.Notes = model.Notes;
                audit.ReceiptImageUrl = model.ReceiptImageUrls != null && model.ReceiptImageUrls.Count > 0 ? model.ReceiptImageUrls[0] : model.ReceiptImageUrl;

                var existingDetails = audit.Details.ToList();
                _context.AuditItemDetails.RemoveRange(existingDetails);
                audit.Details.Clear();
                await AddAuditDetailsFromModelAsync(audit, model);

                var existingImages = audit.Images.ToList();
                _context.AuditItemImages.RemoveRange(existingImages);
                audit.Images.Clear();
                if (model.ReceiptImageUrls != null && model.ReceiptImageUrls.Count > 0)
                {
                    for (int i = 0; i < model.ReceiptImageUrls.Count; i++)
                    {
                        audit.Images.Add(new AuditItemImage
                        {
                            ImageUrl = model.ReceiptImageUrls[i],
                            DisplayOrder = i
                        });
                    }
                }

                if (amountDelta != 0)
                {
                    _context.PettyCashLedgers.Add(new PettyCashLedger
                    {
                        UserId = audit.BuyerId,
                        TransactionType = LedgerTransactionType.ManualAdjustment,
                        Amount = -amountDelta,
                        ResultingBalance = await _pcfFund.GetAvailableBalanceAsync(audit.Buyer),
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = audit.Id,
                        CounterpartyUserId = userId == audit.BuyerId ? null : userId,
                        Notes = $"Pending audit amount corrected from ₱{oldAmount:N2} to ₱{model.Amount:N2}."
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Void(int id)
        {
            var audit = await _context.AuditItems
                .Include(a => a.Buyer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (audit == null)
            {
                return NotFound();
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!IsPendingAuditStatus(audit.Status))
            {
                return BadRequest("Only pending audits can be voided.");
            }

            if (!CanCorrectPendingAudit(audit, userId, role))
            {
                return Forbid();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                audit.Status = AuditStatus.Cancelled;
                audit.VerifiedById = userId;
                audit.VerificationDate = DateTime.Now;
                await _pcfFund.CreditAsync(audit.Buyer, audit.Amount);
                var voidBalance = await _pcfFund.GetAvailableBalanceAsync(audit.Buyer);

                _context.PettyCashLedgers.Add(new PettyCashLedger
                {
                    UserId = audit.BuyerId,
                    TransactionType = LedgerTransactionType.ReversalRefund,
                    Amount = audit.Amount,
                    ResultingBalance = voidBalance,
                    Timestamp = DateTime.Now,
                    AssociatedRecordId = audit.Id,
                    CounterpartyUserId = userId == audit.BuyerId ? null : userId,
                    Notes = $"Pending audit voided. Refund of ₱{audit.Amount:N2} to uploader."
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet("Audits/Receipt/{filename}")]
        [Authorize]
        public async Task<IActionResult> Receipt(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return NotFound();
            }

            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            var safeFilename = Path.GetFileName(filename);
            var filePath = Path.Combine(uploadsFolder, safeFilename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
            {
                return Challenge();
            }
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var auditItem = await _context.AuditItems
                .Include(a => a.Buyer)
                .FirstOrDefaultAsync(a => (a.ReceiptImageUrl != null && a.ReceiptImageUrl.Contains(safeFilename)) ||
                                           _context.AuditItemImages.Any(ai => ai.AuditItemId == a.Id && ai.ImageUrl.Contains(safeFilename)));

            if (auditItem == null)
            {
                // Allow viewing if the filename is in the current user's session (for Review page preview before submission)
                var sessionUrl = HttpContext.Session.GetString("ReceiptImageUrl");
                var sessionUrlsJson = HttpContext.Session.GetString("ReceiptImageUrls");
                var sessionUrls = string.IsNullOrEmpty(sessionUrlsJson) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(sessionUrlsJson) ?? new List<string>();

                if ((!string.IsNullOrEmpty(sessionUrl) && sessionUrl.Contains(safeFilename)) ||
                    sessionUrls.Any(u => u.Contains(safeFilename)))
                {
                    if (currentUserRole is "Owner" or "Buyer" or "Manager" or "BranchStaff" or "Admin" or "Auditor")
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                        return File(fileBytes, GetMimeType(filePath));
                    }
                }

                // Also check current user's pending audit drafts stored in session (for BatchReview page)
                var pendingDrafts = GetPendingAuditDrafts();
                if (pendingDrafts.Any(d =>
                    (!string.IsNullOrEmpty(d.ReceiptImageUrl) && d.ReceiptImageUrl.Contains(safeFilename)) ||
                    (d.ReceiptImageUrls != null && d.ReceiptImageUrls.Any(u => u.Contains(safeFilename)))))
                {
                    if (currentUserRole is "Owner" or "Buyer" or "Manager" or "BranchStaff" or "Admin" or "Auditor")
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                        return File(fileBytes, GetMimeType(filePath));
                    }
                }

                return Forbid();
            }

            bool isAuthorized = false;
            if (currentUserRole == "Owner" || currentUserRole == "Auditor")
            {
                isAuthorized = true;
            }
            else if (auditItem.BuyerId == currentUserId)
            {
                isAuthorized = true;
            }
            else if (auditItem.Buyer != null && auditItem.Buyer.ManagerId == currentUserId)
            {
                isAuthorized = true;
            }
            else if (currentUserRole == "BranchStaff")
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
                if (currentUser != null && currentUser.EstablishmentId.HasValue && auditItem.EstablishmentId == currentUser.EstablishmentId.Value)
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                return Forbid();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, GetMimeType(filePath));
        }

        private string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        [HttpGet]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        public async Task<IActionResult> Surrender()
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId && !u.IsDeleted);
            if (buyer == null)
            {
                return NotFound("Buyer not found.");
            }

            var reserved = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId && s.Status == SurrenderStatus.Pending)
                .SumAsync(s => s.DeclaredAmount);

            var availablePcf = await _pcfFund.GetAvailableBalanceAsync(buyer);
            await PopulateSurrenderLookupsAsync(availablePcf, reserved, Math.Max(availablePcf - reserved, 0m));

            var requests = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId)
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSurrender(decimal amount, string? notes, int? assignedReceiverId = null)
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId && !u.IsDeleted);
            if (buyer == null)
            {
                return NotFound("Buyer not found.");
            }

            ModelState.Remove(nameof(notes));
            ModelState.Remove(nameof(SurrenderRequest.BuyerNotes));

            var selectedReceiverId = await ResolvePrivilegedReviewerIdAsync(assignedReceiverId, buyer.Role, "assignedReceiverId");

            var reserved = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId && s.Status == SurrenderStatus.Pending)
                .SumAsync(s => s.DeclaredAmount);

            var availablePcf = await _pcfFund.GetAvailableBalanceAsync(buyer);
            var availableBalance = Math.Max(availablePcf - reserved, 0m);

            var invalidAmount = amount <= 0 || amount > availableBalance;
            if (invalidAmount)
            {
                ModelState.AddModelError("", "Invalid surrender amount. Amount must be greater than zero and cannot exceed available balance.");
            }

            if (invalidAmount || !ModelState.IsValid)
            {
                await PopulateSurrenderLookupsAsync(availablePcf, reserved, availableBalance);

                var requests = await _context.SurrenderRequests
                    .Where(s => s.BuyerId == buyerId)
                    .OrderByDescending(s => s.RequestDate)
                    .ToListAsync();

                return View("Surrender", requests);
            }

            var surrenderRequest = new SurrenderRequest
            {
                BuyerId = buyerId,
                DeclaredAmount = amount,
                Status = SurrenderStatus.Pending,
                RequestDate = DateTime.UtcNow,
                BuyerNotes = notes,
                AssignedReceiverId = selectedReceiverId
            };

            _context.SurrenderRequests.Add(surrenderRequest);
            await _context.SaveChangesAsync();

            int surrenderManagerId;
            if (selectedReceiverId.HasValue)
            {
                surrenderManagerId = selectedReceiverId.Value;
            }
            else if (buyer.ManagerId.HasValue)
            {
                surrenderManagerId = buyer.ManagerId.Value;
            }
            else
            {
                var owner = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Owner);
                surrenderManagerId = owner?.Id ?? buyerId;
            }

            var surrenderPendingNotification = new Notification
            {
                UserId = surrenderManagerId,
                Title = "Surrender Request Pending",
                Message = $"A surrender request of ₱{amount:N2} from {buyer.Email} is pending confirmation.",
                Category = "SurrenderSubmit",
                LinkUrl = Url.Action("SurrenderQueue", "Audits") ?? "/Audits/SurrenderQueue",
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(surrenderPendingNotification);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Surrender));
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Manager")]
        public async Task<IActionResult> SurrenderQueue()
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
            {
                return Challenge();
            }
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<SurrenderRequest> query = _context.SurrenderRequests
                .Include(s => s.Buyer)
                .Where(s => s.Status == SurrenderStatus.Pending);

            if (currentUserRole == "Manager")
            {
                var coveredManagerIds = _coverageService != null
                    ? await _coverageService.GetCoveredManagerIdsAsync(currentUserId, DateTime.Today, CoverageScope.AuditSettlement | CoverageScope.BranchHandovers)
                    : new List<int>();

                query = query.Where(s => s.AssignedReceiverId == currentUserId 
                    || s.Buyer.ManagerId == currentUserId
                    || (coveredManagerIds.Any() && s.Buyer.ManagerId.HasValue && coveredManagerIds.Contains(s.Buyer.ManagerId.Value)));
            }

            var pendingRequests = await query.OrderByDescending(s => s.RequestDate).ToListAsync();
            return View(pendingRequests);
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActionSurrender(int id, string actionType, string actionNotes)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
            {
                return Challenge();
            }

            var request = await _context.SurrenderRequests
                .Include(s => s.Buyer)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                return NotFound("Surrender request not found.");
            }

            if (request.Status != SurrenderStatus.Pending)
            {
                ModelState.AddModelError("", "This request is no longer pending.");
                return RedirectToAction(nameof(SurrenderQueue));
            }

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            bool isAuthorized = currentUserRole == "Owner" || request.AssignedReceiverId == currentUserId || request.Buyer.ManagerId == currentUserId;
            if (!isAuthorized && currentUserRole == "Manager" && _coverageService != null)
            {
                var coveredManagerIds = await _coverageService.GetCoveredManagerIdsAsync(currentUserId, DateTime.Today, CoverageScope.AuditSettlement | CoverageScope.BranchHandovers);
                if (request.Buyer.ManagerId.HasValue && coveredManagerIds.Contains(request.Buyer.ManagerId.Value))
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                return Forbid();
            }

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId && !u.IsDeleted);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (actionType == "Confirm")
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var actionDate = DateTime.UtcNow;
                    request.Status = SurrenderStatus.Confirmed;
                    request.ActionDate = actionDate;
                    request.ActionByUserId = currentUserId;
                    request.ActionNotes = actionNotes;
                    request.ConfirmedAmount = request.DeclaredAmount;

                    await _pcfFund.DebitAsync(request.Buyer, request.DeclaredAmount);
                    await _pcfFund.ResetFloatOnFullSurrenderAsync(request.Buyer);

                    var cashFlowDate = actionDate.Date;
                    var flow = await _context.TreasuryCashFlows
                        .Include(f => f.Entries)
                        .FirstOrDefaultAsync(f => f.CashFlowDate == cashFlowDate);

                    if (flow == null)
                    {
                        flow = new TreasuryCashFlow
                        {
                            TreasuryUserId = currentUserId,
                            CashFlowDate = cashFlowDate,
                            StartingBalance = 0m,
                            Status = TreasuryCashFlowStatus.Open
                        };
                        _context.TreasuryCashFlows.Add(flow);
                    }

                    var treasuryEntry = new CashFlowEntry
                    {
                        TreasuryCashFlow = flow,
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.ChangePcf,
                        EstablishmentId = request.Buyer.EstablishmentId,
                        RelatedUserId = request.BuyerId,
                        Amount = request.DeclaredAmount,
                        Notes = $"PCF change surrendered by {request.Buyer.Email}. Notes: {actionNotes}",
                        CreatedByUserId = currentUserId,
                        ConfirmedByUserId = currentUserId
                    };
                    flow.Entries.Add(treasuryEntry);
                    flow.RecomputeTotals();

                    var buyerLedger = new PettyCashLedger
                    {
                        UserId = request.BuyerId,
                        TransactionType = LedgerTransactionType.CashSurrender,
                        Amount = -request.DeclaredAmount,
                        ResultingBalance = await _pcfFund.GetAvailableBalanceAsync(request.Buyer),
                        Timestamp = actionDate,
                        AssociatedRecordId = request.Id,
                        CounterpartyUserId = currentUserId,
                        Notes = $"Cash surrender request confirmed. Notes: {actionNotes}"
                    };

                    _context.PettyCashLedgers.Add(buyerLedger);
                    await _context.SaveChangesAsync();
                    // Create notification for Buyer stating surrender request confirmed
                    var confirmNotification = new Notification
                    {
                        UserId = request.BuyerId,
                        Title = "Surrender Request Confirmed",
                        Message = $"Your surrender request of ₱{request.DeclaredAmount:N2} has been confirmed.",
                        Category = "SurrenderConfirm",
                        LinkUrl = Url.Action("Surrender", "Audits") ?? "/Audits/Surrender",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(confirmNotification);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else if (actionType == "Reject")
            {
                request.Status = SurrenderStatus.Rejected;
                request.ActionDate = DateTime.UtcNow;
                request.ActionByUserId = currentUserId;
                request.ActionNotes = actionNotes;

                // Create notification for Buyer stating surrender request rejected
                var rejectNotification = new Notification
                {
                    UserId = request.BuyerId,
                    Title = "Surrender Request Rejected",
                    Message = $"Your surrender request of ₱{request.DeclaredAmount:N2} has been rejected.",
                    Category = "SurrenderReject",
                    LinkUrl = Url.Action("Surrender", "Audits") ?? "/Audits/Surrender",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(rejectNotification);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SurrenderQueue));
        }

        [HttpPost]
        [Authorize(Roles = "Buyer,Owner,Manager,BranchStaff,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSurrender(int id)
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var request = await _context.SurrenderRequests.FirstOrDefaultAsync(s => s.Id == id);
            if (request == null)
            {
                return NotFound("Surrender request not found.");
            }

            if (request.BuyerId != buyerId)
            {
                return Forbid();
            }

            if (request.Status != SurrenderStatus.Pending)
            {
                ModelState.AddModelError("", "Only pending requests can be cancelled.");
                return RedirectToAction(nameof(Surrender));
            }

            request.Status = SurrenderStatus.Cancelled;
            request.ActionDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Surrender));
        }

        private string GetPendingAuditDraftsSessionKey()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrWhiteSpace(userId)
                ? PendingAuditDraftsSessionKey
                : $"{PendingAuditDraftsSessionKey}:{userId}";
        }

        private List<AuditSubmissionViewModel> GetPendingAuditDrafts()
        {
            var json = HttpContext.Session.GetString(GetPendingAuditDraftsSessionKey());
            return string.IsNullOrEmpty(json)
                ? new List<AuditSubmissionViewModel>()
                : System.Text.Json.JsonSerializer.Deserialize<List<AuditSubmissionViewModel>>(json) ?? new List<AuditSubmissionViewModel>();
        }

        private void SavePendingAuditDrafts(List<AuditSubmissionViewModel> drafts)
        {
            HttpContext.Session.SetString(GetPendingAuditDraftsSessionKey(), System.Text.Json.JsonSerializer.Serialize(drafts));
        }

        private void ClearCurrentUploadSession()
        {
            HttpContext.Session.Remove("ReceiptImageUrl");
            HttpContext.Session.Remove("ReceiptImageUrls");
            HttpContext.Session.Remove("TotalAmount");
            HttpContext.Session.Remove("TransactionDate");
            HttpContext.Session.Remove("OcrItems");
        }
        private async Task ApplyCombinedDestinationAsync(AuditSubmissionViewModel model)
        {
            if (string.IsNullOrEmpty(model.CombinedDestinationId))
            {
                return;
            }

            if (model.CombinedDestinationId.StartsWith("branch-"))
            {
                model.EstablishmentId = int.Parse(model.CombinedDestinationId.Replace("branch-", ""));
            }
            else if (model.CombinedDestinationId == "others")
            {
                model.EstablishmentId = await EnsureMiscellaneousEstablishmentAsync();
            }
        }

        private static bool IsPendingAuditStatus(AuditStatus status)
        {
            return status == AuditStatus.AwaitingBranchVerification
                || status == AuditStatus.AwaitingManagerApproval
                || status == AuditStatus.Pending;
        }

        private static bool CanCorrectPendingAudit(AuditItem audit, int userId, string? role)
        {
            if (!IsPendingAuditStatus(audit.Status))
            {
                return false;
            }

            var canEditOwnSubmission = IsNewAuditRole(role) && audit.BuyerId == userId;
            var canEditManagerQueue = audit.Status == AuditStatus.AwaitingManagerApproval
                && (role == "Owner" || (role == "Manager" && audit.Buyer.ManagerId == userId));

            return canEditOwnSubmission || canEditManagerQueue;
        }

        private static bool IsNewAuditRole(string? role)
        {
            return role == "Buyer"
                || role == "Owner"
                || role == "Manager"
                || role == "BranchStaff"
                || role == "Admin";
        }

        private async Task AddAuditDetailsFromModelAsync(AuditItem audit, AuditSubmissionViewModel model)
        {
            if (model.Items == null)
            {
                return;
            }

            foreach (var item in model.Items)
            {
                var itemName = string.IsNullOrWhiteSpace(item.Name) ? "Unknown Item" : item.Name;
                int? assignedBranchId = null;
                int? costCenterId = null;

                if (!string.IsNullOrEmpty(item.CombinedDestinationId))
                {
                    if (item.CombinedDestinationId.StartsWith("branch-"))
                    {
                        assignedBranchId = int.Parse(item.CombinedDestinationId.Replace("branch-", ""));
                    }
                    else if (item.CombinedDestinationId.StartsWith("cc-"))
                    {
                        costCenterId = int.Parse(item.CombinedDestinationId.Replace("cc-", ""));
                    }
                }

                var pnlCategory = User.IsInRole("BranchStaff")
                    ? await ResolvePnlCategoryAsync(item.PnlCategoryId)
                    : null;
                audit.Details.Add(new AuditItemDetail
                {
                    ItemName = itemName,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Total = item.Total,
                    AssignedEstablishmentId = assignedBranchId,
                    CostCenterId = costCenterId,
                    AllocationNotes = item.AllocationNotes,
                    PnlCategoryId = pnlCategory?.Id,
                    PnlSection = pnlCategory?.Section ?? ResolveFallbackPnlSection(item),
                    PnlCategoryName = pnlCategory?.Name ?? NormalizePnlFallbackName(ResolveFallbackPnlSection(item))
                });
            }
        }

        private async Task<int> EnsureMiscellaneousEstablishmentAsync()
        {
            var existing = await _context.Establishments
                .FirstOrDefaultAsync(e => e.IsMiscellaneous && e.Name == "Others");

            if (existing != null)
            {
                return existing.Id;
            }

            var establishment = new Establishment
            {
                Name = "Others",
                IsOperatingBranch = false,
                IsMiscellaneous = true,
                IsActive = true
            };

            _context.Establishments.Add(establishment);
            await _context.SaveChangesAsync();
            return establishment.Id;
        }

        private async Task<int?> ResolvePrivilegedReviewerIdAsync(int? selectedReviewerId, UserRole submitterRole, string modelStateKey)
        {
            if (submitterRole is not (UserRole.Owner or UserRole.Manager))
            {
                return null;
            }

            if (!selectedReviewerId.HasValue)
            {
                return null;
            }

            var reviewerExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == selectedReviewerId.Value
                    && !u.IsDeleted
                    && (u.Role == UserRole.Owner || u.Role == UserRole.Manager));

            if (!reviewerExists)
            {
                ModelState.AddModelError(modelStateKey, "Select an active owner or manager.");
                return null;
            }

            return selectedReviewerId.Value;
        }


        private async Task PopulateSurrenderLookupsAsync(decimal pcfBalance, decimal reservedBalance, decimal availableBalance)
        {
            ViewBag.PcfBalance = pcfBalance;
            ViewBag.ReservedBalance = reservedBalance;
            ViewBag.AvailableBalance = availableBalance;

            var reviewers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && (u.Role == UserRole.Owner || u.Role == UserRole.Manager))
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();

            ViewBag.ReviewerUsers = new SelectList(reviewers, "Id", "Name");
        }

        private async Task<PnlCategory?> ResolvePnlCategoryAsync(int? categoryId)
        {
            return categoryId.HasValue && categoryId.Value > 0
                ? await _context.PnlCategories.FirstOrDefaultAsync(category => category.Id == categoryId.Value && category.IsActive)
                : null;
        }

        private static PnlExpenseSection ResolveFallbackPnlSection(OcrItemResult item)
        {
            return item.PnlCategoryId == -1 ? PnlExpenseSection.COGS : PnlExpenseSection.OPEX;
        }

        private static string NormalizePnlFallbackName(PnlExpenseSection section)
        {
            return section == PnlExpenseSection.COGS ? "Other - COGS" : "Other - OPEX";
        }

        private async Task PopulateReviewLookupsAsync()
        {
            var establishments = await _context.Establishments.ToListAsync();

            var combinedList = new List<SelectListItem>();
            combinedList.Add(new SelectListItem { Value = "", Text = "-- Select Destination --" });

            foreach (var est in establishments)
            {
                combinedList.Add(new SelectListItem
                {
                    Value = $"branch-{est.Id}",
                    Text = est.Name
                });
            }

            combinedList.Add(new SelectListItem
            {
                Value = "others",
                Text = "Others"
            });

            ViewBag.CombinedDestinations = combinedList;
            ViewBag.LineDestinations = combinedList
                .Where(item => item.Value.StartsWith("branch-"))
                .ToList();

            var pnlCategories = await _context.PnlCategories
                .AsNoTracking()
                .Where(category => category.IsActive && (category.Section == PnlExpenseSection.COGS || category.Section == PnlExpenseSection.OPEX))
                .OrderBy(category => category.Section)
                .ThenBy(category => category.Name)
                .Select(category => new SelectListItem
                {
                    Value = category.Id.ToString(),
                    Text = $"{category.Name} ({category.Section})"
                })
                .ToListAsync();
            pnlCategories.Insert(0, new SelectListItem { Value = "", Text = "-- Select P&L Category --" });
            pnlCategories.Add(new SelectListItem { Value = "-1", Text = "Other - COGS" });
            pnlCategories.Add(new SelectListItem { Value = "-2", Text = "Other - OPEX" });
            ViewBag.PnlCategories = pnlCategories;

            var reviewers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && (u.Role == UserRole.Owner || u.Role == UserRole.Manager))
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();

            ViewBag.ReviewerUsers = new SelectList(reviewers, "Id", "Name");

            var buyers = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && u.Role == UserRole.Buyer)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.BuyerUsers = new SelectList(buyers, "Id", "Name");
        }
    }
}
