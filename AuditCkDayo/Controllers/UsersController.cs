using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager")]
    public class UsersController : Controller
    {
        private readonly AuditDbContext _context;

        public UsersController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }

            var isOwner = User.IsInRole("Owner");
            if (isOwner)
            {
                var usersList = await _context.Users.Include(u => u.Manager).ToListAsync();
                var sortedUsers = usersList
                    .OrderBy(u => u.Role)
                    .ThenBy(u => u.Name)
                    .ToList();

                var managers = await _context.Users
                    .Where(u => u.Role == UserRole.Manager)
                    .OrderBy(u => u.Name)
                    .ToListAsync();
                
                ViewBag.Managers = managers;
                return View(sortedUsers);
            }
            else // Manager
            {
                var users = await _context.Users
                    .Include(u => u.Manager)
                    .Where(u => u.ManagerId == userId)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                return View(users);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPcf(int id, decimal amount, string actionType)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a valid amount.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
            {
                return Challenge();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUser = await _context.Users.FindAsync(currentUserId);
                var targetUser = await _context.Users.FindAsync(id);

                if (currentUser == null || targetUser == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                // If subtracting cash, convert amount to negative.
                decimal finalAmount = actionType == "Subtract" ? -amount : amount;
                bool isSelfTransfer = (targetUser.Id == currentUser.Id);

                if (isSelfTransfer)
                {
                    // Check that new balance won't fall below 0
                    decimal newBalance = targetUser.PcfBalance + finalAmount;
                    if (newBalance < 0)
                    {
                        TempData["Error"] = "Error: Not enough funds.";
                        return RedirectToAction(nameof(Index));
                    }

                    targetUser.PcfBalance += finalAmount;
                    targetUser.DailyStartingFloat += finalAmount;
                }
                else
                {
                    // Check that target user's new balance won't fall below 0 when subtracting
                    decimal newTargetBalance = targetUser.PcfBalance + finalAmount;
                    if (newTargetBalance < 0)
                    {
                        TempData["Error"] = "Error: Not enough funds.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Check that manager has enough balance to cover a positive transfer
                    if (finalAmount > 0 && currentUser.PcfBalance < finalAmount)
                    {
                        TempData["Error"] = "Error: You don't have enough funds.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Deduct amount from manager's PcfBalance and DailyStartingFloat
                    currentUser.PcfBalance -= finalAmount;
                    currentUser.DailyStartingFloat -= finalAmount;

                    // Add amount to target user's PcfBalance and DailyStartingFloat
                    targetUser.PcfBalance += finalAmount;
                    targetUser.DailyStartingFloat += finalAmount;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (isSelfTransfer && finalAmount > 0)
                {
                    TempData["Message"] = $"Master Vault funded with ₱{finalAmount}!";
                }
                else
                {
                    string actionWord = finalAmount > 0 ? "added to" : "subtracted from";
                    TempData["Message"] = $"Successfully {actionWord} {targetUser.Email}.";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignManager(int id, int? managerId)
        {
            var targetUser = await _context.Users.FindAsync(id);
            if (targetUser == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            targetUser.ManagerId = managerId;
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Successfully updated manager for {targetUser.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
