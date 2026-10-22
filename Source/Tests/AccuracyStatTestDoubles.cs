// Standalone adapters for the linked production stat part; the solution build checks the real API.
using System.Collections.Generic;

namespace Verse
{
    internal readonly record struct CurvePoint(float x, float y);
    internal sealed class SimpleCurve
    {
        public List<CurvePoint> Points = [];
        public float Evaluate(float value)
        {
            if (value <= Points[0].x) return Points[0].y;
            for (int i = 1; i < Points.Count; i++)
            {
                if (value <= Points[i].x)
                {
                    var a = Points[i - 1]; var b = Points[i];
                    return a.y + (b.y - a.y) * (value - a.x) / (b.x - a.x);
                }
            }
            return Points[^1].y;
        }
    }
    internal readonly struct StatRequest { }
    internal abstract class StatPart
    {
        public RimWorld.StatDef parentStat;
        public abstract void TransformValue(StatRequest request, ref float value);
        public abstract string ExplanationPart(StatRequest request);
    }
    internal static class AccuracyTranslationDouble
    {
        public static string Translate(this string key) => key;
    }
}

namespace RimWorld
{
    internal sealed class StatDef
    {
        public float maxValue = 13;
        public List<Verse.StatPart> parts;
        public Verse.SimpleCurve postProcessCurve;
    }
    internal static class StatDefOf { public static StatDef ShootingAccuracyPawn; }
}

namespace CombatExtended
{
    internal static class CE_StatDefOf
    {
        public static RimWorld.StatDef AimingAccuracy;
        public static readonly RimWorld.StatDef NightVisionEfficiency = new();
    }
}
