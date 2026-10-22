using System;
using CombatExtended.Utilities;
using Xunit;

namespace Tests;

public class BurstEstimateTests
{
    private static BurstEstimateInput Scene() => new()
    {
        Source = new(0, 0.8, 0), Target = new(0, 0, 30), Minimum = new(-0.18, 0, 29.82), Maximum = new(0.18, 1.6, 30.18),
        Rounds = 6, Interval = 4, Speed = 150, Instant = true, AimHeight = 0.8,
        Aiming = 1e10f, Handling = 2.875f, Sights = 1, MaxRange = 55,
        Recoil = 1.5f, Spread = 0.07f, Sway = 0.2f, TrackingSpeed = 60,
        MassMin = 1, MassMax = 1, CoefficientMin = 1, CoefficientMax = 1
    };

    [Fact]
    public void SamplingIsRepeatableAndDoesNotChangeTheSnapshot()
    {
        var input = Scene();
        long fingerprint = input.Fingerprint();
        float first = BurstHitEstimator.Estimate(input);
        Assert.Equal(first, BurstHitEstimator.Estimate(input));
        Assert.Equal(fingerprint, input.Fingerprint());
        Assert.Equal(6, input.Rounds); Assert.Equal(0, input.Target.X);
        Assert.InRange(first, 0.01f, 0.99f);
    }

    [Fact]
    public void ReaimingAndStrengthImproveFastCrossingTracking()
    {
        var input = Scene();
        input.Target = new(0, 0, 10); input.Minimum = new(-0.18, 0, 9.82); input.Maximum = new(0.18, 1.6, 10.18);
        input.Velocity = new(8, 0, 0); input.Rounds = 10; input.Interval = 7;
        input.Recoil = input.Spread = input.Sway = 0;
        input.Limited = true; input.TrackingSpeed = 10;
        float slow = BurstHitEstimator.Estimate(input);
        input.TrackingSpeed = 60;
        float strong = BurstHitEstimator.Estimate(input);
        input.Limited = false;
        float free = BurstHitEstimator.Estimate(input);
        Assert.True(strong > slow);
        Assert.Equal(1, free);
    }

    [Fact]
    public void SpreadRemainsIndependentOfPawnStatsAndPelletsDoNotAdvanceTracking()
    {
        var input = Scene();
        input.Recoil = input.Sway = 0; input.Spread = 1;
        float initial = BurstHitEstimator.Estimate(input);
        input.Handling = 1000; input.Aiming *= 100;
        Assert.Equal(initial, BurstHitEstimator.Estimate(input));
        input.Pellets = 8;
        Assert.True(BurstHitEstimator.Estimate(input) > initial);
    }

    [Fact]
    public void EmptyMagazineAndFullyCoveredTargetsHaveZeroChance()
    {
        var input = Scene(); input.Rounds = 0;
        Assert.Equal(0, BurstHitEstimator.Estimate(input));
        input.Rounds = 1; input.Minimum = new(-0.18, 2, 29.82);
        Assert.Equal(0, BurstHitEstimator.Estimate(input));
    }

    [Fact]
    public void InstantEstimatesRespectTheRaycastRange()
    {
        var input = Scene(); input.RayRange = 10;
        Assert.Equal(0, BurstHitEstimator.Estimate(input));
    }

    [Fact]
    public void CleanBallisticShotsUseFlightTimeAndMovingCollisionBounds()
    {
        var input = Scene(); input.Instant = false; input.Gravity = 1.962;
        input.Recoil = input.Spread = input.Sway = 0; input.Velocity = new(3, 0, 0);
        Assert.Equal(1, BurstHitEstimator.Estimate(input));
    }

    [Fact]
    public void CacheExpiresAtFifteenTicksAndInvalidatesForChangedInputs()
    {
        var input = Scene();
        var cache = new BurstEstimateCache();
        long key = input.Fingerprint();
        cache.Store(key, 100, 0.5f);
        Assert.True(cache.TryGet(key, 114, out float value)); Assert.Equal(0.5f, value);
        Assert.False(cache.TryGet(key, 115, out _));
        input.Recoil += 1; Assert.False(cache.TryGet(input.Fingerprint(), 101, out _));
        Assert.False(cache.TryGet(key, 99, out _));
    }

    [Theory]
    [InlineData(false, 0, false)] [InlineData(false, 4, false)] [InlineData(true, 4, false)]
    [InlineData(false, 0, true)] [InlineData(false, 4, true)] [InlineData(true, 4, true)]
    public void EstimateAgreesWithIndependentLargeSimulation(bool limited, int moveSpeed, bool flight)
    {
        var input = Scene(); input.Velocity = new(moveSpeed, 0, 0); input.Limited = limited; input.TrackingSpeed = 2;
        input.Instant = !flight; input.Gravity = flight ? 1.962 : 0;
        float estimate = BurstHitEstimator.Estimate(input);
        double reference = IndependentSimulation(input, 60000);
        Assert.InRange(Math.Abs(estimate - reference), 0, 0.05);
    }

