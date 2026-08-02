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
        public async Task<IActionResult> ProcessUpload(IFormFile receipt)
        {
            if (receipt == null || receipt.Length == 0)
            {
                ModelState.AddModelError("", "Please upload a valid receipt image.");
                return View("Upload");
            }

            var extension = Path.GetExtension(receipt.FileName)?.ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".webp")
            {
                ModelState.AddModelError("", "Invalid file format. Please upload a receipt in PNG, JPG, JPEG, or WEBP format.");
                return View("Upload");
            }

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }
            var uploadsFolder = Path.Combine(webRoot, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(receipt.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await receipt.CopyToAsync(stream);
            }

            // Perform OCR
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var ocrResult = await _ocrService.ParseReceiptAsync(stream);
                
                HttpContext.Session.SetString("ReceiptImageUrl", "/uploads/" + fileName);
                HttpContext.Session.SetString("TotalAmount", ocrResult.TotalAmount.ToString("F2"));
                HttpContext.Session.SetString("TransactionDate", ocrResult.TransactionDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"));
                HttpContext.Session.SetString("OcrItems", System.Text.Json.JsonSerializer.Serialize(ocrResult.Items));
            }

            return RedirectToAction(nameof(Review));
        }

        [HttpGet]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Review()
        {
            var imageUrl = HttpContext.Session.GetString("ReceiptImageUrl");
            if (string.IsNullOrEmpty(imageUrl))
            {
                return RedirectToAction(nameof(Upload));
            }

            var establishments = await _context.Establishments.ToListAsync();
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name");

            var itemsJson = HttpContext.Session.GetString("OcrItems") ?? "[]";
            var items = System.Text.Json.JsonSerializer.Deserialize<List<OcrItemResult>>(itemsJson) ?? new();

            var model = new AuditSubmissionViewModel
            {
                ReceiptImageUrl = imageUrl,
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
                ReceiptImageUrl = model.ReceiptImageUrl,
                Status = AuditStatus.Pending
            };

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

            // Clear session data for the upload after successful submission
            HttpContext.Session.Remove("ReceiptImageUrl");
            HttpContext.Session.Remove("TotalAmount");
            HttpContext.Session.Remove("TransactionDate");
            HttpContext.Session.Remove("OcrItems");

            return RedirectToAction("Index", "Home");
        }
    }
}
