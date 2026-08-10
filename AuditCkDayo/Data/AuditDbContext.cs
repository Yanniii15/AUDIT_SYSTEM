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
        public DbSet<AuditItemImage> AuditItemImages { get; set; }
        public DbSet<PettyCashLedger> PettyCashLedgers { get; set; }
        public DbSet<SurrenderRequest> SurrenderRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }

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

            modelBuilder.Entity<User>()
                .Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            // Self-referential User relationship for Manager -> Staff
            modelBuilder.Entity<User>()
                .HasOne(u => u.Manager)
                .WithMany(u => u.StaffMembers)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Establishment relationship
            modelBuilder.Entity<User>()
                .HasOne(u => u.Establishment)
                .WithMany()
                .HasForeignKey(u => u.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Establishment configuration
            modelBuilder.Entity<Establishment>()
                .HasIndex(e => e.Name)
                .IsUnique();

            modelBuilder.Entity<Establishment>()
                .Property(e => e.IsOperatingBranch)
                .HasDefaultValue(true);

            modelBuilder.Entity<Establishment>()
                .Property(e => e.IsMiscellaneous)
                .HasDefaultValue(false);

            modelBuilder.Entity<Establishment>()
                .Property(e => e.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<CostCenter>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<CostCenter>()
                .Property(c => c.Category)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<CostCenter>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<AuditItemDetail>()
                .Property(ad => ad.ReceiptStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditItemDetail>()
                .HasOne(ad => ad.AssignedEstablishment)
                .WithMany()
                .HasForeignKey(ad => ad.AssignedEstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditItemDetail>()
                .HasOne(ad => ad.CostCenter)
                .WithMany()
                .HasForeignKey(ad => ad.CostCenterId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItem configuration
            modelBuilder.Entity<AuditItem>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasDefaultValue(AuditStatus.AwaitingBranchVerification);

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

            // AuditItemImage -> AuditItem relationship (Cascade Delete)
            modelBuilder.Entity<AuditItemImage>()
                .HasOne(ai => ai.AuditItem)
                .WithMany(a => a.Images)
                .HasForeignKey(ai => ai.AuditItemId)
                .OnDelete(DeleteBehavior.Cascade);
            // PettyCashLedger configuration
            modelBuilder.Entity<PettyCashLedger>()
                .Property(l => l.TransactionType)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<PettyCashLedger>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PettyCashLedger>()
                .HasOne(l => l.CounterpartyUser)
                .WithMany()
                .HasForeignKey(l => l.CounterpartyUserId)
                .OnDelete(DeleteBehavior.Restrict);


            // SurrenderRequest configuration
            modelBuilder.Entity<SurrenderRequest>()
                .Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(50);


            modelBuilder.Entity<SurrenderRequest>()
                .HasOne(s => s.Buyer)
                .WithMany()
                .HasForeignKey(s => s.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SurrenderRequest>()
                .HasOne(s => s.ActionByUser)
                .WithMany()
                .HasForeignKey(s => s.ActionByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification configuration
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
