// Standalone host for the production burst preflight. This does not emulate the game scheduler.
using System;
using System.Collections.Generic;
using CombatExtended.Utilities;

namespace UnityEngine
{
    internal struct Vector3(float x, float y, float z)
    {
        internal float x = x, y = y, z = z;
    }
    internal struct Vector2(float x, float y)
    {
        internal float x = x, y = y;
        internal static Vector2 zero => new(0, 0);
    }
    internal static class Mathf
    {
        internal const float Rad2Deg = 180f / (float)Math.PI;
        internal const float Deg2Rad = (float)Math.PI / 180;
    }
}

namespace Verse
{
    internal class Thing
    {
        internal UnityEngine.Vector3 DrawPos;
        internal IntVec3 Position => DrawPos.ToIntVec3();
        internal Map Map;
        internal int thingIDNumber, Faction = 1;
        internal bool Spawned = true, Destroyed;
        internal UnityEngine.Vector3 TrueCenter() => DrawPos;
    }
    internal sealed partial class Pawn : Thing
    {
        internal bool Dead, Downed, DisabledThreat;
        internal bool AutoTargetable = true, Targetable = true, Visible = true, ClearLine = true, InRange = true;
        internal UnityEngine.Vector2 Lead;
        internal FixtureHealth health = new();
        internal bool HostileTo(Thing other) => Faction != 0 && Faction != other.Faction;
        internal bool ThreatDisabled(Pawn searcher) => DisabledThreat;
    }
    internal readonly record struct IntVec3(int x, int z)
    {
        internal static IntVec3 Invalid => new(-1000, -1000);
        internal bool IsValid => x >= 0 && z >= 0;
        internal UnityEngine.Vector3 ToVector3Shifted() => new(x + 0.5f, 0, z + 0.5f);
        internal bool Fogged(Map map) => map.Fog;
    }
    internal readonly struct LocalTargetInfo
    {
        internal readonly Thing Thing;
        private readonly IntVec3 cell;
        internal LocalTargetInfo(Thing thing) { Thing = thing; cell = default; }
        internal LocalTargetInfo(IntVec3 cell) { Thing = null; this.cell = cell; }
        internal Pawn Pawn => Thing as Pawn;
        internal IntVec3 Cell => Thing?.Position ?? cell;
        public static implicit operator LocalTargetInfo(Pawn pawn) => new(pawn);
    }
    internal readonly struct TargetInfo(Pawn pawn) { internal readonly Pawn Pawn = pawn; }
    internal sealed class Map
    {
        internal FixtureMapPawns mapPawns = new();
        internal bool Fog;
    }
    internal sealed class FixtureMapPawns { internal List<Pawn> AllPawnsSpawned = []; }
    internal sealed class FixtureSkills { internal FixtureSkill Skill = new(); }
    internal sealed class FixtureSkill { internal int Level = 20; }
    internal sealed class FixtureHealth { internal FixtureCapacities capacities = new(); }
    internal sealed class FixtureCapacities
    {
        internal float Consciousness = 1;
        internal float GetLevel(object capacity) => Consciousness;
    }
    internal static class FixtureExtensions
    {
        internal static FixtureSkill GetSkill(this object skills, object def) => (skills as FixtureSkills)?.Skill;
        internal static UnityEngine.Vector3 WithY(this UnityEngine.Vector3 vector, float y) => new(vector.x, y, vector.z);
        internal static IntVec3 ToIntVec3(this UnityEngine.Vector3 vector) => new((int)Math.Floor(vector.x), (int)Math.Floor(vector.z));
        internal static bool CanSee(this Thing source, Pawn pawn, object validator) => pawn.Visible;
        internal static bool HostileTo(this Thing source, Thing target) => target.Faction != 0 && source.Faction != target.Faction;
    }
    internal static class Rand
    {
        internal static int Calls;
        internal static float Sample;
        internal static float Value { get { Calls++; return Sample; } }
    }
}

namespace Verse.AI
{
    internal static class AttackTargetFinder
    {
        internal static bool IsAutoTargetable(Verse.Pawn pawn) => pawn.AutoTargetable;
    }
}

