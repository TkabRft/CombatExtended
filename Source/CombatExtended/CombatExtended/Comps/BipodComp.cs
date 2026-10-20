using System.Collections.Generic;
using CombatExtended.Utilities;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace CombatExtended;
public class BipodComp : CompRangedGizmoGiver
{
    #region Fields
    public bool ShouldSetUpint;
    private CompFireModes compFireMode;

    public bool IsSetUpRn;

    private int starts = 5;
    private readonly BipodVerbState<VerbPropertiesCE> verbState = new(props => (VerbPropertiesCE)props.MemberwiseClone());

    #endregion

    #region Properties
    public bool ShouldSetUp
    {
        get
        {
            if (Controller.settings.AutoSetUp && compFireMode != null)
            {
                Pawn pawn = (this.ParentHolder as Pawn_EquipmentTracker)?.pawn;
                bool matchesMode = Props.catDef?.useAutoSetMode == true
                    ? compFireMode.CurrentAimMode == Props.catDef.autosetMode
                    : compFireMode.CurrentAimMode != AimMode.Snapshot;
                return matchesMode && !IsSetUpRn && pawn != null && !pawn.IsCarryingPawn();
            }
            return ShouldSetUpint && !IsSetUpRn;
        }
    }
    #endregion

    #region Methods

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        compFireMode = this.parent.TryGetComp<CompFireModes>();
        if (respawningAfterLoad && IsSetUpRn)
        {
            SetupFromLoad();
        }
        else
        {
            SetUpInvert(this.parent);
        }
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref IsSetUpRn, "isBipodSetUp");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            compFireMode = this.parent.TryGetComp<CompFireModes>();

            if (IsSetUpRn)
            {
                SetupFromLoad();
            }
            else
            {
                SetUpInvert(this.parent);
            }
        }
        base.PostExposeData();
    }

    [Compatibility.Multiplayer.SyncMethod]
    public void DeployUpBipod() => ShouldSetUpint = true;

    [Compatibility.Multiplayer.SyncMethod]
    public void CloseBipod()
    {
        ShouldSetUpint = false;
        IsSetUpRn = false;
        SetUpInvert(parent);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Controller.settings.BipodMechanics || this.ParentHolder is not Pawn_EquipmentTracker tracker)
        {
            yield break;
        }
        Pawn dad = tracker.pawn;
        if (!dad.Drafted)
        {
            yield break;
        }
        if (Controller.settings.AutoSetUp)
        {
            if (compFireMode == null)
            {
                yield return new Command_Action
                {
                    action = ShouldSetUpint ? CloseBipod : DeployUpBipod,
                    defaultLabel = ShouldSetUpint ? "CE_Close_Bipod".Translate() : "CE_Deploy_Bipod".Translate(),
                    icon = ContentFinder<Texture2D>.Get(ShouldSetUpint ? "UI/Buttons/closed_bipod" : "UI/Buttons/open_bipod")
                };

            }
            else
            {
                yield return new Command_Action
                {
                    action = delegate { },
                    defaultLabel = IsSetUpRn ? "CE_Bipod_Set_Up".Translate() : "CE_Bipod_Not_Set_Up".Translate(),
                    icon = ContentFinder<Texture2D>.Get(IsSetUpRn ? "UI/Buttons/open_bipod" : "UI/Buttons/closed_bipod")
                };
            }
        }
        else
        {
            yield return new Command_Action
            {
                action = ShouldSetUpint ? CloseBipod : DeployUpBipod,
                defaultLabel = ShouldSetUpint ? "CE_Close_Bipod".Translate() : "CE_Deploy_Bipod".Translate(),
                icon = ContentFinder<Texture2D>.Get(ShouldSetUpint ? "UI/Buttons/closed_bipod" : "UI/Buttons/open_bipod")
            };
        }
    }

    public override void Notify_Unequipped(Pawn pawn)
    {
        IsSetUpRn = false;
        ResetVerbProps(this.parent);
    }

    public override void Notify_Equipped(Pawn pawn)
    {
        base.Notify_Equipped(pawn);
        compFireMode = this.parent.TryGetComp<CompFireModes>();
        SetUpInvert(this.parent);
    }

    public CompProperties_BipodComp Props => (CompProperties_BipodComp)this.props;

    public VerbPropertiesCE CopyVerbPropsFromThing(Thing source)
    {
        return (VerbPropertiesCE)(source?.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps as VerbPropertiesCE)?.MemberwiseClone();
    }

    public void AssignVerbProps(Thing target, VerbPropertiesCE props)
    {
        target.TryGetComp<CompEquippable>().PrimaryVerb.verbProps = props;
    }

    public void ResetVerbProps(Thing source)
    {
        ApplyState(source, false);
    }

    internal VerbPropertiesCE CopyBaselineVerbProps()
    {
        return verbState.CopyBaseline(this.parent.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps as VerbPropertiesCE);
    }

    internal static BipodValues ValuesFor(VerbPropertiesCE baseline, CompProperties_BipodComp props, bool deployed)
    {
        return new BipodValues(baseline.range, baseline.recoilAmount, baseline.warmupTime).Modified(
            deployed ? props.additionalrange : 0f,
            deployed ? props.recoilMulton : props.recoilMultoff,
            deployed ? props.warmupMult : props.warmupPenalty);
    }

    private void ApplyState(Thing source, bool applyModifiers)
    {
        var verb = source?.TryGetComp<CompEquippable>()?.PrimaryVerb;
        if (verb?.verbProps is not VerbPropertiesCE current)
        {
            return;
        }
        var changed = verbState.Apply(current, copy =>
        {
            if (applyModifiers)
            {
                BipodValues values = ValuesFor(copy, Props, IsSetUpRn);
                copy.range = values.Range;
                copy.recoilAmount = values.Recoil;
                copy.warmupTime = values.Warmup;
            }
        });
        AssignVerbProps(source, changed);
    }

    public void SetUpInvert(Thing source)
    {
        IsSetUpRn = false;
        ApplyState(source, Controller.settings.BipodMechanics);
    }

    public void SetUpEnd(Thing source, Pawn pawn)
    {
        IsSetUpRn = true;
        ApplyState(source, Controller.settings.BipodMechanics);

        CE_SoundDefOf.Interact_Bipod.PlayOneShot(new TargetInfo(source.PositionHeld, pawn.Map));
    }

    public void SetUpStart(Pawn pawn = null)
    {
        var current = this.parent.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps as VerbPropertiesCE;
        if (!verbState.Owns(current))
        {
            ApplyState(this.parent, Controller.settings.BipodMechanics);
        }
        if (!(pawn?.Drafted ?? false))
        {
            return;
        }

        starts--;

        if (starts == 0)
        {
            if ((pawn.jobs?.curJob?.def?.HasModExtension<JobDefBipodCancelExtension>() ?? false) && !pawn.pather.MovingNow && ShouldSetUp)
            {
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(new Job { def = CE_JobDefOf.JobDef_SetUpBipod, targetA = this.parent }, JobCondition.InterruptForced);
            }

            if (pawn.pather.MovingNow)
            {
                SetUpInvert(this.parent);
            }
            starts = 5;
        }
    }

    public void SetupFromLoad()
    {
        ApplyState(this.parent, Controller.settings.BipodMechanics);
    }
    #endregion

}
