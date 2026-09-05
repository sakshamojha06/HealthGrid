namespace HealthGrid.Api.Domain;

/// <summary>Every way an inventory balance can change. The ledger is append-only.</summary>
public enum InventoryTransactionType
{
    Receipt,
    PatientIssue,
    Adjustment,
    TransferOut,
    TransferIn
}

/// <summary>Lifecycle of a PHC-to-PHC medicine request.</summary>
public enum MedicineRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    PartiallyFulfilled,
    Fulfilled,
    Completed
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
