using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Models;

namespace AuditCkDayo.Data
{
    public static class DbSeeder
    {
        public static void Seed(AuditDbContext db)
        {
            // Seed Establishments if empty
            if (!db.Establishments.Any())
            {
                var establishments = new List<Establishment>
                {
                    new Establishment { Name = "Dayo" },
                    new Establishment { Name = "CKR Main" },
                    new Establishment { Name = "CKR Branch 2" },
                    new Establishment { Name = "CKR Branch 4" }
                };
                db.Establishments.AddRange(establishments);
                db.SaveChanges();
            }

            if (!db.CostCenters.Any())
            {
                var costCenters = new List<CostCenter>
                {
                    new CostCenter { Name = "Cater", Category = CostCenterCategory.Cater },
                    new CostCenter { Name = "Comm", Category = CostCenterCategory.Comm },
                    new CostCenter { Name = "Payroll", Category = CostCenterCategory.Payroll },
                    new CostCenter { Name = "Utilities", Category = CostCenterCategory.Utilities },
                    new CostCenter { Name = "Staff Meal", Category = CostCenterCategory.StaffMeal },
                    new CostCenter { Name = "Vehicle", Category = CostCenterCategory.Vehicle },
                    new CostCenter { Name = "Person Tag", Category = CostCenterCategory.PersonTag },
                    new CostCenter { Name = "Miscellaneous", Category = CostCenterCategory.Miscellaneous },
                    new CostCenter { Name = "Others", Category = CostCenterCategory.Others }
                };
                db.CostCenters.AddRange(costCenters);
                db.SaveChanges();
            }

            if (!db.PnlCategories.Any())
            {
                db.PnlCategories.AddRange(
                    new PnlCategory { Name = "Food Ingredients", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Name = "Beverages", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Name = "Packaging", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Name = "Utilities", Section = PnlExpenseSection.OPEX },
                    new PnlCategory { Name = "Repairs and Maintenance", Section = PnlExpenseSection.OPEX },
                    new PnlCategory { Name = "Rent", Section = PnlExpenseSection.MonthlyFixedCost },
                    new PnlCategory { Name = "Miscellaneous", Section = PnlExpenseSection.Other });
                db.SaveChanges();
            }

            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");

            User EnsureUser(string email, Func<User> createUser)
            {
                var existingUser = db.Users.FirstOrDefault(u => u.Email == email);
                if (existingUser != null)
                {
                    return existingUser;
                }

                var user = createUser();
                db.Users.Add(user);
                db.SaveChanges();
                return user;
            }

            // Keep one default System Admin after a database reset.
            EnsureUser("admin@test.com", () => new User
            {
                Name = "System Admin",
                Email = "admin@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Admin,
                PcfBalance = 0m,
                DailyStartingFloat = 0m,
                IsTreasury = true
            });

            // Seed two users for each operational role.
            EnsureUser("owner1@test.com", () => new User
            {
                Name = "Owner One",
                Email = "owner1@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Owner,
                PcfBalance = 1000m,
                DailyStartingFloat = 1000m,
                IsTreasury = true
            });

            EnsureUser("owner2@test.com", () => new User
            {
                Name = "Owner Two",
                Email = "owner2@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Owner,
                PcfBalance = 1000m,
                DailyStartingFloat = 1000m,
                IsTreasury = true
            });

            var managerOne = EnsureUser("manager1@test.com", () => new User
            {
                Name = "Manager One",
                Email = "manager1@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Manager,
                PcfBalance = 500m,
                DailyStartingFloat = 500m,
                IsTreasury = true
            });

            var managerTwo = EnsureUser("manager2@test.com", () => new User
            {
                Name = "Manager Two",
                Email = "manager2@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Manager,
                PcfBalance = 500m,
                DailyStartingFloat = 500m,
                IsTreasury = true
            });

            EnsureUser("maymay@ckr.com", () => new User
            {
                Name = "Dorothy May",
                Email = "maymay@ckr.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Manager,
                PcfBalance = 500m,
                DailyStartingFloat = 500m,
                IsTreasury = true
            });

            EnsureUser("chelsea@ckr.com", () => new User
            {
                Name = "Chelsea Manager",
                Email = "chelsea@ckr.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Manager,
                PcfBalance = 500m,
                DailyStartingFloat = 500m,
                IsTreasury = true
            });
            EnsureUser("buyer1@test.com", () => new User
            {
                Name = "Buyer One",
                Email = "buyer1@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Buyer,
                PcfBalance = 200m,
                DailyStartingFloat = 200m,
                ManagerId = managerOne.Id
            });

            EnsureUser("buyer2@test.com", () => new User
            {
                Name = "Buyer Two",
                Email = "buyer2@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Buyer,
                PcfBalance = 200m,
                DailyStartingFloat = 200m,
                ManagerId = managerTwo.Id
            });

            var dayo = db.Establishments.FirstOrDefault(e => e.Name == "Dayo");
            var ckrMain = db.Establishments.FirstOrDefault(e => e.Name == "CKR Main");
            var ckrBranchTwo = db.Establishments.FirstOrDefault(e => e.Name == "CKR Branch 2");
            var ckrBranchFour = db.Establishments.FirstOrDefault(e => e.Name == "CKR Branch 4");

            if (ckrMain != null)
            {
                EnsureUser("staff1@test.com", () => new User
                {
                    Name = "Branch Staff One",
                    Email = "staff1@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.BranchStaff,
                    PcfBalance = 0m,
                    DailyStartingFloat = 0m,
                    EstablishmentId = ckrMain.Id,
                    ManagerId = managerOne.Id
                });
            }

            if (ckrBranchTwo != null)
            {
                EnsureUser("staff2@test.com", () => new User
                {
                    Name = "Branch Staff Two",
                    Email = "staff2@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.BranchStaff,
                    PcfBalance = 0m,
                    DailyStartingFloat = 0m,
                    EstablishmentId = ckrBranchTwo.Id,
                    ManagerId = managerTwo.Id
                });
            }

            if (dayo != null)
            {
                EnsureUser("staff-dayo@test.com", () => new User
                {
                    Name = "Dayo Branch Staff",
                    Email = "staff-dayo@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.BranchStaff,
                    PcfBalance = 0m,
                    DailyStartingFloat = 0m,
                    EstablishmentId = dayo.Id,
                    ManagerId = managerOne.Id
                });
            }

            if (ckrBranchFour != null)
            {
                EnsureUser("staff4@test.com", () => new User
                {
                    Name = "Branch Staff Four",
                    Email = "staff4@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.BranchStaff,
                    PcfBalance = 0m,
                    DailyStartingFloat = 0m,
                    EstablishmentId = ckrBranchFour.Id,
                    ManagerId = managerTwo.Id
                });
            }

            SeedThirtyDayQaData(db);
        }

