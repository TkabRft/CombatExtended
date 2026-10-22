using System;

namespace CombatExtended.Utilities;

// Numeric snapshot only. No game objects, gameplay RNG, mutable verbs or projectile spawning.
internal sealed class BurstEstimateInput
{
    internal ShotVector Source, Target, Velocity, Minimum, Maximum;
    internal int Rounds, Interval, Pellets = 1;
    internal int[] RoundOptions;
    internal double Speed, Gravity, TrackingSpeed, AimHeight, TickPhase, RayRange;
    internal float Handling, Aiming, Sights, Sway, Spread, Recoil, MaxRange;
    internal float Lighting, Weather, Smoke, Circular, Indirect;
    internal bool Limited, Instant, Blind, Ballistic, LegacyLocked;
    internal double GravityHeight, MassMin, MassMax, DiameterMin, DiameterMax, CoefficientMin, CoefficientMax;
    internal Func<float, float> LightingMultiplier = distance => 1f;

    internal long Fingerprint()
    {
        // Positions and sway phase age out at 15 ticks; all mode/stat/shape inputs invalidate immediately.
        long hash = 17;
        foreach (double value in new[] { Rounds, Interval, Pellets, Speed, Gravity, TrackingSpeed, AimHeight, Source.Y, RayRange,
            Handling, Aiming, Sights, Sway, Spread, Recoil, MaxRange, Lighting, Weather, Smoke, Circular, Indirect,
            Velocity.X, Velocity.Z, Minimum.Y, Maximum.Y, Maximum.X - Minimum.X, Maximum.Z - Minimum.Z,
            Limited ? 1 : 0, Instant ? 1 : 0, Blind ? 1 : 0, Ballistic ? 1 : 0, LegacyLocked ? 1 : 0, GravityHeight,
            MassMin, MassMax, DiameterMin, DiameterMax, CoefficientMin, CoefficientMax })
        {
            hash = unchecked(hash * 31 + value.GetHashCode());
        }
        if (RoundOptions != null) foreach (int rounds in RoundOptions) hash = unchecked(hash * 31 + rounds);
        return hash;
    }
}

internal sealed class BurstEstimateCache
{
    private long key;
    private int tick;
    private float value;
    private bool valid;
    internal bool TryGet(long requestedKey, int currentTick, out float result)
    {
        result = value;
        return valid && key == requestedKey && currentTick >= tick && (long)currentTick - tick < 15;
    }
    internal void Store(long requestedKey, int currentTick, float result)
    {
        key = requestedKey;
        tick = currentTick;
        value = result;
        valid = true;
    }
}

internal static class BurstHitEstimator
{
    internal const int Samples = 512;

    internal static float Estimate(BurstEstimateInput input, int samples = Samples, uint seed = 0xCE160001)
    {
        if (input.Rounds <= 0 || samples <= 0 || input.Speed <= 0 || input.Maximum.Y <= input.Minimum.Y) return 0;
        var random = new SampleRandom(seed);
        long hits = 0, emitted = 0;
        var tracking = new BurstTrackingState();
        for (int sample = 0; sample < samples; sample++)
        {
            tracking.Reset();
            int rounds = input.RoundOptions == null ? input.Rounds : input.RoundOptions[(int)(random.Next() * input.RoundOptions.Length)];
            emitted += rounds;
            double rangeSample = random.Signed();
            double lockedRotationError = 0, lockedElevationError = 0;
            for (int round = 0; round < rounds; round++)
            {
                int tick = round * input.Interval;
                double elapsed = tick / 60.0;
                ShotVector current = input.Target + input.Velocity * elapsed;
                double distance = (current - input.Source).Horizontal;
                float lightingRange = input.LightingMultiplier((float)distance);
                double accuracy = AccuracyMath.RangeAccuracy(input.Aiming, input.Sights);
                double rangeError = distance * (distance / Math.Max(input.MaxRange, 20)) * Math.Min(accuracy * 0.5, 0.8);
                double environment = ((input.Blind ? 1 : input.Lighting) * 7 + input.Weather * 1.5) * lightingRange + input.Smoke;
                double visibility = environment * distance / 50 / Math.Max(input.Sights, 0.02f) * AccuracyMath.Visibility(input.Aiming);
                double leadDistance = input.Velocity.Horizontal * distance / input.Speed;
                double leadError = leadDistance * Math.Min(accuracy * 0.25, 2.5)
                    + (Math.Min((input.Blind ? 1 : input.Lighting) * lightingRange * leadDistance * 0.25, input.Blind ? 100 : 2)
                    + Math.Min((input.Blind ? 0 : input.Smoke) * 0.5, 2)) * AccuracyMath.EnvironmentalLead(input.Aiming);
                ShotVector lead = input.Instant || input.Blind ? new ShotVector() : ShotGeometry.Lead(input.Source, current, input.Velocity, input.Speed);
                ShotVector clean = current + lead;
                ShotVector noisyLead = lead;
                if (lead.Horizontal > 0) noisyLead += lead * (random.Signed() * leadError / lead.Horizontal);
                ShotVector noisy = ShotGeometry.OffsetTarget(input.Source, current,
                    Math.Max(0.001, distance + rangeSample * rangeError),
                    random.Circle(visibility + input.Circular + input.Indirect, false), noisyLead);
                double cleanRotation = ShotGeometry.Rotation(clean - input.Source);
                double cleanElevation = ShotGeometry.Elevation((clean - input.Source).Horizontal,
                    input.AimHeight - input.Source.Y, input.Speed, input.Gravity, input.Instant);
                double noisyRotation = ShotGeometry.Rotation(noisy - input.Source);
                double noisyElevation = ShotGeometry.Elevation((noisy - input.Source).Horizontal,
                    input.AimHeight - input.Source.Y, input.Speed, input.Gravity, input.Instant);
                if (double.IsNaN(cleanElevation) || double.IsNaN(noisyElevation)) continue;
                tracking.Advance(cleanRotation, cleanElevation * ShotGeometry.Degrees, tick, input.TrackingSpeed, input.Limited);
                double rotationError = BurstTrackingState.Delta(cleanRotation, noisyRotation);
                double elevationError = noisyElevation - cleanElevation;
                if (round == 0) { lockedRotationError = rotationError; lockedElevationError = elevationError; }
                if (input.LegacyLocked) { rotationError = lockedRotationError; elevationError = lockedElevationError; }
                double phase = input.TickPhase + tick;
                double recoil = AccuracyMath.Recoil(input.Handling, round) * input.Recoil;
                double rotation = tracking.Rotation + rotationError
                    + input.Sway * Math.Sin(phase * 0.022) + recoil * random.Signed() * 0.5;
                double elevation = tracking.Elevation / ShotGeometry.Degrees + elevationError
                    + (input.Sway * 0.25 * Math.Sin(phase * 0.0165)
                    + recoil * (-1.0 / 3 + random.Next() * 4.0 / 3)) / ShotGeometry.Degrees;
                bool hit = false;
                for (int pellet = 0; pellet < input.Pellets; pellet++)
                {
                    ShotVector spread = random.Circle(input.Spread, true);
                    hit |= Hits(input, current, rotation + spread.X, elevation + spread.Z / ShotGeometry.Degrees, ref random);
                }
                if (hit) hits++;
            }
        }
        return emitted > 0 ? (float)(hits / (double)emitted) : 0;
    }

