namespace HealthGrid.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public abstract class ScopedEntity : Entity
{
    public Guid DistrictId { get; set; }
    public Guid PhcId { get; set; }
}

public sealed class District : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public ICollection<Phc> Phcs { get; set; } = [];
}

public sealed class Phc : Entity
{
    public Guid DistrictId { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public District? District { get; set; }
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

public sealed class Doctor : ScopedEntity
{
    public required string Name { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? SpecializationId { get; set; }
}

public sealed class DoctorAvailability : ScopedEntity
{
    public Guid DoctorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class Patient : ScopedEntity
{
    public required string LocalIdentifier { get; set; }
    public int? BirthYear { get; set; }
    public string? Gender { get; set; }
}

public sealed class PatientVisit : ScopedEntity
{
    public Guid PatientId { get; set; }
    public Guid? AttendingDoctorId { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? Symptoms { get; set; }
}

public sealed class VisitDiagnosis : ScopedEntity
{
    public Guid PatientVisitId { get; set; }
    public Guid DiseaseId { get; set; }
}

public sealed class Prescription : ScopedEntity
{
    public Guid PatientVisitId { get; set; }
}

public sealed class PrescriptionItem : ScopedEntity
{
    public Guid PrescriptionId { get; set; }
    public Guid MedicineId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class MedicineInventory : ScopedEntity
{
    public Guid MedicineId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal SafetyStock { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InventoryTransaction : ScopedEntity
{
    public Guid MedicineId { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Reference { get; set; }
}

public sealed class MedicineRequest : ScopedEntity
{
    public Guid DestinationPhcId { get; set; }
    public MedicineRequestStatus Status { get; set; } = MedicineRequestStatus.Pending;
    public string? Notes { get; set; }
}

public sealed class MedicineRequestItem : ScopedEntity
{
    public Guid MedicineRequestId { get; set; }
    public Guid MedicineId { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityFulfilled { get; set; }
}

public sealed class MedicineTransfer : ScopedEntity
{
    public Guid MedicineRequestId { get; set; }
    public Guid SourcePhcId { get; set; }
    public Guid DestinationPhcId { get; set; }
    public MedicineTransferStatus Status { get; set; } = MedicineTransferStatus.Pending;
}

public sealed class Notification : ScopedEntity
{
    public required string Title { get; set; }
    public required string Message { get; set; }
    public bool IsAcknowledged { get; set; }
    public string? Severity { get; set; }
}

public sealed class PredictionSnapshot : ScopedEntity
{
    public required string ModelVersion { get; set; }
    public DateTime ForecastGeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ForecastThroughUtc { get; set; }
    public string Status { get; set; } = "Ready";
}

public sealed class PredictionValue : ScopedEntity
{
    public Guid PredictionSnapshotId { get; set; }
    public DateOnly ForecastDate { get; set; }
    public decimal PointForecast { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}

public sealed class AiAlert : ScopedEntity
{
    public required string AlertType { get; set; }
    public required string Message { get; set; }
    public string Severity { get; set; } = "Info";
    public bool IsAcknowledged { get; set; }
}

public sealed class ModelVersion : Entity
{
    public required string Name { get; set; }
    public required string Version { get; set; }
    public string? MetricsJson { get; set; }
}

public sealed class AuditLog : ScopedEntity
{
    public required string Action { get; set; }
    public required string EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? ActorUserId { get; set; }
}
