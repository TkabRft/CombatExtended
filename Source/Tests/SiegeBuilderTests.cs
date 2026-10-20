using System.Linq;
using CombatExtended.HarmonyCE;
using RimWorld;
using Verse;
using Xunit;

namespace Tests;

public class SiegeBuilderTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void MissingTrackersRejectAnOtherwiseEligiblePawn(bool hasMindState, bool hasSkills, bool hasWorkSettings)
    {
        var pawn = new Pawn
        {
            mindState = hasMindState ? new PawnMindState() : null,
            skills = hasSkills ? new object() : null,
            workSettings = hasWorkSettings ? new object() : null
        };

        Assert.False(CanBeBuilder(pawn));
    }

    [Fact]
    public void NullPawnIsRejected()
    {
        Assert.False(CanBeBuilder(null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidPawnPreservesVanillaEligibility(bool vanillaResult)
    {
        Assert.Equal(vanillaResult, CanBeBuilder(new Pawn(), vanillaResult));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ExistingInvalidBuilderReturnsToDefence(bool hasSkills, bool hasWorkSettings)
    {
        var pawn = Builder();
        pawn.skills = hasSkills ? new object() : null;
        pawn.workSettings = hasWorkSettings ? new object() : null;
        var siege = Siege(pawn);

        Harmony_LordToil_Siege_UpdateAllDuties.Prefix(siege);

        Assert.Same(DutyDefOf.Defend, pawn.mindState.duty.def);
        Assert.False(CanBeBuilder(pawn));
        Assert.Same(pawn, Assert.Single(siege.lord.ownedPawns));
        Assert.Equal(1, siege.DefenderAssignments);

        Harmony_LordToil_Siege_UpdateAllDuties.Prefix(siege);
        Assert.Equal(1, siege.DefenderAssignments);
    }

    [Fact]
    public void MixedGroupKeepsValidBuildersAndOtherDuties()
    {
        var builder = Builder();
        var originalBuilderDuty = builder.mindState.duty;
        var drone = Builder();
        drone.skills = null;
        var defender = new Pawn { skills = null };
        defender.mindState.duty = new PawnDuty(DutyDefOf.Defend);
        var originalDefenderDuty = defender.mindState.duty;
        var unassigned = new Pawn { workSettings = null };
        var siege = Siege(builder, drone, defender, unassigned);

        Harmony_LordToil_Siege_UpdateAllDuties.Prefix(siege);

        Assert.Same(originalBuilderDuty, builder.mindState.duty);
        Assert.Same(DutyDefOf.Defend, drone.mindState.duty.def);
        Assert.Same(originalDefenderDuty, defender.mindState.duty);
        Assert.Null(unassigned.mindState.duty);
        Assert.Same(builder, Assert.Single(siege.lord.ownedPawns.Where(p => CanBeBuilder(p))));
        Assert.Equal(4, siege.lord.ownedPawns.Count);
        Assert.Equal(1, siege.DefenderAssignments);
    }

    [Fact]
    public void GroupWithoutEligibleBuildersLeavesNoInvalidBuildDuties()
    {
        var noSkills = Builder();
        noSkills.skills = null;
        var noWorkSettings = Builder();
        noWorkSettings.workSettings = null;
        var siege = Siege(noSkills, noWorkSettings);

        Harmony_LordToil_Siege_UpdateAllDuties.Prefix(siege);

        Assert.Empty(siege.lord.ownedPawns.Where(p => CanBeBuilder(p)));
        Assert.All(siege.lord.ownedPawns, p => Assert.Same(DutyDefOf.Defend, p.mindState.duty.def));
        Assert.Equal(2, siege.DefenderAssignments);
    }

    private static bool CanBeBuilder(Pawn pawn, bool vanillaResult = true)
    {
        Harmony_LordToil_Siege_CanBeBuilder.Postfix(pawn, ref vanillaResult);
        return vanillaResult;
    }

    private static Pawn Builder()
    {
        var pawn = new Pawn();
        pawn.mindState.duty = new PawnDuty(DutyDefOf.Build);
        return pawn;
    }

    private static LordToil_Siege Siege(params Pawn[] pawns)
    {
        var siege = new LordToil_Siege();
        siege.lord.ownedPawns.AddRange(pawns);
        return siege;
    }
}
