using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Scripts
{
    public class ProductionResetPcfFloats
    {
        public static async Task Run(AuditDbContext context)
        {
            // Get all active user accounts with the Buyer role
            var buyers = await context.Users
                .Where(u => !u.IsDeleted && u.Role == UserRole.Buyer)
                .ToListAsync();

            foreach (var buyer in buyers)
            {
                // Reset Starting Float and current PCF balance to 0
                buyer.DailyStartingFloat = 0.00m;
                buyer.PcfBalance = 0.00m;
            }

            await context.SaveChangesAsync();
        }
    }
}
