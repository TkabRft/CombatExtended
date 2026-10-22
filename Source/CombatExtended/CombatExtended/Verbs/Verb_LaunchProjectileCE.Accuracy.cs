using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended.HarmonyCE;
using CombatExtended.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace CombatExtended;

public partial class Verb_LaunchProjectileCE
{
    private readonly BurstEstimateCache burstEstimateCache = new BurstEstimateCache();

    private float? EstimateBurst(LocalTargetInfo target, ShiftVecReport report)
    {
        var props = projectilePropsCE;
        var worker = props.TrajectoryWorker.GetType();
        if (target.Thing == null || props.flyOverhead || props.fuelTicks != 0 || props.speedGain != 0
            || (worker != typeof(LerpedTrajectoryWorker) && worker != typeof(BallisticsTrajectoryWorker) && worker != typeof(BaseTrajectoryWorker)))
            return null;
        var unique = EquipmentSource?.TryGetComp<CompUniqueWeapon>();
        if (unique?.TraitsListForReading.Any(trait => trait is CustomWeaponTraitDef { homingAcceleration: > 0 }) == true)
            return null;

        Vector3 source = caster.TrueCenter().WithY(ShotHeight);
        Vector3 position = target.Thing.DrawPos;
        Bounds bounds = CE_Utility.GetBoundsFor(target.Thing);
        Vector3 minimum = bounds.min;
        minimum.y = Mathf.Max(minimum.y, new CollisionVertical(report.cover).Max);
        Vector2 velocity = report.MoveVelocity;
        var counts = PreviewBurstCounts(unique);
        var input = new BurstEstimateInput
        {
            Source = ToShotVector(source), Target = ToShotVector(position),
            Velocity = new ShotVector(velocity.x, 0, velocity.y), Minimum = ToShotVector(minimum), Maximum = ToShotVector(bounds.max),
            Rounds = counts.Max(), RoundOptions = counts, Pellets = props.pelletCount,
            Interval = Harmony_Verb_TryCastNextBurstShot.GetTicksBetweenBurstShots(this),
            Speed = report.shotSpeed, Gravity = props.GravityPerWidth, GravityHeight = props.GravityPerHeight,
            Ballistic = worker == typeof(BallisticsTrajectoryWorker),
            TrackingSpeed = UsesBurstTracking ? TrackingSpeed : 0,
            Limited = !UsesBurstTracking || Controller.settings.LimitTrackingSpeed,
            LegacyLocked = !UsesBurstTracking, RayRange = verbProps.range,
            AimHeight = GetTargetHeight(target, report.cover, report.roofed, position),
            TickPhase = Find.TickManager.TicksAbs + Shooter.thingIDNumber,
            Handling = ShootingAccuracy, Aiming = report.aimingAccuracy, Sights = report.sightsEfficiency,
            Sway = report.swayDegrees, Spread = props.isInstant && props.damageFalloff ? 0 : report.spreadDegrees,
            Recoil = RecoilAmount, MaxRange = report.maxRange,
            Lighting = report.lightingShift, Weather = report.weatherShift, Smoke = report.smokeDensity,
            Circular = report.circularMissRadius, Indirect = report.indirectFireShift,
            Instant = props.isInstant, Blind = report.blindFiring,
            MassMin = props.mass.min, MassMax = props.mass.max,
            DiameterMin = props.diameter.min, DiameterMax = props.diameter.max,
            CoefficientMin = props.ballisticCoefficient.min, CoefficientMax = props.ballisticCoefficient.max,
            LightingMultiplier = CE_Utility.LightingRangeMultiplier
        };
        long key = input.Fingerprint();
        key = unchecked(key * 31 + target.Thing.thingIDNumber);
        key = unchecked(key * 31 + (EquipmentSource?.thingIDNumber ?? 0));
        key = unchecked(key * 31 + Projectile.shortHash);
        key = unchecked(key * 31 + (CompAmmo?.CurrentAmmo?.shortHash ?? 0));
        key = unchecked(key * 31 + (int)(CompFireModes?.CurrentAimMode ?? AimMode.Snapshot));
        key = unchecked(key * 31 + (int)(CompFireModes?.CurrentFireMode ?? FireMode.AutoFire));
        key = unchecked(key * 31 + (CompAmmo?.CurMagCount ?? -1));
        int tick = Find.TickManager.TicksGame;
        if (burstEstimateCache.TryGet(key, tick, out float cached)) return cached;
        float estimate = BurstHitEstimator.Estimate(input);
        burstEstimateCache.Store(key, tick, estimate);
        return estimate;
    }

    // ShotsPerBurstFor can draw a unique-weapon multiplier from gameplay RNG. Enumerate instead.
    private int[] PreviewBurstCounts(CompUniqueWeapon unique)
    {
        var mode = CompFireModes?.CurrentFireMode ?? FireMode.AutoFire;
        float count = VerbPropsCE.burstShotCount;
        bool fixedCount = false;
        if (CompFireModes != null && mode == FireMode.SingleFire) { count = 1; fixedCount = true; }
        else if (CompFireModes != null && mode == FireMode.BurstFire && CompFireModes.Props.aimedBurstShotCount > 0)
        { count = CompFireModes.Props.aimedBurstShotCount; fixedCount = true; }
        else if (VerbPropsCE.useEquipmentStatValues && EquipmentSource != null
            && !(EquipmentSource.TryGetComp<CompUnderBarrel>()?.usingUnderBarrel ?? false))
        {
            float modified = EquipmentSource.GetStatValue(CE_StatDefOf.BurstShotCount);
            if (modified > 0) count = modified;
        }
        List<float> multipliers = fixedCount ? null : unique?.TraitsListForReading.OfType<CustomWeaponTraitDef>()
            .FirstOrDefault(trait => trait.burstShotCountMultipliers?.Count > 0)?.burstShotCountMultipliers;
        int available = int.MaxValue;
        if (CompAmmo?.HasMagazine == true)
        {
            int consumed = Math.Max(1, (CompAmmo.Props.ammoSet?.ammoConsumedPerShot ?? 1) * VerbPropsCE.ammoConsumedPerShotCount);
            available = (int)Math.Ceiling(CompAmmo.CurMagCount / (double)consumed);
        }
        if (multipliers == null) return new[] { Math.Min(available, Math.Max(0, (int)count)) };
        return multipliers.Select(multiplier => Math.Min(available, Math.Max(0, (int)(count * multiplier)))).ToArray();
    }

    private static ShotVector ToShotVector(Vector3 value) => new ShotVector(value.x, value.y, value.z);
}
