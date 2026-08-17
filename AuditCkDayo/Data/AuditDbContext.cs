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
        public DbSet<DocumentRecord> DocumentRecords { get; set; }
        public DbSet<SalesReport> SalesReports { get; set; }
        public DbSet<SalesReportLine> SalesReportLines { get; set; }
        public DbSet<CashBreakdownLine> CashBreakdownLines { get; set; }
        public DbSet<TreasuryCashFlow> TreasuryCashFlows { get; set; }
        public DbSet<CashFlowEntry> CashFlowEntries { get; set; }
        public DbSet<PcfRelease> PcfReleases { get; set; }
        public DbSet<AuditSettlement> AuditSettlements { get; set; }
        public DbSet<ManagerCoverage> ManagerCoverages { get; set; }
        public DbSet<PnlCategory> PnlCategories { get; set; }

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

            modelBuilder.Entity<Establishment>()
                .Property(e => e.PcfBalance)
                .HasDefaultValue(0.00m);

            modelBuilder.Entity<Establishment>()
                .Property(e => e.DailyStartingFloat)
                .HasDefaultValue(0.00m);

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

            modelBuilder.Entity<PnlCategory>()
                .Property(category => category.Section)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<PnlCategory>()
                .Property(category => category.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<PnlCategory>()
                .Property(category => category.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<PnlCategory>()
                .HasIndex(category => new { category.Section, category.Name })
                .IsUnique();

            modelBuilder.Entity<AuditItemDetail>()
                .Property(ad => ad.ReceiptStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditItemDetail>()
                .Property(ad => ad.BranchVerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasDefaultValue(BranchVerificationStatus.Pending);

            modelBuilder.Entity<AuditItemDetail>()
                .Property(ad => ad.PnlSection)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditItemDetail>()
                .HasOne(ad => ad.PnlCategory)
                .WithMany()
                .HasForeignKey(ad => ad.PnlCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

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

            // AuditItem -> AssignedReviewer relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.AssignedReviewer)
                .WithMany()
                .HasForeignKey(a => a.AssignedReviewerId)
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

            modelBuilder.Entity<SurrenderRequest>()
                .HasOne(s => s.AssignedReceiver)
                .WithMany()
                .HasForeignKey(s => s.AssignedReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // DocumentRecord configuration
            modelBuilder.Entity<DocumentRecord>()
                .Property(d => d.DocumentType)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<DocumentRecord>()
                .Property(d => d.OcrStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<DocumentRecord>()
                .Property(d => d.ReviewStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<DocumentRecord>()
                .HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentRecord>()
                .HasOne(d => d.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(d => d.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesReport>()
                .Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<SalesReport>()
                .HasOne(s => s.DocumentRecord)
                .WithMany()
                .HasForeignKey(s => s.DocumentRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesReport>()
                .HasOne(s => s.Establishment)
                .WithMany()
                .HasForeignKey(s => s.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesReport>()
                .HasMany(s => s.CashBreakdownLines)
                .WithOne(c => c.SalesReport)
                .HasForeignKey(c => c.SalesReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesReportLine>()
                .Property(l => l.LineType)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<SalesReportLine>()
                .HasOne(l => l.SalesReport)
                .WithMany(r => r.Lines)
                .HasForeignKey(l => l.SalesReportId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<CashBreakdownLine>()
                .Property(c => c.OwnerType)
                .HasConversion<string>()
                .HasMaxLength(50);

            // TreasuryCashFlow configuration
            modelBuilder.Entity<TreasuryCashFlow>()
                .Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<TreasuryCashFlow>()
                .HasOne(t => t.TreasuryUser)
                .WithMany()
                .HasForeignKey(t => t.TreasuryUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TreasuryCashFlow>()
                .HasMany(t => t.Entries)
                .WithOne(e => e.TreasuryCashFlow)
                .HasForeignKey(e => e.TreasuryCashFlowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CashFlowEntry>()
                .Property(e => e.Direction)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<CashFlowEntry>()
                .Property(e => e.Category)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<CashFlowEntry>()
                .HasIndex(e => new { e.SourceDocumentId, e.Category })
                .IsUnique();

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.Establishment)
                .WithMany()
                .HasForeignKey(e => e.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.CostCenter)
                .WithMany()
                .HasForeignKey(e => e.CostCenterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.RelatedUser)
                .WithMany()
                .HasForeignKey(e => e.RelatedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.SourceDocument)
                .WithMany()
                .HasForeignKey(e => e.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(e => e.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            // PcfRelease configuration
            modelBuilder.Entity<PcfRelease>()
                .Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<PcfRelease>()
                .HasOne(p => p.ReleasedByTreasuryUser)
                .WithMany()
                .HasForeignKey(p => p.ReleasedByTreasuryUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PcfRelease>()
                .HasOne(p => p.ReceiverUser)
                .WithMany()
                .HasForeignKey(p => p.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PcfRelease>()
                .HasOne(p => p.Establishment)
                .WithMany()
                .HasForeignKey(p => p.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PcfRelease>()
                .HasOne(p => p.CashFlowEntry)
                .WithMany()
                .HasForeignKey(p => p.CashFlowEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditSettlement configuration
            modelBuilder.Entity<AuditSettlement>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditSettlement>()
                .HasOne(a => a.PcfRelease)
                .WithMany()
                .HasForeignKey(a => a.PcfReleaseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditSettlement>()
                .HasOne(a => a.ReceiverUser)
                .WithMany()
                .HasForeignKey(a => a.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditSettlement>()
                .HasOne(a => a.ResponsibleManager)
                .WithMany()
                .HasForeignKey(a => a.ResponsibleManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditSettlement>()
                .HasOne(a => a.ProcessedByUser)
                .WithMany()
                .HasForeignKey(a => a.ProcessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ManagerCoverage configuration
            modelBuilder.Entity<ManagerCoverage>()
                .Property(c => c.Scope)
                .HasConversion<string>()
                .HasMaxLength(255);

            modelBuilder.Entity<ManagerCoverage>()
                .HasOne(c => c.CoveredManager)
                .WithMany()
                .HasForeignKey(c => c.CoveredManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ManagerCoverage>()
                .HasOne(c => c.CoveringManager)
                .WithMany()
                .HasForeignKey(c => c.CoveringManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ManagerCoverage>()
                .HasOne(c => c.CreatedByUser)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
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
