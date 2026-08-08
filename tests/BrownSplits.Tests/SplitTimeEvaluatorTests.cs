using System;
using Xunit;

namespace BrownSplits.Tests;

public sealed class SplitTimeEvaluatorTests
{
    [Fact]
    public void UsesInclusiveLinearPercentileInterpolation()
    {
        TimeSpan[] history =
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
        };

        Assert.False(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(19.4), history, 95.0, null));
        Assert.True(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(19.5), history, 95.0, null));
    }

    [Fact]
    public void CanLimitEvaluationToNewestRecordedTimes()
    {
        TimeSpan[] newestFirst =
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(11),
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(100),
        };

        Assert.False(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(50), newestFirst, 95.0, null));
        Assert.True(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(50), newestFirst, 95.0, 3));
    }

    [Fact]
    public void DoesNotOverrideWithoutRecordedHistory()
    {
        Assert.False(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromMinutes(1), Array.Empty<TimeSpan>(), 95.0, null));
    }

    [Fact]
    public void MissingPercentileRequiresTheSlowestRecordedTime()
    {
        TimeSpan[] history =
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
        };

        Assert.False(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(29.9), history, null, null));
        Assert.True(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(30), history, null, null));
    }

    [Fact]
    public void RecentLimitAlsoAppliesWithoutAPercentile()
    {
        TimeSpan[] newestFirst =
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(100),
        };

        Assert.False(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(50), newestFirst, null, null));
        Assert.True(SplitTimeEvaluator.IsAtOrAbovePercentile(
            TimeSpan.FromSeconds(50), newestFirst, null, 3));
    }

    [Theory]
    [InlineData(-20.0, 10.0)]
    [InlineData(0.0, 10.0)]
    [InlineData(100.0, 30.0)]
    [InlineData(120.0, 30.0)]
    public void BoundsPercentileToValidRange(double percentile, double expectedSeconds)
    {
        TimeSpan[] sortedHistory =
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
        };

        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            SplitTimeEvaluator.CalculatePercentile(sortedHistory, percentile));
    }

    [Theory]
    [InlineData(10.0, 10.0, false)]
    [InlineData(9.0, 10.0, false)]
    [InlineData(10.1, 10.0, true)]
    public void RequiresTheCurrentTimeToBeStrictlySlowerThanBest(
        double currentSeconds,
        double bestSeconds,
        bool expected)
    {
        Assert.Equal(
            expected,
            SplitTimeEvaluator.IsSlowerThanBest(
                TimeSpan.FromSeconds(currentSeconds),
                TimeSpan.FromSeconds(bestSeconds)));
    }

    [Fact]
    public void DoesNotOverrideWhenNoBestSegmentExists()
    {
        Assert.False(SplitTimeEvaluator.IsSlowerThanBest(TimeSpan.FromSeconds(10), null));
    }
}
