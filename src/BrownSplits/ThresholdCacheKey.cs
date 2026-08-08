using System;
using LiveSplit.Model;

namespace BrownSplits;

// Identifies a cached threshold by split number and timing method
internal readonly struct ThresholdCacheKey : IEquatable<ThresholdCacheKey>
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
