using CombatExtended.Utilities;
using Xunit;

namespace Tests;

public class BipodTests
{
    private sealed class Verb
    {
        internal BipodValues Values = new(30f, 4f, 1f);
        internal string Projectile = "main";
        internal int BurstCount = 6;

        internal Verb Clone() => (Verb)MemberwiseClone();
    }

    private static BipodVerbState<Verb> NewState() => new(verb => verb.Clone());

    private static Verb Apply(BipodVerbState<Verb> state, Verb current, bool? deployed)
    {
        return state.Apply(current, copy =>
        {
            if (deployed.HasValue)
            {
                copy.Values = Preview(copy, deployed.Value);
            }
        });
    }

    private static BipodValues Preview(Verb baseline, bool deployed) =>
        baseline.Values.Modified(deployed ? 12f : 0f, deployed ? 0.9f : 1.5f, deployed ? 0.85f : 1.5f);

    private static void AssertValues(BipodValues actual, float range, float recoil, float warmup)
    {
        Assert.Equal(range, actual.Range, 5);
        Assert.Equal(recoil, actual.Recoil, 5);
        Assert.Equal(warmup, actual.Warmup, 5);
    }

    [Fact]
    public void DeployRetractAndResetDoNotAccumulateModifiers()
    {
        var source = new Verb();
        var state = NewState();
        var current = source;
        for (int i = 0; i < 20; i++)
        {
            current = Apply(state, current, false);
            AssertValues(current.Values, 30f, 6f, 1.5f);
            current = Apply(state, current, true);
            current = Apply(state, current, true);
            AssertValues(current.Values, 42f, 3.6f, 0.85f);
            current = Apply(state, current, null);
            AssertValues(current.Values, 30f, 4f, 1f);
        }
        AssertValues(source.Values, 30f, 4f, 1f);
    }

    [Theory]
    [InlineData(true, 42f, 3.6f, 0.85f)]
    [InlineData(false, 30f, 6f, 1.5f)]
    public void LoadReconstructionMatchesLiveTransitions(bool deployed, float range, float recoil, float warmup)
    {
        var source = new Verb();
        var loadedState = NewState();
        var loaded = Apply(loadedState, source, deployed);
        loaded = Apply(loadedState, loaded, deployed);
        var liveState = NewState();
        var live = Apply(liveState, Apply(liveState, source, false), deployed);
        AssertValues(loaded.Values, range, recoil, warmup);
        AssertValues(live.Values, range, recoil, warmup);
    }

    [Fact]
    public void UnequipThenEquipRestoresTheUndeployedPenalty()
    {
        var state = NewState();
        var current = Apply(state, new Verb(), true);
        current = Apply(state, current, null);
        AssertValues(current.Values, 30f, 4f, 1f);
        current = Apply(state, current, false);
        AssertValues(current.Values, 30f, 6f, 1.5f);
    }

    [Fact]
    public void WeaponsSharingDefinitionPropertiesHaveIndependentState()
    {
        var definition = new Verb();
        var firstState = NewState();
        var secondState = NewState();
        var deployed = Apply(firstState, definition, true);
        var undeployed = Apply(secondState, definition, false);
        AssertValues(deployed.Values, 42f, 3.6f, 0.85f);
        AssertValues(undeployed.Values, 30f, 6f, 1.5f);
        AssertValues(definition.Values, 30f, 4f, 1f);
        Assert.NotSame(deployed, undeployed);
    }

    [Theory]
    [InlineData("trait", 3)]
    [InlineData("underbarrel", 1)]
    public void ReplacementPropertiesBecomeTheBaselineWithoutLosingOtherFields(string projectile, int burst)
    {
        var state = NewState();
        var main = new Verb();
        _ = Apply(state, main, true);
        var replacement = new Verb { Values = new(10f, 2f, 2f), Projectile = projectile, BurstCount = burst };
        Assert.False(state.Owns(replacement));
        var current = Apply(state, replacement, true);
        AssertValues(current.Values, 22f, 1.8f, 1.7f);
        current = Apply(state, current, false);
        AssertValues(current.Values, 10f, 3f, 3f);
        current = Apply(state, current, null);
        AssertValues(current.Values, 10f, 2f, 2f);
        Assert.Equal(projectile, current.Projectile);
        Assert.Equal(burst, current.BurstCount);
        AssertValues(replacement.Values, 10f, 2f, 2f);
        current = Apply(state, main, true);
        AssertValues(current.Values, 42f, 3.6f, 0.85f);
        Assert.Equal("main", current.Projectile);
        Assert.Equal(6, current.BurstCount);
    }

    [Fact]
    public void PreviewBeforeInitializationCannotCaptureOrModifyRuntimeState()
    {
        var state = NewState();
        var source = new Verb();
        var preview = state.CopyBaseline(source);
        Assert.NotSame(source, preview);
        preview.Values = preview.Values.Modified(100f, 0f, 0f);
        AssertValues(source.Values, 30f, 4f, 1f);
        Assert.False(state.Owns(source));
        source.Values = new(50f, 8f, 2f);
        var current = Apply(state, source, true);
        AssertValues(current.Values, 62f, 7.2f, 1.7f);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void LmgPreviewsRemainCorrectAcrossAllRuntimeStates(bool? deployed)
    {
        var state = NewState();
        var current = Apply(state, new Verb(), deployed);
        var before = current.Values;
        var baseline = state.CopyBaseline(current);
        AssertValues(Preview(baseline, true), 42f, 3.6f, 0.85f);
        AssertValues(Preview(baseline, false), 30f, 6f, 1.5f);
        baseline.Values = new(0f, 0f, 0f);
        AssertValues(current.Values, before.Range, before.Recoil, before.Warmup);
        Assert.True(state.Owns(current));
        AssertValues(state.CopyBaseline(current).Values, 30f, 4f, 1f);
    }

    [Fact]
    public void PreviewOfReplacementDoesNotChangeTrackedBaseline()
    {
        var state = NewState();
        var current = Apply(state, new Verb(), true);
        var replacement = new Verb { Values = new(20f, 8f, 4f) };
        AssertValues(Preview(state.CopyBaseline(replacement), true), 32f, 7.2f, 3.4f);
        Assert.True(state.Owns(current));
        Assert.False(state.Owns(replacement));
        AssertValues(state.CopyBaseline(current).Values, 30f, 4f, 1f);
    }

    [Fact]
    public void ZeroMultipliersDoNotRequireReversingModifiedValues()
    {
        var state = NewState();
        var current = state.Apply(new Verb(), copy => copy.Values = copy.Values.Modified(12f, 0f, 0f));
        AssertValues(current.Values, 42f, 0f, 0f);
        AssertValues(Preview(state.CopyBaseline(current), true), 42f, 3.6f, 0.85f);
        current = Apply(state, current, false);
        AssertValues(current.Values, 30f, 6f, 1.5f);
    }

    [Fact]
    public void MissingVerbHasNoBaselineAndCannotInvokeTransformation()
    {
        var state = NewState();
        Assert.Null(state.CopyBaseline(null));
        Assert.Null(state.Apply(null, _ => Assert.Fail("Missing verb must not be transformed")));
        Assert.False(state.Owns(null));
    }
}
