using CombatExtended;
using CombatExtended.AI;
using Verse;
using Xunit;

namespace Tests;

public class LightingSafetyTests
{
    private static (Map map, Pawn pawn) NightScene()
    {
        var map = new Map(); map.skyManager.CurSkyGlow = 0.1f;
        map.Tracker = new LightingTracker(map);
        return (map, new Pawn { Map = map, DrawPos = new(0, 0, 0) });
    }

    [Fact]
    public void EarlyNullLookupRecoversWhenComponentBecomesAvailable()
    {
        var map = new Map();
        Assert.Null(map.GetLightingTracker()); Assert.Equal(1, map.TrackerLookups);
        map.Tracker = new LightingTracker(map);
        Assert.Same(map.Tracker, map.GetLightingTracker()); Assert.Equal(2, map.TrackerLookups);
        Assert.Same(map.Tracker, map.GetLightingTracker()); Assert.Equal(2, map.TrackerLookups);
    }

    [Fact]
    public void CacheHandlesIndexReuseGrowthAndInvalidOwners()
    {
        var first = new Map { Index = 64 }; first.Tracker = new LightingTracker(first);
        Assert.Same(first.Tracker, first.GetLightingTracker());
        var replacement = new Map { Index = 64 }; replacement.Tracker = new LightingTracker(replacement);
        var cached = replacement.GetLightingTracker();
        Assert.Same(replacement.Tracker, cached);
        Assert.NotSame(first.Tracker, cached);
        cached.map = first;
        replacement.Tracker = new LightingTracker(replacement);
        Assert.Same(replacement.Tracker, replacement.GetLightingTracker());
        Assert.Equal(2, replacement.TrackerLookups);
    }

    [Fact]
    public void InvalidMapsDoNotResolveComponents()
    {
        Map missing = null;
        Assert.Null(missing.GetLightingTracker());
        var removed = new Map { Index = -1 };
        Assert.Null(removed.GetLightingTracker()); Assert.Equal(0, removed.TrackerLookups);
    }

    [Theory]
    [InlineData("map")] [InlineData("pawn")] [InlineData("despawned")] [InlineData("differentMap")]
    [InlineData("unregistered")] [InlineData("sky")] [InlineData("target")]
    [InlineData("tracker")] [InlineData("trackerOwner")]
    public void UnavailableLightingSkipsOptionalFlareDecisionWithoutReadingStats(string unavailable)
    {
        var (map, pawn) = NightScene(); var target = new IntVec3(0, 30);
        switch (unavailable)
        {
            case "map": map = null; break;
            case "pawn": pawn = null; break;
            case "despawned": pawn.Spawned = false; break;
            case "differentMap": pawn.Map = new Map(); break;
            case "unregistered": map.Index = -1; break;
            case "sky": map.skyManager = null; break;
            case "target": target = IntVec3.Invalid; break;
            case "tracker": map.Tracker = null; break;
            case "trackerOwner": map.Tracker.map = new Map(); break;
        }
        Assert.True(map.VisibilityGoodAt(pawn, target));
        if (pawn != null) Assert.Equal(0, pawn.NightVisionReads);
    }

    [Theory]
    [InlineData("day", true)] [InlineData("near", true)] [InlineData("lit", true)]
    [InlineData("nightVision", true)] [InlineData("roof", true)] [InlineData("dark", false)]
    public void OrdinaryVisibilityAndFlareThresholdsRemainUnchanged(string condition, bool expected)
    {
        var (map, pawn) = NightScene(); var target = new IntVec3(0, 30);
        switch (condition)
        {
            case "day": map.skyManager.CurSkyGlow = 0.5f; break;
            case "near": target = new IntVec3(0, 14); break;
            case "lit": map.Tracker.Glow = 0.5f; break;
            case "nightVision": pawn.NightVision = 0.61f; break;
            case "roof": map.Roofed = true; break;
        }
        Assert.Equal(expected, map.VisibilityGoodAt(pawn, target));
    }

    [Fact]
    public void ExplicitNightVisionOverrideDoesNotReadPawnStat()
    {
        var (map, pawn) = NightScene(); var target = new IntVec3(0, 30);
        Assert.True(map.VisibilityGoodAt(pawn, target, 0.61f));
        pawn.NightVision = 1;
        Assert.False(map.VisibilityGoodAt(pawn, target, 0.5f));
        Assert.Equal(0, pawn.NightVisionReads);
    }
}
