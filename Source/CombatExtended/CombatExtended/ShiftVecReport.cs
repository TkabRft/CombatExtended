using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using RimWorld.Planet;
using CombatExtended.Utilities;

namespace CombatExtended;
public class ShiftVecReport
{
    public LocalTargetInfo target = null;
    public GlobalTargetInfo globalTarget = GlobalTargetInfo.Invalid;

    private string hitChance = "";
    internal Func<float?> burstEstimate;
    public string HitChance
    {
        get
        {
            if (burstEstimate != null)
            {
                float? probability = burstEstimate();
                return probability.HasValue ? GenText.ToStringByStyle(probability.Value * 100, ToStringStyle.FloatTwo) : "-";
            }
            if (hitChance == "")
            {
                if (target.Thing is null)
                {
                    return "-";
                }

                Bounds bounds = CE_Utility.GetBoundsFor(target.Thing);

                if (cover != null)
                {
                    var cv = new CollisionVertical(cover);
                    var cy_min = Mathf.Max(bounds.min.y, cv.Max);
                    var cy_max = bounds.max.y;
                    if (cy_max - cy_min < 0) // behind and shorter than cover
                    {
                        cy_max = cy_min;
                    }
                    bounds = new Bounds(new Vector3(bounds.center.x, cy_min + cy_max / 2, bounds.center.z),
                                        new Vector3(bounds.size.x, (cy_max - cy_min), bounds.size.z));
                }

                var offset = bounds.size.y / 2;
                float dist = shotDist;

                // calculate uncertainty in xz position
                float VS = Mathf.Sqrt(visibilityShift * visibilityShift
                                      + circularMissRadius * circularMissRadius
                                      + indirectFireShift * indirectFireShift
                                      + leadShift * leadShift);

                float prob = CE_Math.CalculateHitPercent(dist, bounds, offset, shotSpeed, shotAngle, swayDegrees, spreadDegrees, VS, CE_Utility.GravityConst);

                hitChance = GenText.ToStringByStyle(prob * 100, ToStringStyle.FloatTwo);
            }
            return hitChance;
        }
    }

    public Pawn targetPawn
    {
        get
        {
            return target.Thing as Pawn;
        }
    }
    public float aimingAccuracy = 1f;
    public float sightsEfficiency = 1f;

    private float accuracyFactorInt = -1f;
    public float accuracyFactor
    {
        get
        {
            if (accuracyFactorInt < 0)
            {
                accuracyFactorInt = AccuracyMath.RangeAccuracy(aimingAccuracy, sightsEfficiency);
            }
            return accuracyFactorInt;
        }
    }

    public float circularMissRadius = 0f;
    public float indirectFireShift = 0f;

    // Visibility variables
    public float lightingShift = 0f;
    public float weatherShift = 0f;

    private float environmentShiftInt = -1;
    public float environmentShift
    {
        get
        {
            if (environmentShiftInt < 0)
            {
                environmentShiftInt = ((blindFiring ? 1 : lightingShift) * 7f + weatherShift * 1.5f) * CE_Utility.LightingRangeMultiplier(shotDist) + smokeDensity;
            }
            return environmentShiftInt;
        }
    }


    private float visibilityShiftInt = -1f;
    public float visibilityShift
    {
        get
        {
            if (visibilityShiftInt < 0)
            {
                float se = sightsEfficiency;
                if (se < 0.02f)
                {
                    se = 0.02f;
                }
                visibilityShiftInt = environmentShift * (shotDist / 50 / se) * AccuracyMath.Visibility(aimingAccuracy);
            }
            return visibilityShiftInt;
        }
    }

    // Leading variables
    public Vector2 shotSource = new Vector2(0, 0); // muzzle/eye position (xz) at fire time, used for time-of-flight
    public float shotSpeed = 0f;
    private bool targetIsMoving
    {
        get
        {
            return targetPawn != null && targetPawn.pather != null && targetPawn.pather.MovingNow && (targetPawn.stances.stunner == null || !targetPawn.stances.stunner.Stunned);
        }
    }
    private float leadDistInt = -1f;
    public float leadDist
    {
        get
        {
            if (leadDistInt < 0)
            {
                if (targetIsMoving)
                {
                    float targetSpeed = MoveVelocity.magnitude;
                    float timeToTarget = shotDist / shotSpeed;
                    leadDistInt = targetSpeed * timeToTarget;
                }
                else
                {
                    leadDistInt = 0f;
                }
            }
            return leadDistInt;
        }
    }
    public float leadShift
    {
        get
        {
            return leadDist * Mathf.Min(accuracyFactor * 0.25f, 2.5f)
                   + (Mathf.Min((blindFiring ? 1 : lightingShift) * CE_Utility.LightingRangeMultiplier(shotDist) * leadDist * 0.25f, (blindFiring ? 100f : 2.0f))
                   + Mathf.Min((blindFiring ? 0 : smokeDensity) * 0.5f, 2.0f)) * AccuracyMath.EnvironmentalLead(aimingAccuracy);
        }
    }

