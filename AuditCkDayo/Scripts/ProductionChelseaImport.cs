using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Scripts
{
    public class ProductionChelseaImport
    {
        public static async Task Run(AuditDbContext context)
        {
            var chelseaUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "chelsea@ckr.com");
            if (chelseaUser == null)
            {
                throw new Exception("Error: Chelsea Manager (chelsea@ckr.com) user account not found in database.");
            }

            var mainBranch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Main"));
            var b4Branch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Branch 4") || e.Name.Contains("B4"));
            var dayoBranch = await context.Establishments.FirstOrDefaultAsync(e => e.Name.Contains("Dayo"));

            var mainId = mainBranch?.Id ?? 1;
            var b4Id = b4Branch?.Id ?? 2;
            var dayoId = dayoBranch?.Id ?? 3;

            var daysData = new List<DailySheet>
            {
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 1),
                    StartingBalance = 56159.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 29341.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Main Sales" },
                        new Entry { Amount = 13333.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "B4 Sales" },
                        new Entry { Amount = 16097.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Dayo Sales" },
                        new Entry { Amount = 12250.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change Main" },
                        new Entry { Amount = 974.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = dayoId, Notes = "Change Dayo" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 13000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = dayoId, Notes = "PCF Dayo" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Uya" },
                        new Entry { Amount = 20549.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica" },
                        new Entry { Amount = 14000.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 3),
                    StartingBalance = 54605.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 4),
                    StartingBalance = 54605.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 5),
                    StartingBalance = 54605.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 2.00m, Category = CashFlowCategory.Others, Notes = "Over after audit" }
                    },
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 6),
                    StartingBalance = 54607.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 7),
                    StartingBalance = 54607.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 8),
                    StartingBalance = 54607.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 24593.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Main Sales" },
                        new Entry { Amount = 13807.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "B4 Sales" },
                        new Entry { Amount = 530.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Dayo Sales" },
                        new Entry { Amount = 3455.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change B4" },
                        new Entry { Amount = 1750.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = dayoId, Notes = "Change Dayo" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.Others, Notes = "Dayo for Asog" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF B4" },
                        new Entry { Amount = 1750.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = dayoId, Notes = "PCF Dayo" },
                        new Entry { Amount = 6839.00m, Category = CashFlowCategory.Others, Notes = "BNB Main" },
                        new Entry { Amount = 16100.00m, Category = CashFlowCategory.Others, Notes = "Asog" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Rovi" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jerose" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Bryan" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jovan" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Paul" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Ghie Ar" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tristan" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jackie" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tere" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 17),
                    StartingBalance = 14561.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 18),
                    StartingBalance = 14561.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>()
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 19),
                    StartingBalance = 14561.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 3654.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs 8/14" },
                        new Entry { Amount = 8037.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs" },
                        new Entry { Amount = 195.00m, Category = CashFlowCategory.Others, Notes = "Over B4 Beginning" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 3654.00m, Category = CashFlowCategory.Others, Notes = "Dayo Coffee & Syrup" },
                        new Entry { Amount = 8037.00m, Category = CashFlowCategory.Payroll, Notes = "Lance Payroll Blakhaws" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 20),
                    StartingBalance = 34756.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 50000.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 15000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Poto" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 21),
                    StartingBalance = 69756.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.Others, Notes = "Cashout M. Barbs" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Ate Beth" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 22),
                    StartingBalance = 54756.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 34187.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Main Sales" },
                        new Entry { Amount = 9256.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "B4 Sales" },
                        new Entry { Amount = 12933.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Dayo Sales" },
                        new Entry { Amount = 860.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change Main" },
                        new Entry { Amount = 4491.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = dayoId, Notes = "Change Dayo" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = dayoId, Notes = "PCF Dayo" },
                        new Entry { Amount = 10563.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica" },
                        new Entry { Amount = 12850.00m, Category = CashFlowCategory.Payroll, Notes = "Payroll Labor Mplaza" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Ate Uya" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 24),
                    StartingBalance = 60070.00m,
                    CashIn = new List<Entry>(),
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 30.00m, Category = CashFlowCategory.Others, Notes = "SM Parking" },
                        new Entry { Amount = 220.00m, Category = CashFlowCategory.Others, Notes = "LBC Pru life" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Ate Uya" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 25),
                    StartingBalance = 49820.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 3515.00m, Category = CashFlowCategory.Others, Notes = "M. Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 3515.00m, Category = CashFlowCategory.Others, Notes = "CKR Philhealth Aug" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 26),
                    StartingBalance = 49820.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 137.00m, Category = CashFlowCategory.ChangePcf, Notes = "PCF Poto Change" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 7500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Labor Mplaza" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Poto" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.Others, Notes = "Notary Cater" },
                        new Entry { Amount = 50.00m, Category = CashFlowCategory.Others, Notes = "Baptismal Cert. Pam" },
                        new Entry { Amount = 1300.00m, Category = CashFlowCategory.Others, Notes = "PC repair" }
                    }
                }
            };

            foreach (var day in daysData)
            {
                var flow = await context.TreasuryCashFlows
                    .Include(f => f.Entries)
                    .FirstOrDefaultAsync(f => f.CashFlowDate == day.Date && f.TreasuryUserId == chelseaUser.Id);

                if (flow == null)
                {
                    flow = new TreasuryCashFlow
                    {
                        TreasuryUserId = chelseaUser.Id,
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
                        CreatedByUserId = chelseaUser.Id,
                        ConfirmedByUserId = chelseaUser.Id
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
                        CreatedByUserId = chelseaUser.Id,
                        ConfirmedByUserId = chelseaUser.Id
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
}
