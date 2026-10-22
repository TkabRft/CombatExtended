using System;
using System.Collections.Generic;
using CombatExtended;
using CombatExtended.Utilities;
using RimWorld;
using Verse;
using Xunit;

namespace Tests;

public class AccuracyTests
{
    [Theory]
    [InlineData(4.5f, 4f)]
    [InlineData(1f, 0.9375f)]
    [InlineData(1.5f, 1.25f)]
    [InlineData(2f, 1.5f)]
    [InlineData(5f, 4.5f)]
    public void PositiveTailsPreserveBaselineAreContinuousAndNeverPlateau(float intercept, float knee)
    {
        Assert.Equal(intercept, AccuracyMath.Tail(intercept, 0, knee));
        Assert.Equal(intercept - knee, AccuracyMath.Tail(intercept, knee, knee));
        Assert.InRange(Math.Abs(AccuracyMath.Tail(intercept, knee - 0.0001f, knee)
            - AccuracyMath.Tail(intercept, knee + 0.0001f, knee)), 0, 0.00021f);
        float previous = intercept - knee;
        foreach (float value in new[] { knee + 0.1f, knee + 1, 20, 100, 1000, 1e10f, float.MaxValue })
        {
            float current = AccuracyMath.Tail(intercept, value, knee);
            Assert.True(float.IsFinite(current) && current > 0 && current < previous);
            previous = current;
        }
    }

    [Theory]
    [InlineData(0f)] [InlineData(1f)] [InlineData(2.875f)]
    public void OrdinaryRecoilIsUnchanged(float handling)
    {
        Assert.Equal(0, AccuracyMath.Recoil(handling, 0));
        for (int n = 1; n <= 20; n++)
            Assert.Equal((float)Math.Pow(5 - handling, Math.Min(10, n) / 6.25), AccuracyMath.Recoil(handling, n), 5);
    }

