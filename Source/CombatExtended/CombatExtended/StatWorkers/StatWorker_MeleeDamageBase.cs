using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using CombatExtended.Utilities;

namespace CombatExtended;
public class StatWorker_MeleeDamageBase : StatWorker_MeleeStats
{
    #region Constants

    public const float damageVariationMin = 0.5f;
    public const float damageVariationMax = 1.5f;
    public const float damageVariationPerSkillLevel = 0.025f;

    #endregion

    #region Methods

    public static float GetDamageVariationMin(Pawn pawn)
    {
        float unskilledReturnValue = damageVariationMin;
        if (!ShouldUseSkillVariation(pawn, ref unskilledReturnValue))
        {
            return unskilledReturnValue;
        }
        return damageVariationMin + (damageVariationPerSkillLevel * pawn.skills.GetSkill(SkillDefOf.Melee).Level);
    }

    public static float GetDamageVariationMax(Pawn pawn)
    {
        float unskilledReturnValue = damageVariationMax;
        if (!ShouldUseSkillVariation(pawn, ref unskilledReturnValue))
        {
            return unskilledReturnValue;
        }
        return damageVariationMax - (damageVariationPerSkillLevel * (20 - pawn.skills.GetSkill(SkillDefOf.Melee).Level));
    }

    public static bool ShouldUseSkillVariation(Pawn pawn, ref float unskilledReturnValue)
    {
        if (pawn == null)       //Info windows for when weapon isn't equipped
        {
            return false;
        }
        if ((pawn?.skills?.GetSkill(SkillDefOf.Melee) ?? null) == null)     //Pawns that can equip weapons but don't use skill (mechanoids, custom races)
        {
            //No damage variation applied (same as animals for unarmed damage)
            unskilledReturnValue = 1.0f;
            return false;
        }
        return true;
    }


    // Retained for callers compiled against the existing public API.
    public static float GetAdjustedDamage(ToolCE tool, Thing thingOwner)
    {
        return tool.AdjustedBaseMeleeDamageAmount(thingOwner, tool.capacities?.First()?.VerbsProperties?.First()?.meleeDamageDef);
    }

    protected sealed class DamageReport
    {
        internal Tool Tool;
        internal ManeuverDef Maneuver;
        internal float BaseDamage;
        internal float AdjustedDamage;
        internal bool Weapon;
        internal MeleeStatMath.Attack Attack;
    }

    protected List<DamageReport> GetDamageReports(StatRequest req)
    {
        var reports = new List<DamageReport>();
        if (req.Def is not ThingDef thingDef)
        {
            return reports;
        }
        var pawn = GetCurrentWielder(req);
        var variationMin = GetDamageVariationMin(pawn);
        var variationMax = GetDamageVariationMax(pawn);
        float unarmedBonus = pawn != null && !CE_StatDefOf.UnarmedDamage.Worker.IsDisabledFor(pawn)
            ? pawn.GetStatValue(CE_StatDefOf.UnarmedDamage) : 0f;

        void Add(Tool tool, ManeuverDef maneuver, float weight, float damage, float cooldown, bool equipped, bool weapon)
        {
            var attack = new MeleeStatMath.Attack(weight, damage, cooldown, equipped, variationMin, variationMax, unarmedBonus);
            if (attack.IsUsable)
            {
                reports.Add(new DamageReport
                {
                    Tool = tool, Maneuver = maneuver, BaseDamage = tool?.power ?? damage,
                    AdjustedDamage = damage, Weapon = weapon, Attack = attack
                });
            }
        }

        if (req.Thing is Pawn)
        {
            foreach (var entry in pawn.meleeVerbs.GetUpdatedAvailableVerbsList(terrainTools: false))
            {
                var verb = entry.verb;
                bool equipped = verb.EquipmentSource != null && verb.EquipmentSource != pawn;
                Add(verb.tool, verb.maneuver, entry.GetSelectionWeight(null),
                    verb.verbProps.AdjustedMeleeDamageAmount(verb, pawn),
                    verb.verbProps.AdjustedCooldown(verb, pawn), equipped,
                    equipped && verb.EquipmentSource.def.IsWeapon);
            }
        }
        else
        {
            foreach (var vps in AllMeleeVerbPropsWithSource(thingDef))
            {
                Add(vps.tool, vps.maneuver, AdjustedMeleeSelectionWeight(vps, pawn, req),
                    AdjustedMeleeDamageAmount(vps, pawn, req), AdjustedCooldown(vps, pawn, req),
                    !thingDef.isTechHediff, thingDef.IsWeapon);
            }
        }
        return reports;
    }

    protected static void AppendDamageExplanation(StringBuilder builder, DamageReport report)
    {
        builder.AppendLine("  " + "Tool".Translate() + ": " + MeleeToolLabel(report.Tool, report.Maneuver));
        builder.AppendLine("    " + "CE_DescBaseDamage".Translate() + ": " + report.BaseDamage.ToStringByStyle(ToStringStyle.FloatMaxTwo));
        if (MeleeStatMath.ShowWeaponAdjustment(report.Weapon, report.BaseDamage, report.AdjustedDamage))
        {
            builder.AppendLine("    " + "CE_AdjustedForWeapon".Translate() + ": " + report.AdjustedDamage.ToStringByStyle(ToStringStyle.FloatMaxTwo));
        }
    }

    /// <summary>
    /// Get the melee damage dealt by a single VerbProperties + Tool combination of a weapon,
    /// adjusted according to the wielder's (if any) stats/capacities and multipliers from the weapon's stuff.
    /// </summary>
    /// <param name="vps">Verb data (VerbProperties + Tool + Maneuver)</param>
    /// <param name="pawn">The wielder of the weapon, or null if the stat request is for an unwielded weapon.</param>
    /// <param name="req">Stat request for a weapon or weapon def</param>
    /// <returns>Melee damage adjusted for the wielder and weapon.</returns>
    protected float AdjustedMeleeDamageAmount(VerbUtility.VerbPropertiesWithSource vps, Pawn pawn, StatRequest req)
    {
        return req.HasThing ?
            vps.verbProps.AdjustedMeleeDamageAmount(vps.tool, pawn, req.Thing, hediffCompSource: null) :
            vps.verbProps.AdjustedMeleeDamageAmount(vps.tool, pawn, req.Def as ThingDef, req.StuffDef, hediffCompSource: null);
    }

    /// <summary>
    /// Get the cooldown (in seconds) a single VerbProperties + Tool combination of a weapon,
    /// adjusted according to the wielder's (if any) stats/capacities and multipliers from the weapon's stuff.
    /// </summary>
    /// <param name="vps">Verb data (VerbProperties + Tool + Maneuver)</param>
    /// <param name="pawn">The wielder of the weapon, or null if the stat request is for an unwielded weapon.</param>
    /// <param name="req">Stat request for a weapon or weapon def</param>
    /// <returns>Cooldown time in seconds, adjusted for the wielder and weapon.</returns>
    protected float AdjustedCooldown(VerbUtility.VerbPropertiesWithSource vps, Pawn pawn, StatRequest req)
    {
        return req.HasThing ?
            vps.verbProps.AdjustedCooldown(vps.tool, pawn, req.Thing) :
            vps.verbProps.AdjustedCooldown(vps.tool, pawn, req.Def as ThingDef, req.StuffDef);
    }

    #endregion

}
