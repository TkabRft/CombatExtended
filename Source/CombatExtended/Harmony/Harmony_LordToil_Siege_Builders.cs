using HarmonyLib;
using RimWorld;
using Verse;

namespace CombatExtended.HarmonyCE;

/// <summary>
/// Keep pawns without the trackers used by SetAsBuilder out of the builder pool.
/// </summary>
[HarmonyPatch(typeof(LordToil_Siege), nameof(LordToil_Siege.CanBeBuilder))]
internal static class Harmony_LordToil_Siege_CanBeBuilder
{
    internal static bool HasRequiredTrackers(Pawn pawn)
    {
        return pawn?.mindState != null && pawn.skills != null && pawn.workSettings != null;
    }

    internal static void Postfix(Pawn p, ref bool __result)
    {
        __result = __result && HasRequiredTrackers(p);
    }
}

/// <summary>
/// Recover existing invalid builder duties before vanilla remembers and counts them.
/// </summary>
[HarmonyPatch(typeof(LordToil_Siege), nameof(LordToil_Siege.UpdateAllDuties))]
internal static class Harmony_LordToil_Siege_UpdateAllDuties
{
    internal static void Prefix(LordToil_Siege __instance)
    {
        foreach (Pawn pawn in __instance.lord.ownedPawns)
        {
            if (pawn?.mindState?.duty?.def == DutyDefOf.Build
                && !Harmony_LordToil_Siege_CanBeBuilder.HasRequiredTrackers(pawn))
            {
                __instance.SetAsDefender(pawn);
            }
        }
    }
}
