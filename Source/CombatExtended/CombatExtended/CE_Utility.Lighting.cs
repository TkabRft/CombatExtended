using System;
using Verse;

namespace CombatExtended;

static partial class CE_Utility
{
    private static Map[] _mapsLighting = new Map[20];
    private static LightingTracker[] _lightingTrackers = new LightingTracker[20];

    public static LightingTracker GetLightingTracker(this Map map)
    {
        int index = map?.Index ?? -1;
        if (index < 0) return null;
        if (index >= _mapsLighting.Length)
        {
            int expandedLength = Math.Max(_mapsLighting.Length * 2, index + 1);
            Array.Resize(ref _mapsLighting, expandedLength);
            Array.Resize(ref _lightingTrackers, expandedLength);
        }
        // A component can be unavailable during map construction. Never make that
        // transient miss permanent, and never return a tracker owned by another map.
        LightingTracker tracker = _lightingTrackers[index];
        if (_mapsLighting[index] == map && tracker != null && tracker.map == map) return tracker;
        _mapsLighting[index] = map;
        return _lightingTrackers[index] = map.GetComponent<LightingTracker>();
    }
}