        private static void SeedThirtyDayQaData(AuditDbContext db)
        {
            const string marker = "[seed:thirty-day-qa]";
            var existingSalesReportCount = db.SalesReports.Count(r => r.Notes != null && r.Notes.Contains(marker));
            var existingCashFlowCount = db.TreasuryCashFlows.Count(f => f.Entries.Any(e => e.Notes != null && e.Notes.Contains(marker)));
            var existingAuditCount = db.AuditItems.Count(a => a.Notes != null && a.Notes.Contains(marker));
            if (existingSalesReportCount >= 120 && existingCashFlowCount >= 30 && existingAuditCount >= 120)
            {
                return;
            }

            if (existingSalesReportCount > 0 || existingCashFlowCount > 0 || existingAuditCount > 0)
            {
                RemoveThirtyDayQaData(db, marker);
            }

            var admin = db.Users.First(u => u.Email == "admin@test.com");
            var owner = db.Users.First(u => u.Email == "owner1@test.com");
            var managerOne = db.Users.First(u => u.Email == "manager1@test.com");
            var managerTwo = db.Users.First(u => u.Email == "manager2@test.com");
            var buyerOne = db.Users.First(u => u.Email == "buyer1@test.com");
            var buyerTwo = db.Users.First(u => u.Email == "buyer2@test.com");
            var branches = db.Establishments
                .Where(e => e.IsOperatingBranch && e.IsActive && !e.IsMiscellaneous)
                .OrderBy(e => e.Id)
                .ToList();
            if (branches.Count == 0)
            {
                return;
            }

            var staffByBranch = db.Users
                .Where(u => u.Role == UserRole.BranchStaff && u.EstablishmentId.HasValue && !u.IsDeleted)
                .ToList()
                .GroupBy(u => u.EstablishmentId!.Value)
                .ToDictionary(group => group.Key, group => group.OrderBy(u => u.Id).First());
            var utilities = db.CostCenters.First(c => c.Name == "Utilities");
            var payroll = db.CostCenters.First(c => c.Name == "Payroll");
            var categories = db.PnlCategories.ToList();
            var foodCategory = categories.FirstOrDefault(c => c.Name == "Food Ingredients");
            var beverageCategory = categories.FirstOrDefault(c => c.Name == "Beverages");
            var packagingCategory = categories.FirstOrDefault(c => c.Name == "Packaging");
            var utilitiesCategory = categories.FirstOrDefault(c => c.Name == "Utilities");
            var repairsCategory = categories.FirstOrDefault(c => c.Name == "Repairs and Maintenance");
            var rentCategory = categories.FirstOrDefault(c => c.Name == "Rent");
            var startDate = DateTime.Today.AddDays(-29);
            var runningBalance = 50000m;
            var pcfReleases = new List<PcfRelease>();
            var audits = new List<AuditItem>();
            var settlements = new List<AuditSettlement>();
            var surrenderRequests = new List<SurrenderRequest>();
            var ledgers = new List<PettyCashLedger>();

            for (var day = 0; day < 30; day++)
            {
                var businessDate = startDate.AddDays(day);
                var flow = new TreasuryCashFlow
                {
                    TreasuryUserId = owner.Id,
                    CashFlowDate = businessDate,
                    StartingBalance = runningBalance,
                    Status = day == 29 ? TreasuryCashFlowStatus.Open : TreasuryCashFlowStatus.Closed
                };

                for (var branchIndex = 0; branchIndex < branches.Count; branchIndex++)
                {
                    var branch = branches[branchIndex];
                    var staff = staffByBranch.TryGetValue(branch.Id, out var assignedStaff) ? assignedStaff : owner;
                    var grossSales = 12000m + (branchIndex * 1750m) + (day * 210m);
                    var cashOut = 650m + (branchIndex * 90m) + (day % 5 * 35m);
                    var gcash = 1800m + (branchIndex * 120m);
                    var credit = 700m + (day % 4 * 80m);
                    var otherPayment = 250m + (branchIndex * 45m);
                    var confirmedCash = grossSales - gcash - credit - otherPayment;

                    var salesDocument = new DocumentRecord
                    {
                        DocumentType = DocumentType.DailySalesReport,
                        UploadedByUserId = staff.Id,
                        UploadedAt = businessDate.AddHours(20).AddMinutes(branchIndex),
                        ImageUrl = $"/seed/thirty-day/daily-sales-{businessDate:yyyyMMdd}-{branch.Id}.png",
                        OcrRawJson = $$"""{"seed":"thirty-day-qa","type":"daily-sales","branch":"{{branch.Name}}"}""",
                        OcrStatus = OcrStatus.Parsed,
                        ReviewStatus = DocumentReviewStatus.Confirmed,
                        ConfirmedByUserId = day % 2 == 0 ? managerOne.Id : managerTwo.Id,
                        ConfirmedAt = businessDate.AddHours(21).AddMinutes(branchIndex)
                    };

                    var salesReport = new SalesReport
                    {
                        DocumentRecord = salesDocument,
                        EstablishmentId = branch.Id,
                        CashierUserId = staff.Id,
                        CashierName = staff.Name,
                        BusinessDate = businessDate,
                        HandoverDate = businessDate,
                        GrossSales = grossSales,
                        CashOut = cashOut,
                        ConfirmedCashToHandover = confirmedCash,
                        GCashAmount = gcash,
                        CreditAmount = credit,
                        OtherPaymentAmount = otherPayment,
                        ReceiptNumberStart = $"QA{day + 1:00}{branchIndex + 1:00}001",
                        ReceiptNumberEnd = $"QA{day + 1:00}{branchIndex + 1:00}240",
                        WitnessName = day % 2 == 0 ? "Maria Santos" : "Jose Reyes",
                        Notes = $"{marker} Confirmed daily sales for {branch.Name} on {businessDate:yyyy-MM-dd}.",
                        Status = SalesReportStatus.Confirmed,
                        ConfirmedByUserId = day % 2 == 0 ? managerOne.Id : managerTwo.Id,
                        ConfirmedAt = businessDate.AddHours(21).AddMinutes(branchIndex),
                        ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { salesDocument.ImageUrl })
                    };

                    flow.Entries.Add(new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.Sales,
                        Establishment = branch,
                        RelatedUser = staff,
                        SourceDocument = salesDocument,
                        Amount = confirmedCash,
                        Notes = $"{marker} Sales cash-in for {branch.Name}.",
                        CreatedByUserId = staff.Id,
                        ConfirmedByUserId = salesReport.ConfirmedByUserId
                    });

                    db.DocumentRecords.Add(salesDocument);
                    db.SalesReports.Add(salesReport);

                    var buyer = (day + branchIndex) % 2 == 0 ? buyerOne : buyerTwo;
                    var reviewer = buyer.ManagerId == managerOne.Id ? managerOne : managerTwo;
                    var auditStatus = (day + branchIndex) % 5 == 0 ? AuditStatus.Rejected : AuditStatus.Approved;
                    var detailStatus = auditStatus == AuditStatus.Rejected ? BranchVerificationStatus.Rejected : BranchVerificationStatus.Verified;
                    var auditAmount = 520m + (day * 18m) + (branchIndex * 75m);
                    var audit = CreateAudit(
                        buyer.Id,
                        branch.Id,
                        auditAmount,
                        auditStatus == AuditStatus.Approved ? $"Approved QA deliverable {day + 1:00}-{branchIndex + 1:00}" : $"Rejected QA deliverable {day + 1:00}-{branchIndex + 1:00}",
                        businessDate.AddHours(11 + branchIndex),
                        auditStatus,
                        marker,
                        auditStatus == AuditStatus.Approved ? reviewer.Id : staff.Id,
                        new[]
                        {
                            CreateDetail("Food stock", 2 + branchIndex, 110m + day, branch.Id, null, detailStatus, foodCategory),
                            CreateDetail("Packaging supplies", 1 + (day % 3), 85m + branchIndex, branch.Id, null, detailStatus, packagingCategory),
                            CreateDetail("Branch utilities", 1, 95m + (day % 7), branch.Id, utilities.Id, detailStatus, utilitiesCategory)
                        });
                    audit.AssignedReviewerId = reviewer.Id;
                    audits.Add(audit);

                    ledgers.Add(new PettyCashLedger
                    {
                        UserId = buyer.Id,
                        TransactionType = auditStatus == AuditStatus.Approved ? LedgerTransactionType.ExpenseDeduction : LedgerTransactionType.ReversalRefund,
                        Amount = auditStatus == AuditStatus.Approved ? -auditAmount : auditAmount,
                        ResultingBalance = buyer.PcfBalance,
                        Timestamp = businessDate.AddHours(12 + branchIndex),
                        Notes = $"{marker} {auditStatus} audit ledger row for {branch.Name}."
                    });
                }

