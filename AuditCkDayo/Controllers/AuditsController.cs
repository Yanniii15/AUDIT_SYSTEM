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

        public AuditsController(AuditDbContext context, IOcrService ocrService, IWebHostEnvironment env)
        {
            _context = context;
            _ocrService = ocrService;
            _env = env;
        }

        [HttpGet]
        [Authorize(Roles = "Buyer")]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Buyer")]
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
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Review()
        {
            var imageUrlsJson = HttpContext.Session.GetString("ReceiptImageUrls");
            var imageUrls = string.IsNullOrEmpty(imageUrlsJson) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(imageUrlsJson);

            if (imageUrls == null || imageUrls.Count == 0)
            {
                return RedirectToAction(nameof(Upload));
            }

            var establishments = await _context.Establishments.ToListAsync();
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name");

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
        [Authorize(Roles = "Buyer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAudit(AuditSubmissionViewModel model)
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FindAsync(buyerId);
            if (buyer == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                var establishments = await _context.Establishments.ToListAsync();
                ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
                return View("Review", model);
            }

            var establishmentExists = await _context.Establishments.AnyAsync(e => e.Id == model.EstablishmentId);
            if (!establishmentExists)
            {
                ModelState.AddModelError("EstablishmentId", "The selected establishment does not exist.");
                var establishments = await _context.Establishments.ToListAsync();
                ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
                return View("Review", model);
            }

            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    if (item.Quantity < 0 || item.Price < 0 || item.Total < 0)
                    {
                        ModelState.AddModelError("", "Line item quantities, prices, and totals must be non-negative.");
                        var establishments = await _context.Establishments.ToListAsync();
                        ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
                        return View("Review", model);
                    }
                }
            }

            if (buyer.PcfBalance < model.Amount)
            {
                ModelState.AddModelError("", $"Insufficient Petty Cash Fund balance. Required: ₱{model.Amount:N2}, Available: ₱{buyer.PcfBalance:N2}");
                var establishments = await _context.Establishments.ToListAsync();
                ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
                return View("Review", model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Deduct from wallet immediately
                buyer.PcfBalance -= model.Amount;

                var auditItem = new AuditItem
                {
                    BuyerId = buyerId,
                    EstablishmentId = model.EstablishmentId,
                    Amount = model.Amount,
                    Description = model.Description,
                    EntryDate = model.EntryDate,
                    Notes = model.Notes,
                    ReceiptImageUrl = model.ReceiptImageUrls != null && model.ReceiptImageUrls.Count > 0 ? model.ReceiptImageUrls[0] : model.ReceiptImageUrl,
                    Status = AuditStatus.AwaitingBranchVerification
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
                        var detail = new AuditItemDetail
                        {
                            ItemName = itemName,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            Total = item.Total
                        };
                        auditItem.Details.Add(detail);
                    }
                }

                _context.AuditItems.Add(auditItem);
                await _context.SaveChangesAsync();

                var ledger = new PettyCashLedger
                {
                    UserId = buyerId,
                    TransactionType = LedgerTransactionType.ExpenseDeduction,
                    Amount = -model.Amount,
                    ResultingBalance = buyer.PcfBalance,
                    Timestamp = DateTime.Now,
                    AssociatedRecordId = auditItem.Id,
                    Notes = $"Expense deduction for submitted AuditItem ID {auditItem.Id}: {model.Description}"
                };
                _context.PettyCashLedgers.Add(ledger);
                await _context.SaveChangesAsync();

                var branchStaffIds = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Role == UserRole.BranchStaff && u.EstablishmentId == model.EstablishmentId)
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
                .Where(a => a.Status == AuditStatus.AwaitingManagerApproval);

            if (role == "Manager")
            {
                // Only see assigned buyers
                query = query.Where(a => a.Buyer.ManagerId == userId);
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
            if (role == "Manager" && audit.Buyer.ManagerId != userId)
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
                    // Refund money to buyer wallet
                    audit.Buyer.PcfBalance += audit.Amount;

                    var ledger = new PettyCashLedger
                    {
                        UserId = audit.BuyerId,
                        TransactionType = LedgerTransactionType.ReversalRefund,
                        Amount = audit.Amount,
                        ResultingBalance = audit.Buyer.PcfBalance,
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
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            
            if (currentUser == null || !currentUser.EstablishmentId.HasValue) 
            {
                return Challenge();
            }

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE AuditItems SET Status = {0} WHERE Status = '' OR Status IS NULL",
                AuditStatus.AwaitingBranchVerification.ToString());

            var pendingAudits = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Details)
                .AsNoTracking()
                .Where(a => a.Status == AuditStatus.AwaitingBranchVerification && a.EstablishmentId == currentUser.EstablishmentId.Value)
                .ToListAsync();

            Console.WriteLine($"[DEBUG_QUEUE] BranchStaff ID: {userId}, Establishment ID: {currentUser.EstablishmentId.Value}, Queue size: {pendingAudits.Count}");
            return View(pendingAudits);
        }

        [HttpPost]
        [Authorize(Roles = "BranchStaff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchVerify(int id, string actionType)
        {
            var audit = await _context.AuditItems.Include(a => a.Buyer).FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return NotFound();

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (currentUser == null || audit.EstablishmentId != currentUser.EstablishmentId) return Forbid();
            if (audit.Status != AuditStatus.AwaitingBranchVerification) return BadRequest("This item is not awaiting branch verification.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (actionType == "Verify")
                {
                    audit.Status = AuditStatus.AwaitingManagerApproval;
                }
                else if (actionType == "Reject")
                {
                    audit.Status = AuditStatus.Rejected;
                    // Refund immediately
                    audit.Buyer.PcfBalance += audit.Amount;

                    var ledger = new PettyCashLedger
                    {
                        UserId = audit.BuyerId,
                        TransactionType = LedgerTransactionType.ReversalRefund,
                        Amount = audit.Amount,
                        ResultingBalance = audit.Buyer.PcfBalance,
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = audit.Id,
                        CounterpartyUserId = userId,
                        Notes = $"Audit item rejected by branch staff. Refund of ₱{audit.Amount:N2} to buyer."
                    };
                    _context.PettyCashLedgers.Add(ledger);
                }

                // BranchVerify notifications:
                if (actionType == "Verify")
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
                    if (audit.Buyer.ManagerId.HasValue)
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
                    if (currentUserRole == "Owner" || currentUserRole == "Buyer")
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                        return File(fileBytes, GetMimeType(filePath));
                    }
                }
                return Forbid();
            }

            bool isAuthorized = false;
            if (currentUserRole == "Owner")
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
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Surrender()
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId);
            if (buyer == null)
            {
                return NotFound("Buyer not found.");
            }

            var reserved = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId && s.Status == SurrenderStatus.Pending)
                .SumAsync(s => s.DeclaredAmount);

            ViewBag.PcfBalance = buyer.PcfBalance;
            ViewBag.ReservedBalance = reserved;
            ViewBag.AvailableBalance = buyer.PcfBalance - reserved;

            var requests = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId)
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSurrender(decimal amount, string notes)
        {
            var buyerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerIdString) || !int.TryParse(buyerIdString, out var buyerId))
            {
                return Challenge();
            }

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId);
            if (buyer == null)
            {
                return NotFound("Buyer not found.");
            }

            var reserved = await _context.SurrenderRequests
                .Where(s => s.BuyerId == buyerId && s.Status == SurrenderStatus.Pending)
                .SumAsync(s => s.DeclaredAmount);

            var availableBalance = buyer.PcfBalance - reserved;

            if (amount <= 0 || amount > availableBalance)
            {
                ModelState.AddModelError("", "Invalid surrender amount. Amount must be greater than zero and cannot exceed available balance.");
                
                ViewBag.PcfBalance = buyer.PcfBalance;
                ViewBag.ReservedBalance = reserved;
                ViewBag.AvailableBalance = availableBalance;

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
                BuyerNotes = notes
            };

            _context.SurrenderRequests.Add(surrenderRequest);
            await _context.SaveChangesAsync();
            // SubmitSurrender: create a notification for the buyer's manager (or Owner) stating a surrender request is pending confirmation.
            int surrenderManagerId;
            if (buyer.ManagerId.HasValue)
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
                query = query.Where(s => s.Buyer.ManagerId == currentUserId);
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

            // Verify manager has access to this buyer
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "Manager" && request.Buyer.ManagerId != currentUserId)
            {
                return Forbid();
            }

            if (actionType == "Confirm")
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    request.Status = SurrenderStatus.Confirmed;
                    request.ActionDate = DateTime.UtcNow;
                    request.ActionByUserId = currentUserId;
                    request.ActionNotes = actionNotes;
                    request.ConfirmedAmount = request.DeclaredAmount;

                    request.Buyer.PcfBalance -= request.DeclaredAmount;
                    request.Buyer.DailyStartingFloat -= request.DeclaredAmount;

                    var ledger = new PettyCashLedger
                    {
                        UserId = request.BuyerId,
                        TransactionType = LedgerTransactionType.CashSurrender,
                        Amount = -request.DeclaredAmount,
                        ResultingBalance = request.Buyer.PcfBalance,
                        Timestamp = DateTime.Now,
                        AssociatedRecordId = request.Id,
                        CounterpartyUserId = currentUserId,
                        Notes = $"Cash surrender request confirmed. Notes: {actionNotes}"
                    };

                    _context.PettyCashLedgers.Add(ledger);
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
        [Authorize(Roles = "Buyer")]
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
    }
}
