using System;
using System.Collections.Generic;

namespace CombatExtended.Utilities;

// Shared by live fire, stat conversion, previews and the standalone tests.
internal static class AccuracyMath
{
    // Effective handling at unmodified Shooting 20 in CE's original conversion curve.
    internal const float ExpertHandling = 2.875f;
    internal const float ExpertAiming = 0.9625f;

    internal static float Tail(float intercept, float value, float knee)
    {
        if (value <= knee)
        {
            return intercept - value;
        }
        double remaining = intercept - knee;
        return (float)(remaining * remaining / (remaining + (double)value - knee));
    }

    internal static float HandlingSway(float handling) => Tail(4.5f, handling, 4f);
    internal static float AimedSway(float aiming) => Tail(1f, aiming, 0.9375f);
    internal static float RangeAccuracy(float aiming, float sights) => Tail(1.5f, aiming, 1.25f) / Math.Max(0.01f, sights);
    internal static float Visibility(float aiming) => Tail(2f, aiming, 1.5f);
    internal static float EnvironmentalLead(float aiming) => aiming <= 1.5f ? 1f : Visibility(aiming) / 0.5f;

    internal static float Recoil(float handling, int previousRounds)
    {
        if (previousRounds <= 0) return 0f;
        double expert = Math.Max(0, (double)handling - ExpertHandling);
        // Apply control outside the burst exponent: a fractional power otherwise
        // pulls even a very small recoil base back toward one on the second round.
        double control = 1 / (1 + 16 * expert * expert);
        return (float)(Math.Pow(Tail(5f, handling, 4.5f), Math.Min(10, previousRounds) / 6.25) * control);
    }

    internal static float ModeSway(float sway, float aiming, float sights, bool aimed, bool suppressed)
        => aimed ? sway * AimedSway(aiming) / Math.Max(1, sights) : suppressed ? sway * 1.5f : sway;

    // Preserve ordinary aim preparation. Enhanced control shortens the extra aiming
    // stage instead of leaving an almost error-free shooter waiting the full delay.
    internal static int AimPreparationTicks(float distance, float handling, float aiming)
    {
        double handlingScale = Math.Min(1, HandlingSway(handling) / HandlingSway(ExpertHandling));
        double aimingScale = Math.Min(1, AimedSway(aiming) / AimedSway(ExpertAiming));
        double ticks = (30 + 210 * Math.Max(0, Math.Min(1, distance / 100))) * handlingScale * aimingScale;
        return (int)ticks;
    }

    internal static float ExtendCurve(float value, float lastX, float lastY, double slope)
    {
        return (float)Math.Min(float.MaxValue, lastY + slope * ((double)value - lastX));
    }

    // Keep other mods' stat parts in place and make repeated initialization harmless.
    internal static void AppendOnce<T>(List<T> parts, T conversion) where T : class
    {
        if (!parts.Contains(conversion))
        {
            parts.Add(conversion);
        }
    }
}
