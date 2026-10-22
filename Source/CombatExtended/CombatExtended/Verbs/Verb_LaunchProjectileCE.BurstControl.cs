using System;
using System.Collections.Generic;
using CombatExtended.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CombatExtended;

public partial class Verb_LaunchProjectileCE
{
    private readonly BurstControlState burstControl = new BurstControlState();
    private bool adaptiveRoundPrepared, adaptiveRoundRetargeted, holdAdaptiveSolution, reconstructBurstControl;
    private Pawn stoppedBurstTarget;

    private bool UsesAdaptiveBurstControl => UsesBurstTracking && VerbPropsCE.interruptibleBurst
        && ShooterPawn.skills?.GetSkill(SkillDefOf.Shooting) != null;

    private static bool Incapacitated(Pawn pawn) => pawn.Dead || pawn.Downed || pawn.Destroyed;

    private bool IsStoppedBurstTarget(LocalTargetInfo target)
        => Controller.settings.StopBurstAfterTargetFalls && stoppedBurstTarget != null
            && target.Thing == stoppedBurstTarget && Incapacitated(stoppedBurstTarget);

    private void ResetAdaptiveBurst(bool? reconstruct = null)
    {
        burstControl.Reset();
        adaptiveRoundPrepared = adaptiveRoundRetargeted = holdAdaptiveSolution = false;
        if (reconstruct.HasValue) reconstructBurstControl = reconstruct.Value;
    }

    // Called only by the standard shooting loop, before even magazine-less ammo preparation.
    // Compatibility firing loops retain the existing protected Retarget entry point.
    protected bool PrepareAdaptiveBurstRound()
    {
        adaptiveRoundPrepared = adaptiveRoundRetargeted = holdAdaptiveSolution = false;
        if (!UsesAdaptiveBurstControl) return true;
        if (IsStoppedBurstTarget(currentTarget)) return StopAdaptiveBurst();
        Pawn pawn = currentTarget.Pawn;
        if (pawn == null) return true; // An explicit cell order remains area fire.

        bool standing = pawn.Spawned && !Incapacitated(pawn);
        if (standing && stoppedBurstTarget == pawn) stoppedBurstTarget = null;
        Vector3 position = pawn.DrawPos;
        if (!burstControl.Initialized || burstControl.CurrentId != pawn.thingIDNumber)
        {
            burstControl.Begin(pawn.thingIDNumber, standing, ToShotVector(position), reconstructBurstControl);
            if (reconstructBurstControl)
            {
                Vector3 restored = lastTargetPos.IsValid ? lastTargetPos.ToVector3Shifted() : position;
                burstControl.RecordSolution(ToShotVector(restored), ShotHeight, lastShotRotation, lastShotAngle * Mathf.Rad2Deg);
            }
            reconstructBurstControl = false;
        }
        adaptiveRoundPrepared = true;
        bool retarget = Controller.settings.MidBurstRetarget && doRetarget;
        bool clearLine = standing && CanHitFromCellIgnoringRange(caster.TrueCenter().WithY(ShotHeight), pawn, out _);
        burstControl.Observe(clearLine, ToShotVector(position));

        if (standing)
        {
            // Retain the previous alive-target LoS-loss retarget opportunity, now bounded.
            if (MidBurst && retarget && !clearLine)
            {
                burstControl.FreezeAnchor();
                adaptiveRoundRetargeted = SelectBurstReplacement();
            }
            return true;
        }

        BurstControlAction action = burstControl.React(Incapacitated(pawn), retarget,
            Controller.settings.StopBurstAfterTargetFalls, ShooterPawn.skills.GetSkill(SkillDefOf.Shooting).Level,
            ShooterPawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness),
            () => Rand.Value, SelectBurstReplacement);
        adaptiveRoundRetargeted = action == BurstControlAction.Retargeted;
        if (action == BurstControlAction.Stop) return StopAdaptiveBurst();
        holdAdaptiveSolution = action == BurstControlAction.Hold && burstControl.HasSolution;
        return true;
    }

    private bool StopAdaptiveBurst()
    {
        stoppedBurstTarget = currentTarget.Pawn;
        // Returning false follows the normal scheduler's completion/cooldown path.
        CancelSubsequentShotWarmup();
        return false;
    }

    private bool ValidBurstReplacement(Pawn pawn)
    {
        return pawn != currentTarget.Thing && pawn.Spawned && pawn.Map == caster.Map && !Incapacitated(pawn)
            && Shooter.HostileTo(pawn) && !pawn.Position.Fogged(caster.Map)
            && !pawn.ThreatDisabled(ShooterPawn) && AttackTargetFinder.IsAutoTargetable(pawn)
            && targetParams.CanTarget(new TargetInfo(pawn), this) && Shooter.CanSee(pawn, null)
            && CanHitTargetFrom(caster.Position, pawn);
    }

    private bool ScoreBurstReplacement(Pawn pawn, out double angle, out double distance)
    {
        angle = distance = double.PositiveInfinity;
        Vector3 position = pawn.DrawPos;
        if ((ToShotVector(position) - burstControl.Anchor).Horizontal > 5 || !ValidBurstReplacement(pawn)) return false;
        ShiftVecReport report = ShiftVecReportFor(new LocalTargetInfo(pawn));
        Vector2 lead = projectilePropsCE.isInstant ? Vector2.zero : report.GetLeadVec(0);
        ShotVector clean = new ShotVector(position.x + lead.x, 0, position.z + lead.y);
        return BurstControlState.CandidateScore(ToShotVector(caster.TrueCenter()), burstControl.Anchor,
            ToShotVector(position), clean, true, out angle, out distance);
    }

    private bool SelectBurstReplacement()
    {
        var candidates = new List<(Pawn pawn, double angle, double distance)>();
        foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            if (ScoreBurstReplacement(pawn, out double angle, out double distance)) candidates.Add((pawn, angle, distance));
        candidates.Sort((a, b) => BurstControlState.Better(a.angle, a.distance, a.pawn.thingIDNumber,
            b.angle, b.distance, b.pawn.thingIDNumber) ? -1 :
            BurstControlState.Better(b.angle, b.distance, b.pawn.thingIDNumber,
                a.angle, a.distance, a.pawn.thingIDNumber) ? 1 : 0);
        foreach (var candidate in candidates)
        {
            if (!ScoreBurstReplacement(candidate.pawn, out _, out _)) continue;
            currentTarget = new LocalTargetInfo(candidate.pawn);
            burstControl.Switch(candidate.pawn.thingIDNumber);
            lastTarget = currentTarget;
            lastTargetPos = currentTarget.Cell;
            shootingAtDowned = false;
            // Do not rotate a manned turret top directly: tracking retains the previous solution.
            return true;
        }
        return false;
    }

    private void RecordAdaptiveSolution(Vector3 position, float height)
    {
        if (!adaptiveRoundPrepared || holdAdaptiveSolution || currentTarget.Pawn == null) return;
        burstControl.RecordSolution(ToShotVector(position), height, burstTracking.Rotation, burstTracking.Elevation);
    }
}
