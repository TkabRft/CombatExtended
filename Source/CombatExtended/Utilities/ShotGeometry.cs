using System;

namespace CombatExtended.Utilities;

internal readonly struct ShotVector
{
    internal readonly double X, Y, Z;
    internal ShotVector(double x, double y, double z) { X = x; Y = y; Z = z; }
    internal double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    internal double Horizontal => Math.Sqrt(X * X + Z * Z);
    internal ShotVector WithY(double y) => new ShotVector(X, y, Z);
    public static ShotVector operator +(ShotVector a, ShotVector b) => new ShotVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static ShotVector operator -(ShotVector a, ShotVector b) => new ShotVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static ShotVector operator *(ShotVector a, double b) => new ShotVector(a.X * b, a.Y * b, a.Z * b);
}

internal static class ShotGeometry
{
    internal const double Degrees = 180 / Math.PI;
    internal static double Rotation(ShotVector delta) => Math.Atan2(-delta.X, delta.Z) * Degrees;

    internal static double Elevation(double range, double height, double speed, double gravity, bool instant, bool overhead = false)
    {
        if (instant || gravity == 0) return Math.Atan2(height, range);
        double v2 = speed * speed;
        double discriminant = v2 * v2 - gravity * (gravity * range * range + 2 * height * v2);
        if (discriminant < 0 || range <= 0 || speed <= 0) return double.NaN;
        // Rationalize the low arc to avoid catastrophic cancellation with very fast rounds.
        return overhead ? Math.Atan((v2 + Math.Sqrt(discriminant)) / (gravity * range))
            : Math.Atan((gravity * range * range + 2 * height * v2) / (range * (v2 + Math.Sqrt(discriminant))));
    }

    internal static ShotVector Lead(ShotVector source, ShotVector position, ShotVector velocity, double speed)
    {
        if (speed <= 0) return new ShotVector();
        ShotVector relative = (position - source).WithY(0);
        velocity = velocity.WithY(0);
        double c = relative.X * relative.X + relative.Z * relative.Z;
        if (c == 0) return new ShotVector();
        double velocitySquared = velocity.X * velocity.X + velocity.Z * velocity.Z;
        double a = velocitySquared - speed * speed;
        double b = 2 * (relative.X * velocity.X + relative.Z * velocity.Z);
        // If interception is impossible, retain a finite forward prediction.
        double time = Math.Sqrt(c) / speed;
        if (Math.Abs(a) <= 1e-12 * (velocitySquared + speed * speed))
        {
            if (b < 0) time = -c / b;
        }
        else
        {
            double discriminant = b * b - 4 * a * c;
            if (discriminant >= 0)
            {
                // Stable quadratic roots avoid cancellation for approaching targets.
                double q = -0.5 * (b + (b >= 0 ? 1 : -1) * Math.Sqrt(discriminant));
                double first = q / a;
                double second = q == 0 ? double.PositiveInfinity : c / q;
                double intercept = Math.Min(first > 0 ? first : double.PositiveInfinity,
                    second > 0 ? second : double.PositiveInfinity);
                if (!double.IsInfinity(intercept)) time = intercept;
            }
        }
        return velocity * time;
    }

    // Use the path follower's actual step duration, including diagonal travel,
    // locomotion urgency, terrain and other movement-cost modifiers. Render offsets
    // belong to the collision position, never to movement heading or speed.
    internal static ShotVector StepVelocity(ShotVector cellDelta, double stepTicks, bool moving)
        => !moving || stepTicks <= 0 || double.IsNaN(stepTicks) || double.IsInfinity(stepTicks)
            ? new ShotVector() : cellDelta.WithY(0) * (60 / stepTicks);

    internal static ShotVector OffsetTarget(ShotVector source, ShotVector target, double range,
        ShotVector circular, ShotVector lead)
    {
        ShotVector direction = (target + circular - source).WithY(0);
        double length = direction.Horizontal;
        return source.WithY(0) + (length > 0 ? direction * (range / length) : new ShotVector()) + lead;
    }

    // Slab intersection with the actual axis-aligned collision box, not its diagonal projection.
    internal static bool Intersects(ShotVector from, ShotVector to, ShotVector minimum, ShotVector maximum)
    {
        double near = 0, far = 1;
        return Slab(from.X, to.X - from.X, minimum.X, maximum.X, ref near, ref far)
            && Slab(from.Y, to.Y - from.Y, minimum.Y, maximum.Y, ref near, ref far)
            && Slab(from.Z, to.Z - from.Z, minimum.Z, maximum.Z, ref near, ref far);
    }

    private static bool Slab(double origin, double direction, double min, double max, ref double near, ref double far)
    {
        if (max <= min) return false;
        if (Math.Abs(direction) < 1e-12) return origin >= min && origin <= max;
        double a = (min - origin) / direction, b = (max - origin) / direction;
        near = Math.Max(near, Math.Min(a, b));
        far = Math.Min(far, Math.Max(a, b));
        return near <= far;
    }
}
