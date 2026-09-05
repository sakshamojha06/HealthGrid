using HealthGrid.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Data;

public sealed class HealthGridDbContext(DbContextOptions<HealthGridDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<District> Districts => Set<District>();
    public DbSet<Phc> Phcs => Set<Phc>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Disease> Diseases => Set<Disease>();
    public DbSet<Specialization> Specializations => Set<Specialization>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientVisit> PatientVisits => Set<PatientVisit>();
    public DbSet<VisitDiagnosis> VisitDiagnoses => Set<VisitDiagnosis>();
    public DbSet<VisitPrescriptionItem> VisitPrescriptionItems => Set<VisitPrescriptionItem>();
    public DbSet<MedicineInventory> MedicineInventories => Set<MedicineInventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<MedicineRequest> MedicineRequests => Set<MedicineRequest>();
    public DbSet<MedicineRequestItem> MedicineRequestItems => Set<MedicineRequestItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PredictionSnapshot> PredictionSnapshots => Set<PredictionSnapshot>();
    public DbSet<PredictionValue> PredictionValues => Set<PredictionValue>();
    public DbSet<StockoutRiskRow> StockoutRiskRows => Set<StockoutRiskRow>();
    public DbSet<AiAlert> AiAlerts => Set<AiAlert>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>().Property(x => x.FullName).HasMaxLength(200);

        b.Entity<District>().HasIndex(x => x.Code).IsUnique();
        b.Entity<District>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<District>().Property(x => x.Code).HasMaxLength(20);

        b.Entity<Phc>().HasIndex(x => new { x.DistrictId, x.Code }).IsUnique();
        b.Entity<Phc>().HasOne(x => x.District).WithMany(x => x.Phcs)
            .HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Medicine>().HasIndex(x => new { x.Name, x.GenericName }).IsUnique();
        b.Entity<Disease>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Specialization>().HasIndex(x => x.Name).IsUnique();

        b.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

        b.Entity<Doctor>().HasMany(x => x.Availability).WithOne(x => x.Doctor)
            .HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Patient>().HasIndex(x => new { x.DistrictId, x.PhcId, x.LocalIdentifier }).IsUnique();
        b.Entity<PatientVisit>().HasIndex(x => new { x.DistrictId, x.PhcId, x.VisitDate });
        b.Entity<PatientVisit>().HasMany(x => x.Diagnoses).WithOne(x => x.PatientVisit)
            .HasForeignKey(x => x.PatientVisitId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PatientVisit>().HasMany(x => x.PrescriptionItems).WithOne(x => x.PatientVisit)
            .HasForeignKey(x => x.PatientVisitId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<VisitDiagnosis>().HasIndex(x => new { x.DistrictId, x.DiseaseId, x.CreatedAtUtc });

        b.Entity<MedicineInventory>().HasIndex(x => new { x.DistrictId, x.PhcId, x.MedicineId }).IsUnique();
        b.Entity<MedicineInventory>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<MedicineInventory>().Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        b.Entity<MedicineInventory>().Property(x => x.SafetyStock).HasPrecision(18, 3);
        b.Entity<MedicineInventory>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_MedicineInventory_OnHand_NonNegative", "[QuantityOnHand] >= 0");
            t.HasCheckConstraint("CK_MedicineInventory_Safety_NonNegative", "[SafetyStock] >= 0");
        });
        // InventoryTransaction.Quantity is a signed delta (negative for a reducing
        // adjustment / issue), so no positivity constraint here.

        b.Entity<InventoryTransaction>().Property(x => x.Quantity).HasPrecision(18, 3);
        b.Entity<InventoryTransaction>().Property(x => x.BalanceAfter).HasPrecision(18, 3);
        b.Entity<InventoryTransaction>().HasIndex(x => new { x.DistrictId, x.PhcId, x.CreatedAtUtc });
        b.Entity<InventoryTransaction>().HasIndex(x => new { x.PhcId, x.IdempotencyKey })
            .IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

        b.Entity<VisitPrescriptionItem>().Property(x => x.Quantity).HasPrecision(18, 3);

        b.Entity<MedicineRequest>().HasIndex(x => new { x.DistrictId, x.DestinationPhcId, x.Status });
        b.Entity<MedicineRequest>().HasIndex(x => new { x.DistrictId, x.SourcePhcId, x.Status });
        b.Entity<MedicineRequest>().HasOne(x => x.SourcePhc).WithMany()
            .HasForeignKey(x => x.SourcePhcId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MedicineRequest>().HasOne(x => x.DestinationPhc).WithMany()
            .HasForeignKey(x => x.DestinationPhcId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MedicineRequest>().HasMany(x => x.Items).WithOne(x => x.MedicineRequest)
            .HasForeignKey(x => x.MedicineRequestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MedicineRequestItem>().Property(x => x.QuantityRequested).HasPrecision(18, 3);
        b.Entity<MedicineRequestItem>().Property(x => x.QuantityFulfilled).HasPrecision(18, 3);

        b.Entity<Notification>().HasIndex(x => new { x.PhcId, x.IsAcknowledged, x.CreatedAtUtc });
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsAcknowledged });

        b.Entity<PredictionSnapshot>().Property(x => x.PatientVolumePctChange).HasPrecision(9, 2);
        b.Entity<PredictionSnapshot>().HasIndex(x => new { x.DistrictId, x.PhcId, x.GeneratedAtUtc });
        b.Entity<PredictionSnapshot>().HasMany(x => x.Values).WithOne()
            .HasForeignKey(x => x.PredictionSnapshotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PredictionSnapshot>().HasMany(x => x.StockoutRisks).WithOne()
            .HasForeignKey(x => x.PredictionSnapshotId).OnDelete(DeleteBehavior.Cascade);
        foreach (var prop in new[] { "PointForecast", "LowerBound", "UpperBound" })
            b.Entity<PredictionValue>().Property<decimal>(prop).HasPrecision(18, 3);
        b.Entity<StockoutRiskRow>().Property(x => x.RecommendedReorderQty).HasPrecision(18, 3);

        b.Entity<AiAlert>().HasIndex(x => new { x.DistrictId, x.PhcId, x.Severity, x.IsAcknowledged });
        b.Entity<AuditLog>().HasIndex(x => new { x.EntityName, x.EntityId });

        // Store enums as readable strings.
        b.Entity<InventoryTransaction>().Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Entity<MedicineRequest>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Notification>().Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        b.Entity<AiAlert>().Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        b.Entity<StockoutRiskRow>().Property(x => x.Risk).HasConversion<string>().HasMaxLength(16);
    }
}
