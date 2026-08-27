using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Scripts
{
    public class ProductionTreasuryImport
    {
        public static async Task Run(AuditDbContext context)
        {
            var maymayUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "maymay@ckr.com");
            if (maymayUser == null)
            {
                throw new Exception("Error: Dorothy May (maymay@ckr.com) user account not found in database.");
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
                    Date = new DateTime(2026, 8, 2),
                    StartingBalance = 17597.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 44485.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 17824.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 9115.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 3330.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.Others, EstablishmentId = dayoId, Notes = "Dayo Gift Voucher" },
                        new Entry { Amount = 1045.00m, Category = CashFlowCategory.Others, Notes = "Sandra Loan Payment 1/2" },
                        new Entry { Amount = 36440.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 4100.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 5850.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.Others, Notes = "Loan Tereh" },
                        new Entry { Amount = 13000.00m, Category = CashFlowCategory.Payroll, Notes = "Ate Papits Team Cater Payroll" },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Sandra" },
                        new Entry { Amount = 1479.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Micha" },
                        new Entry { Amount = 1018.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Boboy" },
                        new Entry { Amount = 1260.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Janine" },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Kim" },
                        new Entry { Amount = 2608.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Ate Angie" },
                        new Entry { Amount = 36440.00m, Category = CashFlowCategory.Others, Notes = "Raels" },
                        new Entry { Amount = 4100.00m, Category = CashFlowCategory.Others, Notes = "Dayo Desserts" },
                        new Entry { Amount = 5850.00m, Category = CashFlowCategory.Others, Notes = "Dayo Desserts" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 3),
                    StartingBalance = 48025.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 28871.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 21967.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 20433.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 2590.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 3984.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, EstablishmentId = dayoId, Notes = "Dayo Beginning" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.Others, Notes = "Loan Danica" },
                        new Entry { Amount = 40000.00m, Category = CashFlowCategory.Others, Notes = "SMB Resto" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 4),
                    StartingBalance = 44870.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 19860.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 10258.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 3735.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 3043.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 7000.00m, Category = CashFlowCategory.Others, Notes = "Rent Kuya Kalbo" },
                        new Entry { Amount = 450.00m, Category = CashFlowCategory.Others, Notes = "Kuryente Kuya Kalbo" },
                        new Entry { Amount = 5060.00m, Category = CashFlowCategory.Others, Notes = "Sukli SMB Resto" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 30000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 2000.00m, Category = CashFlowCategory.Others, Notes = "Loan Bully" },
                        new Entry { Amount = 1383.00m, Category = CashFlowCategory.Others, Notes = "Withholding Tax MPlaza" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 5),
                    StartingBalance = 46893.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 29528.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 14388.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 4750.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 4480.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 27000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 25000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Jerome R." },
                        new Entry { Amount = 8475.00m, Category = CashFlowCategory.Others, Notes = "Water Bill Tesda" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 6),
                    StartingBalance = 22064.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 35250.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 4338.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 822.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5918.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.Others, Notes = "Dayo Gift Voucher" },
                        new Entry { Amount = 80.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Aug 5" },
                        new Entry { Amount = 100.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Today Dyd-Monte-Dayo" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 7),
                    StartingBalance = 42108.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 23935.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 9166.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 5181.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5723.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 7000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 200.00m, Category = CashFlowCategory.Others, Notes = "Notaryo CNC" },
                        new Entry { Amount = 2415.00m, Category = CashFlowCategory.Others, Notes = "Dayo Syrups 3 Bottles Parcel" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Ate Cherry" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 9),
                    StartingBalance = 49998.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 41544.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 11212.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 12964.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 3022.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 3710.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 51782.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth" },
                        new Entry { Amount = 4407.00m, Category = CashFlowCategory.Utilities, Notes = "Elec Bill Calauag" },
                        new Entry { Amount = 878.00m, Category = CashFlowCategory.Utilities, Notes = "Elec Bill Siruma" },
                        new Entry { Amount = 13640.00m, Category = CashFlowCategory.Utilities, Notes = "Elec Bill Bahay Dyd" },
                        new Entry { Amount = 25652.00m, Category = CashFlowCategory.Utilities, Notes = "Elec Bill Main" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tito B." },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Lex" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Bully" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Bryan" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Carly" },
                        new Entry { Amount = 2000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Ate Myla" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Cheska" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Nad" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Angelo" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Nash" },
                        new Entry { Amount = 51782.00m, Category = CashFlowCategory.Others, Notes = "Raels" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 10),
                    StartingBalance = 47373.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 25199.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 18563.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 11734.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 5492.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 4920.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, EstablishmentId = dayoId, Notes = "Dayo Beginning" },
                        new Entry { Amount = 8240.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Myla" },
                        new Entry { Amount = 36302.00m, Category = CashFlowCategory.Others, Notes = "SMB Resto" },
                        new Entry { Amount = 15635.00m, Category = CashFlowCategory.Others, Notes = "Pam & Jose School" },
                        new Entry { Amount = 6701.00m, Category = CashFlowCategory.Others, Notes = "S&R Dayo July 26" },
                        new Entry { Amount = 1215.00m, Category = CashFlowCategory.Others, Notes = "Dayo Hong" },
                        new Entry { Amount = 324.00m, Category = CashFlowCategory.Others, Notes = "S&R Main July 26" },
                        new Entry { Amount = 3500.00m, Category = CashFlowCategory.Payroll, Notes = "Ate Papits & Ryan Cater Payroll" },
                        new Entry { Amount = 1479.00m, Category = CashFlowCategory.Payroll, Notes = "Sandra Sahod" },
                        new Entry { Amount = 1335.00m, Category = CashFlowCategory.Payroll, Notes = "Micha Sahod" },
                        new Entry { Amount = 1018.00m, Category = CashFlowCategory.Payroll, Notes = "Boboy Sahod" },
                        new Entry { Amount = 1378.00m, Category = CashFlowCategory.Payroll, Notes = "Janine Sahod" },
                        new Entry { Amount = 1237.00m, Category = CashFlowCategory.Payroll, Notes = "Kim Sahod" },
                        new Entry { Amount = 1668.00m, Category = CashFlowCategory.Payroll, Notes = "Ate Angie Sahod" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 11),
                    StartingBalance = 41729.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 19295.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 2850.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 3287.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 1899.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 20387.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" },
                        new Entry { Amount = 3475.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 1122.00m, Category = CashFlowCategory.Others, Notes = "Deli Gold Office Supplies" },
                        new Entry { Amount = 2800.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica Com Aug 3" },
                        new Entry { Amount = 7057.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica Com Aug 4" },
                        new Entry { Amount = 10530.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica Dayo Aug 4" },
                        new Entry { Amount = 3475.00m, Category = CashFlowCategory.Others, Notes = "Dayo Cups" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 12),
                    StartingBalance = 53938.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 16365.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 8723.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 3010.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 187.00m, Category = CashFlowCategory.Others, Notes = "Payment Credit Kuya GJ" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 15000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Myla" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 13),
                    StartingBalance = 46223.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 17283.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 6998.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 3438.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 3201.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 25000.00m, Category = CashFlowCategory.Others, Notes = "Chelsea Transfer" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, Notes = "Loan Bryan" },
                        new Entry { Amount = 4824.00m, Category = CashFlowCategory.Others, Notes = "Dayo Truffle Paste 6pcs" },
                        new Entry { Amount = 1098.00m, Category = CashFlowCategory.Others, Notes = "Dayo Italian Seasoning 2pcs" },
                        new Entry { Amount = 66068.00m, Category = CashFlowCategory.Payroll, Notes = "Payroll July 26 - Aug 10" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 15),
                    StartingBalance = 13153.00m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 23623.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 11160.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 9658.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 1200.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 1266.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 598.00m, Category = CashFlowCategory.Others, Notes = "Charlomar Reservation Fee Bigorot" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 6678.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica Com" },
                        new Entry { Amount = 6504.00m, Category = CashFlowCategory.Others, Notes = "Zabaldica Dayo" },
                        new Entry { Amount = 50.50m, Category = CashFlowCategory.Others, Notes = "DP Squarefoot CKR QR Standee" },
                        new Entry { Amount = 4200.00m, Category = CashFlowCategory.Payroll, Notes = "Kuya Poto Sahod" },
                        new Entry { Amount = 536.00m, Category = CashFlowCategory.Others, Notes = "Dayo Semento Aug 11" },
                        new Entry { Amount = 90.00m, Category = CashFlowCategory.Others, Notes = "Kuya Poto Gas Aug 11" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.Others, Notes = "Ate Beth Cater" },
                        new Entry { Amount = 14737.00m, Category = CashFlowCategory.Payroll, Notes = "Payroll Aug 1 - 15" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 16),
                    StartingBalance = 13362.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 42703.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 15334.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 11352.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 2260.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 1196.00m, Category = CashFlowCategory.Others, Notes = "Lorenz Alfred Reyes Reservation Fee Bigorot" },
                        new Entry { Amount = 350.00m, Category = CashFlowCategory.Others, Notes = "Used Oil Main" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 717.00m, Category = CashFlowCategory.Others, Notes = "Bus Out Tray" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 17),
                    StartingBalance = 59840.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 29221.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 13920.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 14850.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 8565.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5644.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, EstablishmentId = dayoId, Notes = "Dayo Beginning" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.Others, Notes = "Loan Sandra Payment" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.Others, Notes = "Loan Micha Payment" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth Yesterday" },
                        new Entry { Amount = 5500.00m, Category = CashFlowCategory.Payroll, Notes = "Biboy Payroll Cater" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.Payroll, Notes = "Sounds & Lights Cater" },
                        new Entry { Amount = 990.00m, Category = CashFlowCategory.Payroll, Notes = "Sandra Sahod" },
                        new Entry { Amount = 859.00m, Category = CashFlowCategory.Payroll, Notes = "Micha Sahod" },
                        new Entry { Amount = 1018.00m, Category = CashFlowCategory.Payroll, Notes = "Boboy Sahod" },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Janine Sahod" },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Kim Sahod" },
                        new Entry { Amount = 1692.00m, Category = CashFlowCategory.Payroll, Notes = "Ate Angie Sahod" },
                        new Entry { Amount = 34960.00m, Category = CashFlowCategory.Others, Notes = "SMB Resto" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 18),
                    StartingBalance = 42015.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 16095.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 6237.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 2811.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 19),
                    StartingBalance = 37158.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 17159.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 11139.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 3143.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 754.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 16940.00m, Category = CashFlowCategory.Others, Notes = "Bigorot" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 30000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth" },
                        new Entry { Amount = 1833.00m, Category = CashFlowCategory.Others, Notes = "Dishwashing Resto" },
                        new Entry { Amount = 277.00m, Category = CashFlowCategory.Others, Notes = "Liters Treat M.Barbs" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 20),
                    StartingBalance = 36183.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 32166.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 12573.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 452.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 4260.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 579.00m, Category = CashFlowCategory.Others, Notes = "Willows Barista Credit Payment" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 680.00m, Category = CashFlowCategory.Utilities, Notes = "Water Bill Calauag" },
                        new Entry { Amount = 50.00m, Category = CashFlowCategory.Others, Notes = "Service Maymay Aug 15&16" },
                        new Entry { Amount = 100.00m, Category = CashFlowCategory.Others, Notes = "Keith Gas Today Dyd-Nawasa-Monte-Dayo" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 21),
                    StartingBalance = 54269.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 20025.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 8697.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 5920.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 90739.81m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 8000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.Others, Notes = "Foreman Pambakal Sako & Pala" },
                        new Entry { Amount = 11000.00m, Category = CashFlowCategory.Others, Notes = "Loan GRT" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Ate Cherry" },
                        new Entry { Amount = 2000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Ate Myla" },
                        new Entry { Amount = 90739.81m, Category = CashFlowCategory.Others, Notes = "Raels" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 23),
                    StartingBalance = 67411.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 34557.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 18782.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 12035.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 2834.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Beth" },
                        new Entry { Amount = 970.00m, Category = CashFlowCategory.Payroll, Notes = "Kim Sahod Dayo" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Jajam" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Lex" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Carly" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Bully" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Bryan" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jacky" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Geraldo" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Nad" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jerose" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Rovi" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA GRT" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Paul" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Nash" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Cheska" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tereh" },
                        new Entry { Amount = 500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tristan" },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Jovan" },
                        new Entry { Amount = 1500.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Kuya Jerome R." },
                        new Entry { Amount = 1000.00m, Category = CashFlowCategory.CashAdvance, Notes = "CA Tito B." },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Sandra" },
                        new Entry { Amount = 917.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Micha" },
                        new Entry { Amount = 1018.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Boboy" },
                        new Entry { Amount = 1503.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Janine" },
                        new Entry { Amount = 2773.00m, Category = CashFlowCategory.Payroll, Notes = "Sahod Ate Angie" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 24),
                    StartingBalance = 84935.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 22960.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 11387.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 31586.00m, Category = CashFlowCategory.Sales, EstablishmentId = dayoId, Notes = "Sales Handover Dayo" },
                        new Entry { Amount = 1358.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 4920.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = dayoId, Notes = "Change PCF Dayo" },
                        new Entry { Amount = 3000.00m, Category = CashFlowCategory.Others, EstablishmentId = dayoId, Notes = "Dayo Beginning" },
                        new Entry { Amount = 29917.47m, Category = CashFlowCategory.Others, Notes = "M.Barbs" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 10000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 20000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 8345.11m, Category = CashFlowCategory.Others, Notes = "Princeton DYD Aug 3" },
                        new Entry { Amount = 9764.36m, Category = CashFlowCategory.Others, Notes = "Princeton DYD Aug 10" },
                        new Entry { Amount = 11808.00m, Category = CashFlowCategory.Others, Notes = "Princeton DYD Aug 19" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 25),
                    StartingBalance = 133146.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 14912.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 9134.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 185.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 173.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" },
                        new Entry { Amount = 300.00m, Category = CashFlowCategory.Others, Notes = "Main Used Oil" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 15000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 6000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" }
                    }
                },
                new DailySheet
                {
                    Date = new DateTime(2026, 8, 26),
                    StartingBalance = 136850.50m,
                    CashIn = new List<Entry>
                    {
                        new Entry { Amount = 17871.00m, Category = CashFlowCategory.Sales, EstablishmentId = mainId, Notes = "Sales Handover Main" },
                        new Entry { Amount = 7793.00m, Category = CashFlowCategory.Sales, EstablishmentId = b4Id, Notes = "Sales Handover B4" },
                        new Entry { Amount = 6592.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = mainId, Notes = "Change PCF Main" },
                        new Entry { Amount = 3889.00m, Category = CashFlowCategory.ChangePcf, EstablishmentId = b4Id, Notes = "Change PCF B4" }
                    },
                    CashOut = new List<Entry>
                    {
                        new Entry { Amount = 15000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = mainId, Notes = "PCF Release Main" },
                        new Entry { Amount = 5000.00m, Category = CashFlowCategory.PcfRelease, EstablishmentId = b4Id, Notes = "PCF Release B4" },
                        new Entry { Amount = 30000.00m, Category = CashFlowCategory.PcfRelease, Notes = "PCF Release Ate Uya" }
                    }
                }
            };

            foreach (var day in daysData)
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
                    context.CashFlowEntries.RemoveRange(flow.Entries);
                    flow.Entries.Clear();
                    await context.SaveChangesAsync();
                }

                foreach (var inItem in day.CashIn)
                {
                    var entry = new CashFlowEntry
                    {
                        TreasuryCashFlowId = flow.Id,
                        Direction = CashFlowDirection.In,
                        Category = inItem.Category,
                        EstablishmentId = inItem.EstablishmentId,
                        Amount = inItem.Amount,
                        Notes = inItem.Notes,
                        CreatedByUserId = maymayUser.Id,
                        ConfirmedByUserId = maymayUser.Id
                    };
                    context.CashFlowEntries.Add(entry);
                }

                foreach (var outItem in day.CashOut)
                {
                    var entry = new CashFlowEntry
                    {
                        TreasuryCashFlowId = flow.Id,
                        Direction = CashFlowDirection.Out,
                        Category = outItem.Category,
                        EstablishmentId = outItem.EstablishmentId,
                        Amount = outItem.Amount,
                        Notes = outItem.Notes,
                        CreatedByUserId = maymayUser.Id,
                        ConfirmedByUserId = maymayUser.Id
                    };
                    context.CashFlowEntries.Add(entry);
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
