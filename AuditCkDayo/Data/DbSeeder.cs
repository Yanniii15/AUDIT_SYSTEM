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

            var ckrMain = db.Establishments.FirstOrDefault(e => e.Name == "CKR Main");
            var ckrBranchTwo = db.Establishments.FirstOrDefault(e => e.Name == "CKR Branch 2");

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
                    EstablishmentId = ckrMain.Id
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
                    EstablishmentId = ckrBranchTwo.Id
                });
            }

            SeedWeeklyDemoData(db);
        }

        private static void SeedWeeklyDemoData(AuditDbContext db)
        {
            const string marker = "[seed:weekly-demo]";
            var existingDemoAuditCount = db.AuditItems.Count(a => a.Notes != null && a.Notes.Contains(marker));
            var existingDemoDocumentCount = db.DocumentRecords.Count(d => d.ImageUrl.Contains("/seed/week/"));
            if (existingDemoAuditCount >= 44 && existingDemoDocumentCount >= 14)
            {
                return;
            }

            if (existingDemoAuditCount > 0 || existingDemoDocumentCount > 0)
            {
                RemoveWeeklyDemoData(db, marker);
            }

            var admin = db.Users.First(u => u.Email == "admin@test.com");
            var owner = db.Users.First(u => u.Email == "owner1@test.com");
            var managerOne = db.Users.First(u => u.Email == "manager1@test.com");
            var managerTwo = db.Users.First(u => u.Email == "manager2@test.com");
            var buyerOne = db.Users.First(u => u.Email == "buyer1@test.com");
            var buyerTwo = db.Users.First(u => u.Email == "buyer2@test.com");
            var staffOne = db.Users.First(u => u.Email == "staff1@test.com");
            var staffTwo = db.Users.First(u => u.Email == "staff2@test.com");
            var ckrMain = db.Establishments.First(e => e.Name == "CKR Main");
            var ckrBranchTwo = db.Establishments.First(e => e.Name == "CKR Branch 2");
            var cater = db.CostCenters.First(c => c.Name == "Cater");
            var utilities = db.CostCenters.First(c => c.Name == "Utilities");
            var startDate = DateTime.Today.AddDays(-6);

            for (var day = 0; day < 7; day++)
            {
                var businessDate = startDate.AddDays(day);
                var establishment = day % 2 == 0 ? ckrMain : ckrBranchTwo;
                var staff = establishment.Id == ckrMain.Id ? staffOne : staffTwo;
                var grossSales = 18500m + (day * 725m);
                var cashOut = 1200m + (day * 65m);
                var cashToHandover = grossSales - cashOut - 4200m - 1800m - 350m;

                var salesDocument = new DocumentRecord
                {
                    DocumentType = DocumentType.DailySalesReport,
                    UploadedByUserId = staff.Id,
                    UploadedAt = businessDate.AddHours(20),
                    ImageUrl = $"/seed/week/daily-sales-{businessDate:yyyyMMdd}.png",
                    OcrRawJson = """{"seed":"weekly-demo","type":"daily-sales"}""",
                    OcrStatus = OcrStatus.Parsed,
                    ReviewStatus = DocumentReviewStatus.Confirmed,
                    ConfirmedByUserId = managerOne.Id,
                    ConfirmedAt = businessDate.AddHours(21)
                };

                var salesReport = new SalesReport
                {
                    DocumentRecord = salesDocument,
                    EstablishmentId = establishment.Id,
                    CashierUserId = staff.Id,
                    CashierName = staff.Name,
                    BusinessDate = businessDate,
                    HandoverDate = businessDate.AddHours(20),
                    GrossSales = grossSales,
                    CashOut = cashOut,
                    ConfirmedCashToHandover = cashToHandover,
                    GCashAmount = 4200m,
                    CreditAmount = 1800m,
                    OtherPaymentAmount = 350m,
                    ReceiptNumberStart = $"W{day + 1:00}0001",
                    ReceiptNumberEnd = $"W{day + 1:00}0180",
                    WitnessName = day % 2 == 0 ? "Maria Santos" : "Jose Reyes",
                    Notes = $"{marker} Confirmed sales report for {businessDate:yyyy-MM-dd}.",
                    Status = SalesReportStatus.Confirmed,
                    ConfirmedByUserId = managerOne.Id,
                    ConfirmedAt = businessDate.AddHours(21),
                    ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { salesDocument.ImageUrl })
                };

                var flow = new TreasuryCashFlow
                {
                    TreasuryUserId = owner.Id,
                    CashFlowDate = businessDate,
                    StartingBalance = 50000m + (day * 1500m),
                    Status = day == 6 ? TreasuryCashFlowStatus.Open : TreasuryCashFlowStatus.Closed
                };
                flow.Entries.Add(new CashFlowEntry
                {
                    Direction = CashFlowDirection.In,
                    Category = CashFlowCategory.Sales,
                    EstablishmentId = establishment.Id,
                    RelatedUserId = staff.Id,
                    SourceDocument = salesDocument,
                    Amount = cashToHandover,
                    Notes = $"{marker} Sales cash-in for {establishment.Name}.",
                    CreatedByUserId = staff.Id,
                    ConfirmedByUserId = managerOne.Id
                });

                if (day == 2)
                {
                    flow.Entries.Add(new CashFlowEntry
                    {
                        Direction = CashFlowDirection.Out,
                        Category = CashFlowCategory.Utilities,
                        EstablishmentId = ckrMain.Id,
                        CostCenterId = utilities.Id,
                        Amount = 2750m,
                        Notes = $"{marker} Utility cash-out.",
                        CreatedByUserId = owner.Id,
                        ConfirmedByUserId = managerOne.Id
                    });
                }

                flow.RecomputeTotals();
                db.DocumentRecords.Add(salesDocument);
                db.SalesReports.Add(salesReport);
                db.TreasuryCashFlows.Add(flow);
            }

            var receiptDocuments = Enumerable.Range(0, 7)
                .Select(index => new DocumentRecord
                {
                    DocumentType = DocumentType.ExpenseReceipt,
                    UploadedByUserId = index % 2 == 0 ? buyerOne.Id : buyerTwo.Id,
                    UploadedAt = startDate.AddDays(index).AddHours(12),
                    ImageUrl = $"/seed/week/receipt-{startDate.AddDays(index):yyyyMMdd}.png",
                    OcrRawJson = """{"seed":"weekly-demo","type":"expense-receipt"}""",
                    OcrStatus = OcrStatus.Parsed,
                    ReviewStatus = DocumentReviewStatus.Confirmed
                })
                .ToList();
            db.DocumentRecords.AddRange(receiptDocuments);

            var audits = new List<AuditItem>
            {
                CreateAudit(buyerOne.Id, ckrMain.Id, 850m, "Approved catering supplies", startDate, AuditStatus.Approved, marker, managerOne.Id, new[]
                {
                    CreateDetail("Paper plates", 5, 70m, ckrMain.Id, cater.Id, BranchVerificationStatus.Verified),
                    CreateDetail("Packed meal trays", 10, 50m, ckrMain.Id, cater.Id, BranchVerificationStatus.Verified)
                }),
                CreateAudit(buyerOne.Id, ckrMain.Id, 640m, "Pending split branch receipt", startDate.AddDays(1), AuditStatus.AwaitingBranchVerification, marker, null, new[]
                {
                    CreateDetail("CKR Main cleaning supplies", 2, 160m, ckrMain.Id, null, BranchVerificationStatus.Pending),
                    CreateDetail("Branch 2 paper bags", 4, 80m, ckrBranchTwo.Id, null, BranchVerificationStatus.Pending)
                }),
                CreateAudit(buyerTwo.Id, ckrBranchTwo.Id, 1120m, "Awaiting manager approval", startDate.AddDays(2), AuditStatus.AwaitingManagerApproval, marker, staffTwo.Id, new[]
                {
                    CreateDetail("Branch stock replenishment", 8, 140m, ckrBranchTwo.Id, null, BranchVerificationStatus.Verified)
                }),
                CreateAudit(buyerOne.Id, ckrMain.Id, 430m, "Rejected duplicate claim", startDate.AddDays(3), AuditStatus.Rejected, marker, staffOne.Id, new[]
                {
                    CreateDetail("Duplicate receipt line", 1, 430m, ckrMain.Id, null, BranchVerificationStatus.Rejected)
                }),
                CreateAudit(buyerTwo.Id, ckrBranchTwo.Id, 980m, "Approved utilities reimbursement", startDate.AddDays(4), AuditStatus.Approved, marker, managerTwo.Id, new[]
                {
                    CreateDetail("Water delivery", 2, 240m, ckrBranchTwo.Id, utilities.Id, BranchVerificationStatus.Verified),
                    CreateDetail("Light bulbs", 5, 100m, ckrBranchTwo.Id, utilities.Id, BranchVerificationStatus.Verified)
                }),
                CreateAudit(buyerOne.Id, ckrMain.Id, 350m, "Pending manager receipt check", startDate.AddDays(5), AuditStatus.AwaitingManagerApproval, marker, staffOne.Id, new[]
                {
                    CreateDetail("Emergency grocery run", 1, 350m, ckrMain.Id, null, BranchVerificationStatus.Verified)
                }),
                CreateAudit(buyerTwo.Id, ckrBranchTwo.Id, 720m, "Approved branch supplies", startDate.AddDays(6), AuditStatus.Approved, marker, managerTwo.Id, new[]
                {
                    CreateDetail("Receipt rolls", 6, 120m, ckrBranchTwo.Id, null, BranchVerificationStatus.Verified)
                }),
                CreateAudit(buyerOne.Id, ckrMain.Id, 560m, "Current pending delivery", DateTime.Today, AuditStatus.AwaitingBranchVerification, marker, null, new[]
                {
                    CreateDetail("Delivery for CKR Main", 2, 280m, ckrMain.Id, null, BranchVerificationStatus.Pending)
                })
            };

            for (var index = 0; index < 15; index++)
            {
                var buyer = index % 2 == 0 ? buyerOne : buyerTwo;
                var establishment = index % 2 == 0 ? ckrMain : ckrBranchTwo;
                var manager = index % 2 == 0 ? managerOne : managerTwo;
                var date = startDate.AddDays(index % 7).AddHours(index);
                var amount = 420m + (index * 35m);
                audits.Add(CreateAudit(buyer.Id, establishment.Id, amount, $"Approved audit sample {index + 1:00}", date, AuditStatus.Approved, marker, manager.Id, new[]
                {
                    CreateDetail($"Approved delivered item {index + 1:00}", 2 + (index % 3), amount / (2 + (index % 3)), establishment.Id, index % 3 == 0 ? cater.Id : null, BranchVerificationStatus.Verified)
                }));
            }

            for (var index = 0; index < 10; index++)
            {
                var buyer = index % 2 == 0 ? buyerOne : buyerTwo;
                var establishment = index % 2 == 0 ? ckrMain : ckrBranchTwo;
                var staff = index % 2 == 0 ? staffOne : staffTwo;
                var date = startDate.AddDays(index % 7).AddHours(9 + index);
                var amount = 510m + (index * 40m);
                audits.Add(CreateAudit(buyer.Id, establishment.Id, amount, $"Manager approval queue sample {index + 1:00}", date, AuditStatus.AwaitingManagerApproval, marker, staff.Id, new[]
                {
                    CreateDetail($"Branch verified item {index + 1:00}", 1 + (index % 4), amount / (1 + (index % 4)), establishment.Id, null, BranchVerificationStatus.Verified)
                }));
            }

            for (var index = 0; index < 10; index++)
            {
                var buyer = index % 2 == 0 ? buyerOne : buyerTwo;
                var primaryBranch = index % 2 == 0 ? ckrMain : ckrBranchTwo;
                var secondaryBranch = index % 2 == 0 ? ckrBranchTwo : ckrMain;
                var date = startDate.AddDays(index % 7).AddHours(13 + index);
                var amount = 360m + (index * 45m);
                audits.Add(CreateAudit(buyer.Id, primaryBranch.Id, amount, $"Delivery verification queue sample {index + 1:00}", date, AuditStatus.AwaitingBranchVerification, marker, null, new[]
                {
                    CreateDetail($"Pending primary branch item {index + 1:00}", 2, amount / 2m, primaryBranch.Id, null, BranchVerificationStatus.Pending),
                    CreateDetail($"Pending split branch item {index + 1:00}", 1, 125m + index, secondaryBranch.Id, null, BranchVerificationStatus.Pending)
                }));
            }

            audits.Add(CreateAudit(buyerTwo.Id, ckrBranchTwo.Id, 455m, "Rejected audit sample 02", DateTime.Today.AddHours(8), AuditStatus.Rejected, marker, staffTwo.Id, new[]
            {
                CreateDetail("Rejected damaged delivery", 1, 455m, ckrBranchTwo.Id, null, BranchVerificationStatus.Rejected)
            }));
            db.AuditItems.AddRange(audits);

            var pcfReleaseOne = new PcfRelease
            {
                ReleasedByTreasuryUserId = owner.Id,
                ReceiverUserId = buyerOne.Id,
                ReceiverName = buyerOne.Name,
                EstablishmentId = ckrMain.Id,
                Amount = 10000m,
                ReleaseDate = startDate,
                Purpose = $"{marker} Weekly operating PCF for Buyer One.",
                Status = PcfReleaseStatus.PartiallyAudited
            };
            var pcfReleaseTwo = new PcfRelease
            {
                ReleasedByTreasuryUserId = owner.Id,
                ReceiverUserId = buyerTwo.Id,
                ReceiverName = buyerTwo.Name,
                EstablishmentId = ckrBranchTwo.Id,
                Amount = 8000m,
                ReleaseDate = startDate.AddDays(2),
                Purpose = $"{marker} Weekly operating PCF for Buyer Two.",
                Status = PcfReleaseStatus.Released
            };
            db.PcfReleases.AddRange(pcfReleaseOne, pcfReleaseTwo);

            db.SurrenderRequests.AddRange(
                new SurrenderRequest
                {
                    BuyerId = buyerOne.Id,
                    DeclaredAmount = 1200m,
                    ConfirmedAmount = 1200m,
                    Status = SurrenderStatus.Confirmed,
                    RequestDate = startDate.AddDays(5),
                    ActionDate = startDate.AddDays(5).AddHours(2),
                    ActionByUserId = managerOne.Id,
                    BuyerNotes = $"{marker} Buyer One confirmed weekly cash surrender.",
                    ActionNotes = "Count matched declared cash."
                },
                new SurrenderRequest
                {
                    BuyerId = buyerTwo.Id,
                    DeclaredAmount = 900m,
                    Status = SurrenderStatus.Pending,
                    RequestDate = DateTime.Today,
                    BuyerNotes = $"{marker} Buyer Two pending cash surrender."
                });

            var settlement = new AuditSettlement
            {
                PcfRelease = pcfReleaseOne,
                ReceiverUserId = buyerOne.Id,
                ReceiverName = $"{marker} {buyerOne.Name}",
                ResponsibleManagerId = managerOne.Id,
                ProcessedByUserId = admin.Id,
                TotalPCReleased = 10000m,
                TotalAcceptedExpenses = 2180m,
                ActualChangeReturned = 1200m,
                Status = AuditSettlementStatus.Draft
            };
            settlement.Recompute();
            db.AuditSettlements.Add(settlement);

            db.PettyCashLedgers.AddRange(
                new PettyCashLedger
                {
                    UserId = buyerOne.Id,
                    TransactionType = LedgerTransactionType.ManagerFunding,
                    Amount = 10000m,
                    ResultingBalance = buyerOne.PcfBalance + 10000m,
                    Timestamp = startDate,
                    Notes = $"{marker} Seed PCF funding."
                },
                new PettyCashLedger
                {
                    UserId = buyerOne.Id,
                    TransactionType = LedgerTransactionType.ExpenseDeduction,
                    Amount = -850m,
                    ResultingBalance = buyerOne.PcfBalance + 9150m,
                    Timestamp = startDate.AddHours(12),
                    Notes = $"{marker} Seed approved audit expense."
                });

            db.SaveChanges();
        }

        private static void RemoveWeeklyDemoData(AuditDbContext db, string marker)
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
                .Where(d => d.ImageUrl.Contains("/seed/week/"))
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
                SubmittedAt = entryDate.AddHours(10),
                Status = status,
                Notes = $"{marker} {description}.",
                ReceiptImageUrl = $"/seed/week/audit-{entryDate:yyyyMMdd}-{Math.Abs(description.GetHashCode()):X}.png",
                VerifiedById = verifiedById,
                VerificationDate = verifiedById.HasValue ? entryDate.AddHours(16) : null,
                Details = details.ToList()
            };
        }

        private static AuditItemDetail CreateDetail(string itemName, int quantity, decimal price, int? assignedEstablishmentId, int? costCenterId, BranchVerificationStatus branchVerificationStatus)
        {
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
                AllocationNotes = "[seed:weekly-demo]"
            };
        }
    }
}
