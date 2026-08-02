using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Models;

namespace AuditCkDayo.Data
{
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Establishment> Establishments { get; set; }
        public DbSet<AuditItem> AuditItems { get; set; }
        public DbSet<AuditItemDetail> AuditItemDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<User>()
                .Property(u => u.PcfBalance)
                .HasDefaultValue(0.00m);

            modelBuilder.Entity<User>()
                .Property(u => u.DailyStartingFloat)
                .HasDefaultValue(0.00m);

            // Self-referential User relationship for Manager -> Staff
            modelBuilder.Entity<User>()
                .HasOne(u => u.Manager)
                .WithMany(u => u.StaffMembers)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Establishment configuration
            modelBuilder.Entity<Establishment>()
                .HasIndex(e => e.Name)
                .IsUnique();

            // AuditItem configuration
            modelBuilder.Entity<AuditItem>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(AuditStatus.Pending);

            modelBuilder.Entity<AuditItem>()
                .Property(a => a.EntryDate)
                .HasColumnType("date");

            // AuditItem -> Buyer relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.Buyer)
                .WithMany(u => u.AuditItems)
                .HasForeignKey(a => a.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItem -> Establishment relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.Establishment)
                .WithMany(e => e.AuditItems)
                .HasForeignKey(a => a.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItem -> VerifiedBy relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.VerifiedBy)
                .WithMany()
                .HasForeignKey(a => a.VerifiedById)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItemDetail configuration
            modelBuilder.Entity<AuditItemDetail>()
                .Property(ad => ad.Quantity)
                .HasDefaultValue(1);

            // AuditItemDetail -> AuditItem relationship (Cascade Delete)
            modelBuilder.Entity<AuditItemDetail>()
                .HasOne(ad => ad.AuditItem)
                .WithMany(a => a.Details)
                .HasForeignKey(ad => ad.AuditItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
