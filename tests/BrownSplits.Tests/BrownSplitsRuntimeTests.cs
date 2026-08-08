using System;
using System.Drawing;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using Xunit;

namespace BrownSplits.Tests;

public sealed class BrownSplitsRuntimeTests
{
    [Fact]
    public void OverridesOnlyTheQualifyingCompletedSplit()
    {
        LiveSplitState state = CreateTwoSplitState();
        var settings = new BrownSplitsSettings();

        using (BrownSplitsRuntime.Register(state, settings))
        {
            Color? qualifyingColor = GetBehindColor(state, 0);
            Color? ordinaryColor = GetBehindColor(state, 1);

            Assert.Equal(BrownSplitsSettings.DefaultOverrideColor, qualifyingColor);
            Assert.Equal(state.LayoutSettings.BehindLosingTimeColor, ordinaryColor);
        }

        Assert.Equal(state.LayoutSettings.BehindLosingTimeColor, GetBehindColor(state, 0));
    }

    [Fact]
    public void AppliesImmediatelyWhenTheSplitTimeIsWritten()
    {
        LiveSplitState state = CreateSingleSplitState(completed: false);

        using (BrownSplitsRuntime.Register(state, new BrownSplitsSettings()))
        {
            Assert.Equal(state.LayoutSettings.BehindLosingTimeColor, GetBehindColor(state, 0));

            state.Run[0].SplitTime = new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(12));

            Assert.Equal(BrownSplitsSettings.DefaultOverrideColor, GetBehindColor(state, 0));
        }
    }

    [Fact]
    public void UpdatesActiveSemanticTimersAsTheLiveSegmentCrossesTheThreshold()
    {
        LiveSplitState state = CreateSingleSplitState(completed: false);
        state.CurrentPhase = TimerPhase.Paused;
        state.CurrentSplitIndex = 0;

        using (BrownSplitsRuntime.Register(state, new BrownSplitsSettings()))
        {
            state.TimePausedAt = TimeSpan.FromSeconds(10);
            Assert.Equal(state.LayoutSettings.BehindLosingTimeColor, GetBehindColor(state, 0));

            state.TimePausedAt = TimeSpan.FromSeconds(12);
            Assert.Equal(BrownSplitsSettings.DefaultOverrideColor, GetBehindColor(state, 0));
        }
    }

    [Fact]
    public void DoesNotOverrideAnEqualBestCutsceneSplit()
    {
        LiveSplitState state = CreateSingleSplitState(completed: true);
        state.Run[0].BestSegmentTime = new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(12));

        using (BrownSplitsRuntime.Register(state, new BrownSplitsSettings()))
        {
            Assert.Equal(state.LayoutSettings.BehindLosingTimeColor, GetBehindColor(state, 0));
        }
    }

    private static LiveSplitState CreateTwoSplitState()
    {
        LiveSplitState state = CreateSingleSplitState(completed: true);
        state.Run.AddSegment(
            "Ordinary",
            bestSegmentTime: new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(5)),
            splitTime: new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(18)));
        AddHistory(state.Run[1], 8, 9, 10);
        return state;
    }

    private static LiveSplitState CreateSingleSplitState(bool completed)
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment(
            "Worst",
            bestSegmentTime: new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(8)),
            splitTime: completed
                ? new Time(TimingMethod.RealTime, TimeSpan.FromSeconds(12))
                : default);

        for (int attemptIndex = 1; attemptIndex <= 3; attemptIndex++)
        {
            run.AttemptHistory.Add(new Attempt(attemptIndex, default, null, null, null));
        }

        AddHistory(run[0], 9, 10, 11);
        var layoutSettings = new LayoutSettings
        {
            AheadGainingTimeColor = Color.Green,
            AheadLosingTimeColor = Color.LightGreen,
            BehindGainingTimeColor = Color.IndianRed,
            BehindLosingTimeColor = Color.Red,
            BestSegmentColor = Color.Gold,
            ShowBestSegments = true,
        };
        return new LiveSplitState(run, null!, null!, layoutSettings, null!);
    }

    private static void AddHistory(ISegment segment, params double[] seconds)
    {
        for (int index = 0; index < seconds.Length; index++)
        {
            segment.SegmentHistory[index + 1] = new Time(
                TimingMethod.RealTime,
                TimeSpan.FromSeconds(seconds[index]));
        }
    }

    private static Color? GetBehindColor(LiveSplitState state, int splitNumber)
        => LiveSplitStateHelper.GetSplitColor(
            state,
            TimeSpan.FromSeconds(1),
            splitNumber,
            showSegmentDeltas: false,
            showBestSegments: false,
            Run.PersonalBestComparisonName,
            TimingMethod.RealTime);
}
