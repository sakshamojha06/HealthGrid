namespace HealthGrid.Domain.Entities;

public enum InventoryTransactionType
{
    Receipt,
    PatientIssue,
    Adjustment,
    TransferOut,
    TransferIn
}

public enum MedicineRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    PartiallyFulfilled,
    Fulfilled,
    Completed
}

public enum MedicineTransferStatus
{
    Pending,
    InTransit,
    Completed,
    Cancelled
}
