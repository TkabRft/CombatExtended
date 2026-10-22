// Minimal map lifecycle inputs for linked production cache/visibility methods.
using System;

namespace Verse
{
    internal sealed partial class Map
    {
        internal int Index, TrackerLookups;
        internal FixtureSky skyManager = new();
        internal CombatExtended.LightingTracker Tracker;
        internal bool Roofed;
        internal T GetComponent<T>() where T : class { TrackerLookups++; return Tracker as T; }
    }
    internal sealed class FixtureSky { internal float CurSkyGlow = 1; }
    internal sealed partial class Pawn
    {
        internal float NightVision;
        internal int NightVisionReads;
    }
    internal static class LightingFixtureExtensions
    {
        internal static bool InBounds(this IntVec3 cell, Map map) => cell.x >= 0 && cell.z >= 0 && cell.x < 200 && cell.z < 200;
        internal static bool Roofed(this IntVec3 cell, Map map) => map.Roofed;
        internal static float DistanceTo(this IntVec3 cell, IntVec3 other)
            => (float)Math.Sqrt((cell.x - other.x) * (cell.x - other.x) + (cell.z - other.z) * (cell.z - other.z));
        internal static float GetStatValue(this Pawn pawn, RimWorld.StatDef stat)
        { pawn.NightVisionReads++; return pawn.NightVision; }
    }
}

namespace CombatExtended
{
    internal sealed class LightingTracker(Verse.Map map)
    {
        internal Verse.Map map = map;
        internal float Glow;
        internal bool IsNight => map.skyManager.CurSkyGlow < 0.5f;
        internal float CombatGlowAtFor(Verse.IntVec3 source, Verse.IntVec3 target) => Glow;
    }
}
