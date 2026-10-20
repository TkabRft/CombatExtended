using System;
using System.Collections.Generic;
using System.Linq;

namespace CombatExtended.Utilities;

// Display-only arithmetic. Combat continues to use the existing verb implementation.
internal static class MeleeStatMath
{
    internal readonly struct Attack
    {
        internal readonly float Weight;
        internal readonly float MinimumDamage;
        internal readonly float MaximumDamage;
        internal readonly float Cooldown;

        internal Attack(float weight, float damage, float cooldown, bool equipped,
            float variationMin, float variationMax, float unarmedBonus)
        {
            Weight = weight;
            MinimumDamage = equipped ? damage * variationMin : damage + unarmedBonus;
            MaximumDamage = equipped ? damage * variationMax : damage + unarmedBonus;
            Cooldown = cooldown;
        }

        internal bool IsUsable => Positive(Weight) && Positive(Cooldown)
            && Finite(MinimumDamage) && MinimumDamage >= 0f
            && Finite(MaximumDamage) && MaximumDamage >= MinimumDamage;
    }

    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool Positive(float value) => Finite(value) && value > 0f;

    internal static bool HasVariation(float minimum, float maximum) => Math.Abs(maximum - minimum) > 0.00001f;

    internal static bool ExplainPenetration(float value) => Math.Abs(value) > 0.00001f;

    internal static bool ShowWeaponAdjustment(bool weapon, float baseDamage, float adjustedDamage) =>
        weapon && Math.Abs(adjustedDamage - baseDamage) > 0.00001f;

    internal static float Penetration(float toolPenetration, float otherFactors, float skillFactor, float? equipmentFactor) =>
        toolPenetration * otherFactors * skillFactor * (equipmentFactor ?? 1f);

    internal static float Dps(IEnumerable<Attack> attacks, float hitChance)
    {
        double damage = 0;
        double cooldown = 0;
        foreach (var attack in attacks.Where(attack => attack.IsUsable))
        {
            damage += attack.Weight * ((double)attack.MinimumDamage + attack.MaximumDamage) / 2;
            cooldown += (double)attack.Weight * attack.Cooldown;
        }
        var result = cooldown > 0 && Finite(hitChance) && hitChance >= 0f ? (float)(damage / cooldown * hitChance) : 0f;
        return Finite(result) ? result : 0f;
    }

    internal static float Average<T>(IEnumerable<T> entries, Func<T, float> weight, Func<T, float> value)
    {
        double total = 0;
        double weighted = 0;
        foreach (var entry in entries)
        {
            float w = weight(entry);
            float v = value(entry);
            if (!Positive(w) || !Finite(v))
            {
                continue;
            }
            total += w;
            weighted += (double)w * v;
        }
        return total > 0 ? (float)(weighted / total) : 0f;
    }

    // A recipe may not install a hediff, and a verb giver may contain only ranged verbs.
    internal static List<TVerb> FirstMeleeProvider<TProvider, TVerb>(IEnumerable<TProvider> providers,
        Func<TProvider, IEnumerable<TVerb>> meleeVerbs) where TProvider : class
    {
        foreach (var provider in providers)
        {
            if (provider == null)
            {
                continue;
            }
            var verbs = meleeVerbs(provider)?.ToList();
            if (verbs != null && verbs.Count > 0)
            {
                return verbs;
            }
        }
        return new List<TVerb>();
    }
}
