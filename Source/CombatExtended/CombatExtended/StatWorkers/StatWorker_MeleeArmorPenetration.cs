using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using CombatExtended.Utilities;

namespace CombatExtended;

public class StatWorker_MeleeArmorPenetration : StatWorker_MeleeStats
{
    public const float skillFactorPerLevel = (25f / 19f) / 100f;
    public const float powerForOtherFactors = 0.75f;

    private sealed class PenetrationReport
    {
        internal Tool Tool;
        internal ManeuverDef Maneuver;
        internal float Weight;
        internal float Sharp;
        internal float Blunt;
        internal float EquipmentFactor;
        internal float OtherFactors;
        internal float SkillFactor;
    }

    private List<PenetrationReport> GetReports(StatRequest req)
    {
        var reports = new List<PenetrationReport>();
        if (req.Def is not ThingDef thingDef)
        {
            return reports;
        }
        var pawn = GetCurrentWielder(req);
        // Match the current combat multiplier, including its existing skill-level convention.
        var skillFactor = 1f + (pawn?.skills?.GetSkill(SkillDefOf.Melee).Level ?? 0) * skillFactorPerLevel;
        if (req.Thing is Pawn)
        {
            foreach (var entry in pawn.meleeVerbs.GetUpdatedAvailableVerbsList(terrainTools: false))
            {
                var verb = entry.verb;
                var weight = entry.GetSelectionWeight(null);
                if (!MeleeStatMath.Positive(weight))
                {
                    continue;
                }
                reports.Add(new PenetrationReport
                {
                    Tool = verb.tool, Maneuver = verb.maneuver, Weight = weight,
                    Sharp = (verb as Verb_MeleeAttackCE)?.ArmorPenetrationSharp ?? 0f,
                    Blunt = (verb as Verb_MeleeAttackCE)?.ArmorPenetrationBlunt ?? 0f,
                    EquipmentFactor = verb.EquipmentSource?.GetStatValue(CE_StatDefOf.MeleePenetrationFactor) ?? 1f,
                    OtherFactors = Mathf.Pow(verb.verbProps.GetDamageFactorFor(verb, pawn), powerForOtherFactors),
                    SkillFactor = skillFactor
                });
            }
        }
        else
        {
            var equipmentFactor = req.HasThing ? req.Thing.GetStatValue(CE_StatDefOf.MeleePenetrationFactor)
                : thingDef.GetStatValueAbstract(CE_StatDefOf.MeleePenetrationFactor, req.StuffDef);
            foreach (var vps in AllMeleeVerbPropsWithSource(thingDef))
            {
                var weight = AdjustedMeleeSelectionWeight(vps, pawn, req);
                if (!MeleeStatMath.Positive(weight))
                {
                    continue;
                }
                var otherFactors = pawn != null
                    ? Mathf.Pow(vps.verbProps.GetDamageFactorFor(vps.tool, pawn, hediffCompSource: null), powerForOtherFactors) : 1f;
                var tool = vps.tool as ToolCE;
                reports.Add(new PenetrationReport
                {
                    Tool = vps.tool, Maneuver = vps.maneuver, Weight = weight,
                    Sharp = MeleeStatMath.Penetration(tool?.armorPenetrationSharp ?? 0f, otherFactors, skillFactor, equipmentFactor),
                    Blunt = MeleeStatMath.Penetration(tool?.armorPenetrationBlunt ?? 0f, otherFactors, skillFactor, equipmentFactor),
                    EquipmentFactor = equipmentFactor, OtherFactors = otherFactors, SkillFactor = skillFactor
                });
            }
        }
        return reports.Where(report => MeleeStatMath.Finite(report.Sharp) && MeleeStatMath.Finite(report.Blunt)).ToList();
    }

    public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
    {
        return GetFinalDisplayValue(optionalReq);
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        var reports = GetReports(req);
        double totalWeight = reports.Sum(report => (double)report.Weight);
        var builder = new StringBuilder();
        foreach (var report in reports)
        {
            builder.AppendLine("  " + "Tool".Translate() + ": " + MeleeToolLabel(report.Tool, report.Maneuver));
            builder.AppendLine("   " + "CE_WeaponPenetrationFactor".Translate() + ": " + report.EquipmentFactor.ToStringByStyle(ToStringStyle.PercentZero));
            if (!Mathf.Approximately(report.SkillFactor, 1f))
            {
                builder.AppendLine("   " + "CE_WeaponPenetrationSkillFactor".Translate() + ": " + report.SkillFactor.ToStringByStyle(ToStringStyle.PercentZero));
            }
            if (!Mathf.Approximately(report.OtherFactors, 1f))
            {
                builder.AppendLine("   " + "CE_WeaponPenetrationOtherFactors".Translate() + ": " + report.OtherFactors.ToStringByStyle(ToStringStyle.PercentZero));
            }
            AppendPenetration(builder, "CE_DescSharpPenetration", "CE_mmRHA",
                (report.Tool as ToolCE)?.armorPenetrationSharp ?? 0f, report.Sharp, report);
            AppendPenetration(builder, "CE_DescBluntPenetration", "CE_MPa",
                (report.Tool as ToolCE)?.armorPenetrationBlunt ?? 0f, report.Blunt, report);
            builder.AppendLine("    " + "CE_ChanceFactor".Translate() + ": " +
                ((float)(report.Weight / totalWeight)).ToStringByStyle(ToStringStyle.PercentZero));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static void AppendPenetration(StringBuilder builder, string label, string unit, float baseValue,
        float finalValue, PenetrationReport report)
    {
        builder.Append("    " + label.Translate() + ": ");
        if (MeleeStatMath.ExplainPenetration(finalValue))
        {
            builder.Append(baseValue.ToStringByStyle(ToStringStyle.FloatMaxTwo) + " x " +
                report.EquipmentFactor.ToStringByStyle(ToStringStyle.FloatMaxThree));
            if (!Mathf.Approximately(report.SkillFactor, 1f))
            {
                builder.Append(" x " + report.SkillFactor.ToStringByStyle(ToStringStyle.FloatMaxTwo));
            }
            if (!Mathf.Approximately(report.OtherFactors, 1f))
            {
                builder.Append(" x " + report.OtherFactors.ToStringByStyle(ToStringStyle.FloatMaxTwo));
            }
            builder.Append(" = ");
        }
        builder.AppendLine(finalValue.ToStringByStyle(ToStringStyle.FloatMaxTwo) + " " + unit.Translate());
    }

    public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
    {
        return "StatsReport_FinalValue".Translate() + ": " + GetFinalDisplayValue(req);
    }

    private string GetFinalDisplayValue(StatRequest req)
    {
        var reports = GetReports(req);
        return MeleeStatMath.Average(reports, report => report.Weight, report => report.Sharp).ToStringByStyle(ToStringStyle.FloatMaxTwo)
            + " " + "CE_mmRHA".Translate() + ", "
            + MeleeStatMath.Average(reports, report => report.Weight, report => report.Blunt).ToStringByStyle(ToStringStyle.FloatMaxTwo)
            + " " + "CE_MPa".Translate();
    }
}
