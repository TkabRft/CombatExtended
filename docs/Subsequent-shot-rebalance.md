# Subsequent-shot rebalance

code-synced: 2026-10-22

Status: implemented.

This integration preserves the five commits from [upstream PR #4429](https://github.com/CombatExtended-Continued/CombatExtended/pull/4429), through revision `3513de32a`, and corrects the warmup calculation, burst history, and settings layout. The corrective runtime and test commit is `398f52a13`.

## Behavior and balance

With **Faster subsequent shots** enabled, an eligible weapon can shorten warmup after firing a previous burst. Weapons explicitly lacking a magazine remain excluded. A fresh chain starts with a full warmup; later bursts accumulate a bonus subject to their current aiming mode.

The two curves in [`SubsequentShotCurve.xml`](../Defs/GameSetupSteps/SubsequentShotCurve.xml) retain the PR's original points:

- The mass curve supplies a turning threshold from loaded weapon mass divided by shooter body size. Higher weapon handling increases this threshold. Equipment-less ability verbs use zero equipment mass; an absent pawn uses body size one.
- The recoil curve supplies the vertical recovery factor from deterministic recoil accumulated during the preceding burst. The difficulty setting multiplies this factor. Random recoil still affects projectile trajectories, but does not randomize this warmup factor.

The recovery sample now uses the uncapped positive-tail handling calculation and expert-control factor described in [Targeting, tracking, and accuracy](Targeting-and-accuracy.md). Handling through the unmodified skill-20 baseline of 287.5% retains its previous recoil magnitude; higher handling reduces it progressively for all weapons. The additional aiming phase also shortens with handling and aiming accuracy beyond their unmodified expert baselines, before this feature applies its existing phase multiplier. Per-round target tracking and its optional speed limit are separate from this between-burst warmup calculation.

Bounded retargeting preserves the same burst's recoil progression and committed recovery history. Reaction-based early stopping cancels only pending warmup bookkeeping and adds no recoil sample for the un-emitted round. Its preflight runs before ammunition preparation; the normal burst completion/cooldown path remains responsible for ending the burst. See the burst-control section of [Targeting, tracking, and accuracy](Targeting-and-accuracy.md) for the skill/consciousness model, independent settings, and playtest limits.

Turning uses the shortest absolute angular difference, between zero and 180 degrees. A reversal therefore remains 180 degrees, and a turn from 89 to -269 degrees is two degrees.

For each upcoming burst, the calculation snapshots:

```text
angleFactor = max(0, angle / massThreshold / (1 + shootingAccuracy / 9) - 1)
recoilFactor = recoilCurve(previousBurstRecoil) * difficulty
pendingFactor = (angleFactor + recoilFactor) * previousMultiplier
warmupMultiplier = clamp01(max(modeFloor, pendingFactor))
```

The initial previous multiplier is one. The lower bounds on remaining warmup are 0.10 for suppressing fire, 0.25 for ordinary warmup, and 0.50 for the additional aiming phase. Ordinary warmup and aiming share the same pending factor; each applies its own floor without multiplying the chain again. The last applied phase multiplier becomes the previous multiplier when the burst actually fires.

## State ownership and emission

[`SubsequentShotState`](../Source/CombatExtended/Utilities/SubsequentShotState.cs) owns transient committed history, the pending calculation, and burst recoil. [`Verb_ShootCE`](../Source/CombatExtended/CombatExtended/Verbs/Verb_ShootCE.cs) reads the curves and applies the resulting multiplier to the existing warmup ticks.

`Verb_LaunchProjectileCE` prepares a deterministic recoil sample during aiming calculation. Its emission notification records that sample only after launching or raycasting the first projectile of a shot. The first successful shot in a burst commits the pending multiplier and starts a fresh recoil total. Later shots add recoil without advancing the bonus; additional pellets do neither. An unsuccessful cast discards its pending warmup calculation without earning a bonus or adding recoil.

The SOS2 ground-defense projectile loop uses the same emission and cancellation helpers. Turret scheduling and the separate SOS2 ship-combat branch retain their existing behavior. Warmup calculations with no positive remaining duration do not create a pending bonus.

Existing targeting/reload resets clear all chain history. Disabling the setting or using a magazine-less weapon clears history as well, so excluded shots cannot build a bonus for later use.

`GameSetupStep_SubsequentShotCurve` initializes both static curves on fresh-game generation and save loading through `CE_SubsequentShotCurve`. Its overrides match the existing pinned reference API and the explosion-curve setup pattern.

## Settings and persistence

The existing checkbox and the new **Subsequent shot difficulty** slider appear together in the active right-hand mechanics listing. The slider retains the PR's range of 0.1 to 3.0 and default of 1.0. Increasing it increases the recoil penalty, slowing bonus accumulation. Debug mode exposes the PR's calculation logging option.

The additive settings keys are `fasterRepeatShotsRecoilMult` and `debugSubsequentShotLogging`. Existing translation identifiers, def identifiers, settings keys, and verb Scribe keys remain unchanged. Dependency pins, framework targets, and package identity remain unchanged.

Chain bookkeeping is transient. At `PostLoadInit`, the verb clears its repeat-shot flag, cached multiplier, recoil, and pending sample while preserving the loaded stance or turret warmup duration and target information. Subsequent emitted shots rebuild a neutral chain. The existing `lastRecoilDeg` save key remains present; its loaded value is discarded with this transient history. There is no save-schema migration.

## Validation and acceptance

The solution build succeeded against the existing frozen references. The only reported analyzer warnings were the existing xUnit2031 and xUnit2029 warnings in the untouched siege tests.

The standalone test project links the production state helper directly. All 33 cases in [`SubsequentShotTests`](../Source/Tests/SubsequentShotTests.cs) passed, covering angular boundaries, cumulative bonuses and all three floors, shared warmup/aiming snapshots, burst recoil, multiple shots and pellets, failed casts, exclusions, resets, and neutral history after loading. The existing source load-layout test also passed: 34 focused cases total, zero failures.

Changed runtime XML parses, the added settings translation references resolve, and the settings controls use an active listing. The PR's curve definitions and setup step remain unchanged. Existing Scribe keys, fork-specific Rimsenal ammunition changes, siege guards, and the main project's dependency references were checked against the pre-integration baseline. The whitespace check passed.

Manual acceptance remains pending for the mechanics settings layout; sustained single, burst, and automatic fire with light and heavy weapons; nearby-target turns and reversals; ordinary, aimed, and suppressing fire; reload/reset and mid-combat save loading; turret behavior; and SOS2 ground defense when available. Compilation and the standalone state tests do not establish in-game acceptance.