    [Theory]
    [InlineData(5f)] [InlineData(10f)] [InlineData(100f)] [InlineData(1000f)]
    public void HighHandlingRecoilRemainsFiniteAndImproves(float handling)
    {
        for (int n = 1; n <= 12; n++)
        {
            float recoil = AccuracyMath.Recoil(handling, n);
            Assert.True(float.IsFinite(recoil) && recoil > 0);
            Assert.True(AccuracyMath.Recoil(handling * 2, n) < recoil);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(10)]
    public void ExpertRecoilIsContinuousAndMonotoneAcrossFormerCaps(int previousRounds)
    {
        float previous = AccuracyMath.Recoil(2.875f, previousRounds);
        for (float handling = 2.876f; handling < 12; handling += 0.001f)
        {
            float current = AccuracyMath.Recoil(handling, previousRounds);
            Assert.True(current >= 0 && current < previous);
            previous = current;
        }
        const float step = 0.0001f;
        float center = AccuracyMath.Recoil(2.875f, previousRounds);
        float left = (center - AccuracyMath.Recoil(2.875f - step, previousRounds)) / step;
        float right = (AccuracyMath.Recoil(2.875f + step, previousRounds) - center) / step;
        Assert.InRange(Math.Abs(left - right), 0, 0.02f);
        foreach (float handling in new[] { 1e10f, float.MaxValue })
            Assert.True(float.IsFinite(AccuracyMath.Recoil(handling, previousRounds)));
    }

    [Theory]
    [InlineData(2.875f, 0.75f)] [InlineData(3.615f, 1.5f)] [InlineData(10f, 10f)]
    public void ModesKeepWeaponSwayAndSightsWhileAimedRemainsMorePrecise(float handling, float aiming)
    {
        float sway = AccuracyMath.HandlingSway(handling) * 1.9f;
        float snapshot = AccuracyMath.ModeSway(sway, aiming, 1.5f, false, false);
        float aimed = AccuracyMath.ModeSway(sway, aiming, 1.5f, true, false);
        Assert.True(aimed > 0 && aimed < snapshot);
        Assert.Equal(sway, snapshot);
        Assert.Equal(aimed * 2, AccuracyMath.ModeSway(sway * 2, aiming, 1.5f, true, false));
        Assert.Equal(aimed / 2, AccuracyMath.ModeSway(sway, aiming, 3, true, false));
        Assert.Equal(sway * 1.5f, AccuracyMath.ModeSway(sway, aiming, 1.5f, false, true));
    }

    [Fact]
    public void EnhancedControlShortensExtraAimPreparationWithoutDelayingSnapshot()
    {
        Assert.Equal(156, AccuracyMath.AimPreparationTicks(60, 2.875f, 0.75f));
        Assert.Equal(156, AccuracyMath.AimPreparationTicks(60, 1, 0.5f));
        Assert.Equal(156, AccuracyMath.AimPreparationTicks(60, 2.875f, 0.9625f));
        Assert.Equal(11, AccuracyMath.AimPreparationTicks(60, 3.615f, 1.5f));
        Assert.True(AccuracyMath.AimPreparationTicks(60, 3.615f, 0.75f) < 156);
        Assert.True(AccuracyMath.AimPreparationTicks(60, 2.875f, 1.5f) < 156);
        Assert.Equal(0, AccuracyMath.AimPreparationTicks(60, float.MaxValue, float.MaxValue));
        Assert.Equal(30, AccuracyMath.AimPreparationTicks(0, 2.875f, 0.75f));
        Assert.Equal(240, AccuracyMath.AimPreparationTicks(150, 2.875f, 0.75f));
    }

    [Fact]
    public void SightsAndEnvironmentalErrorsRemainPositiveBeyondOldCaps()
    {
        Assert.Equal(0.25f, AccuracyMath.AimedSway(0.75f));
        Assert.Equal(1, AccuracyMath.EnvironmentalLead(1.5f));
        Assert.True(AccuracyMath.EnvironmentalLead(100) < AccuracyMath.EnvironmentalLead(10));
        Assert.True(float.IsFinite(AccuracyMath.RangeAccuracy(10, 0)));
        Assert.Equal(AccuracyMath.RangeAccuracy(10, 1) / 2, AccuracyMath.RangeAccuracy(10, 2));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StatMigrationPreservesMechanitorPartsAndIsIdempotent(bool otherModFirst)
    {
        var mechanitor = new OffsetPart(0.5f);
        var curve = new SimpleCurve { Points = [new(0, 0), new(2, 2), new(3, 2.5f), new(4, 2.75f), new(5, 2.875f), new(13, 4.5f)] };
        var stat = new StatDef { postProcessCurve = curve, parts = [] };
        CE_StatDefOf.AimingAccuracy = new StatDef { postProcessCurve = new SimpleCurve { Points = [new(0, 0), new(6.5f, 1.5f)] } };
        StatDefOf.ShootingAccuracyPawn = stat;
        // Both orders of XML mods finish before the post-def conversion is installed.
        if (otherModFirst) stat.parts.Add(mechanitor);
        Assert.Same(curve, stat.postProcessCurve);
        if (!otherModFirst) stat.parts.Add(mechanitor);
        StatPart_UncappedAccuracyCurve.Initialize();
        StatPart_UncappedAccuracyCurve.Initialize();
        Assert.Equal(2, stat.parts.Count); Assert.Same(mechanitor, stat.parts[0]);
        Assert.Null(stat.postProcessCurve); Assert.Equal(float.MaxValue, stat.maxValue);
        foreach (var point in curve.Points)
        {
            float value = point.x - 0.5f;
            foreach (var part in stat.parts) part.TransformValue(new(), ref value);
            Assert.Equal(point.y, value);
        }
        float high = 20;
        foreach (var part in stat.parts) part.TransformValue(new(), ref high);
        Assert.True(high > 4.5f);
        Assert.Equal(6, curve.Points.Count);
    }

    private sealed class OffsetPart(float offset) : StatPart
    {
        public override void TransformValue(StatRequest request, ref float value) => value += offset;
        public override string ExplanationPart(StatRequest request) => "mechanitor";
    }

    [Fact]
    public void FlatCurveEndpointUsesTheLastPositiveSlope()
    {
        var stat = new StatDef { postProcessCurve = new SimpleCurve { Points = [new(0, 0), new(1, 1), new(2, 1)] } };
        CE_StatDefOf.AimingAccuracy = stat;
        StatDefOf.ShootingAccuracyPawn = null;
        StatPart_UncappedAccuracyCurve.Initialize();
        float value = 3;
        stat.parts[0].TransformValue(new(), ref value);
        Assert.Equal(2, value);
    }

}
