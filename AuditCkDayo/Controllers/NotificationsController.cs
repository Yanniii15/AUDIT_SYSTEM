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
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AuditDbContext _context;

        public NotificationsController(AuditDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                throw new InvalidOperationException("User ID is missing or invalid.");
            }
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestUnread()
        {
            int userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch (InvalidOperationException)
            {
                return Challenge();
            }

            var unreadQuery = _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null);

            var unreadCount = await unreadQuery.CountAsync();

            var latestUnread = await unreadQuery
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Category,
                    n.LinkUrl,
                    CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return Json(new
            {
                unreadCount,
                notifications = latestUnread
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            int userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch (InvalidOperationException)
            {
                return Challenge();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification != null && notification.ReadAt == null)
            {
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            int userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch (InvalidOperationException)
            {
                return Challenge();
            }

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ToListAsync();

            foreach (var n in unreadNotifications)
            {
                n.ReadAt = DateTime.UtcNow;
            }

            if (unreadNotifications.Any())
            {
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch (InvalidOperationException)
            {
                return Challenge();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }
    }
}
