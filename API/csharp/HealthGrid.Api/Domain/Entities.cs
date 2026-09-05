namespace HealthGrid.Api.Domain;

/// <summary>Base type: GUID key + UTC audit timestamps on every row.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// A row that belongs to exactly one PHC inside one district. District isolation
/// is enforced by always filtering on <see cref="DistrictId"/> (and usually
/// <see cref="PhcId"/>) before results are materialised.
/// </summary>
public abstract class ScopedEntity : Entity
{
    public Guid DistrictId { get; set; }
    public Guid PhcId { get; set; }
}

// ---------------------------------------------------------------------------
// Organisation / reference data (global, System Administrator owned)
// ---------------------------------------------------------------------------
public sealed class District : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public ICollection<Phc> Phcs { get; set; } = [];
}

public sealed class Phc : Entity
{
    public Guid DistrictId { get; set; }
    public District? District { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
}

public sealed class Medicine : Entity
{
    public required string Name { get; set; }
    public required string GenericName { get; set; }
    public required string Unit { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Disease : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
}

public sealed class Specialization : Entity
{
    public required string Name { get; set; }
}

// ---------------------------------------------------------------------------
// Auth support
// ---------------------------------------------------------------------------
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

// ---------------------------------------------------------------------------
// Doctors / specialists
// ---------------------------------------------------------------------------
public sealed class Doctor : ScopedEntity
{
    public required string Name { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? SpecializationId { get; set; }
    public Specialization? Specialization { get; set; }
    public ICollection<DoctorAvailability> Availability { get; set; } = [];
}

public sealed class DoctorAvailability : ScopedEntity
{
    public Guid DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int DayOfWeek { get; set; } // 0 = Sunday .. 6 = Saturday
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

// ---------------------------------------------------------------------------
// Patients / visits — data minimisation: PHC-local pseudonymous identifier only
// ---------------------------------------------------------------------------
public sealed class Patient : ScopedEntity
{
    public required string LocalIdentifier { get; set; }
    public int? BirthYear { get; set; }
    public string? Gender { get; set; } // "M" / "F" / "O"
    public ICollection<PatientVisit> Visits { get; set; } = [];
}

public sealed class PatientVisit : ScopedEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public Guid? AttendingDoctorId { get; set; }
    public Doctor? AttendingDoctor { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? Symptoms { get; set; }
    public ICollection<VisitDiagnosis> Diagnoses { get; set; } = [];
    public ICollection<VisitPrescriptionItem> PrescriptionItems { get; set; } = [];
}

public sealed class VisitDiagnosis : ScopedEntity
{
    public Guid PatientVisitId { get; set; }
    public PatientVisit? PatientVisit { get; set; }
    public Guid DiseaseId { get; set; }
    public Disease? Disease { get; set; }
}

/// <summary>A prescribed line item. Recording a visit issues this quantity from stock.</summary>
public sealed class VisitPrescriptionItem : ScopedEntity
{
    public Guid PatientVisitId { get; set; }
    public PatientVisit? PatientVisit { get; set; }
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public decimal Quantity { get; set; }
}

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------
public sealed class MedicineInventory : ScopedEntity
{
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal SafetyStock { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Optimistic-concurrency token — guards concurrent stock mutations.</summary>
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InventoryTransaction : ScopedEntity
{
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? IdempotencyKey { get; set; }
}

// ---------------------------------------------------------------------------
// PHC-to-PHC medicine requests / transfers (same district only)
// ---------------------------------------------------------------------------
public sealed class MedicineRequest : Entity
{
    /// <summary>Both PHCs live in this district. Cross-district exchange is disabled.</summary>
    public Guid DistrictId { get; set; }

    /// <summary>The PHC that needs the medicine and raised the request.</summary>
    public Guid SourcePhcId { get; set; }
    public Phc? SourcePhc { get; set; }

    /// <summary>The PHC being asked to supply the medicine from its stock.</summary>
    public Guid DestinationPhcId { get; set; }
    public Phc? DestinationPhc { get; set; }

    public MedicineRequestStatus Status { get; set; } = MedicineRequestStatus.Pending;
    public string? Notes { get; set; }
    public DateOnly? NeededByDate { get; set; }
    public Guid RequestedByUserId { get; set; }
    public required string RequestedByName { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ICollection<MedicineRequestItem> Items { get; set; } = [];
}

public sealed class MedicineRequestItem : Entity
{
    public Guid MedicineRequestId { get; set; }
    public MedicineRequest? MedicineRequest { get; set; }
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityFulfilled { get; set; }
}

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------
public sealed class Notification : Entity
{
    public Guid? DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public Guid? UserId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    public bool IsAcknowledged { get; set; }
}

// ---------------------------------------------------------------------------
// AI / prediction persistence
// ---------------------------------------------------------------------------
public sealed class PredictionSnapshot : Entity
{
    public Guid DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public required string ModelVersion { get; set; }
    public string Status { get; set; } = "Ready"; // Ready | InsufficientData | Stale
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public int HorizonDays { get; set; }
    public decimal? PatientVolumePctChange { get; set; }
    public ICollection<PredictionValue> Values { get; set; } = [];
    public ICollection<StockoutRiskRow> StockoutRisks { get; set; } = [];
}

public sealed class PredictionValue : Entity
{
    public Guid PredictionSnapshotId { get; set; }
    public string Kind { get; set; } = "PatientVolume";
    public DateOnly TargetDate { get; set; }
    public decimal PointForecast { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}

public sealed class StockoutRiskRow : Entity
{
    public Guid PredictionSnapshotId { get; set; }
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public Guid PhcId { get; set; }
    public int? DaysToStockout { get; set; }
    public DateOnly? StockoutDate { get; set; }
    public AlertSeverity Risk { get; set; } = AlertSeverity.Warning;
    public decimal RecommendedReorderQty { get; set; }
}

public sealed class AiAlert : Entity
{
    public Guid DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public required string AlertType { get; set; } // Stockout | DiseaseAnomaly | PatientVolume
    public required string Message { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    public bool IsAcknowledged { get; set; }
}

// ---------------------------------------------------------------------------
// Audit
// ---------------------------------------------------------------------------
public sealed class AuditLog : Entity
{
    public Guid? DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
}
