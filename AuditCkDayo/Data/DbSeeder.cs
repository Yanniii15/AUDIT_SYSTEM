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

            // Seed Users if empty
            if (!db.Users.Any())
            {
                var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");

                // 1. Seed Owner
                var owner = new User
                {
                    Name = "Alice Owner",
                    Email = "alice@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Owner,
                    PcfBalance = 1000m,
                    DailyStartingFloat = 1000m
                };
                db.Users.Add(owner);

                // 2. Seed Manager
                var manager = new User
                {
                    Name = "Bob Manager",
                    Email = "bob@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Manager,
                    PcfBalance = 500m,
                    DailyStartingFloat = 500m
                };
                db.Users.Add(manager);
                db.SaveChanges(); // Save to generate IDs

                // 3. Seed Buyers
                var charlie = new User
                {
                    Name = "Charlie Buyer",
                    Email = "charlie@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Buyer,
                    PcfBalance = 200m,
                    DailyStartingFloat = 200m,
                    ManagerId = manager.Id
                };
                var david = new User
                {
                    Name = "David Buyer",
                    Email = "david@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Buyer,
                    PcfBalance = 100m,
                    DailyStartingFloat = 100m,
                    ManagerId = null
                };
                db.Users.AddRange(charlie, david);

                // 4. Seed Branch Staff
                var ckrMain = db.Establishments.FirstOrDefault(e => e.Name == "CKR Main");
                if (ckrMain != null)
                {
                    var staff = new User
                    {
                        Name = "Branch Staff",
                        Email = "staff@test.com",
                        PasswordHash = defaultPasswordHash,
                        Role = UserRole.BranchStaff,
                        PcfBalance = 0m,
                        DailyStartingFloat = 0m,
                        EstablishmentId = ckrMain.Id
                    };
                    db.Users.Add(staff);
                }

                db.SaveChanges();
            }
        }
    }
}