    [Theory]
    [InlineData(0.6f)] [InlineData(1.9f)] [InlineData(4f)]
    public void ExpertHeavyWeaponCombinesSwayRecoilAndModeTiming(float weaponSway)
    {
        var input = Scene();
        input.Target = new(0, 0, 60); input.Minimum = new(-0.18, 0, 59.82); input.Maximum = new(0.18, 1.6, 60.18);
        input.MaxRange = 87; input.Rounds = 10; input.Interval = 6;
        input.Handling = 3.615f; input.Aiming = 1.5f; input.Sights = 1.5f;
        input.Recoil = 2.8f * 0.9f; input.Spread = 0.03f;
        float sway = AccuracyMath.HandlingSway(input.Handling) * weaponSway * 0.9f;
        input.Sway = AccuracyMath.ModeSway(sway, input.Aiming, input.Sights, true, false);
        float aimed = BurstHitEstimator.Estimate(input);
        Assert.InRange(aimed, 0.8f, 1);
        Assert.InRange(Math.Abs(aimed - IndependentSimulation(input, 60000)), 0, 0.05);

        input.Sway = AccuracyMath.ModeSway(sway, input.Aiming, input.Sights, false, false);
        float snapshot = BurstHitEstimator.Estimate(input);
        Assert.True(aimed > snapshot + 0.25f);
        // Kord's base warmup with deployed bipod, plus the nine firing intervals.
        double snapshotTime = 2.1 * 0.85 + 9 * 6 / 60.0;
        double aimedTime = snapshotTime + AccuracyMath.AimPreparationTicks(60, input.Handling, input.Aiming) / 60.0;
        Assert.True(aimed / aimedTime > snapshot / snapshotTime);

        float single = 0, burst = 0;
        foreach (int rounds in new[] { 1, 3, 10 })
        {
            input.Rounds = rounds;
            input.Sway = AccuracyMath.ModeSway(sway, input.Aiming, input.Sights, true, false);
            float estimate = BurstHitEstimator.Estimate(input);
            if (rounds == 1) single = estimate;
            else if (rounds == 3) burst = estimate;
            else Assert.True(single >= burst && burst >= estimate);
        }
    }

    [Theory]
    [InlineData(3.615f)] [InlineData(4.5f)] [InlineData(10f)]
    public void ExpertBurstEstimateAgreesWithIndependentRecoilModel(float handling)
    {
        var input = Scene(); input.Handling = handling;
        Assert.InRange(Math.Abs(BurstHitEstimator.Estimate(input) - IndependentSimulation(input, 60000)), 0, 0.05);
    }

    // Separate RNG and ray/plane calculation. The target's small depth makes the plane
    // approximation negligible relative to the five-percentage-point calibration tolerance.
    private static double IndependentSimulation(BurstEstimateInput input, int samples)
    {
        var random = new Random(9876543);
        double distance = input.Target.Z;
        int hits = 0;
        for (int sample = 0; sample < samples; sample++)
        {
            double heading = 0;
            for (int shot = 0; shot < input.Rounds; shot++)
            {
                double elapsed = shot * input.Interval / 60.0;
                double x = input.Velocity.X * elapsed;
                double leadTime = Math.Sqrt(x * x + distance * distance) / input.Speed;
                if (input.Instant) leadTime = 0;
                double leadX = x + input.Velocity.X * leadTime;
                double desired = -Math.Atan2(leadX, distance) * 180 / Math.PI;
                double range = Math.Sqrt(leadX * leadX + distance * distance);
                double idealElevation = input.Instant ? 0 : Math.Atan((input.Speed * input.Speed
                    - Math.Sqrt(Math.Pow(input.Speed, 4) - input.Gravity * input.Gravity * range * range)) / (input.Gravity * range));
                double turn = input.TrackingSpeed * input.Interval / 60.0;
                heading = shot == 0 || !input.Limited ? desired : heading + Math.Clamp(desired - heading, -turn, turn);
                double excess = Math.Max(0, input.Handling - 2.875);
                double recoilBase = input.Handling <= 4.5 ? 5 - input.Handling : 0.25 / (input.Handling - 4);
                double recoil = shot == 0 ? 0 : Math.Pow(recoilBase, Math.Min(10, shot) / 6.25)
                    * input.Recoil / (1 + 16 * excess * excess);
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = Math.Sqrt(random.NextDouble()) * input.Spread;
                double rotation = heading + input.Sway * Math.Sin(shot * input.Interval * 0.022)
                    + (random.NextDouble() - 0.5) * recoil + Math.Cos(angle) * radius;
                double elevation = input.Sway * 0.25 * Math.Sin(shot * input.Interval * 0.0165)
                    + (-1.0 / 3 + random.NextDouble() * 4.0 / 3) * recoil + Math.Sin(angle) * radius;
                elevation += idealElevation * 180 / Math.PI;
                double dx = -Math.Sin(rotation * Math.PI / 180), dz = Math.Cos(rotation * Math.PI / 180);
                double px = distance * dx / dz;
                double py = 0.8 + Math.Tan(elevation * Math.PI / 180) * distance / dz;
                double time = input.Instant ? 0 : distance / (input.Speed * Math.Cos(elevation * Math.PI / 180) * dz);
                py -= input.Gravity * time * time / 2;
                if (Math.Abs(px - x - input.Velocity.X * time) <= 0.18 && py >= 0 && py <= 1.6) hits++;
            }
        }
        return hits / (double)(samples * input.Rounds);
    }
}
