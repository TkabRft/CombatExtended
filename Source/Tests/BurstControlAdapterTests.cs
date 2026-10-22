using CombatExtended;
using Verse;
using Xunit;

namespace Tests;

public class BurstControlAdapterTests
{
    private static (Verb_LaunchProjectileCE verb, Pawn target) Scene(int skill = 20)
    {
        Controller.settings = new(); Rand.Calls = 0; Rand.Sample = 0.9f;
        var map = new Map();
        var shooter = new Pawn { Map = map, thingIDNumber = 1, skills = new FixtureSkills() };
        ((FixtureSkills)shooter.skills).Skill.Level = skill;
        var target = new Pawn { Map = map, thingIDNumber = 100, Faction = 2, DrawPos = new(0, 0, 30) };
        map.mapPawns.AllPawnsSpawned.Add(target);
        return (new Verb_LaunchProjectileCE { caster = shooter, ShooterPawn = shooter, currentTarget = target }, target);
    }

    private static Pawn Add(Verb_LaunchProjectileCE verb, int id = 2, float x = 1, float z = 30)
    {
        var pawn = new Pawn { Map = verb.caster.Map, thingIDNumber = id, Faction = 3, DrawPos = new(x, 0, z) };
        pawn.Map.mapPawns.AllPawnsSpawned.Add(pawn);
        return pawn;
    }

    [Fact]
    public void FirstRoundInstantKillStopsBeforeAmmoPreparationAndKeepsCommittedRecovery()
    {
        var (verb, target) = Scene();
        Assert.True(verb.Emit(firstPelletImpact: () => { target.Dead = true; target.Spawned = false; }));
        float recoil = verb.Recovery.BurstRecoil;
        Assert.False(verb.Emit());
        Assert.Equal(1, verb.AmmoPrepared); Assert.Equal(19, verb.Ammo);
        Assert.Equal(1, verb.Emitted); Assert.Equal(1, verb.CompletionCalls); Assert.Equal(30, verb.Cooldown);
        Assert.Equal(recoil, verb.Recovery.BurstRecoil); Assert.True(verb.Recovery.HasPreviousShot);
        Assert.True(verb.Cancelled); Assert.Equal(0, Rand.Calls);
    }

    [Fact]
    public void NoAutomaticRestartWithoutNewFinishingOrder()
    {
        var (verb, target) = Scene(); verb.Emit(); target.Downed = true;
        Assert.False(verb.Emit()); Assert.True(verb.Blocked(target));
        verb.BeginAgain();
        Assert.False(verb.Prepare()); Assert.Equal(1, verb.AmmoPrepared);
        verb.Force(target); verb.BeginAgain();
        Assert.True(verb.Emit()); Assert.True(verb.Control.FinishingOrder);
    }

    [Fact]
    public void RetargetsOtherHostileFactionsWithoutResettingBurstOrAmmo()
    {
        var (verb, target) = Scene();
        var replacement = Add(verb);
        verb.burstTracking.Advance(359, 2, 100, 60, true);
        verb.Emit(); target.Downed = true;
        Assert.True(verb.Emit()); Assert.Same(replacement, verb.currentTarget.Pawn);
        Assert.Equal(2, verb.Emitted); Assert.Equal(18, verb.Ammo); Assert.Equal(359, verb.burstTracking.Rotation);
        Assert.Equal(0, Rand.Calls); Assert.True(verb.Retargeted);
    }

    [Theory]
    [InlineData("neutral")] [InlineData("ally")] [InlineData("downed")] [InlineData("dead")]
    [InlineData("destroyed")] [InlineData("despawned")] [InlineData("disabled")]
    [InlineData("notAutoTargetable")] [InlineData("wrongParams")] [InlineData("invisible")]
    [InlineData("blocked")] [InlineData("range")] [InlineData("distant")] [InlineData("leadAngle")]
    [InlineData("fog")]
    public void CandidateMustPassAllEligibilityAndGeometryChecks(string reason)
    {
        var (verb, target) = Scene(); var candidate = Add(verb);
        switch (reason)
        {
            case "neutral": candidate.Faction = 0; break;
            case "ally": candidate.Faction = 1; break;
            case "downed": candidate.Downed = true; break;
            case "dead": candidate.Dead = true; break;
            case "destroyed": candidate.Destroyed = true; break;
            case "despawned": candidate.Spawned = false; break;
            case "disabled": candidate.DisabledThreat = true; break;
            case "notAutoTargetable": candidate.AutoTargetable = false; break;
            case "wrongParams": candidate.Targetable = false; break;
            case "invisible": candidate.Visible = false; break;
            case "blocked": candidate.ClearLine = false; break;
            case "range": candidate.InRange = false; break;
            case "distant": candidate.DrawPos = new(6, 0, 30); break;
            case "leadAngle": candidate.Lead = new(10, 0); break;
            case "fog": candidate.Map.Fog = true; break;
        }
        verb.Emit(); target.Downed = true;
        Assert.False(verb.Emit()); Assert.Same(target, verb.currentTarget.Pawn);
    }