    // Range variables
    public float shotDist = 0f;
    public float shotHeight = 0f;
    public float targetHeight = 0f;
    public float shotAngle = 0f;
    public float maxRange;
    public float distShift
    {
        get
        {
            return shotDist * (shotDist / Math.Max(maxRange, 20)) * Mathf.Min(accuracyFactor * 0.5f, 0.8f);
        }
    }

    public bool isAiming = false;
    public float swayDegrees = 0f;
    public float spreadDegrees = 0f;
    public Thing cover = null;
    public float smokeDensity = 0f;
    public bool blindFiring = false;
    public bool roofed = false;

    // Copy-constructor
    public ShiftVecReport(ShiftVecReport report)
    {
        target = report.target;
        sightsEfficiency = report.sightsEfficiency;
        aimingAccuracy = report.aimingAccuracy;
        circularMissRadius = report.circularMissRadius;
        indirectFireShift = report.indirectFireShift;
        lightingShift = report.lightingShift;
        weatherShift = report.weatherShift;
        shotSource = report.shotSource;
        shotSpeed = report.shotSpeed;
        shotDist = report.shotDist;
        maxRange = report.maxRange;
        isAiming = report.isAiming;
        swayDegrees = report.swayDegrees;
        spreadDegrees = report.spreadDegrees;
        cover = report.cover;
        smokeDensity = report.smokeDensity;
        blindFiring = report.blindFiring;
        roofed = report.roofed;
    }

    public ShiftVecReport()
    {
    }

    public Vector2 GetRandCircularVec()
    {
        Vector2 vec = CE_Utility.GenRandInCircle(visibilityShift + circularMissRadius + indirectFireShift);
        return vec;
    }

    public float GetRandDist()
    {
        float dist = shotDist + Rand.Range(-distShift, distShift);
        return dist;
    }

    public Vector2 GetRandLeadVec()
    {
        if (blindFiring || !targetIsMoving) return Vector2.zero;
        float error = Rand.Range(-leadShift, leadShift);
        Vector2 lead = GetLeadVec(error);
        if (Controller.settings.DebugDrawInterceptChecks)
        {
            Vector2 velocity = MoveVelocity;
            Log.Message($"[CE-Debug] lead: cell={targetPawn.Position} next={targetPawn.pather.nextCell} stepTicks={targetPawn.pather.nextCellCostTotal:F3} velocity=({velocity.x:F3},{velocity.y:F3}) lead=({lead.x:F3},{lead.y:F3}) error={error:F3}");
        }
        return lead;
    }

    internal Vector2 MoveVelocity
    {
        get
        {
            if (!targetIsMoving) return Vector2.zero;
            IntVec3 step = targetPawn.pather.nextCell - targetPawn.Position;
            ShotVector velocity = ShotGeometry.StepVelocity(new ShotVector(step.x, 0, step.z),
                targetPawn.pather.nextCellCostTotal, true);
            return new Vector2((float)velocity.X, (float)velocity.Z);
        }
    }

    internal Vector2 GetLeadVec(float error)
    {
        if (blindFiring || !targetIsMoving) return Vector2.zero;
        Vector3 position = targetPawn.DrawPos;
        Vector2 velocity = MoveVelocity;
        ShotVector lead = ShotGeometry.Lead(new ShotVector(shotSource.x, 0, shotSource.y),
            new ShotVector(position.x, 0, position.z), new ShotVector(velocity.x, 0, velocity.y), shotSpeed);
        var direction = new Vector2((float)lead.X, (float)lead.Z);
        if (direction.sqrMagnitude < 1e-6f) direction = velocity;
        return direction.normalized * ((float)lead.Horizontal + error);
    }

    /// <returns>Angle Vector2 in degrees</returns>
    public Vector2 GetRandSpreadVec()
    {
        Vector2 vec = Rand.InsideUnitCircle * spreadDegrees;
        return vec;
    }

    public static string AsPercent(float pct)
    {
        return Mathf.RoundToInt(100f * pct) + "%";
    }

    public string GetTextReadout()
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine("   " + "CE_VisibilityError".Translate() + "\t" + GenText.ToStringByStyle(visibilityShift, ToStringStyle.FloatTwo) + " " + "CE_cells".Translate());

