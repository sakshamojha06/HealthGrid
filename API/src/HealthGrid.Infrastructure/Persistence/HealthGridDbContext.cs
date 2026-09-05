using HealthGrid.Domain.Entities;
using HealthGrid.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Infrastructure.Persistence;

public sealed class HealthGridDbContext(DbContextOptions<HealthGridDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<District> Districts => Set<District>();
    public DbSet<Phc> Phcs => Set<Phc>();
    public DbSet<UserPhcMembership> UserPhcMemberships => Set<UserPhcMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Disease> Diseases => Set<Disease>();
    public DbSet<Specialization> Specializations => Set<Specialization>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientVisit> PatientVisits => Set<PatientVisit>();
    public DbSet<VisitDiagnosis> VisitDiagnoses => Set<VisitDiagnosis>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<MedicineInventory> MedicineInventories => Set<MedicineInventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<MedicineRequest> MedicineRequests => Set<MedicineRequest>();
    public DbSet<MedicineRequestItem> MedicineRequestItems => Set<MedicineRequestItem>();
    public DbSet<MedicineTransfer> MedicineTransfers => Set<MedicineTransfer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PredictionSnapshot> PredictionSnapshots => Set<PredictionSnapshot>();
    public DbSet<PredictionValue> PredictionValues => Set<PredictionValue>();
    public DbSet<AiAlert> AiAlerts => Set<AiAlert>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<District>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Phc>().HasIndex(x => new { x.DistrictId, x.Code }).IsUnique();
        modelBuilder.Entity<Phc>().HasOne(x => x.District).WithMany(x => x.Phcs).HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserPhcMembership>().HasIndex(x => new { x.UserId, x.PhcId }).IsUnique();
        modelBuilder.Entity<UserPhcMembership>().HasIndex(x => new { x.DistrictId, x.PhcId });
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        modelBuilder.Entity<Medicine>().HasIndex(x => new { x.Name, x.GenericName }).IsUnique();
        modelBuilder.Entity<Disease>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Patient>().HasIndex(x => new { x.DistrictId, x.PhcId, x.LocalIdentifier }).IsUnique();
        modelBuilder.Entity<MedicineInventory>().HasIndex(x => new { x.DistrictId, x.PhcId, x.MedicineId }).IsUnique();
        modelBuilder.Entity<MedicineInventory>().Property(x => x.RowVersion).IsRowVersion();
        modelBuilder.Entity<MedicineInventory>().Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        modelBuilder.Entity<MedicineInventory>().Property(x => x.SafetyStock).HasPrecision(18, 3);
        modelBuilder.Entity<InventoryTransaction>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<InventoryTransaction>().Property(x => x.BalanceAfter).HasPrecision(18, 3);
        modelBuilder.Entity<MedicineRequestItem>().Property(x => x.QuantityRequested).HasPrecision(18, 3);
        modelBuilder.Entity<MedicineRequestItem>().Property(x => x.QuantityFulfilled).HasPrecision(18, 3);
        modelBuilder.Entity<PrescriptionItem>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<PredictionValue>().Property(x => x.PointForecast).HasPrecision(18, 3);
        modelBuilder.Entity<PredictionValue>().Property(x => x.LowerBound).HasPrecision(18, 3);
        modelBuilder.Entity<PredictionValue>().Property(x => x.UpperBound).HasPrecision(18, 3);
        modelBuilder.Entity<InventoryTransaction>().HasIndex(x => new { x.DistrictId, x.PhcId, x.CreatedAtUtc });
        modelBuilder.Entity<InventoryTransaction>().HasIndex(x => new { x.PhcId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        modelBuilder.Entity<MedicineRequest>().HasIndex(x => new { x.DistrictId, x.DestinationPhcId, x.Status });
        modelBuilder.Entity<AiAlert>().HasIndex(x => new { x.DistrictId, x.PhcId, x.Severity, x.IsAcknowledged });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(x => typeof(ScopedEntity).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ScopedEntity.DistrictId), nameof(ScopedEntity.PhcId), nameof(ScopedEntity.CreatedAtUtc));
        }

        modelBuilder.Entity<MedicineInventory>().ToTable(t => t.HasCheckConstraint("CK_MedicineInventory_QuantityOnHand_NonNegative", "[QuantityOnHand] >= 0"));
        modelBuilder.Entity<MedicineInventory>().ToTable(t => t.HasCheckConstraint("CK_MedicineInventory_SafetyStock_NonNegative", "[SafetyStock] >= 0"));
        modelBuilder.Entity<InventoryTransaction>().ToTable(t => t.HasCheckConstraint("CK_InventoryTransaction_Quantity_Positive", "[Quantity] > 0"));
    }
}
