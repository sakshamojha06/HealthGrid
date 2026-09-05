using System.ComponentModel.DataAnnotations;

namespace HealthGrid.Api.Contracts;

// ---------------------------------------------------------------------------
// Shared
// ---------------------------------------------------------------------------
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------
public sealed record LoginRequest([Required] string Email, [Required] string Password);
public sealed record RefreshRequest([Required] string RefreshToken);
public sealed record LogoutRequest([Required] string RefreshToken);
public sealed record AuthTokensResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

public sealed record CurrentUserResponse(
    Guid Id, string Email, string FullName, string Role,
    Guid? DistrictId, string? DistrictName, Guid? PhcId, string? PhcName);

// ---------------------------------------------------------------------------
// Reference / organisation
// ---------------------------------------------------------------------------
public sealed record DistrictDto(Guid Id, string Name, string Code, int PhcCount);
public sealed record DistrictInput([Required] string Name, [Required] string Code);

public sealed record PhcDto(Guid Id, Guid DistrictId, string DistrictName, string Name, string Code);
public sealed record PhcInput([Required] Guid DistrictId, [Required] string Name, [Required] string Code);

public sealed record MedicineDto(Guid Id, string Name, string GenericName, string Unit, bool IsActive);
public sealed record MedicineInput(
    [Required] string Name, [Required] string GenericName, [Required] string Unit, bool IsActive = true);

public sealed record DiseaseDto(Guid Id, string Name, string Code);
public sealed record DiseaseInput([Required] string Name, [Required] string Code);

public sealed record SpecializationDto(Guid Id, string Name);
public sealed record SpecializationInput([Required] string Name);

public sealed record UserAccountDto(
    Guid Id, string Email, string FullName, string Role,
    Guid? DistrictId, Guid? PhcId, bool IsActive);

public sealed record CreateUserInput(
    [Required, EmailAddress] string Email,
    [Required] string FullName,
    [Required, MinLength(12)] string Password,
    [Required] string Role,
    Guid? DistrictId,
    Guid? PhcId);

public sealed record UpdateUserInput(
    string? FullName, string? Role, Guid? DistrictId, Guid? PhcId, bool? IsActive);

// ---------------------------------------------------------------------------
// Patients / visits
// ---------------------------------------------------------------------------
public sealed record PrescriptionItemInput([Required] Guid MedicineId, [Range(0.001, 1_000_000)] decimal Quantity);

public sealed record RecordVisitRequest(
    [Required] string LocalIdentifier,
    int? BirthYear,
    string? Gender,
    [Required] DateOnly VisitDate,
    string? Symptoms,
    Guid? AttendingDoctorId,
    List<Guid> DiseaseIds,
    List<PrescriptionItemInput> PrescriptionItems);

public sealed record PrescribedMedicineDto(string Name, decimal Quantity);

public sealed record PatientVisitDto(
    Guid Id, Guid PhcId, string PhcName, string LocalIdentifier,
    int? BirthYear, string? Gender, DateOnly VisitDate, string? Symptoms,
    string? AttendingDoctorName, IReadOnlyList<string> Diseases,
    IReadOnlyList<PrescribedMedicineDto> Medicines);

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------
public sealed record InventoryRowDto(
    Guid Id, Guid DistrictId, Guid PhcId, string PhcName,
    Guid MedicineId, string MedicineName, string Unit,
    decimal QuantityOnHand, decimal SafetyStock, DateOnly? ExpiryDate, bool IsLow);

public sealed record InventoryTransactionDto(
    Guid Id, Guid PhcId, string PhcName, string MedicineName,
    string Type, decimal Quantity, decimal BalanceAfter, string? Reference, DateTime CreatedAtUtc);

public sealed record StockMutationRequest(
    [Required] Guid PhcId,
    [Required] Guid MedicineId,
    decimal Quantity,
    string? Reference,
    string? IdempotencyKey);

// ---------------------------------------------------------------------------
// Medicine requests
// ---------------------------------------------------------------------------
public sealed record MedicineRequestItemDto(
    Guid MedicineId, string MedicineName, decimal QuantityRequested, decimal QuantityFulfilled);