    [Fact]
    public void StableSelectionAndReplacementLossKeepOriginalAnchor()
    {
        var (verb, target) = Scene();
        var second = Add(verb, 3, 0, 34); var chosen = Add(verb, 2, 0, 34);
        var distant = Add(verb, 4, 0, 38);
        verb.Emit(); target.Downed = true; verb.Emit();
        Assert.Same(chosen, verb.currentTarget.Pawn);
        chosen.Downed = true; second.Downed = true;
        Assert.False(verb.Emit());
        Assert.Equal(30, verb.Control.Anchor.Z);
        Assert.NotSame(distant, verb.currentTarget.Pawn);
    }

    [Fact]
    public void AliveTargetLosRetargetUsesBoundsWithoutDeathReaction()
    {
        var (verb, target) = Scene(0); var replacement = Add(verb);
        verb.Emit(); target.ClearLine = false;
        Assert.True(verb.Prepare()); Assert.Same(replacement, verb.currentTarget.Pawn);
        Assert.Equal(0, Rand.Calls);
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    public void SettingsWorkIndependentlyInPreflight(bool retarget, bool stop, bool emitted, bool changed)
    {
        var (verb, target) = Scene(); var replacement = Add(verb);
        Controller.settings.MidBurstRetarget = retarget; Controller.settings.StopBurstAfterTargetFalls = stop;
        verb.Emit(); target.Downed = true;
        Assert.Equal(emitted, verb.Emit());
        Assert.Equal(changed, verb.currentTarget.Pawn == replacement);
        Assert.Equal(0, Rand.Calls);
    }

    [Theory]
    [InlineData(1)] [InlineData(8)]
    public void FailedReactionRollsOncePerRoundNotPerPellet(int pellets)
    {
        var (verb, target) = Scene(10);
        verb.Emit(pellets); target.Downed = true;
        Assert.True(verb.Emit(pellets));
        Assert.Equal(1, Rand.Calls); Assert.True(verb.Holding);
        Assert.Equal(1.2, verb.Control.Height, 6);
        Controller.settings.StopBurstAfterTargetFalls = false;
        Rand.Sample = 0; verb.Emit(pellets); Assert.Equal(2, Rand.Calls);
        verb.Emit(pellets); Assert.Equal(2, Rand.Calls); // Awareness stays latched.
    }

    [Fact]
    public void ImpairedExpertAndDelayedImpactOnlyReactAfterActualIncapacitation()
    {
        var (verb, target) = Scene();
        verb.ShooterPawn.health.capacities.Consciousness = 0.5f;
        verb.Emit(); verb.Emit(); Assert.Equal(0, Rand.Calls);
        target.Downed = true;
        Assert.True(verb.Emit()); Assert.Equal(1, Rand.Calls);
        Rand.Sample = 0;
        Assert.False(verb.Emit()); Assert.Equal(2, Rand.Calls);
    }

    [Fact]
    public void LoadedDownedTargetIsNotAssumedToBeAFinishingOrder()
    {
        var (verb, target) = Scene(); verb.Emit(); target.Downed = true;
        verb.Reconstruct(); verb.ResetTargeting(); verb.BeginAgain();
        Assert.False(verb.Emit()); Assert.False(verb.Control.FinishingOrder);
    }

    [Fact]
    public void CellSuppressionAndExcludedWeaponsBypassController()
    {
        var (verb, target) = Scene();
        verb.currentTarget = new LocalTargetInfo(new IntVec3(0, 30));
        Assert.True(verb.Prepare()); Assert.False(verb.Control.Initialized);
        verb.currentTarget = target;
        verb.VerbPropsCE.interruptibleBurst = false;
        Assert.True(verb.Prepare()); Assert.False(verb.Control.Initialized);
        verb.VerbPropsCE.interruptibleBurst = true; verb.UsesBurstTracking = false;
        Assert.True(verb.Prepare()); Assert.False(verb.Control.Initialized);
        verb.UsesBurstTracking = true; verb.ShooterPawn.skills = null;
        Assert.True(verb.Prepare()); Assert.False(verb.Control.Initialized);
        Assert.Equal(0, Rand.Calls);
    }

    [Fact]
    public void DisablingStoppingReleasesGuardAndRevivalDoesNotInheritOldReaction()
    {
        var (verb, target) = Scene(10); verb.Emit(); target.Downed = true; Rand.Sample = 0;
        Assert.False(verb.Emit()); Assert.True(verb.Blocked(target));
        Controller.settings.StopBurstAfterTargetFalls = false;
        Assert.False(verb.Blocked(target));
        Controller.settings.StopBurstAfterTargetFalls = true;
        target.Downed = false; verb.BeginAgain();
        Assert.True(verb.Emit());
        target.Downed = true; Rand.Sample = 0.9f;
        Assert.True(verb.Emit()); Assert.True(verb.Holding);
    }

    [Fact]
    public void ExistingRetargetVetoRemainsEffective()
    {
        var (verb, target) = Scene(); Add(verb); verb.Emit(); target.Downed = true;
        verb.doRetarget = false;
        Assert.False(verb.Prepare()); Assert.Same(target, verb.currentTarget.Pawn);
    }
}
