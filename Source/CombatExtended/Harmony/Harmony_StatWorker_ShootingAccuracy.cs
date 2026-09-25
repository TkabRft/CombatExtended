using HarmonyLib;
using RimWorld;

namespace CombatExtended.HarmonyCE;

[HarmonyPatch(typeof(StatWorker_ShootingAccuracy), "GetExplanationFinalizePart")]
internal static class Harmony_StatWorker_ShootingAccuracy
{
    internal static bool Prefix()
    {
        return false;
    }
}
