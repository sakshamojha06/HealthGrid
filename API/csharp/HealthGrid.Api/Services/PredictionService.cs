using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

public sealed class PredictionError(string message) : Exception(message);

/// <summary>
/// Orchestrates the prediction pipeline: aggregates authorised features, asks the
/// AI service (or the local baseline), persists a versioned snapshot, and raises
/// AI alerts. Predictions are decision support — the system never acts on them
/// automatically.
/// </summary>
public sealed class PredictionService(
    HealthGridDbContext db,
    ICurrentUser currentUser,
    AnalyticsService analytics,
    AiClient ai,
    NotificationDispatcher notifications)
{
    private const int Horizon = 7;
    private const int HistoryDays = 60;

    public async Task<DateTime> RunAsync(CancellationToken ct)
    {
        if (currentUser.DistrictId is not { } districtId)
            throw new PredictionError("Prediction runs are scoped to a district.");
        var phcId = currentUser.IsInRole(Roles.DistrictAdministrator) ? (Guid?)null : currentUser.PhcId;

        var startedAt = DateTime.UtcNow;
        var to = DateOnly.FromDateTime(startedAt);
        var from = to.AddDays(-HistoryDays);

        // -- Patient volume forecast -------------------------------------
        var history = (await analytics.PatientVolumeAsync(from, to, ct))
            .Select(p => new AiPoint(p.Date, p.Value)).ToList();

        string modelVersion;
        string status;
        IReadOnlyList<AiForecastPoint> forecast;
        decimal? pctChange;

        var aiForecast = await ai.ForecastPatientVolumeAsync(
            new PatientVolumeForecastRequest(history, Horizon), ct);
        if (aiForecast is not null)
        {
            modelVersion = aiForecast.ModelVersion;
            status = aiForecast.Status;
            forecast = aiForecast.Points;
            pctChange = aiForecast.PctChange;
        }
        else
        {
            modelVersion = ForecastingMath.ModelVersion;
            forecast = ForecastingMath.ForecastMovingAverage(history, Horizon, out pctChange);
            status = history.Count(p => p.Value > 0) >= 14 ? "Ready" : "InsufficientData";
        }

        var snapshot = new PredictionSnapshot
        {
            DistrictId = districtId,
            PhcId = phcId,
            ModelVersion = modelVersion,
            Status = status,
            GeneratedAtUtc = startedAt,
            HorizonDays = Horizon,
            PatientVolumePctChange = pctChange,
            Values = forecast.Select(p => new PredictionValue
            {
                Kind = "PatientVolume",
                TargetDate = p.Date,
                PointForecast = p.Yhat,
                LowerBound = p.Lower,
                UpperBound = p.Upper,
            }).ToList(),
        };

        // -- Stock-out risk --------------------------------------------
        var stockItems = await BuildStockFeaturesAsync(districtId, phcId, ct);
        var aiStockout = await ai.ForecastStockoutAsync(
            new StockoutForecastRequest(stockItems.Select(x => x.Feature).ToList(), Horizon), ct);

        var risks = aiStockout?.Risks
            ?? stockItems.Select(x =>
            {
                var (days, date, risk, qty) = ForecastingMath.ProjectStockout(
                    x.Feature.OnHand, x.Feature.SafetyStock, x.Feature.DailyConsumption, Horizon);
                return new StockoutRisk(x.Feature.MedicineId, x.Feature.PhcId, days, date, risk, qty);
            }).ToList();

        snapshot.StockoutRisks = risks
            .Where(r => r.Risk is "Warning" or "Critical")
            .Select(r => new StockoutRiskRow
            {
                MedicineId = r.MedicineId,
                PhcId = r.PhcId,
                DaysToStockout = r.DaysToStockout,
                StockoutDate = r.StockoutDate,
                Risk = Enum.TryParse<AlertSeverity>(r.Risk, out var s) ? s : AlertSeverity.Warning,
                RecommendedReorderQty = r.RecommendedReorderQty,
            }).ToList();

        db.PredictionSnapshots.Add(snapshot);

        // Mark previous snapshots for this scope as stale.
        await db.PredictionSnapshots
            .Where(s => s.DistrictId == districtId && s.PhcId == phcId && s.Status == "Ready")
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, "Stale"), ct);

        await db.SaveChangesAsync(ct);

        // -- Alerts ---------------------------------------------------
        await RaiseStockoutAlertsAsync(districtId, phcId, snapshot, ct);
        await RaiseDiseaseAnomalyAlertsAsync(districtId, phcId, from, to, ct);
        await RaiseVolumeAlertAsync(districtId, phcId, pctChange, ct);

        return startedAt;
    }

    public async Task<PredictionsResponse> GetPredictionsAsync(CancellationToken ct)
    {
        var snapshot = await LatestSnapshotAsync(ct);
        if (snapshot is null)
            return new PredictionsResponse([], []);

        var forecast = snapshot.Values
            .Where(v => v.Kind == "PatientVolume")
            .OrderBy(v => v.TargetDate)
            .Select(v => new ForecastPoint(v.TargetDate, v.PointForecast, v.LowerBound, v.UpperBound))
            .ToList();

        var medNames = await db.Medicines.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var phcNames = await db.Phcs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var risks = snapshot.StockoutRisks.Select(r => new StockoutRiskDto(
            r.MedicineId, medNames.GetValueOrDefault(r.MedicineId, "?"),
            phcNames.GetValueOrDefault(r.PhcId, "?"),
            r.DaysToStockout, r.StockoutDate, r.Risk.ToString(), r.RecommendedReorderQty)).ToList();

        return new PredictionsResponse(forecast, risks);
    }

    public async Task<PredictionSnapshot?> LatestSnapshotAsync(CancellationToken ct)
    {
        if (currentUser.DistrictId is not { } districtId && !currentUser.IsSystemAdministrator)
            return null;

        var query = db.PredictionSnapshots.AsNoTracking()
            .Include(s => s.Values)
            .Include(s => s.StockoutRisks)
            .AsQueryable();

        if (!currentUser.IsSystemAdministrator)
            query = query.Where(s => s.DistrictId == currentUser.DistrictId);

        return await query.OrderByDescending(s => s.GeneratedAtUtc).FirstOrDefaultAsync(ct);
    }

    // -- feature builders / alerting -------------------------------------

    private async Task<List<(StockoutItem Feature, string MedicineName)>> BuildStockFeaturesAsync(
        Guid districtId, Guid? phcId, CancellationToken ct)
    {
        var inventoryQuery = db.MedicineInventories.AsNoTracking()
            .Where(x => x.DistrictId == districtId);
        if (phcId is { } p)
            inventoryQuery = inventoryQuery.Where(x => x.PhcId == p);

        var inventory = await inventoryQuery.ToListAsync(ct);
        var since = DateTime.UtcNow.AddDays(-14);

        var consumption = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.DistrictId == districtId
                        && t.Type == InventoryTransactionType.PatientIssue
                        && t.CreatedAtUtc >= since)
            .GroupBy(t => new { t.PhcId, t.MedicineId })
            .Select(g => new { g.Key.PhcId, g.Key.MedicineId, Total = g.Sum(x => -x.Quantity) })
            .ToListAsync(ct);

        var consumptionMap = consumption.ToDictionary(x => (x.PhcId, x.MedicineId), x => x.Total);
        var medNames = await db.Medicines.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        return inventory.Select(inv =>
        {
            var used = consumptionMap.GetValueOrDefault((inv.PhcId, inv.MedicineId), 0m);
            var daily = Math.Round(used / 14m, 3);
            return (new StockoutItem(inv.MedicineId, inv.PhcId, inv.QuantityOnHand, inv.SafetyStock, daily),
                    medNames.GetValueOrDefault(inv.MedicineId, "?"));
        }).ToList();
    }

    private async Task RaiseStockoutAlertsAsync(
        Guid districtId, Guid? phcId, PredictionSnapshot snapshot, CancellationToken ct)
    {
        var medNames = await db.Medicines.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        foreach (var risk in snapshot.StockoutRisks)
        {
            var medName = medNames.GetValueOrDefault(risk.MedicineId, "A medicine");
            var message = risk.DaysToStockout is { } d
                ? $"{medName} may stock out in approximately {d} day(s)."
                : $"{medName} is at risk of stocking out.";

            await RaiseIfNewAsync(districtId, risk.PhcId, "Stockout", message, risk.Risk.ToString(), ct);
        }
    }

    private async Task RaiseDiseaseAnomalyAlertsAsync(
        Guid districtId, Guid? phcId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var trends = await analytics.DiseaseTrendsAsync(from, to, ct);

        var aiResult = await ai.DetectDiseaseAnomaliesAsync(new DiseaseAnomalyRequest(
            trends.Select(t => new DiseaseSeries(t.DiseaseId, t.DiseaseName,
                t.Series.Select(p => new AiPoint(p.Date, p.Value)).ToList())).ToList()), ct);

        if (aiResult is not null)
        {
            foreach (var a in aiResult.Anomalies)
                await RaiseIfNewAsync(districtId, phcId, "DiseaseAnomaly", a.Message,
                    a.Severity, ct);
            return;
        }

        foreach (var trend in trends)
        {
            var (isAnomaly, score, baseline) = ForecastingMath.RollingZScore(
                trend.Series.Select(p => p.Value).ToList());
            if (!isAnomaly)
                continue;
            var message =
                $"Potential abnormal increase in {trend.DiseaseName} cases detected (today {trend.Today} vs baseline {baseline}). Verification is recommended.";
            await RaiseIfNewAsync(districtId, phcId, "DiseaseAnomaly", message, "Warning", ct);
        }
    }

    private async Task RaiseVolumeAlertAsync(
        Guid districtId, Guid? phcId, decimal? pctChange, CancellationToken ct)
    {
        if (pctChange is not { } pct || pct < 15m)
            return;
        var message = $"Patient volume is expected to increase by about {Math.Round(pct)}% next week.";
        await RaiseIfNewAsync(districtId, phcId, "PatientVolume", message, "Info", ct);
    }

    private async Task RaiseIfNewAsync(
        Guid districtId, Guid? phcId, string type, string message, string severity, CancellationToken ct)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var exists = await db.AiAlerts.AnyAsync(a =>
            a.DistrictId == districtId && a.PhcId == phcId && a.AlertType == type
            && a.Message == message && a.CreatedAtUtc >= yesterday, ct);
        if (exists)
            return;

        await notifications.RaiseAiAlertAsync(new AiAlert
        {
            DistrictId = districtId,
            PhcId = phcId,
            AlertType = type,
            Message = message,
            Severity = Enum.TryParse<AlertSeverity>(severity, out var s) ? s : AlertSeverity.Info,
        }, ct);
    }
}
