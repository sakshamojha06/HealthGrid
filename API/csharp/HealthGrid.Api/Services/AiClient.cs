using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthGrid.Api.Services;

// -- Wire contracts shared with the FastAPI service (snake_case on the wire) ---
public sealed record AiPoint(DateOnly Date, decimal Value);
public sealed record AiForecastPoint(DateOnly Date, decimal Yhat, decimal Lower, decimal Upper);

public sealed record PatientVolumeForecastRequest(IReadOnlyList<AiPoint> History, int Horizon);
public sealed record PatientVolumeForecastResponse(
    string ModelVersion, string Status, IReadOnlyList<AiForecastPoint> Points, decimal? PctChange);

public sealed record StockoutItem(
    Guid MedicineId, Guid PhcId, decimal OnHand, decimal SafetyStock, decimal DailyConsumption);
public sealed record StockoutForecastRequest(IReadOnlyList<StockoutItem> Items, int Horizon);
public sealed record StockoutRisk(
    Guid MedicineId, Guid PhcId, int? DaysToStockout, DateOnly? StockoutDate,
    string Risk, decimal RecommendedReorderQty);
public sealed record StockoutForecastResponse(string ModelVersion, IReadOnlyList<StockoutRisk> Risks);

public sealed record DiseaseSeries(Guid DiseaseId, string DiseaseName, IReadOnlyList<AiPoint> Values);
public sealed record DiseaseAnomalyRequest(IReadOnlyList<DiseaseSeries> Series);
public sealed record DiseaseAnomaly(
    Guid DiseaseId, string DiseaseName, string Message, string Severity, decimal Score, decimal Baseline);
public sealed record DiseaseAnomalyResponse(string ModelVersion, IReadOnlyList<DiseaseAnomaly> Anomalies);

/// <summary>
/// HTTP client for the independent FastAPI AI service. The ASP.NET Core API owns
/// authentication, scope filtering and persistence; the AI service only ever
/// receives already-authorised aggregate features.
///
/// When the service is not configured or unreachable, callers fall back to the
/// interpretable baseline in <see cref="ForecastingMath"/>.
/// </summary>
public sealed class AiClient(HttpClient http, ILogger<AiClient> logger)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsConfigured => http.BaseAddress is not null;

    public Task<PatientVolumeForecastResponse?> ForecastPatientVolumeAsync(
        PatientVolumeForecastRequest request, CancellationToken ct)
        => PostAsync<PatientVolumeForecastRequest, PatientVolumeForecastResponse>(
            "/forecast/patient-volume", request, ct);

    public Task<StockoutForecastResponse?> ForecastStockoutAsync(
        StockoutForecastRequest request, CancellationToken ct)
        => PostAsync<StockoutForecastRequest, StockoutForecastResponse>(
            "/forecast/stockout", request, ct);

    public Task<DiseaseAnomalyResponse?> DetectDiseaseAnomaliesAsync(
        DiseaseAnomalyRequest request, CancellationToken ct)
        => PostAsync<DiseaseAnomalyRequest, DiseaseAnomalyResponse>(
            "/anomaly/disease", request, ct);

    private async Task<TOut?> PostAsync<TIn, TOut>(string path, TIn body, CancellationToken ct)
        where TOut : class
    {
        if (!IsConfigured)
            return null;
        try
        {
            using var response = await http.PostAsJsonAsync(path, body, Json, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TOut>(Json, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI service call to {Path} failed; using local baseline.", path);
            return null;
        }
    }
}
