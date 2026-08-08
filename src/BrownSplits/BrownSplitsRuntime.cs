using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LiveSplit.Model;

namespace BrownSplits;

// Connects BrownSplits to LiveSplit's shared semantic-color calculation.
internal static class BrownSplitsRuntime
{
    private const string HarmonyId = "BrownSplits.SplitColorOverride";

    // Harmony's postfix and LiveSplit's component lifecycle may enter this class
    // through different paths, so registration and patch state are kept together.
    private static readonly object Locker = new();
    private static readonly Dictionary<LiveSplitState, RegisteredState> RegisteredStates = new();

    // Accessing the LiveSplitStateHelper.GetSplitColor method using Harmony to override its return value.
    private static readonly MethodInfo SplitColorMethod = AccessTools.Method(
        typeof(LiveSplitStateHelper), // method class
        nameof(LiveSplitStateHelper.GetSplitColor), // method name
        // Method signature
        new[]
        {
            typeof(LiveSplitState),
            typeof(TimeSpan?),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(string),
            typeof(TimingMethod),
        });

    // Harmony changes a method's behavior in memory while the application is running;
    // it does not modify LiveSplit.Core.dll on disk. Our postfix runs immediately after
    // LiveSplitStateHelper.GetSplitColor and may replace the color that LiveSplit chose.
    // This lets us override one requested split or live timer without changing the
    // layout's global green, red, and gold color settings.
    private static Harmony? harmony;
    private static int nextOwnerId;

    public static IDisposable Register(LiveSplitState state, BrownSplitsSettings settings)
    {
        lock (Locker)
        {
            EnsureHarmonyPatchIsInstalled();

            if (!RegisteredStates.TryGetValue(state, out RegisteredState? registeredState))
            {
                registeredState = new RegisteredState(state);
                RegisteredStates.Add(state, registeredState);
            }

            // A layout can technically contain BrownSplits more than once. This is why the
            // owner ID exists and is incremented for each registration.
            int ownerId = ++nextOwnerId;
            registeredState.AddOwner(ownerId, settings);

            return new RegistrationToken(state, ownerId);
        }
    }

    private static void EnsureHarmonyPatchIsInstalled()
    {
        if (harmony is not null)
        {
            return;
        }

        harmony = new Harmony(HarmonyId);

        // Postfix means that the BrownSplits method is called after the original Method.
        // If the original method returns a color, we may replace it with our override color.
        var postfix = new HarmonyMethod(
            typeof(BrownSplitsRuntime),
            nameof(AfterGetSplitColor));

        harmony.Patch(SplitColorMethod, postfix: postfix);
    }

    // Harmony calls this after every LiveSplitStateHelper.GetSplitColor invocation.
    // The special __result parameter contains LiveSplit's original return value.
    private static void AfterGetSplitColor(
        LiveSplitState state,
        int splitNumber,
        TimingMethod method,
        ref Color? __result)
    {
        // Null means that LiveSplit did not choose a semantic color for this value.
        // In that case we trust LiveSplit's judgement and do not override it with some bullshit.
        if (!__result.HasValue)
        {
            return;
        }

        lock (Locker)
        {
            if (RegisteredStates.TryGetValue(state, out RegisteredState? registeredState)
                && registeredState.TryGetOverrideColor(splitNumber, method, out Color overrideColor))
            {
                __result = overrideColor;
            }
        }
    }

    private static void Unregister(LiveSplitState state, int ownerId)
    {
        lock (Locker)
        {
            if (!RegisteredStates.TryGetValue(state, out RegisteredState? registeredState))
            {
                return;
            }

            registeredState.RemoveOwner(ownerId);
            if (registeredState.HasOwners)
            {
                return;
            }

            registeredState.Dispose();
            RegisteredStates.Remove(state);

            // Leaving patches installed after the last component is removed makes no sense
            if (RegisteredStates.Count == 0 && harmony is not null)
            {
                harmony.Unpatch(SplitColorMethod, HarmonyPatchType.Postfix, HarmonyId);
                harmony = null;
            }
        }
    }

    private sealed class RegistrationToken : IDisposable
    {
        private LiveSplitState? state;
        private readonly int ownerId;

        public RegistrationToken(LiveSplitState state, int ownerId)
        {
            this.state = state;
            this.ownerId = ownerId;
        }

        public void Dispose()
        {
            // Setting state to null makes repeated Dispose calls harmless.
            LiveSplitState? registeredState = state;
            state = null;

            if (registeredState is not null)
            {
                Unregister(registeredState, ownerId);
            }
        }
    }

    private sealed class RegisteredState : IDisposable
    {
        private readonly LiveSplitState state;
        private readonly Dictionary<int, BrownSplitsSettings> settingsByOwner = new();
        private readonly Dictionary<ThresholdCacheKey, ThresholdCacheEntry> thresholdCache = new();

        public RegisteredState(LiveSplitState state)
        {
            this.state = state;

            // Manual edits can alter histories, best segments, or the active run,
            // this is why an event handler is registered to clear the cache when the user makes a manual change.
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
                settings
            );

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
            // Live and previous segments need to be handled differently
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

            // Sorting history on every timer frame would be wasteful. Recalculate
            // only when the run, history, or relevant settings have changed.
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
            // SegmentHistory is keyed by attempt ID. Walking AttemptHistory in
            // reverse preserves the meaning of the "most recent N" setting.
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

    private readonly struct ThresholdCacheKey : IEquatable<ThresholdCacheKey>
    {
        private readonly int splitNumber;
        private readonly TimingMethod timingMethod;

        public ThresholdCacheKey(int splitNumber, TimingMethod timingMethod)
        {
            this.splitNumber = splitNumber;
            this.timingMethod = timingMethod;
        }

        public bool Equals(ThresholdCacheKey other)
            => splitNumber == other.splitNumber && timingMethod == other.timingMethod;

        public override bool Equals(object? obj)
            => obj is ThresholdCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (splitNumber * 397) ^ (int)timingMethod;
            }
        }
    }

    private readonly struct ThresholdCacheEntry
    {
        private readonly IRun run;
        private readonly ISegment segment;
        private readonly int attemptCount;
        private readonly int historyCount;
        private readonly int latestAttemptIndex;
        private readonly int settingsHash;

        public TimeSpan? Threshold { get; }

        public ThresholdCacheEntry(
            IRun run,
            ISegment segment,
            int attemptCount,
            int historyCount,
            int latestAttemptIndex,
            int settingsHash,
            TimeSpan? threshold)
        {
            this.run = run;
            this.segment = segment;
            this.attemptCount = attemptCount;
            this.historyCount = historyCount;
            this.latestAttemptIndex = latestAttemptIndex;
            this.settingsHash = settingsHash;
            Threshold = threshold;
        }

        public bool Matches(
            IRun candidateRun,
            ISegment candidateSegment,
            int candidateAttemptCount,
            int candidateHistoryCount,
            int candidateLatestAttemptIndex,
            int candidateSettingsHash)
            => ReferenceEquals(run, candidateRun)
                && ReferenceEquals(segment, candidateSegment)
                && attemptCount == candidateAttemptCount
                && historyCount == candidateHistoryCount
                && latestAttemptIndex == candidateLatestAttemptIndex
                && settingsHash == candidateSettingsHash;
    }
}
