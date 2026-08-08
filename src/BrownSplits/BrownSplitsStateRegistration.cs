using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LiveSplit.Model;

namespace BrownSplits;

// Evaluates brown-split eligibility and caches historical thresholds for one LiveSplit state
internal sealed class BrownSplitsStateRegistration : IDisposable
{
    private readonly LiveSplitState state;
    private readonly Dictionary<int, BrownSplitsSettings> settingsByOwner = new();
    private readonly Dictionary<ThresholdCacheKey, ThresholdCacheEntry> thresholdCache = new();

    public BrownSplitsStateRegistration(LiveSplitState state)
    {
        this.state = state;

        // Manual edits can alter histories, best segments, so an event heandler is added to account for these changes
        state.RunManuallyModified += ClearThresholdCache;
    }

    public bool HasOwners => settingsByOwner.Count > 0;

    public void AddOwner(int ownerId, BrownSplitsSettings settings)
    {
        settingsByOwner.Add(ownerId, settings);
        thresholdCache.Clear();
    }

    public void RemoveOwner(int ownerId)
    {
        settingsByOwner.Remove(ownerId);
        thresholdCache.Clear();
    }

    public bool TryGetOverrideColor(
        int splitNumber,
        TimingMethod timingMethod,
        out Color overrideColor)
    {
        overrideColor = default;

        if (!IsValidSplitNumber(splitNumber) || !HasOwners)
        {
            return false;
        }

        BrownSplitsSettings settings = settingsByOwner.Last().Value;
        ISegment segment = state.Run[splitNumber];
        TimeSpan? segmentTime = GetSegmentTime(segment, splitNumber, timingMethod);
        TimeSpan? bestSegmentTime = segment.BestSegmentTime[timingMethod];
        TimeSpan? badTimeThreshold = GetBadTimeThreshold(
            segment,
            splitNumber,
            timingMethod,
            settings);

        bool isSlowerThanBest = segmentTime.HasValue
            && SplitTimeEvaluator.IsSlowerThanBest(segmentTime.Value, bestSegmentTime);
        bool reachedBadTimeThreshold = segmentTime.HasValue
            && badTimeThreshold.HasValue
            && segmentTime.Value >= badTimeThreshold.Value;

        if (!isSlowerThanBest || !reachedBadTimeThreshold)
        {
            return false;
        }

        overrideColor = settings.OverrideColor;
        return true;
    }

    public void Dispose() => state.RunManuallyModified -= ClearThresholdCache;

    private bool IsValidSplitNumber(int splitNumber)
        => splitNumber >= 0 && splitNumber < state.Run.Count;

    private TimeSpan? GetSegmentTime(
        ISegment segment,
        int splitNumber,
        TimingMethod timingMethod)
    {
        bool isCurrentLiveSegment = splitNumber == state.CurrentSplitIndex
            && state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused
            && !segment.SplitTime[timingMethod].HasValue;

        if (isCurrentLiveSegment)
        {
            return LiveSplitStateHelper.GetLiveSegmentTime(state, splitNumber, timingMethod);
        }

        return LiveSplitStateHelper.GetPreviousSegmentTime(state, splitNumber, timingMethod);
    }

    private TimeSpan? GetBadTimeThreshold(
        ISegment segment,
        int splitNumber,
        TimingMethod timingMethod,
        BrownSplitsSettings settings)
    {
        var cacheKey = new ThresholdCacheKey(splitNumber, timingMethod);
        int latestAttemptIndex = GetLatestAttemptIndex();
        int settingsHash = settings.GetEvaluationHashCode();

        if (thresholdCache.TryGetValue(cacheKey, out ThresholdCacheEntry cachedThreshold)
            && cachedThreshold.Matches(
                state.Run,
                segment,
                state.Run.AttemptHistory.Count,
                segment.SegmentHistory.Count,
                latestAttemptIndex,
                settingsHash))
        {
            return cachedThreshold.Threshold;
        }

        // Sorting history on every timer frame would be wasteful. Recalculate only
        // when the run, history, or relevant settings have changed
        TimeSpan? threshold = SplitTimeEvaluator.GetThreshold(
            GetHistoricalTimesNewestFirst(state.Run, segment, timingMethod),
            settings.UsePercentile ? settings.Percentile : null,
            settings.LimitToRecentAttempts ? settings.RecentAttemptCount : null);

        thresholdCache[cacheKey] = new ThresholdCacheEntry(
            state.Run,
            segment,
            state.Run.AttemptHistory.Count,
            segment.SegmentHistory.Count,
            latestAttemptIndex,
            settingsHash,
            threshold);

        return threshold;
    }

    private int GetLatestAttemptIndex()
    {
        int attemptCount = state.Run.AttemptHistory.Count;
        return attemptCount == 0
            ? -1
            : state.Run.AttemptHistory[attemptCount - 1].Index;
    }

    private void ClearThresholdCache(object? sender, EventArgs eventArgs)
        => thresholdCache.Clear();

    private static IEnumerable<TimeSpan> GetHistoricalTimesNewestFirst(
        IRun run,
        ISegment segment,
        TimingMethod timingMethod)
    {
        // SegmentHistory is keyed by attempt ID. Walking AttemptHistory in reverse
        // preserves the meaning of the "most recent N" setting.
        foreach (Attempt attempt in run.AttemptHistory.OrderByDescending(attempt => attempt.Index))
        {
            if (segment.SegmentHistory.TryGetValue(attempt.Index, out Time historyTime)
                && historyTime[timingMethod].HasValue)
            {
                yield return historyTime[timingMethod]!.Value;
            }
        }
    }
}
