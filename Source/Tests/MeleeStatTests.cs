using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended.Utilities;
using Xunit;

namespace Tests;

public class MeleeStatTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(4f, 12f)]
    public void MissingEquipmentDefaultsOnlyTheEquipmentFactor(float toolPenetration, float expected)
    {
        Assert.Equal(expected, MeleeStatMath.Penetration(toolPenetration, 2f, 1.5f, null));
    }

    [Fact]
    public void MaterialQualityAndPawnModifiersRemainMultiplicative()
    {
        Assert.Equal(18f, MeleeStatMath.Penetration(4f, 2f, 1.5f, 1.5f));
    }

    [Fact]
    public void PenetrationUsesWeightedRuntimeValuesWithoutRescalingThem()
    {
        float[] runtimeValues = [0f, 12f];
        Assert.Equal(9f, MeleeStatMath.Average(runtimeValues, value => value == 0 ? 1f : 3f, value => value));
    }

    [Theory]
    [InlineData(0.5f, 1f, 0f, 20f)]
    [InlineData(1f, 1.5f, 0f, 20f)]
    [InlineData(0.5f, 1f, 5f, 25f)]
    [InlineData(1f, 1.5f, 10f, 30f)]
    public void NaturalDamageIgnoresWeaponVariationAndAddsOnlyTheEnabledBonus(
        float skillMin, float skillMax, float enabledBonus, float expected)
    {
        var attack = new MeleeStatMath.Attack(1f, 20f, 2f, false, skillMin, skillMax, enabledBonus);
        Assert.Equal(expected, attack.MinimumDamage);
        Assert.Equal(expected, attack.MaximumDamage);
        Assert.False(MeleeStatMath.HasVariation(attack.MinimumDamage, attack.MaximumDamage));
        Assert.Equal(expected / 2f, MeleeStatMath.Dps([attack], 1f));
    }

    [Theory]
    [InlineData(0.5f, 1f, 10f, 20f)]
    [InlineData(1f, 1.5f, 20f, 30f)]
    public void EquippedDamageRetainsSkillVariationAndIgnoresUnarmedBonus(
        float skillMin, float skillMax, float expectedMin, float expectedMax)
    {
        var attack = new MeleeStatMath.Attack(1f, 20f, 2f, true, skillMin, skillMax, 10f);
        Assert.Equal(expectedMin, attack.MinimumDamage);
        Assert.Equal(expectedMax, attack.MaximumDamage);
        Assert.True(MeleeStatMath.HasVariation(attack.MinimumDamage, attack.MaximumDamage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PawnsWithoutSkillsHaveNoVariation(bool equipped)
    {
        var attack = new MeleeStatMath.Attack(1f, 10f, 2f, equipped, 1f, 1f, 0f);
        Assert.False(MeleeStatMath.HasVariation(attack.MinimumDamage, attack.MaximumDamage));
        Assert.Equal(5f, MeleeStatMath.Dps([attack], 1f));
    }

    [Fact]
    public void MixedAttacksUseExpectedDamageOverExpectedTimeAndOneHitChance()
    {
        MeleeStatMath.Attack[] attacks =
        [
            new(3f, 20f, 2f, true, 0.5f, 1.5f, 99f),
            new(1f, 10f, 1f, false, 0.5f, 1f, 4f)
        ];
        Assert.Equal(74f / 7f * 0.8f, MeleeStatMath.Dps(attacks, 0.8f), 5);
        Assert.True(MeleeStatMath.HasVariation(attacks[0].MinimumDamage, attacks[0].MaximumDamage));
        Assert.False(MeleeStatMath.HasVariation(attacks[1].MinimumDamage, attacks[1].MaximumDamage));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-1f, 1f)]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, -1f)]
    [InlineData(1f, float.NaN)]
    [InlineData(1f, float.PositiveInfinity)]
    public void InvalidAttacksCannotProduceInvalidDps(float weight, float cooldown)
    {
        var attack = new MeleeStatMath.Attack(weight, 10f, cooldown, false, 1f, 1f, 0f);
        Assert.False(attack.IsUsable);
        Assert.Equal(0f, MeleeStatMath.Dps([attack], 1f));
    }

    [Fact]
    public void EmptyOrInvalidInputsReturnFiniteZero()
    {
        Assert.Equal(0f, MeleeStatMath.Dps([], 1f));
        Assert.Equal(0f, MeleeStatMath.Average(Array.Empty<float>(), _ => 1f, value => value));
        Assert.Equal(0f, MeleeStatMath.Average(new[] { 10f }, _ => 0f, value => value));
        Assert.Equal(0f, MeleeStatMath.Dps([new(1f, 10f, 1f, false, 1f, 1f, 0f)], float.NaN));
    }

    [Theory]
    [InlineData(true, 10f, 10f, false)]
    [InlineData(false, 10f, 15f, false)]
    [InlineData(true, 10f, 15f, true)]
    public void OnlyMeaningfulWeaponAdjustmentsAreLabeledAsWeaponAdjustments(
        bool weapon, float baseDamage, float adjustedDamage, bool expected)
    {
        Assert.Equal(expected, MeleeStatMath.ShowWeaponAdjustment(weapon, baseDamage, adjustedDamage));
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(2f, true)]
    public void ZeroPenetrationOmitsTheMultiplicationChain(float value, bool expected)
    {
        Assert.Equal(expected, MeleeStatMath.ExplainPenetration(value));
    }

    [Fact]
    public void ProviderSearchSkipsNullMissingAndEmptyProvidersAndAcceptsVerbOnlyProviders()
    {
        Provider[] providers = [null, new(), new() { Verbs = [] }, new() { Verbs = [7] }, new() { Tools = [9] }];
        var verbs = MeleeStatMath.FirstMeleeProvider(providers, provider =>
            (provider.Verbs ?? []).Concat(provider.Tools ?? []));
        Assert.Equal(new[] { 7 }, verbs);
    }

    [Fact]
    public void ProviderSearchAcceptsToolsWithoutVerbListAndReturnsEmptyWhenNoneApply()
    {
        Assert.Equal(new[] { 9 }, MeleeStatMath.FirstMeleeProvider(new[] { new Provider { Tools = [9] } },
            provider => (provider.Verbs ?? []).Concat(provider.Tools ?? [])));
        Assert.Empty(MeleeStatMath.FirstMeleeProvider(new Provider[] { null, new() }, provider => provider.Verbs));
    }

    private sealed class Provider
    {
        internal IEnumerable<int> Verbs;
        internal IEnumerable<int> Tools;
    }
}