                if (day % 3 == 0)
                {
                    var receiver = day % 2 == 0 ? buyerOne : buyerTwo;
                    var releaseAmount = 4500m + (day * 40m);
                    var release = new PcfRelease
                    {
                        ReleasedByTreasuryUserId = owner.Id,
                        ReceiverUserId = receiver.Id,
                        ReceiverName = receiver.Name,
                        EstablishmentId = branches[day % branches.Count].Id,
                        Amount = releaseAmount,
                        ReleaseDate = businessDate,
                        Purpose = $"{marker} Operating PCF release for {receiver.Name}.",
                        Status = day % 6 == 0 ? PcfReleaseStatus.Settled : PcfReleaseStatus.PartiallyAudited
                    };
                    pcfReleases.Add(release);
                    flow.Entries.Add(new CashFlowEntry
                    {
                        Direction = CashFlowDirection.Out,
                        Category = CashFlowCategory.PcfRelease,
                        EstablishmentId = release.EstablishmentId,
                        RelatedUserId = receiver.Id,
                        Amount = releaseAmount,
                        Notes = $"{marker} PCF release to {receiver.Name}.",
                        CreatedByUserId = owner.Id,
                        ConfirmedByUserId = owner.Id
                    });
                    ledgers.Add(new PettyCashLedger
                    {
                        UserId = receiver.Id,
                        TransactionType = LedgerTransactionType.VaultFunding,
                        Amount = releaseAmount,
                        ResultingBalance = receiver.PcfBalance + releaseAmount,
                        Timestamp = businessDate.AddHours(9),
                        Notes = $"{marker} PCF released for daily operations."
                    });
                }

