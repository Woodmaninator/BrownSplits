using System;
using System.Collections.Generic;
using System.Linq;

namespace BrownSplits;

internal static class SplitTimeEvaluator
{
    public static bool IsSlowerThanBest(TimeSpan currentSegmentTime, TimeSpan? bestSegmentTime)
        => bestSegmentTime.HasValue && currentSegmentTime > bestSegmentTime.Value;

    public static bool IsAtOrAbovePercentile(
        TimeSpan currentSegmentTime,
        IEnumerable<TimeSpan> historicalTimesNewestFirst,
        double? percentile,
        int? recentTimeCount)
    {
        TimeSpan? threshold = GetThreshold(historicalTimesNewestFirst, percentile, recentTimeCount);
        return threshold.HasValue && currentSegmentTime >= threshold.Value;
    }

    public static TimeSpan? GetThreshold(
        IEnumerable<TimeSpan> historicalTimesNewestFirst,
        double? percentile,
        int? recentTimeCount)
    {
        IEnumerable<TimeSpan> selectedTimes = historicalTimesNewestFirst;
        if (recentTimeCount.HasValue)
        {
            selectedTimes = selectedTimes.Take(Math.Max(1, recentTimeCount.Value));
        }

        TimeSpan[] times = selectedTimes.OrderBy(time => time).ToArray();
        if (times.Length == 0)
        {
            return null;
        }

        return CalculatePercentile(times, percentile ?? 100.0);
    }

    internal static TimeSpan CalculatePercentile(IReadOnlyList<TimeSpan> sortedTimes, double percentile)
    {
        if (sortedTimes.Count == 0)
        {
            throw new ArgumentException("At least one historical time is required.", nameof(sortedTimes));
        }

        double boundedPercentile = Math.Max(0.0, Math.Min(100.0, percentile));
        double position = (boundedPercentile / 100.0) * (sortedTimes.Count - 1);
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedTimes[lowerIndex];
        }

        long lowerTicks = sortedTimes[lowerIndex].Ticks;
        long upperTicks = sortedTimes[upperIndex].Ticks;
        double fraction = position - lowerIndex;
        long interpolatedTicks = lowerTicks + (long)Math.Round((upperTicks - lowerTicks) * fraction);
        return TimeSpan.FromTicks(interpolatedTicks);
    }
}
