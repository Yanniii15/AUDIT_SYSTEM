using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Scripts
{
    public class ProductionResetPcf
    {
        public static async Task Run(AuditDbContext context)
        {
            // Get all active user accounts with the Buyer role
            var buyers = await context.Users
                .Where(u => !u.IsDeleted && u.Role == UserRole.Buyer)
                .ToListAsync();

            foreach (var buyer in buyers)
            {
                // Reset PCF Balance to 0
                buyer.PcfBalance = 0.00m;

                // Log a reset entry in the Petty Cash Ledger to preserve auditing trail
                var ledger = new PettyCashLedger
                {
                    UserId = buyer.Id,
                    TransactionType = LedgerTransactionType.Adjustment,
                    Amount = 0.00m,
                    ResultingBalance = 0.00m,
                    Timestamp = DateTime.Now,
                    Notes = "System administrator reset: PCF Balance cleared to zero."
                };
                context.PettyCashLedgers.Add(ledger);
            }

            await context.SaveChangesAsync();
        }
    }
}
