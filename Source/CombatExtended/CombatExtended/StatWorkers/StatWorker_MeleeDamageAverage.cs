using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using CombatExtended.Utilities;

namespace CombatExtended;

public class StatWorker_MeleeDamageAverage : StatWorker_MeleeDamageBase
{
    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
    {
        var pawn = GetCurrentWielder(req);
        return MeleeStatMath.Dps(GetDamageReports(req).Select(report => report.Attack),
            pawn?.GetStatValue(StatDefOf.MeleeHitChance) ?? 1f);
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        var reports = GetDamageReports(req);
        var builder = new StringBuilder();
        var pawn = GetCurrentWielder(req);
        var skill = pawn?.skills?.GetSkill(SkillDefOf.Melee).Level ?? -1;
        if (skill >= 0 && reports.Count > 0)
        {
            builder.AppendLine("CE_WielderSkillLevel".Translate() + ": " + skill);
        }

        // Variation belongs to each attack: a wielded weapon and a fist can both be available.
        double totalWeight = reports.Sum(report => (double)report.Attack.Weight);
        foreach (var report in reports)
        {
            var attack = report.Attack;
            AppendDamageExplanation(builder, report);
            builder.AppendLine("    " + "CooldownTime".Translate() + ": " +
                ((int)(attack.Cooldown * 60f)).ToStringSecondsFromTicks());
            if (MeleeStatMath.HasVariation(attack.MinimumDamage, attack.MaximumDamage))
            {
                builder.AppendLine("    " + "CE_DamageVariation".Translate() + ": " +
                    attack.MinimumDamage.ToStringByStyle(ToStringStyle.FloatMaxTwo) + " - " +
                    attack.MaximumDamage.ToStringByStyle(ToStringStyle.FloatMaxTwo));
            }
            builder.AppendLine("    " + "CE_FinalAverageDamage".Translate() + ": " +
                ((attack.MinimumDamage + attack.MaximumDamage) / 2f).ToStringByStyle(ToStringStyle.FloatMaxTwo));
            builder.AppendLine("    " + "CE_DPS".Translate() + ": " +
                ((attack.MinimumDamage + attack.MaximumDamage) / 2f / attack.Cooldown).ToStringByStyle(ToStringStyle.FloatMaxTwo));
            builder.AppendLine("    " + "CE_ChanceFactor".Translate() + ": " +
                ((float)(attack.Weight / totalWeight)).ToStringByStyle(ToStringStyle.PercentZero));
            builder.AppendLine();
        }

        if (pawn != null && reports.Count > 0)
        {
            var hitChanceReq = StatRequest.For(pawn);
            builder.AppendLine(StatDefOf.MeleeHitChance.Worker.GetExplanationUnfinalized(hitChanceReq,
                StatDefOf.MeleeHitChance.toStringNumberSense).TrimEndNewlines().Indented());
            builder.Append(StatDefOf.MeleeHitChance.Worker.GetExplanationFinalizePart(hitChanceReq,
                StatDefOf.MeleeHitChance.toStringNumberSense, pawn.GetStatValue(StatDefOf.MeleeHitChance)).Indented());
        }
        return builder.ToString();
    }
}