    private static bool Hits(BurstEstimateInput input, ShotVector current, double rotation, double elevation, ref SampleRandom random)
    {
        double r = rotation / ShotGeometry.Degrees;
        ShotVector velocity = new ShotVector(-Math.Sin(r) * Math.Cos(elevation), Math.Sin(elevation), Math.Cos(r) * Math.Cos(elevation)) * input.Speed;
        ShotVector shift = current - input.Target;
        ShotVector min = input.Minimum + shift, max = input.Maximum + shift;
        if (input.Instant)
            return ShotGeometry.Intersects(input.Source, input.Source + velocity * ((input.RayRange > 0 ? input.RayRange : input.MaxRange) / input.Speed), min, max);

        double mass = random.Range(input.MassMin, input.MassMax);
        double radius = random.Range(input.DiameterMin, input.DiameterMax) / 2000;
        double coefficient = random.Range(input.CoefficientMin, input.CoefficientMax);
        ShotVector previous = input.Source;
        // Stop after traversing the target, rather than simulating the rest of the map.
        double relativeSpeed = velocity.Horizontal - input.Velocity.Horizontal;
        double duration = ((current - input.Source).Horizontal + 4) / Math.Max(1, relativeSpeed) + 0.1;
        int ticks = (int)Math.Min(36000, Math.Ceiling(duration * 60));
        for (int tick = 1; tick <= ticks; tick++)
        {
            double time = tick / 60.0;
            ShotVector next;
            if (input.Ballistic)
            {
                double speedPerTick = velocity.Length / 60;
                double drag = coefficient > 0 && mass > 0 ? 2.5 * speedPerTick * speedPerTick * radius * radius * Math.PI / coefficient / mass : 0;
                double scale = speedPerTick > 0 ? Math.Max(0, 1 - drag / speedPerTick) : 0;
                velocity = (velocity * scale).WithY(velocity.Y * scale - input.GravityHeight / 60);
                next = previous + velocity * (1.0 / 60);
            }
            else
            {
                next = input.Source + velocity * time;
                next = next.WithY(Math.Round(input.Source.Y + velocity.Y * time - input.Gravity * time * time / 2, 3));
            }
            if (ShotGeometry.Intersects(previous - input.Velocity * ((tick - 1) / 60.0),
                next - input.Velocity * time, min, max)) return true;
            previous = next;
            if (next.Y < min.Y && (input.Ballistic ? velocity.Y : velocity.Y - input.Gravity * time) < 0) break;
        }
        return false;
    }

    // A local, platform-independent stream. Never calls Verse.Rand or System.Random.
    private struct SampleRandom
    {
        private uint state;
        internal SampleRandom(uint seed) { state = seed == 0 ? 1u : seed; }
        internal double Next()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state + 0.5) / 4294967296.0;
        }
        internal double Signed() => Next() * 2 - 1;
        internal double Range(double min, double max) => min + (max - min) * Next();
        internal ShotVector Circle(double radius, bool uniformArea)
        {
            double angle = Next() * Math.PI * 2;
            double distance = (uniformArea ? Math.Sqrt(Next()) : Next()) * radius;
            return new ShotVector(Math.Cos(angle) * distance, 0, Math.Sin(angle) * distance);
        }
    }
}
