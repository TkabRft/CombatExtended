using System.Collections.Generic;
using System.Linq;
using CombatExtended.Utilities;
using RimWorld;
using Verse;

namespace CombatExtended;

internal sealed class StatPart_UncappedAccuracyCurve : StatPart
{
    private readonly SimpleCurve curve;

    private StatPart_UncappedAccuracyCurve(StatDef stat, SimpleCurve curve)
    {
        parentStat = stat;
        this.curve = curve;
    }

    internal static void Initialize()
    {
        Install(CE_StatDefOf.AimingAccuracy);
        Install(StatDefOf.ShootingAccuracyPawn);
    }

    private static void Install(StatDef stat)
    {
        if (stat == null) return;
        stat.maxValue = float.MaxValue;
        stat.parts ??= new List<StatPart>();
        if (stat.parts.Any(part => part is StatPart_UncappedAccuracyCurve)) return;
        if (stat.postProcessCurve == null) return;
        var conversion = new StatPart_UncappedAccuracyCurve(stat, stat.postProcessCurve);
        stat.postProcessCurve = null;
        AccuracyMath.AppendOnce(stat.parts, conversion);
    }

    public override void TransformValue(StatRequest req, ref float val)
    {
        var points = curve.Points;
        if (points.Count >= 2 && val > points[points.Count - 1].x)
        {
            var last = points[points.Count - 1];
            double slope = 1;
            for (int i = points.Count - 1; i > 0; i--)
            {
                var a = points[i - 1];
                var b = points[i];
                if (b.x <= a.x || b.y <= a.y) continue;
                slope = ((double)b.y - a.y) / (b.x - a.x);
                break;
            }
            val = AccuracyMath.ExtendCurve(val, last.x, last.y, slope);
        }
        else
        {
            val = curve.Evaluate(val);
        }
    }

    public override string ExplanationPart(StatRequest req) => "CE_AccuracyCurveExplanation".Translate();
}
