using System;
using System.Text;
using CombatExtended.Utilities;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended;
public class StatWorker_BipodDisplay : StatWorker
{
    public CompProperties_BipodComp bipodComp(StatRequest req)
    {
        return req.Thing?.TryGetComp<BipodComp>()?.Props;
    }

    public CompProperties_BipodComp bipodCompDef(StatRequest req)
    {
        return (req.Def as ThingDef)?.GetCompProperties<CompProperties_BipodComp>();
    }

    public VerbPropertiesCE verbPropsCE(StatRequest req)
    {
        return req.Thing?.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps as VerbPropertiesCE;
    }

    public VerbPropertiesCE verbPropsDef(StatRequest req)
    {
        return (req.Def as ThingDef)?.verbs?.Find(x => x is VerbPropertiesCE) as VerbPropertiesCE;
    }

    public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
    {
        CompProperties_BipodComp props;
        VerbPropertiesCE baseline;
        ThingDef def;
        if (req.HasThing)
        {
            var comp = req.Thing?.TryGetComp<BipodComp>();
            props = comp?.Props;
            baseline = comp?.CopyBaselineVerbProps();
            def = req.Thing?.def;
        }
        else
        {
            props = bipodCompDef(req);
            baseline = verbPropsDef(req);
            def = req.Def as ThingDef;
        }
        if (props == null || baseline == null || def == null)
        {
            return base.GetExplanationFinalizePart(req, numberSense, finalVal);
        }

        var result = new StringBuilder();
        result.Append("CE_BipodSetupTime".Translate()).Append(props.ticksToSetUp)
            .Append(" ticks (").Append(props.ticksToSetUp / 60f).AppendLine("s)");
        if (Controller.settings.AutoSetUp)
        {
            result.AppendLine("CE_BipodAutoSetupMode".Translate());
            if (props.catDef?.useAutoSetMode == true)
            {
                AppendAimMode(result, props.catDef.autosetMode);
            }
            else
            {
                AppendAimMode(result, AimMode.AimedShot);
                AppendAimMode(result, AimMode.SuppressFire);
            }
        }

        float sway = def.statBases?.Find(x => x.stat == CE_StatDefOf.SwayFactor)?.value
            ?? CE_StatDefOf.SwayFactor.defaultBaseValue;
        result.AppendLine().AppendLine("CE_BipodStatWhenSetUp".Translate().Colorize(ColorLibrary.Green));
        AppendValues(result, BipodComp.ValuesFor(baseline, props, true), sway * props.swayMult);
        result.AppendLine().AppendLine().AppendLine("CE_BipodStatWhenNotSetUp".Translate().Colorize(ColorLibrary.LogError));
        AppendValues(result, BipodComp.ValuesFor(baseline, props, false), sway * props.swayPenalty);
        return result.ToString();
    }

    private static void AppendAimMode(StringBuilder result, AimMode mode)
    {
        result.Append("- ").AppendLine(("CE_" + mode + "Label").Translate());
    }

    private static void AppendValues(StringBuilder result, BipodValues values, float sway)
    {
        result.Append(CE_StatDefOf.Recoil.label).Append(": ").AppendLine(Math.Round(values.Recoil, 2).ToString());
        result.Append(CE_StatDefOf.SwayFactor.label).Append(": ").AppendLine(Math.Round(sway, 2).ToString());
        result.Append("CE_BipodStatRange".Translate()).Append(": ").AppendLine(values.Range.ToString());
        result.Append("CE_BipodStatWarmUp".Translate()).Append(": ").Append(values.Warmup);
    }

    public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
    {
        return finalized ? "CE_BipodHoverOverStat".Translate().ToString() : null;
    }

    public override bool ShouldShowFor(StatRequest req)
    {
        return req.HasThing ? bipodComp(req) != null && verbPropsCE(req) != null
            : bipodCompDef(req) != null && verbPropsDef(req) != null;
    }
}