                var manualCashOutAmount = 1400m + (day % 6 * 125m);
                flow.Entries.Add(new CashFlowEntry
                {
                    Direction = CashFlowDirection.Out,
                    Category = day % 2 == 0 ? CashFlowCategory.Utilities : CashFlowCategory.Payroll,
                    EstablishmentId = day % 2 == 0 ? branches[day % branches.Count].Id : null,
                    CostCenterId = day % 2 == 0 ? utilities.Id : payroll.Id,
                    Amount = manualCashOutAmount,
                    Notes = $"{marker} Manual cash-out for {(day % 2 == 0 ? "utilities" : "payroll")}.",
                    CreatedByUserId = owner.Id,
                    ConfirmedByUserId = owner.Id
                });

                if (day % 4 == 0)
                {
                    flow.Entries.Add(new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.OwnerFunding,
                        Amount = 2500m + (day * 25m),
                        Notes = $"{marker} Manual owner cash-in.",
                        CreatedByUserId = owner.Id,
                        ConfirmedByUserId = owner.Id
                    });
                }

                if (day % 5 == 0)
                {
                    var buyer = day % 10 == 0 ? buyerOne : buyerTwo;
                    var amount = 700m + (day * 12m);
                    surrenderRequests.Add(new SurrenderRequest
                    {
                        BuyerId = buyer.Id,
                        DeclaredAmount = amount,
                        ConfirmedAmount = amount,
                        Status = SurrenderStatus.Confirmed,
                        RequestDate = businessDate.AddHours(15),
                        ActionDate = businessDate.AddHours(16),
                        ActionByUserId = buyer.ManagerId ?? owner.Id,
                        BuyerNotes = $"{marker} Confirmed cash surrender.",
                        ActionNotes = "Count matched declared cash."
                    });
                    flow.Entries.Add(new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.ChangePcf,
                        RelatedUserId = buyer.Id,
                        Amount = amount,
                        Notes = $"{marker} Confirmed PCF change surrender by {buyer.Name}.",
                        CreatedByUserId = buyer.ManagerId ?? owner.Id,
                        ConfirmedByUserId = buyer.ManagerId ?? owner.Id
                    });
                    ledgers.Add(new PettyCashLedger
                    {
                        UserId = buyer.Id,
                        TransactionType = LedgerTransactionType.CashSurrender,
                        Amount = -amount,
                        ResultingBalance = buyer.PcfBalance - amount,
                        Timestamp = businessDate.AddHours(16),
                        Notes = $"{marker} Confirmed cash surrender ledger."
                    });
                }

                flow.RecomputeTotals();
                runningBalance = flow.ClosingBalance;
                db.TreasuryCashFlows.Add(flow);
            }

            var rentAudit = CreateAudit(
                buyerOne.Id,
                branches[0].Id,
                15000m,
                "Approved monthly fixed rent",
                startDate.AddDays(14),
                AuditStatus.Approved,
                marker,
                managerOne.Id,
                new[]
                {
                    CreateDetail("Monthly rent", 1, 15000m, branches[0].Id, null, BranchVerificationStatus.Verified, rentCategory),
                    CreateDetail("Minor repairs", 1, 1850m, branches[0].Id, null, BranchVerificationStatus.Verified, repairsCategory),
                    CreateDetail("Beverage stock", 10, 120m, branches[0].Id, null, BranchVerificationStatus.Verified, beverageCategory)
                });
            rentAudit.Amount = rentAudit.Details.Sum(detail => detail.Total);
            rentAudit.AssignedReviewerId = managerOne.Id;
            audits.Add(rentAudit);

            db.AuditItems.AddRange(audits);
            db.PcfReleases.AddRange(pcfReleases);
            db.SurrenderRequests.AddRange(surrenderRequests);

            foreach (var release in pcfReleases.Where(release => release.Status == PcfReleaseStatus.Settled).Take(3))
            {
                var totalAcceptedExpenses = 2500m + (release.Id * 0m);
                var settlement = new AuditSettlement
                {
                    PcfRelease = release,
                    ReceiverUserId = release.ReceiverUserId,
                    ReceiverName = $"{marker} {release.ReceiverName}",
                    ResponsibleManagerId = release.ReceiverUserId == buyerTwo.Id ? managerTwo.Id : managerOne.Id,
                    ProcessedByUserId = admin.Id,
                    TotalPCReleased = release.Amount,
                    TotalAcceptedExpenses = totalAcceptedExpenses,
                    ActualChangeReturned = release.Amount - totalAcceptedExpenses,
                    Status = AuditSettlementStatus.Confirmed
                };
                settlement.Recompute();
                settlements.Add(settlement);
            }

            db.AuditSettlements.AddRange(settlements);
            db.PettyCashLedgers.AddRange(ledgers);
            db.SaveChanges();
        }

        private static void RemoveThirtyDayQaData(AuditDbContext db, string marker)
        {
            var demoSettlements = db.AuditSettlements
                .Where(s => s.ReceiverName != null && s.ReceiverName.Contains(marker))
                .ToList();
            db.AuditSettlements.RemoveRange(demoSettlements);

            var demoReleases = db.PcfReleases
                .Where(r => r.Purpose != null && r.Purpose.Contains(marker))
                .ToList();
            db.PcfReleases.RemoveRange(demoReleases);

            var demoSurrenders = db.SurrenderRequests
                .Where(r => r.BuyerNotes != null && r.BuyerNotes.Contains(marker))
                .ToList();
            db.SurrenderRequests.RemoveRange(demoSurrenders);

            var demoLedgers = db.PettyCashLedgers
                .Where(l => l.Notes != null && l.Notes.Contains(marker))
                .ToList();
            db.PettyCashLedgers.RemoveRange(demoLedgers);

            var demoAudits = db.AuditItems
                .Include(a => a.Details)
                .Where(a => a.Notes != null && a.Notes.Contains(marker))
                .ToList();
            db.AuditItemDetails.RemoveRange(demoAudits.SelectMany(a => a.Details));
            db.AuditItems.RemoveRange(demoAudits);

            var demoSalesReports = db.SalesReports
                .Where(r => r.Notes != null && r.Notes.Contains(marker))
                .ToList();
            db.SalesReports.RemoveRange(demoSalesReports);

            var demoCashFlows = db.TreasuryCashFlows
                .Include(f => f.Entries)
                .Where(f => f.Entries.Any(e => e.Notes != null && e.Notes.Contains(marker)))
                .ToList();
            db.CashFlowEntries.RemoveRange(demoCashFlows.SelectMany(f => f.Entries));
            db.TreasuryCashFlows.RemoveRange(demoCashFlows);

            var demoDocuments = db.DocumentRecords
                .Where(d => d.ImageUrl.Contains("/seed/thirty-day/"))
                .ToList();
            db.DocumentRecords.RemoveRange(demoDocuments);

            db.SaveChanges();
        }

        private static AuditItem CreateAudit(int buyerId, int establishmentId, decimal amount, string description, DateTime entryDate, AuditStatus status, string marker, int? verifiedById, IEnumerable<AuditItemDetail> details)
        {
            return new AuditItem
            {
                BuyerId = buyerId,
                EstablishmentId = establishmentId,
                Amount = amount,
                Description = description,
                EntryDate = entryDate,
                SubmittedAt = entryDate.AddHours(1),
                Status = status,
                Notes = $"{marker} {description}.",
                ReceiptImageUrl = $"/seed/thirty-day/audit-{entryDate:yyyyMMdd}-{Math.Abs(description.GetHashCode()):X}.png",
                VerifiedById = verifiedById,
                VerificationDate = verifiedById.HasValue ? entryDate.AddHours(2) : null,
                Details = details.ToList()
            };
        }

        private static AuditItemDetail CreateDetail(string itemName, int quantity, decimal price, int? assignedEstablishmentId, int? costCenterId, BranchVerificationStatus branchVerificationStatus, PnlCategory? pnlCategory = null)
        {
            var section = pnlCategory?.Section ?? PnlExpenseSection.Other;
            var categoryName = pnlCategory?.Name ?? "Other";
            return new AuditItemDetail
            {
                ItemName = itemName,
                Quantity = quantity,
                Price = price,
                Total = quantity * price,
                AssignedEstablishmentId = assignedEstablishmentId,
                CostCenterId = costCenterId,
                ReceiptStatus = ReceiptLineStatus.HasReceipt,
                BranchVerificationStatus = branchVerificationStatus,
                AllocationNotes = "[seed:thirty-day-qa]",
                PnlCategoryId = pnlCategory?.Id,
                PnlCategory = pnlCategory,
                PnlSection = section,
                PnlCategoryName = categoryName
            };
        }
    }
}
