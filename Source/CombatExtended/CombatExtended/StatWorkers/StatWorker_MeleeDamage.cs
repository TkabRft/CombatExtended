using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using CombatExtended.Utilities;

namespace CombatExtended;

public class StatWorker_MeleeDamage : StatWorker_MeleeDamageBase
{
    public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
    {
        return GetFinalDisplayValue(optionalReq);
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        var builder = new StringBuilder();
        foreach (var report in GetDamageReports(req))
        {
            AppendDamageExplanation(builder, report);
            var attack = report.Attack;
            if (MeleeStatMath.HasVariation(attack.MinimumDamage, attack.MaximumDamage))
            {
                builder.AppendLine("    " + "CE_DamageVariation".Translate() + ": " +
                    FormatRange(attack.MinimumDamage, attack.MaximumDamage));
            }
            builder.AppendLine("    " + "StatsReport_FinalValue".Translate() + ": " +
                FormatRange(attack.MinimumDamage, attack.MaximumDamage));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
    {
        return "StatsReport_FinalValue".Translate() + ": " + GetFinalDisplayValue(req);
    }

    private string GetFinalDisplayValue(StatRequest req)
    {
        var reports = GetDamageReports(req);
        return reports.Count == 0 ? "" : FormatRange(reports.Min(report => report.Attack.MinimumDamage),
            reports.Max(report => report.Attack.MaximumDamage));
    }

    private static string FormatRange(float minimum, float maximum) =>
        minimum.ToStringByStyle(ToStringStyle.FloatMaxTwo) + (MeleeStatMath.HasVariation(minimum, maximum)
            ? " - " + maximum.ToStringByStyle(ToStringStyle.FloatMaxTwo) : "");
}
