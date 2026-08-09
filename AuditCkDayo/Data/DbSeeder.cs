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

            // Ensure default System Admin exists
            EnsureUser("admin@test.com", () => new User
            {
                Name = "System Admin",
                Email = "admin@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Admin,
                PcfBalance = 0m,
                DailyStartingFloat = 0m
            });

            // Ensure default Owner exists
            EnsureUser("alice@test.com", () => new User
            {
                Name = "Alice Owner",
                Email = "alice@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Owner,
                PcfBalance = 1000m,
                DailyStartingFloat = 1000m
            });

            // Ensure default Manager exists
            var manager = EnsureUser("bob@test.com", () => new User
            {
                Name = "Bob Manager",
                Email = "bob@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Manager,
                PcfBalance = 500m,
                DailyStartingFloat = 500m
            });

            // Ensure default Buyers exist
            EnsureUser("charlie@test.com", () => new User
            {
                Name = "Charlie Buyer",
                Email = "charlie@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Buyer,
                PcfBalance = 200m,
                DailyStartingFloat = 200m,
                ManagerId = manager.Id
            });

            EnsureUser("david@test.com", () => new User
            {
                Name = "David Buyer",
                Email = "david@test.com",
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Buyer,
                PcfBalance = 100m,
                DailyStartingFloat = 100m
            });

            // Ensure default Branch Staff exists
            var ckrMain = db.Establishments.FirstOrDefault(e => e.Name == "CKR Main");
            if (ckrMain != null)
            {
                EnsureUser("staff@test.com", () => new User
                {
                    Name = "Branch Staff",
                    Email = "staff@test.com",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.BranchStaff,
                    PcfBalance = 0m,
                    DailyStartingFloat = 0m,
                    EstablishmentId = ckrMain.Id
                });
            }
        }
    }
}
