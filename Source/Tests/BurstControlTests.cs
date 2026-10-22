using System;
using CombatExtended.Utilities;
using Xunit;

namespace Tests;

public class BurstControlTests
{
    [Theory]
    [InlineData(0, 0.002, 8.910478)]
    [InlineData(5, 0.159175, 4.173)]
    [InlineData(10, 0.398056, 1.497)]
    [InlineData(15, 0.682057, 0.466)]
    [InlineData(20, 1, 0)]
    public void ReactionCalibrationMatchesLargeIndependentSimulation(int skill, double chance, double expectedExtra)
    {
        Assert.InRange(Math.Abs(BurstControlState.ReactionChance(skill, 1) - chance), 0, 0.00001);
        var random = new Random(876543);
        long extra = 0;
        const int samples = 100000;
        for (int sample = 0; sample < samples; sample++)
        {
            var state = FiredState();
            for (int round = 0; round < 9; round++)
            {
                if (state.React(true, false, true, skill, 1, random.NextDouble, () => false) == BurstControlAction.Stop) break;
                extra++;
            }
        }
        Assert.InRange(Math.Abs(extra / (double)samples - expectedExtra), 0, 0.04);
    }

    [Fact]
    public void ConsciousnessIsContinuousSeverelyPenalizesImpairmentAndHasDiminishingEnhancement()
    {
        Assert.Equal(0.125, BurstControlState.ConsciousnessFactor(0.5), 10);
        Assert.Equal(1, BurstControlState.ConsciousnessFactor(1));
        Assert.InRange(BurstControlState.ConsciousnessFactor(2), 1.14, 1.15);
        const double step = 1e-6;
        double left = (1 - BurstControlState.ConsciousnessFactor(1 - step)) / step;
        double right = (BurstControlState.ConsciousnessFactor(1 + step) - 1) / step;
        Assert.InRange(Math.Abs(left - right), 0, 0.0001);
        double previous = 0;
        foreach (double value in new[] { 0.1, 0.5, 0.75, 1, 1.1, 2, 100, 1e10 })
        {
            double factor = BurstControlState.ConsciousnessFactor(value);
            Assert.True(factor > previous && factor < 1.15);
            previous = factor;
        }
        Assert.Equal(0.125, BurstControlState.ReactionChance(20, 0.5));
        Assert.Equal(1, BurstControlState.ReactionChance(20, 1));
        Assert.Equal(1, BurstControlState.ReactionChance(20, 100));
        Assert.True(double.IsFinite(BurstControlState.ReactionChance(float.MaxValue, float.MaxValue)));
        Assert.Equal(0, BurstControlState.ReactionChance(20, 0));
    }

    [Fact]
    public void ChanceIsMonotoneForSkillAndConsciousnessAndAlwaysFinite()
    {
        double previous = 0;
        for (int tenth = 0; tenth <= 200; tenth++)
        {
            double chance = BurstControlState.ReactionChance(tenth / 10.0, 0.75);
            Assert.InRange(chance, previous, 1); previous = chance;
        }
        foreach (int skill in new[] { 0, 10, 20 })
        {
            previous = 0;
            for (int tenth = 0; tenth <= 200; tenth++)
            {
                double chance = BurstControlState.ReactionChance(skill, tenth / 10.0);
                Assert.InRange(chance, previous, 1); previous = chance;
            }
        }
        Assert.Equal(BurstControlState.ReactionChance(0, 1), BurstControlState.ReactionChance(-1, 1));
        Assert.Equal(1, BurstControlState.ReactionChance(double.MaxValue, double.MaxValue));
    }

    [Theory]
    [InlineData(0, 45)] [InlineData(5, 45)] [InlineData(12.5, 27.5)] [InlineData(20, 10)] [InlineData(60, 10)]
    public void ConeWidensOnlyAtCloseRange(double distance, double angle)
        => Assert.Equal(angle, BurstControlState.AngleLimit(distance));

    [Fact]
    public void CandidatesMustPassDistanceAndAngleIncludingMovementLead()
    {
        var source = new ShotVector(); var anchor = new ShotVector(0, 0, 5);
        Assert.True(BurstControlState.CandidateScore(source, anchor, new(3, 0, 5), new(3, 0, 5), true, out _, out _));
        Assert.False(BurstControlState.CandidateScore(source, anchor, new(5.001, 0, 5), new(5.001, 0, 5), true, out _, out _));
        // Position qualifies, but the candidate's clean movement lead would sweep too far.
        Assert.False(BurstControlState.CandidateScore(source, anchor, new(3, 0, 5), new(6, 0, 5), true, out _, out _));
        Assert.False(BurstControlState.CandidateScore(source, anchor, anchor, anchor, false, out _, out _));
        Assert.True(BurstControlState.CandidateScore(source, anchor, new(0, 0, 10), new(0, 0, 10), true, out _, out double distance));
        Assert.Equal(5, distance);
    }

    [Fact]
    public void AngularChecksUseShortestDifferenceAcrossWraparound()
    {
        ShotVector Point(double angle) => new(-10 * Math.Sin(angle * Math.PI / 180), 0, 10 * Math.Cos(angle * Math.PI / 180));
        Assert.True(BurstControlState.CandidateScore(new(), Point(179), Point(-179), Point(-179), true, out double angle, out _));
        Assert.Equal(2, angle, 9);
    }

