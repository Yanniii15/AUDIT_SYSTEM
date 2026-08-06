using System;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> CashTransferLocks = new();


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
                var usersList = await _context.Users.Include(u => u.Manager).Where(u => !u.IsDeleted).ToListAsync();
                var sortedUsers = usersList
                    .OrderBy(u => u.Role)
                    .ThenBy(u => u.Name)
                    .ToList();

                var managers = await _context.Users
                    .Where(u => u.Role == UserRole.Manager && !u.IsDeleted)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                ViewBag.TotalPcfBalance = await _context.Users.Where(u => !u.IsDeleted).SumAsync(u => u.PcfBalance);
                ViewBag.LedgerEntries = await _context.PettyCashLedgers
                    .Include(l => l.User)
                    .Include(l => l.CounterpartyUser)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(20)
                    .ToListAsync();
                ViewBag.Managers = managers;
                return View(sortedUsers);
            }
            else // Manager
            {
                var users = await _context.Users
                    .Include(u => u.Manager)
                    .Where(u => u.ManagerId == userId && !u.IsDeleted)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                ViewBag.TotalPcfBalance = await _context.Users
                    .Where(u => (u.Id == userId || u.ManagerId == userId) && !u.IsDeleted)
                    .SumAsync(u => u.PcfBalance);
                ViewBag.LedgerEntries = await _context.PettyCashLedgers
                    .Include(l => l.User)
                    .Include(l => l.CounterpartyUser)
                    .Where(l => l.UserId == userId)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(20)
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

            var lockUserIds = new[] { currentUserId, id }.Distinct().OrderBy(userId => userId).ToArray();
            var lockKey = string.Join(':', lockUserIds);
            var transferLock = CashTransferLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await transferLock.WaitAsync();
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var lockSqlPlaceholders = string.Join(",", lockUserIds.Select((_, index) => $"{{{index}}}"));
                    var lockSqlParameters = lockUserIds.Cast<object>().ToArray();
                    var lockSql = $"SELECT * FROM `Users` WHERE `Id` IN ({lockSqlPlaceholders}) ORDER BY `Id` FOR UPDATE";

                    var lockedUsers = _context.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true
                        ? await _context.Users
                            .FromSqlRaw(lockSql, lockSqlParameters)
                            .ToListAsync()
                        : await _context.Users
                            .Where(u => lockUserIds.Contains(u.Id))
                            .OrderBy(u => u.Id)
                            .ToListAsync();

                    var currentUser = lockedUsers.FirstOrDefault(u => u.Id == currentUserId);
                    var targetUser = lockedUsers.FirstOrDefault(u => u.Id == id);
                if (currentUser == null || targetUser == null || currentUser.IsDeleted || targetUser.IsDeleted)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                bool isSelfTransfer = (targetUser.Id == currentUser.Id);
                bool isAuthorized = User.IsInRole("Owner") || (!isSelfTransfer && targetUser.ManagerId == currentUser.Id);
                if (!isAuthorized)
                {
                    TempData["Error"] = "Unauthorized access.";
                    return RedirectToAction(nameof(Index));
                }
                // If subtracting cash, convert amount to negative.
                decimal finalAmount = actionType == "Subtract" ? -amount : amount;

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

                    var ledger = new PettyCashLedger
                    {
                        UserId = targetUser.Id,
                        TransactionType = LedgerTransactionType.VaultFunding,
                        Amount = finalAmount,
                        ResultingBalance = targetUser.PcfBalance,
                        Timestamp = DateTime.Now,
                        CounterpartyUserId = null,
                        Notes = finalAmount > 0 
                            ? $"Vault funded with ₱{finalAmount:N2}." 
                            : $"Vault adjusted/reduced by ₱{-finalAmount:N2}."
                    };
                    _context.PettyCashLedgers.Add(ledger);
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

                    var managerLedger = new PettyCashLedger
                    {
                        UserId = currentUser.Id,
                        TransactionType = LedgerTransactionType.ManagerFunding,
                        Amount = -finalAmount,
                        ResultingBalance = currentUser.PcfBalance,
                        Timestamp = DateTime.Now,
                        CounterpartyUserId = targetUser.Id,
                        Notes = finalAmount > 0
                            ? $"Allocated ₱{finalAmount:N2} to {targetUser.Email}."
                            : $"Retrieved/deducted ₱{-finalAmount:N2} from {targetUser.Email}."
                    };
                    _context.PettyCashLedgers.Add(managerLedger);

                    var buyerLedger = new PettyCashLedger
                    {
                        UserId = targetUser.Id,
                        TransactionType = LedgerTransactionType.ManagerFunding,
                        Amount = finalAmount,
                        ResultingBalance = targetUser.PcfBalance,
                        Timestamp = DateTime.Now,
                        CounterpartyUserId = currentUser.Id,
                        Notes = finalAmount > 0
                            ? $"Received ₱{finalAmount:N2} from {currentUser.Email}."
                            : $"Returned ₱{-finalAmount:N2} to manager {currentUser.Email}."
                    };
                    _context.PettyCashLedgers.Add(buyerLedger);
                }

                // Create notification for targetUser when funds have been transferred/adjusted.
                var notification = new Notification
                {
                    UserId = targetUser.Id,
                    Title = "Funds Adjusted",
                    Message = finalAmount > 0 
                        ? $"Funds transferred: Received ₱{finalAmount:N2} from {(isSelfTransfer ? "Master Vault" : currentUser.Email)}." 
                        : $"Funds adjusted: Deducted ₱{-finalAmount:N2} by {(isSelfTransfer ? "Master Vault" : currentUser.Email)}.",
                    Category = "Funding",
                    LinkUrl = (Url != null ? (Url.Action("Index", "Users") ?? "/Users") : "/Users"),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);

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
                    Console.WriteLine($"Error in AddPcf: {ex}");
                    TempData["Error"] = "An error occurred while processing the transfer.";
                }
            }
            finally
            {
                transferLock.Release();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignManager(int id, int? managerId)
        {
            var targetUser = await _context.Users.FindAsync(id);
            if (targetUser == null || targetUser.IsDeleted)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Register", "Account");
            }

            targetUser.ManagerId = managerId;
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Successfully updated manager for {targetUser.Email}.";
            return RedirectToAction("Register", "Account");
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.IsDeleted)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Register", "Account");
            }

            var currentUserIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(currentUserIdString, out var currentUserId) && currentUserId == id)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("Register", "Account");
            }

            var hasAuditItems = await _context.AuditItems.AnyAsync(a => a.BuyerId == id || a.VerifiedById == id);
            var hasLedgerEntries = await _context.PettyCashLedgers.AnyAsync(l => l.UserId == id || l.CounterpartyUserId == id);
            var hasSurrenders = await _context.SurrenderRequests.AnyAsync(s => s.BuyerId == id || s.ActionByUserId == id);
            if (hasAuditItems || hasLedgerEntries || hasSurrenders)
            {
                var archivedStaffMembers = await _context.Users.Where(u => u.ManagerId == id).ToListAsync();
                foreach (var staff in archivedStaffMembers)
                {
                    staff.ManagerId = null;
                }

                user.IsDeleted = true;
                await _context.SaveChangesAsync();

                TempData["Message"] = $"User '{user.Name}' has been archived.";
                return RedirectToAction("Register", "Account");
            }

            // Unassign staff reporting to this manager
            var staffMembers = await _context.Users.Where(u => u.ManagerId == id).ToListAsync();
            foreach (var staff in staffMembers)
            {
                staff.ManagerId = null;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"User '{user.Name}' has been successfully deleted.";
            return RedirectToAction("Register", "Account");
        }
    }
}
