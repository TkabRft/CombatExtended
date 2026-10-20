using System;

namespace CombatExtended.Utilities;

// Transient burst history; linked directly into the standalone tests.
internal sealed class SubsequentShotState
{
    private float? pendingFactor;
    private float pendingMultiplier = 1f;

    internal bool HasPreviousShot { get; private set; }
    internal float PreviousMultiplier { get; private set; } = 1f;
    internal float BurstRecoil { get; private set; }

    internal static float AngleDifference(float previous, float current)
    {
        float delta = Math.Abs(current - previous) % 360f;
        return Math.Min(delta, 360f - delta);
    }

    internal bool CanReduceWarmup(bool enabled, bool hasMagazine)
    {
        if (!enabled || !hasMagazine)
        {
            Reset();
        }
        return HasPreviousShot;
    }

    internal float GetWarmupMultiplier(float angleFactor, float recoilFactor, float modeFloor)
    {
        if (!HasPreviousShot)
        {
            return 1f;
        }

        // Both warmup phases use the same preceding burst and multiplier.
        pendingFactor ??= (angleFactor + recoilFactor) * PreviousMultiplier;
        pendingMultiplier = Math.Min(1f, Math.Max(modeFloor, pendingFactor.Value));
        return pendingMultiplier;
    }

    internal bool RecordShot(bool firstInBurst, float recoil, bool enabled, bool hasMagazine, bool firstPellet = true)
    {
        if (!firstPellet)
        {
            return false;
        }
        if (!enabled || !hasMagazine)
        {
            Reset();
            return true;
        }

        if (firstInBurst || !HasPreviousShot)
        {
            PreviousMultiplier = pendingFactor.HasValue ? pendingMultiplier : 1f;
            pendingFactor = null;
            pendingMultiplier = 1f;
            BurstRecoil = 0f;
            HasPreviousShot = true;
        }
        BurstRecoil += recoil;
        return true;
    }

    internal void Reset()
    {
        HasPreviousShot = false;
        PreviousMultiplier = 1f;
        BurstRecoil = 0f;
        CancelWarmup();
    }

    internal void CancelWarmup()
    {
        pendingFactor = null;
        pendingMultiplier = 1f;
    }
}
