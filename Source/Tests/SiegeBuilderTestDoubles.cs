// The siege guards are linked from production source and exercised without loading
// RimWorld's reference-only assemblies. The main project checks the actual game API.
using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string methodName) { }
    }
}

namespace Verse
{
    internal sealed class Pawn
    {
        public PawnMindState mindState = new();
        public object skills = new();
        public object workSettings = new();
    }

    internal sealed class PawnMindState
    {
        public PawnDuty duty;
    }

    internal sealed class PawnDuty(RimWorld.DutyDef dutyDef)
    {
        public RimWorld.DutyDef def = dutyDef;
    }
}

namespace RimWorld
{
    internal sealed class DutyDef { }

    internal static class DutyDefOf
    {
        public static readonly DutyDef Build = new();
        public static readonly DutyDef Defend = new();
    }

    internal sealed class SiegeLord
    {
        public readonly List<Verse.Pawn> ownedPawns = [];
    }

    internal sealed class LordToil_Siege
    {
        public readonly SiegeLord lord = new();
        public int DefenderAssignments;

        public bool CanBeBuilder(Verse.Pawn pawn) => throw new NotSupportedException();
        public void UpdateAllDuties() => throw new NotSupportedException();

        public void SetAsDefender(Verse.Pawn pawn)
        {
            pawn.mindState.duty = new Verse.PawnDuty(DutyDefOf.Defend);
            DefenderAssignments++;
        }
    }
}
