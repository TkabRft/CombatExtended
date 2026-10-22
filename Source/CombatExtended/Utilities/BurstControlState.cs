using System;

namespace CombatExtended.Utilities;

internal enum BurstControlAction { Continue, Hold, Retargeted, Stop }

// Numeric, transient state shared by the firing adapter and standalone fixtures.
internal sealed class BurstControlState
{
    internal bool Initialized { get; private set; }
    internal bool FinishingOrder { get; private set; }
    internal bool CanReact { get; private set; }
    internal bool Aware { get; private set; }
    internal bool AnchorFrozen { get; private set; }
    internal bool HasSolution { get; private set; }
    internal int OriginalId { get; private set; }
    internal int CurrentId { get; private set; }
    internal ShotVector Anchor { get; private set; }
    internal ShotVector LastPosition { get; private set; }
    internal double Height { get; private set; }
    internal double Rotation { get; private set; }
    internal double Elevation { get; private set; }

    internal static double ConsciousnessFactor(double consciousness)
    {
        double c = Math.Max(0, consciousness);
        return c <= 1 ? c * c * c : 1 + 0.15 * ((c - 1) / (0.05 + c - 1));
    }

    internal static double ReactionChance(double skill, double consciousness)
        => Math.Min(1, (0.002 + 0.998 * Math.Pow(Math.Max(0, Math.Min(20, skill)) / 20, 4.0 / 3))
            * ConsciousnessFactor(consciousness));

    internal static double AngleLimit(double distance)
        => 45 - 35 * Math.Max(0, Math.Min(1, (distance - 5) / 15));

    internal static bool CandidateScore(ShotVector source, ShotVector anchor, ShotVector position,
        ShotVector cleanAim, bool eligible, out double angle, out double distance)
    {
        distance = (position - anchor).Horizontal;
        angle = Math.Abs(BurstTrackingState.Delta(ShotGeometry.Rotation(anchor - source),
            ShotGeometry.Rotation(cleanAim - source)));
        return eligible && distance <= 5 && angle <= AngleLimit((anchor - source).Horizontal);
    }

    internal static bool Better(double angle, double distance, int id, double bestAngle, double bestDistance, int bestId)
        => angle < bestAngle || (angle == bestAngle && (distance < bestDistance || (distance == bestDistance && id < bestId)));

    internal void Begin(int targetId, bool standing, ShotVector position, bool reconstructed = false)
    {
        Reset();
        Initialized = true;
        OriginalId = CurrentId = targetId;
        Anchor = LastPosition = position;
        FinishingOrder = !standing && !reconstructed;
        CanReact = reconstructed;
    }

    internal void Observe(bool standing, ShotVector position)
    {
        if (!standing) FreezeAnchor();
        else
        {
            Aware = false;
            if (!AnchorFrozen && CurrentId == OriginalId) Anchor = position;
        }
    }

    internal void FreezeAnchor() => AnchorFrozen = true;

    internal void Switch(int targetId)
    {
        FreezeAnchor();
        CurrentId = targetId;
        Aware = false;
    }

    internal void RecordSolution(ShotVector position, double height, double rotation, double elevation)
    {
        LastPosition = position;
        Height = height;
        Rotation = rotation;
        Elevation = elevation;
        HasSolution = true;
    }

    internal void RecordEmission() => CanReact = true;

    // Holding aim still consumes an emitted round's tracking time. Do not bank that
    // time and spend it as an instantaneous turn after finally noticing a new target.
    internal void AdvanceHeldTracking(BurstTrackingState tracking, int tick)
        => tracking.Advance(Rotation, Elevation, tick, 0, true);

    internal BurstControlAction React(bool incapacitated, bool retarget, bool stop, double skill,
        double consciousness, Func<double> random, Func<bool> selectReplacement)
    {
        if (!CanReact || FinishingOrder || !incapacitated || (!retarget && !stop)) return BurstControlAction.Continue;
        FreezeAnchor();
        if (!Aware)
        {
            double chance = ReactionChance(skill, consciousness);
            Aware = chance >= 1 || (chance > 0 && random() < chance);
        }
        if (!Aware) return BurstControlAction.Hold;
        if (retarget && selectReplacement()) return BurstControlAction.Retargeted;
        return stop ? BurstControlAction.Stop : BurstControlAction.Hold;
    }

    internal void Reset()
    {
        Initialized = FinishingOrder = CanReact = Aware = AnchorFrozen = HasSolution = false;
        OriginalId = CurrentId = 0;
        Anchor = LastPosition = new ShotVector();
        Height = Rotation = Elevation = 0;
    }
}