    [Fact]
    public void SelectionUsesAngleThenDistanceThenStableIdentity()
    {
        Assert.True(BurstControlState.Better(1, 4, 9, 2, 1, 1));
        Assert.True(BurstControlState.Better(1, 2, 9, 1, 3, 1));
        Assert.True(BurstControlState.Better(1, 2, 1, 1, 2, 9));
        Assert.False(BurstControlState.Better(1, 2, 1, 1, 2, 1));
    }

    [Fact]
    public void InitialAnchorFreezesAndCannotChainAwayThroughReplacements()
    {
        var state = FiredState();
        state.Observe(true, new(0, 0, 32));
        Assert.Equal(32, state.Anchor.Z);
        state.Observe(false, new(0, 0, 32));
        state.Switch(2);
        state.Observe(true, new(0, 0, 36));
        Assert.Equal(32, state.Anchor.Z);
        Assert.False(BurstControlState.CandidateScore(new(), state.Anchor, new(0, 0, 39), new(0, 0, 39), true, out _, out _));
        Assert.Equal(2, state.CurrentId);
    }

    [Theory]
    [InlineData(false, false, 0, 0, (int)BurstControlAction.Continue)]
    [InlineData(false, true, 1, 0, (int)BurstControlAction.Stop)]
    [InlineData(true, false, 1, 1, (int)BurstControlAction.Retargeted)]
    [InlineData(true, true, 1, 1, (int)BurstControlAction.Retargeted)]
    public void SettingsAreIndependent(bool retarget, bool stop, int draws, int searches, int action)
    {
        var state = FiredState(); int randomCalls = 0, selectCalls = 0;
        var result = state.React(true, retarget, stop, 10, 1, () => { randomCalls++; return 0; },
            () => { selectCalls++; return true; });
        Assert.Equal((BurstControlAction)action, result);
        Assert.Equal(draws, randomCalls); Assert.Equal(searches, selectCalls);
    }

    [Fact]
    public void FailedReactionHoldsSolutionAndSuccessfulAwarenessDoesNotReroll()
    {
        var state = FiredState();
        state.RecordSolution(new(0, 0, 30), 1.2, 359, 2);
        Assert.Equal(BurstControlAction.Hold, state.React(true, true, false, 10, 1, () => 0.9, () => throw new Exception()));
        Assert.False(state.Aware);
        Assert.Equal(1.2, state.Height); Assert.Equal(359, state.Rotation);
        Assert.Equal(BurstControlAction.Hold, state.React(true, true, false, 10, 1, () => 0, () => false));
        Assert.True(state.Aware);
        Assert.Equal(BurstControlAction.Retargeted, state.React(true, true, false, 10, 1,
            () => throw new Exception("Awareness must not reroll"), () => true));
        state.Switch(2);
        Assert.False(state.Aware);
        Assert.Equal(BurstControlAction.Hold, state.React(true, true, false, 10, 1, () => 0.9, () => false));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void CertainAndImpossibleReactionsDoNotConsumeRng(double consciousness)
    {
        var state = FiredState();
        Assert.Equal(consciousness == 0 ? BurstControlAction.Hold : BurstControlAction.Stop,
            state.React(true, false, true, 20, consciousness, () => throw new Exception(), () => false));
    }

    [Fact]
    public void IntentionalFinishingAndReconstructionHaveDifferentIntent()
    {
        var state = new BurstControlState();
        state.Begin(1, false, new(0, 0, 30)); state.RecordEmission();
        Assert.Equal(BurstControlAction.Continue, state.React(true, true, true, 20, 1, () => throw new Exception(), () => false));
        state.Begin(1, false, new(0, 0, 30), reconstructed: true);
        Assert.False(state.FinishingOrder);
        Assert.Equal(BurstControlAction.Stop, state.React(true, true, true, 20, 1, () => throw new Exception(), () => false));
        state.Reset();
        Assert.False(state.Initialized); Assert.False(state.CanReact); Assert.False(state.HasSolution);
    }

    [Fact]
    public void NoReactionBeforeEmissionAndLivingTargetsDoNotConsumeRng()
    {
        var state = new BurstControlState(); state.Begin(1, true, new(0, 0, 30));
        Assert.Equal(BurstControlAction.Continue, state.React(true, true, true, 20, 1, () => throw new Exception(), () => false));
        state.RecordEmission();
        Assert.Equal(BurstControlAction.Continue, state.React(false, true, true, 10, 1, () => throw new Exception(), () => false));
    }

    [Theory]
    [InlineData(1)] [InlineData(6)] [InlineData(20)]
    public void DelayedReactionCannotBankTrackingTime(int interval)
    {
        var state = FiredState(); state.RecordSolution(new(0, 0, 30), 1.2, 0, 0);
        var tracking = new BurstTrackingState(); tracking.Advance(0, 0, 0, 60, true);
        for (int round = 1; round <= 5; round++) state.AdvanceHeldTracking(tracking, interval * round);
        tracking.Advance(90, 45, interval * 6, 60, true);
        Assert.Equal(interval, tracking.Rotation);
        Assert.Equal(interval, tracking.Elevation);
    }

    private static BurstControlState FiredState()
    {
        var state = new BurstControlState();
        state.Begin(1, true, new(0, 0, 30)); state.RecordEmission();
        return state;
    }
}
