using System;
using LiveSplit.Model;

namespace BrownSplits;

// Stores a calculated bad-time threshold and the inputs used to calculate it
internal readonly struct ThresholdCacheEntry
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