public sealed record MedicineRequestDto(
    Guid Id, Guid SourcePhcId, string SourcePhcName, Guid DestinationPhcId, string DestinationPhcName,
    string Status, string? Notes, DateOnly? NeededByDate, string RequestedByName, DateTime CreatedAtUtc,
    IReadOnlyList<MedicineRequestItemDto> Items);

public sealed record CreateMedicineRequestInput(
    [Required] Guid DestinationPhcId,
    string? Notes,
    DateOnly? NeededByDate,
    List<MedicineRequestLineInput> Items);

public sealed record MedicineRequestLineInput([Required] Guid MedicineId, [Range(0.001, 1_000_000)] decimal QuantityRequested);

public sealed record RejectRequestInput(string? Reason);
public sealed record FulfillMedicineRequestInput(List<FulfillLineInput> Items);
public sealed record FulfillLineInput([Required] Guid MedicineId, [Range(0, 1_000_000)] decimal QuantityFulfilled);

public sealed record RecommendedSourceDto(
    Guid PhcId, string PhcName, decimal AvailableSurplus, decimal PredictedDemand7d);

// ---------------------------------------------------------------------------
// Doctors
// ---------------------------------------------------------------------------
public sealed record DoctorAvailabilityDto(Guid? Id, int DayOfWeek, string StartTime, string EndTime);

public sealed record DoctorDto(
    Guid Id, Guid PhcId, string PhcName, string Name, string? RegistrationNumber,
    Guid? SpecializationId, string? SpecializationName, IReadOnlyList<DoctorAvailabilityDto> Availability);

public sealed record DoctorInput(
    [Required] string Name, [Required] Guid PhcId, string? RegistrationNumber, Guid? SpecializationId);

public sealed record SetAvailabilityInput(List<DoctorAvailabilitySlotInput> Slots);
public sealed record DoctorAvailabilitySlotInput(
    [Range(0, 6)] int DayOfWeek, [Required] string StartTime, [Required] string EndTime);

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------
public sealed record NotificationDto(
    Guid Id, string Title, string Message, string Severity, bool IsAcknowledged, DateTime CreatedAtUtc);

// ---------------------------------------------------------------------------
// Analytics / dashboard / AI
// ---------------------------------------------------------------------------
public sealed record TimeSeriesPoint(DateOnly Date, decimal Value);
public sealed record ForecastPoint(DateOnly Date, decimal Yhat, decimal Lower, decimal Upper);

public sealed record DiseaseTrendDto(
    Guid DiseaseId, string DiseaseName, int Today, IReadOnlyList<TimeSeriesPoint> Series);

public sealed record StockoutRiskDto(
    Guid MedicineId, string MedicineName, string PhcName,
    int? DaysToStockout, DateOnly? StockoutDate, string Risk, decimal RecommendedReorderQty);

public sealed record AiAlertDto(
    Guid Id, string AlertType, string Message, string Severity, bool IsAcknowledged, DateTime CreatedAtUtc);

public sealed record PredictionsResponse(
    IReadOnlyList<ForecastPoint> Forecast, IReadOnlyList<StockoutRiskDto> StockoutRisks);

public sealed record AiRunResponse(DateTime StartedAtUtc);

public sealed record InventorySummaryDto(int Total, int Low);
public sealed record SpecialistCountDto(string Specialization, int Count);

public sealed record DashboardSummaryDto(
    DateTime GeneratedAtUtc,
    string PredictionStatus,
    string? PredictionModelVersion,
    int TodayPatientCount,
    IReadOnlyList<TimeSeriesPoint> PatientVolumeTrend,
    IReadOnlyList<ForecastPoint> PatientVolumeForecast,
    decimal? PatientVolumePctChange,
    IReadOnlyList<DiseaseTrendDto> DiseaseTrends,
    InventorySummaryDto InventorySummary,
    IReadOnlyList<InventoryRowDto> LowStock,
    IReadOnlyList<StockoutRiskDto> StockoutRisks,
    IReadOnlyList<AiAlertDto> AiAlerts,
    int PendingIncomingRequests,
    int PendingOutgoingRequests,
    IReadOnlyList<SpecialistCountDto> AvailableSpecialists);
