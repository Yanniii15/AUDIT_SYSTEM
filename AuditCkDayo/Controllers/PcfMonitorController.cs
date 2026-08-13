using System.Security.Claims;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Controllers
{
    [Authorize]
    public class PcfMonitorController : Controller
    {
        private readonly AuditDbContext _context;

        public PcfMonitorController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Challenge();
            }

            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (currentUser == null)
            {
                return Challenge();
            }

            var usersQuery = _context.Users
                .AsNoTracking()
                .Include(u => u.Establishment)
                .Where(u => !u.IsDeleted)
                .AsQueryable();

            var scopeLabel = role switch
            {
                "Owner" => "All active PCF holders",
                "Admin" => "All active PCF holders",
                "Manager" => "Your balance and assigned team",
                "Buyer" => "Your PCF balance",
                "BranchStaff" => "Your PCF balance",
                _ => "Your PCF balance"
            };

            if (role == "Owner" || role == "Admin")
            {
                usersQuery = usersQuery.Where(u => u.Role == UserRole.Buyer || u.Role == UserRole.BranchStaff || u.Role == UserRole.Manager);
            }
            else if (role == "Manager")
            {
                usersQuery = usersQuery.Where(u => u.Id == userId || u.ManagerId == userId);
            }
            else
            {
                usersQuery = usersQuery.Where(u => u.Id == userId);
            }

            var model = new PcfMonitorViewModel
            {
                ScopeLabel = scopeLabel,
                Users = await usersQuery
                    .OrderBy(u => u.Role)
                    .ThenBy(u => u.Name)
                    .ToListAsync()
            };

            return View(model);
        }
    }
}
