using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Scripts
{
    public class ProductionMergeDoubleDays
    {
        public static async Task Run(AuditDbContext context)
        {
            var maymayUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "maymay@ckr.com");
            if (maymayUser == null) return;

            var mainBranch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Main"));
            var b4Branch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Branch 4") || e.Name.Contains("B4"));
            var dayoBranch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Dayo"));

            var mainId = mainBranch?.Id ?? 1;
            var b4Id = b4Branch?.Id ?? 2;
            var dayoId = dayoBranch?.Id ?? 3;

            // Define the items to merge on Aug 6 and Aug 20
            var mergeDays = new List<DailySheet>
            {
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 6),
                    StartingBalance = 22064.00m,
                    CashIn = new List<Entry>
                    {
                        // Sheet 1
                        new Entry { Amount = 35250.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 4338.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 822.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5918.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                        // Sheet 2: CashIn is 0.00
                    },
                    CashOut = new List<Entry>
                    {
                        // Sheet 1
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.Others, Notes = "Dayo Gift Voucher" },
                        new Entry { Amount = 80.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Aug 5" },
                        new Entry { Amount = 100.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Today Dyd-Monte-Dayo" },
                        // Sheet 2
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = dayoId, Notes = "PCF Release Dayo (Sheet 2)" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, Notes = "Dayo Beginning (Sheet 2)" },
                        new Entry { Amount = 104.00m, Category = CashFlowCategory.Others, Notes = "Short After Audit (Sheet 2)" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 20),
                    StartingBalance = 36183.50m,
                    CashIn = new List<Entry>
                    {
                        // Sheet 1
                        new Entry { Amount = 32166.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 12573.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 452.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 4260.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 579.00m, Category = CashFlowCategory.Others, Notes = "Willows Barista Credit Payment" }
                        // Sheet 2: CashIn is 0.00
                    },
                    CashOut = new List<Entry>
                    {
                        // Sheet 1
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 680.00m, Category = CashFlowCategory.Utilities, Notes = "Water Bill Calauag" },
                        new Entry { Amount = 50.00m, Category = CashFlowCategory.Others, Notes = "Service Maymay Aug 15&16" },
                        new Entry { Amount = 100.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Today DYD-Nawasa-Monte-Dayo" },
                        // Sheet 2
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth (Sheet 2)" },
                        new Entry { Amount = 1101.00m, Category = CashFlowCategory.Others, Notes = "Ate Myla Additional PCF (Sheet 2)" },
                        new Entry { Amount = 3920.00m, Category = CashFlowCategory.Others, Notes = "Ate Beth Additional PCF (Sheet 2)" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, Notes = "Dayo Beginning (Sheet 2)" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, Notes = "Dayo PCF (Sheet 2)" },
                        new Entry { Amount = 1093.00m, Category = CashFlowCategory.Others, Notes = "Short After Audit (Sheet 2)" }
                    }
                }
            };

            foreach (var day in mergeDays)
            {
                var flow = await context.TreasuryCashFlows
                    .Include(f => f.Entries)
                    .FirstOrDefaultAsync(f => f.CashFlowDate == day.Date && f.TreasuryUserId == maymayUser.Id);

                if (flow == null)
                {
                    flow = new TreasuryCashFlow
                    {
                        TreasuryUserId = maymayUser.Id,
                        CashFlowDate = day.Date,
                        StartingBalance = day.StartingBalance,
                        Status = TreasuryCashFlowStatus.Closed
                    };
                    context.TreasuryCashFlows.Add(flow);
                    await context.SaveChangesAsync();
                }
                else
                {
                    flow.StartingBalance = day.StartingBalance;
                    flow.Status = TreasuryCashFlowStatus.Closed;

                    // Unlink PCF
                    var entryIds = flow.Entries.Select(e => e.Id).ToList();
                    var linkedReleases = await context.PcfReleases
                        .Where(r => r.CashFlowEntryId.HasValue && entryIds.Contains(r.CashFlowEntryId.Value))
                        .ToListAsync();
                    foreach (var release in linkedReleases)
                    {
                        release.CashFlowEntryId = null;
                    }
                    await context.SaveChangesAsync();

                    context.CashFlowEntries.RemoveRange(flow.Entries);
                    flow.Entries.Clear();
                    await context.SaveChangesAsync();
                }

                foreach (var inItem in day.CashIn)
                {
                    context.CashFlowEntries.Add(new CashFlowEntry
                    {
                        TreasuryCashFlowId = flow.Id,
                        Direction = CashFlowDirection.In,
                        Category = inItem.Category,
                        EstablishmentId = inItem.EstablishmentId,
                        Amount = inItem.Amount,
                        Notes = inItem.Notes,
                        CreatedByUserId = maymayUser.Id,
                        ConfirmedByUserId = maymayUser.Id
                    });
                }

                foreach (var outItem in day.CashOut)
                {
                    context.CashFlowEntries.Add(new CashFlowEntry
                    {
                        TreasuryCashFlowId = flow.Id,
                        Direction = CashFlowDirection.Out,
                        Category = outItem.Category,
                        EstablishmentId = outItem.EstablishmentId,
                        Amount = outItem.Amount,
                        Notes = outItem.Notes,
                        CreatedByUserId = maymayUser.Id,
                        ConfirmedByUserId = maymayUser.Id
                    });
                }

                await context.SaveChangesAsync();

                var reloadedFlow = await context.TreasuryCashFlows
                    .Include(f => f.Entries)
                    .FirstOrDefaultAsync(f => f.Id == flow.Id);
                
                reloadedFlow?.RecomputeTotals();
                await context.SaveChangesAsync();
            }
        }
    }

    public class DailySheet
    {
        public DateTime Date { get; set; }
        public decimal StartingBalance { get; set; }
        public List<Entry> CashIn { get; set; } = new();
        public List<Entry> CashOut { get; set; } = new();
    }

    public class Entry
    {
        public decimal Amount { get; set; }
        public CashFlowCategory Category { get; set; }
        public int? EstablishmentId { get; set; }
        public string? Notes { get; set; }
    }
}
