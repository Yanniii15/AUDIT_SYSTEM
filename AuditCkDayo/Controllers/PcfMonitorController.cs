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

            var users = await usersQuery
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();

            var establishmentItems = users
                .Where(u => u.Role == UserRole.BranchStaff && u.EstablishmentId.HasValue)
                .GroupBy(u => u.EstablishmentId!.Value)
                .Select(g =>
                {
                    var representative = g.OrderBy(u => u.Role).ThenBy(u => u.Name).First();
                    return new PcfMonitorItem
                    {
                        Id = representative.EstablishmentId.Value,
                        Name = representative.Establishment?.Name ?? $"Establishment {representative.EstablishmentId}",
                        Role = "Establishment",
                        EstablishmentName = representative.Establishment?.Name ?? string.Empty,
                        StartingPcf = representative.Establishment?.DailyStartingFloat ?? 0m,
                        CurrentPcf = representative.Establishment?.PcfBalance ?? 0m
                    };
                });

            var individualItems = users
                .Where(u => !(u.Role == UserRole.BranchStaff && u.EstablishmentId.HasValue))
                .Select(u => new PcfMonitorItem
                {
                    Id = u.Id,
                    Name = u.Name,
                    Role = u.Role.ToString(),
                    EstablishmentName = u.Establishment?.Name ?? "—",
                    StartingPcf = u.DailyStartingFloat,
                    CurrentPcf = u.PcfBalance
                });

            var model = new PcfMonitorViewModel
            {
                ScopeLabel = scopeLabel,
                Items = establishmentItems
                    .Concat(individualItems)
                    .ToList()
            };

            return View(model);
        }
    }
}
