using System;

namespace CombatExtended.Utilities;

// A replacement from another verb writer becomes a new baseline. Previews
// neither capture a baseline nor assign runtime state.
internal sealed class BipodVerbState<T>(Func<T, T> clone) where T : class
{
    private T baseline;
    private T assigned;

    internal bool Owns(T current) => current != null && ReferenceEquals(current, assigned);

    internal T CopyBaseline(T current) => current == null ? null : clone(Owns(current) ? baseline : current);

    internal T Apply(T current, Action<T> transform)
    {
        if (current == null)
        {
            return null;
        }
        if (!Owns(current))
        {
            baseline = clone(current);
        }
        T result = clone(baseline);
        transform(result);
        assigned = result;
        return result;
    }
}

internal readonly struct BipodValues(float range, float recoil, float warmup)
{
    internal readonly float Range = range;
    internal readonly float Recoil = recoil;
    internal readonly float Warmup = warmup;

    internal BipodValues Modified(float additionalRange, float recoilMultiplier, float warmupMultiplier) =>
        new(Range + additionalRange, Recoil * recoilMultiplier, Warmup * warmupMultiplier);
}
