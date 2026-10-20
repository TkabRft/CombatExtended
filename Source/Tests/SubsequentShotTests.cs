using CombatExtended.Utilities;
using Xunit;

namespace Tests;

public class SubsequentShotTests
{
    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0f, 90f, 90f)]
    [InlineData(0f, 180f, 180f)]
    [InlineData(180f, 0f, 180f)]
    [InlineData(89f, -269f, 2f)]
    [InlineData(-269f, 89f, 2f)]
    [InlineData(359f, 1f, 2f)]
    [InlineData(-720f, 720f, 0f)]
    public void TurningUsesTheShortestAngle(float previous, float current, float expected)
    {
        Assert.Equal(expected, SubsequentShotState.AngleDifference(previous, current));
    }

    [Fact]
    public void FirstBurstHasNoRepeatBonus()
    {
        var state = new SubsequentShotState();

        Assert.False(state.CanReduceWarmup(true, true));
        Assert.Equal(1f, state.GetWarmupMultiplier(0f, 0f, 0.25f));
        Assert.False(state.HasPreviousShot);

        Fire(state);

        Assert.True(state.CanReduceWarmup(true, true));
        Assert.Equal(1f, state.PreviousMultiplier);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    public void RepeatedBurstsAccumulateToTheModeFloor(float modeFloor)
    {
        var state = FiredState();
        float[] ordinaryExpected = [0.8f, 0.64f, 0.512f, 0.4096f, 0.32768f, 0.262144f];
        if (modeFloor == 0.25f)
        {
            foreach (float expected in ordinaryExpected)
            {
                Assert.Equal(expected, state.GetWarmupMultiplier(0f, 0.8f, modeFloor), 5);
                Fire(state);
            }
        }

        for (int burst = 0; burst < 30; burst++)
        {
            state.GetWarmupMultiplier(0f, 0.8f, modeFloor);
            Fire(state);
        }

        Assert.Equal(modeFloor, state.PreviousMultiplier);
    }

    [Fact]
    public void WarmupAndAimingShareOneSnapshotAndCommitOnlyOnEmission()
    {
        var state = FiredState();

        Assert.Equal(0.8f, state.GetWarmupMultiplier(0f, 0.8f, 0.25f));
        // Changing live inputs cannot replace the snapshot during the aiming phase.
        Assert.Equal(0.8f, state.GetWarmupMultiplier(5f, 3f, 0.5f));
        Assert.Equal(0.8f, state.GetWarmupMultiplier(0f, 0.8f, 0.5f));
        Assert.Equal(1f, state.PreviousMultiplier);

        Fire(state);

        Assert.Equal(0.8f, state.PreviousMultiplier);
        Assert.Equal(0.64f, state.GetWarmupMultiplier(0f, 0.8f, 0.25f), 5);
    }

    [Fact]
    public void AimingFloorDoesNotMultiplyTheOrdinaryPhaseAgain()
    {
        var state = FiredState();

        Assert.Equal(0.4f, state.GetWarmupMultiplier(0f, 0.4f, 0.25f));
        Assert.Equal(0.5f, state.GetWarmupMultiplier(0f, 0.4f, 0.5f));
        Fire(state);

        Assert.Equal(0.5f, state.PreviousMultiplier);
    }

    [Fact]
    public void RecoilContainsOnlyTheMostRecentBurst()
    {
        var state = FiredState(0.6f);
        state.RecordShot(false, 0.6f, true, true);
        Assert.Equal(1.2f, state.BurstRecoil, 5);

        state.GetWarmupMultiplier(0f, 0.8f, 0.25f);
        Fire(state, 0.4f);

        Assert.Equal(0.4f, state.BurstRecoil);
        Assert.Equal(0.8f, state.PreviousMultiplier);
    }

    [Fact]
    public void LaterShotsInABurstDoNotCommitTheBonusAgain()
    {
        var state = FiredState();
        state.GetWarmupMultiplier(0f, 0.8f, 0.25f);
        Fire(state);

        for (int shot = 0; shot < 10; shot++)
        {
            state.RecordShot(false, 0.1f, true, true);
            Assert.Equal(0.8f, state.PreviousMultiplier);
        }
    }

    [Fact]
    public void MultiplePelletsCountAsOneShotForRecoilAndAccumulation()
    {
        var state = FiredState();
        state.GetWarmupMultiplier(0f, 0.8f, 0.25f);

        for (int pellet = 0; pellet < 8; pellet++)
        {
            Assert.Equal(pellet == 0, state.RecordShot(true, 0.3f, true, true, firstPellet: pellet == 0));
        }
        Assert.Equal(0.8f, state.PreviousMultiplier);
        Assert.Equal(0.3f, state.BurstRecoil);

        for (int pellet = 0; pellet < 8; pellet++)
        {
            state.RecordShot(false, 0.3f, true, true, firstPellet: pellet == 0);
        }
        Assert.Equal(0.8f, state.PreviousMultiplier);
        Assert.Equal(0.6f, state.BurstRecoil, 5);
    }

    [Theory]
    [InlineData(0f, 0.4f, 0.4f)]
    [InlineData(0.4f, 0.4f, 0.8f)]
    [InlineData(0f, 0.9f, 0.9f)]
    [InlineData(2f, 0.4f, 1f)]
    public void TurningAndRecoilPenaltiesWeakenTheBonus(float angle, float recoil, float expected)
    {
        var state = FiredState();
        Assert.Equal(expected, state.GetWarmupMultiplier(angle, recoil, 0.25f), 5);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    public void ZeroRecoilStillRespectsTheModeFloor(float modeFloor)
    {
        var state = FiredState(0f);
        Assert.Equal(modeFloor, state.GetWarmupMultiplier(0f, 0f, modeFloor));
    }

    [Fact]
    public void FailedCastDiscardsOnlyThePendingWarmup()
    {
        var state = FiredState(0.7f);
        state.GetWarmupMultiplier(0f, 0.8f, 0.25f);
        Fire(state, 0.7f);
        state.GetWarmupMultiplier(0f, 0.4f, 0.25f);

        state.CancelWarmup();

        Assert.Equal(0.8f, state.PreviousMultiplier);
        Assert.Equal(0.7f, state.BurstRecoil);
        Assert.Equal(0.72f, state.GetWarmupMultiplier(0f, 0.9f, 0.25f), 5);
    }

    [Fact]
    public void PreparingAnAbortedFirstCastDoesNotCreateHistory()
    {
        var state = new SubsequentShotState();
        state.GetWarmupMultiplier(0f, 0.4f, 0.25f);
        state.CancelWarmup();

        Assert.False(state.HasPreviousShot);
        Assert.Equal(0f, state.BurstRecoil);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void DisabledOrMagazineLessWarmupClearsHistory(bool enabled, bool hasMagazine)
    {
        var state = FiredState();
        state.GetWarmupMultiplier(0f, 0.4f, 0.25f);

        Assert.False(state.CanReduceWarmup(enabled, hasMagazine));
        Assert.False(state.CanReduceWarmup(true, true));
        Assert.Equal(1f, state.PreviousMultiplier);
        Assert.Equal(0f, state.BurstRecoil);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ExcludedShotsCannotRebuildHistory(bool enabled, bool hasMagazine)
    {
        var state = FiredState();
        state.RecordShot(true, 1f, enabled, hasMagazine);

        Assert.False(state.HasPreviousShot);
        Assert.Equal(0f, state.BurstRecoil);
    }

    [Fact]
    public void ResetForReloadTargetResetOrLoadDropsAllTransientHistory()
    {
        var state = FiredState();
        state.GetWarmupMultiplier(0f, 0.8f, 0.25f);
        Fire(state, 2f);
        state.GetWarmupMultiplier(0f, 0.4f, 0.25f);

        state.Reset();

        Assert.False(state.HasPreviousShot);
        Assert.Equal(1f, state.GetWarmupMultiplier(0f, 0f, 0.25f));
        Assert.Equal(1f, state.PreviousMultiplier);
        Assert.Equal(0f, state.BurstRecoil);
        Fire(state, 0.3f);
        Assert.Equal(1f, state.PreviousMultiplier);
        Assert.Equal(0.3f, state.BurstRecoil);
    }

    [Fact]
    public void ResumingMidBurstAfterLoadStartsANeutralChain()
    {
        var state = new SubsequentShotState();
        state.RecordShot(false, 0.3f, true, true);

        Assert.True(state.HasPreviousShot);
        Assert.Equal(1f, state.PreviousMultiplier);
        Assert.Equal(0.3f, state.BurstRecoil);
    }

    private static SubsequentShotState FiredState(float recoil = 0.1f)
    {
        var state = new SubsequentShotState();
        Fire(state, recoil);
        return state;
    }

    private static void Fire(SubsequentShotState state, float recoil = 0.1f)
    {
        state.RecordShot(true, recoil, true, true);
    }
}
