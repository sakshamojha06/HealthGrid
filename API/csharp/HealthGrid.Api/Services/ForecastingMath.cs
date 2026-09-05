namespace HealthGrid.Api.Services;

/// <summary>
/// Interpretable, data-efficient baseline models used when the FastAPI service is
/// unavailable. Deliberately simple: a damped moving-average forecast with a
/// residual-based interval, and a robust rolling z-score for anomaly detection.
/// </summary>
public static class ForecastingMath
{
    public const string ModelVersion = "local-baseline-1";

    /// <summary>Moving-average forecast with a +/- 1.96σ interval from recent residuals.</summary>
    public static IReadOnlyList<AiForecastPoint> ForecastMovingAverage(
        IReadOnlyList<AiPoint> history, int horizon, out decimal? pctChange)
    {
        pctChange = null;
        if (history.Count == 0 || horizon <= 0)
            return [];

        var values = history.Select(p => (double)p.Value).ToArray();
        var window = Math.Min(7, values.Length);
        var recentAvg = values[^window..].Average();

        // Residual spread of the trailing window around its mean.
        var residualStd = window > 1
            ? Math.Sqrt(values[^window..].Select(v => Math.Pow(v - recentAvg, 2)).Sum() / (window - 1))
            : Math.Max(1.0, recentAvg * 0.25);

        if (values.Length >= window * 2)
        {
            var priorAvg = values[^(window * 2)..^window].Average();
            if (priorAvg > 0)
                pctChange = (decimal)Math.Round((recentAvg - priorAvg) / priorAvg * 100, 1);
        }

        var lastDate = history[^1].Date;
        var margin = 1.96 * residualStd;
        var points = new List<AiForecastPoint>(horizon);
        for (var i = 1; i <= horizon; i++)
        {
            var date = lastDate.AddDays(i);
            var yhat = (decimal)Math.Round(recentAvg, 2);
            points.Add(new AiForecastPoint(
                date, yhat,
                (decimal)Math.Round(Math.Max(0, recentAvg - margin), 2),
                (decimal)Math.Round(recentAvg + margin, 2)));
        }
        return points;
    }

    /// <summary>Projects days-to-stockout from average daily consumption.</summary>
    public static (int? Days, DateOnly? Date, string Risk, decimal ReorderQty) ProjectStockout(
        decimal onHand, decimal safetyStock, decimal dailyConsumption, int horizon)
    {
        if (dailyConsumption <= 0)
            return (null, null, "Info", 0);

        var days = (int)Math.Floor((double)(onHand / dailyConsumption));
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));

        var risk = days switch
        {
            <= 3 => "Critical",
            <= 7 => "Warning",
            _ when days <= horizon => "Warning",
            _ => "Info",
        };

        // Cover the horizon plus safety stock, minus what is already on hand.
        var reorder = Math.Max(0, Math.Round(dailyConsumption * horizon + safetyStock - onHand, 2));
        return (days, date, risk, reorder);
    }

    /// <summary>Robust rolling z-score: flags today's value against a trailing median/MAD baseline.</summary>
    public static (bool IsAnomaly, decimal Score, decimal Baseline) RollingZScore(
        IReadOnlyList<decimal> series)
    {
        if (series.Count < 8)
            return (false, 0, series.Count > 0 ? series[^1] : 0);

        var baselineWindow = series.Take(series.Count - 1).TakeLast(21).Select(v => (double)v).ToArray();
        var median = Median(baselineWindow);
        var mad = Median(baselineWindow.Select(v => Math.Abs(v - median)).ToArray());
        var scale = mad > 0 ? mad * 1.4826 : Math.Max(1.0, median * 0.25);

        var latest = (double)series[^1];
        var z = (latest - median) / scale;
        return (z >= 3.0, (decimal)Math.Round(z, 2), (decimal)Math.Round(median, 2));
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0)
            return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
