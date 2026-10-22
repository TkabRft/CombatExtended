using System;

namespace CombatExtended.Utilities;

internal enum BurstSightAction { Stop, HoldLastPosition, FollowSuppressionTarget }

internal sealed class BurstTrackingState
{
    internal bool Initialized { get; private set; }
    internal double Rotation { get; private set; }
    internal double Elevation { get; private set; }
    private int lastTick;

    internal static double Delta(double from, double to)
    {
        double delta = (to - from) % 360;
        if (delta > 180) delta -= 360;
        if (delta < -180) delta += 360;
        return delta;
    }

    internal static double Speed(double handling, double mass, double carryWeight) =>
        60 * (Math.Max(0, handling) / 2.5) * Math.Sqrt((Math.Max(0, carryWeight) / 40) * (4 / Math.Max(0.01, mass)));

    internal static BurstSightAction LostSight(bool previousLine, bool locked, bool interruptible, bool suppressing, bool validTarget)
    {
        if (!previousLine) return BurstSightAction.Stop;
        if (locked) return interruptible && !suppressing ? BurstSightAction.Stop : BurstSightAction.HoldLastPosition;
        return suppressing && validTarget ? BurstSightAction.FollowSuppressionTarget : BurstSightAction.Stop;
    }

    internal void Advance(double rotation, double elevation, int tick, double speed, bool limited)
    {
        if (!Initialized || !limited)
        {
            Rotation = rotation;
            Elevation = elevation;
        }
        else
        {
            double allowance = Math.Max(0, (long)tick - lastTick) / 60.0 * speed;
            Rotation += Move(Delta(Rotation, rotation), allowance);
            Elevation += Move(Delta(Elevation, elevation), allowance);
        }
        Initialized = true;
        lastTick = tick;
    }

    private static double Move(double delta, double allowance) => Math.Max(-allowance, Math.Min(allowance, delta));

    internal void Reset()
    {
        Initialized = false;
        Rotation = Elevation = 0;
        lastTick = 0;
    }
}