        if (Controller.settings.DebuggingMode)
        {
            stringBuilder.AppendLine("   " + $"DEBUG: visibilityShift\t\t{visibilityShift} ");
            stringBuilder.AppendLine("   " + $"DEBUG: leadDist\t\t{leadDist} ");
            stringBuilder.AppendLine("   " + $"DEBUG: environmentShift\t{environmentShift}");
            stringBuilder.AppendLine("   " + $"DEBUG: accuracyFactor\t{accuracyFactor}");
            stringBuilder.AppendLine("   " + $"DEBUG: circularMissRadius\t{circularMissRadius}");
            stringBuilder.AppendLine("   " + $"DEBUG: sightsEfficiency\t{sightsEfficiency}");
            stringBuilder.AppendLine("   " + $"DEBUG: weathershift\t\t{weatherShift}");
            stringBuilder.AppendLine("   " + $"DEBUG: accuracyFactor\t\t{accuracyFactor}");
            stringBuilder.AppendLine("   " + $"DEBUG: lightingShift\t\t{lightingShift}");
        }

        if (lightingShift > 0)
        {
            stringBuilder.AppendLine("      " + "Darkness".Translate() + "\t" + AsPercent(lightingShift));
        }
        if (weatherShift > 0)
        {
            stringBuilder.AppendLine("      " + "Weather".Translate() + "\t" + AsPercent(weatherShift));
        }
        if (smokeDensity > 0)
        {
            stringBuilder.AppendLine("      " + "CE_SmokeDensity".Translate() + "\t" + AsPercent(smokeDensity));
        }
        if (leadShift > 0)
        {
            stringBuilder.AppendLine("   " + "CE_LeadError".Translate() + "\t" + GenText.ToStringByStyle(leadShift, ToStringStyle.FloatTwo) + " " + "CE_cells".Translate());
        }
        if (distShift > 0)
        {
            stringBuilder.AppendLine("   " + "CE_RangeError".Translate() + "\t" + GenText.ToStringByStyle(distShift, ToStringStyle.FloatTwo) + " " + "CE_cells".Translate());
        }
        if (swayDegrees > 0)
        {
            stringBuilder.AppendLine("   " + "CE_Sway".Translate() + "\t\t" + GenText.ToStringByStyle(swayDegrees, ToStringStyle.FloatTwo) + "CE_degrees".Translate());
        }
        if (spreadDegrees > 0)
        {
            stringBuilder.AppendLine("   " + "CE_Spread".Translate() + "\t\t" + GenText.ToStringByStyle(spreadDegrees, ToStringStyle.FloatTwo) + "CE_degrees".Translate());
        }
        // Don't display cover and target size if our weapon has a CEP
        if (circularMissRadius > 0)
        {
            stringBuilder.AppendLine("   " + "CE_MissRadius".Translate() + "\t" + GenText.ToStringByStyle(circularMissRadius, ToStringStyle.FloatTwo) + " " + "CE_cells".Translate());
            if (indirectFireShift > 0)
            {
                stringBuilder.AppendLine("   " + "CE_IndirectFire".Translate() + "\t" + GenText.ToStringByStyle(indirectFireShift, ToStringStyle.FloatTwo) + " " + "CE_cells".Translate());
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_MortarDirectFire, KnowledgeAmount.FrameDisplayed); // Show we learned about indirect fire penalties
            }
        }
        else
        {
            if (cover != null)
            {
                stringBuilder.AppendLine("   " + "CE_CoverHeight".Translate() + "\t" + new CollisionVertical(cover).Max * CE_Utility.MetersPerCellHeight + " " + "CE_meters".Translate());
            }
            if (target.Thing != null)
            {
                bool crouching = target.Thing is Pawn pawn && pawn.IsCrouching();
                if (crouching)
                {
                    LessonAutoActivator.TeachOpportunity(CE_ConceptDefOf.CE_Crouching, OpportunityType.GoodToKnow);
                }
                stringBuilder.AppendLine("   " + "CE_TargetHeight".Translate() + "\t" + GenText.ToStringByStyle(new CollisionVertical(target.Thing).HeightRange.Span * CE_Utility.MetersPerCellHeight, ToStringStyle.FloatTwo) + " " + "CE_meters".Translate() + (string)(crouching ? " " + "CE_Crouched".Translate() : ""));
                stringBuilder.AppendLine("   " + "CE_TargetWidth".Translate() + "\t" + GenText.ToStringByStyle(CE_Utility.GetCollisionWidth(target.Thing) * CE_Utility.MetersPerCellHeight, ToStringStyle.FloatTwo) + " " + "CE_meters".Translate());
            }
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_AimingSystem, KnowledgeAmount.FrameDisplayed); // Show we learned about the aiming system
        }
        string estimate = HitChance;
        stringBuilder.AppendLine("   " + (burstEstimate == null ? "CE_EstimatedHitChance" : "CE_EstimatedBurstHitChance").Translate()
            + "\t" + (estimate == "-" ? "CE_BurstHitChanceUnavailable".Translate().ToString() : estimate + "%"));
        if (burstEstimate != null) stringBuilder.AppendLine("   " + "CE_BurstHitChanceExplanation".Translate());
        return stringBuilder.ToString();
    }
}
