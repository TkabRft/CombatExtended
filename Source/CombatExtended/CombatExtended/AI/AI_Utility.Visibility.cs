using RimWorld;
using Verse;

namespace CombatExtended.AI;

static partial class AI_Utility
{
    public static bool VisibilityGoodAt(this Map map, Pawn shooter, IntVec3 target, float nightVisionEfficiency = -1)
    {
        // This is an optional illumination tactic, not permission to fire. Without
        // a usable lighting context, leave the original attack alone and retry later.
        if (map == null || map.Index < 0 || map.skyManager == null || shooter == null
            || !shooter.Spawned || shooter.Map != map || !target.InBounds(map)) return true;
        LightingTracker tracker = map.GetLightingTracker();
        if (tracker == null || tracker.map != map) return true;
        if (!tracker.IsNight) return true;
        if (target.DistanceTo(shooter.Position) < 15) return true;
        if (tracker.CombatGlowAtFor(shooter.Position, target) >= 0.5f) return true;
        if ((nightVisionEfficiency == -1 ? shooter.GetStatValue(CE_StatDefOf.NightVisionEfficiency) : nightVisionEfficiency) > 0.6)
            return true;
        return target.Roofed(map);
    }
}