namespace RimWorld
{
    internal static class SkillDefOf { internal static readonly object Shooting = new(); }
    internal static class PawnCapacityDefOf { internal static readonly object Consciousness = new(); }
    internal sealed class TargetingParameters
    {
        internal bool CanTarget(Verse.TargetInfo target, object source) => target.Pawn.Targetable;
    }
}

namespace CombatExtended
{
    internal static class Controller { internal static FixtureSettings settings = new(); }
    internal sealed class FixtureSettings
    {
        internal bool MidBurstRetarget = true, StopBurstAfterTargetFalls = true;
    }
    internal sealed class FixtureVerbProps { internal bool interruptibleBurst = true; }
    internal sealed class FixtureProjectileProps { internal bool isInstant; }
    internal sealed class ShiftVecReport(Verse.Pawn pawn)
    {
        internal UnityEngine.Vector2 GetLeadVec(float error) => pawn.Lead;
    }
    public partial class Verb_LaunchProjectileCE
    {
        internal bool UsesBurstTracking = true;
        internal Verse.Pawn ShooterPawn;
        internal Verse.Thing caster;
        internal Verse.Thing Shooter => ShooterPawn;
        internal FixtureVerbProps VerbPropsCE = new();
        internal FixtureProjectileProps projectilePropsCE = new();
        internal RimWorld.TargetingParameters targetParams = new();
        internal Verse.LocalTargetInfo currentTarget, lastTarget;
        internal Verse.IntVec3 lastTargetPos = Verse.IntVec3.Invalid;
        internal bool shootingAtDowned;
        internal bool doRetarget = true;
        internal bool MidBurst => Emitted > 0;
        internal float ShotHeight = 0.8f, lastShotRotation, lastShotAngle;
        internal BurstTrackingState burstTracking = new();
        internal readonly SubsequentShotState Recovery = new();
        internal int Emitted, AmmoPrepared, Ammo = 20, CompletionCalls, Cooldown;
        internal bool Cancelled;
        private static ShotVector ToShotVector(UnityEngine.Vector3 vector) => new(vector.x, vector.y, vector.z);
        private void CancelSubsequentShotWarmup() { Cancelled = true; Recovery.CancelWarmup(); }
        private bool CanHitFromCellIgnoringRange(UnityEngine.Vector3 source, Verse.LocalTargetInfo target, out Verse.IntVec3 dest)
        { dest = target.Cell; return target.Pawn.ClearLine; }
        private bool CanHitTargetFrom(Verse.IntVec3 source, Verse.LocalTargetInfo target)
            => !IsStoppedBurstTarget(target) && target.Pawn.InRange && target.Pawn.ClearLine;
        private ShiftVecReport ShiftVecReportFor(Verse.LocalTargetInfo target) => new(target.Pawn);
        internal bool Prepare() => PrepareAdaptiveBurstRound();
        internal bool Holding => holdAdaptiveSolution;
        internal bool Retargeted => adaptiveRoundRetargeted;
        internal BurstControlState Control => burstControl;
        internal bool Blocked(Verse.Pawn target) => IsStoppedBurstTarget(target);
        internal void Force(Verse.Pawn target) { stoppedBurstTarget = null; ResetAdaptiveBurst(false); currentTarget = target; }
        internal void ResetTargeting() => ResetAdaptiveBurst();
        internal void Reconstruct() { ResetAdaptiveBurst(true); Emitted = 0; }
        internal void BeginAgain() { ResetAdaptiveBurst(reconstructBurstControl); Emitted = 0; }
        internal bool Emit(int pellets = 1, Action firstPelletImpact = null)
        {
            if (!Prepare()) { CompletionCalls++; Cooldown = 30; return false; }
            AmmoPrepared++;
            if (Ammo <= 0) return false;
            for (int pellet = 0; pellet < pellets; pellet++)
            {
                if (pellet == 0)
                {
                    RecordAdaptiveSolution(currentTarget.Thing?.DrawPos ?? default, 1.2f);
                    firstPelletImpact?.Invoke();
                    if (adaptiveRoundPrepared) burstControl.RecordEmission();
                }
            }
            Ammo--; Emitted++;
            Recovery.RecordShot(Emitted == 1, 0.2f, true, true);
            return true;
        }
    }
}
