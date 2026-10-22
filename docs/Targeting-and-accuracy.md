# Targeting, tracking, and accuracy

code-synced: 2026-10-22

Status: implemented.

## Per-round targeting

Pawn-held direct-fire weapons and manned guns recalculate target position, movement lead, and target height for every round of a burst. The first round establishes the direction acquired during warmup. Subsequent rounds track the clean solution; visibility error, range/lead error, sway, recoil, and mechanical spread are applied afterward. Tracking does not smooth these errors or compensate for their random samples.

The **Limit tracking speed** mechanics setting defaults to enabled, including when loading settings without its new `limitTrackingSpeed` key. Disabling it allows each round to fully re-aim. With it enabled, horizontal and vertical tracking use shortest-angle rotation, limited by the elapsed game ticks since the preceding round:

```text
degrees/second = 60 * (weaponHandling / 2.5)
                * sqrt((carryWeight / 40) * (4 / loadedWeaponMass))
```

Carry weight is the shooter's total CE carrying-weight stat, including power armor and other modifiers. It is a strength proxy, not remaining inventory capacity. Manipulation already contributes through weapon handling and is not multiplied again. Bulk capacity has no effect. A shooter with 250% handling and 40 kg carry weight tracks at 60 degrees/second with a loaded 4 kg weapon. Increasing weapon mass to 16 kg halves that rate; increasing carry weight to 160 kg restores it.

Tracking advances once per round, not per pellet. Each burst retains one signed range-error quantile, applied to its current distance and range-error magnitude each round. A different target acquired mid-burst remains subject to the tracking rate. The existing aiming modes, body-region choices, suppression penalties, burst counts, and ammunition consumption remain in effect.

Losing line of sight retains the previous interruption policy. Interruptible fire stops where it previously stopped; eligible non-interruptible bursts retain their last firing direction and position. Suppressive fire retains its separate permission to continue toward a target cell. An unseen moving pawn is not used to update a held firing direction. CIWS, unattended turrets, and indirect-fire verbs retain their specialized or previous direction-lock policy.

`BurstTrackingState` owns transient tracking and the shared lost-sight decision. `Verb_LaunchProjectileCE` applies it around the existing trajectory and emission paths. New bursts, targeting resets, and load reconstruction clear tracking history. There are no new verb save fields. Existing protected targeting entry points and Scribe keys remain present.

### Movement inputs and interception

`ShiftVecReport.MoveVelocity` uses the path follower's current cell displacement and recorded `nextCellCostTotal`: velocity is `60 * (nextCell - Position) / stepTicks`. The recorded step cost includes the movement system's terrain, locomotion, diagonal, and other cost adjustments. `MovingNow` and the existing stun check gate extrapolation; missing or nonpositive step durations produce zero velocity. The report no longer reconstructs speed from the movement stat and terrain costs. Movement direction no longer points from the rendered `DrawPos` toward the next cell, where render offsets could distort it. The current target and collision bounds still use `DrawPos`.

`ShotGeometry.Lead` solves the constant-velocity horizontal intercept equation `|target - source + velocity * t| = projectileSpeed * t` using stable quadratic roots, including the equal-speed linear case. It chooses the earliest positive intercept. If interception is impossible, it retains a finite forward prediction based on the current range divided by speed. This replaces the previous two-pass approximation and is invariant under rotation of the firing line. The live error magnitude and burst estimate use the same movement velocity and intercept helper. Existing intercept debug output includes step duration, velocity, lead, and the sampled lead error without additional RNG draws.

This is an exact intercept for constant horizontal projectile speed and constant target velocity. It does not correct for future target turns, render tween lag, or flight-time changes from drag, gravity, and propulsion. Those remain separate model limits; a reported crossing-target miss is not by itself proof of one cause.

## Bounded burst retargeting and stopping

Standard pawn-held direct-fire weapons and manned guns with a Shooting skill record use a shared preflight before ammunition preparation. This includes all aiming modes when a pawn is selected. Cell-targeted suppression remains area fire. CIWS, indirect fire, unattended turrets, explicitly non-interruptible weapons, and compatibility loops that do not use the standard shooting loop retain their previous behavior. The existing protected retarget/reset entry points and `doRetarget` veto remain available.

**Retarget mid burst** keeps its existing `midBurstRetarget` settings key and default of enabled. **Stop bursts after target falls** is independent, defaults to enabled, and uses the additive `stopBurstAfterTargetFalls` key. Both reset to enabled. Retarget-only permits switching without automatic early termination; stop-only permits termination without switching; disabling both bypasses the reaction model. Existing obstruction and interruption rules still apply.

### Replacement bounds

The burst records its initial pawn and whether it was standing before the first emission, including an instantaneous round that incapacitates it. A burst initially directed at an already downed pawn remains a finishing order. While the initial target remains visible, its position updates the burst anchor. The first target loss freezes that anchor, and subsequent replacement targets cannot move it.

A replacement must be a spawned, standing hostile pawn in the same map, pass automatic-target/threat eligibility, weapon targeting parameters, visibility/fog, range, and CE shooting-line checks. It may belong to a different hostile faction. Its horizontal distance from the anchor must not exceed five cells. Its clean aim direction, including movement lead, must also fall inside this cone around the current shooter-to-anchor direction:

```text
d = horizontal distance from shooter to frozen anchor
angleLimit = 45 - 35 * clamp01((d - 5) / 15) degrees
```

The cone is 45 degrees within five cells and narrows to ten degrees at twenty cells. Selection minimizes angular change, then anchor distance, then thing ID, and revalidates before selection commits. Existing alive-target LoS-loss retarget opportunities also use these bounds. Switching retains remaining rounds, recoil progression, ammunition, and tracking history; it does not add another aiming phase or directly rotate a manned turret to the replacement.

### Reaction calibration

Before each remaining round, a newly downed, dead, or destroyed pawn can trigger one reaction opportunity. Pellets do not receive separate checks. Raw Shooting skill `S` and consciousness capacity `C` give:

```text
S = clamp(Shooting skill, 0, 20)
C = max(0, consciousness)
skillChance = 0.002 + 0.998 * (S / 20)^(4/3)
consciousnessFactor = C^3                                  for C <= 1
                      1 + 0.15*(C-1)/(0.05 + C-1)           for C > 1
reactionChance = clamp01(skillChance * consciousnessFactor)
```

The consciousness function matches value and slope at 100%. Impairment strongly penalizes reaction even at skill twenty. Enhancement has diminishing gains approaching a 15% multiplier; handling and manipulation do not multiply the reaction again. At normal consciousness, when the first round of a ten-round burst immediately incapacitates the target:

| Shooting skill | Reaction chance before each remaining round | Expected extra rounds |
|---|---:|---:|
| 0 | 0.2% | 8.91 |
| 5 | 15.9% | 4.17 |
| 10 | 39.8% | 1.50 |
| 15 | 68.2% | 0.47 |
| 20 | 100% | 0 |

These are truncated geometric expectations with nine rounds remaining. Real projectile flight may allow several rounds to leave before incapacitation occurs; emitted projectiles cannot be recalled.

A failed reaction holds the last valid clean aim and height, with fresh range/visibility, sway, recoil, and spread calculations. Each held round advances the tracking clock, preventing accumulated reaction time from becoming an instantaneous turn. Fresh obstruction checks can stop this fire. A successful reaction latches awareness and prefers an eligible replacement; otherwise stopping ends the burst through the normal scheduler path. When stopping is disabled, an aware shooter continues and can reconsider replacements without rerolling awareness. A replacement falling starts another reaction sequence inside the original bounds.

`BurstControlState` owns numeric per-burst state, reset by new bursts and targeting resets. A separate transient guard prevents automatic restart against the last pawn for which stopping succeeded; it remains effective through automatic warmup/reload attempts. A new explicit attack order releases it for deliberate finishing, disabling early stopping bypasses it, and revival clears it when firing resumes. Load reconstruction clears transient history and the guard, but preserves neutral intent until the next pawn preflight; an already downed loaded target is not assumed to be a fresh finishing order. Reconstruction retains the saved firing direction with a neutral height until a new valid solution is acquired. There are no new verb save fields and existing Scribe keys are unchanged.

The preflight runs before even magazine-less ammunition splitting. Cancelled rounds do not spawn projectiles, consume ammunition, produce shot effects, or add recovery samples. Committed subsequent-shot history remains valid. Probabilities zero and one consume no reaction RNG; intermediate probabilities consume one draw per opportunity. Previews never enter this preflight.

This feature retains existing friendly-fire checks and their limitations. It does not estimate collateral risk or forecast friendly movement during a switch.

## Uncapped stats and diminishing errors

The firing verb no longer clamps aiming accuracy to 150% or weapon handling to 450%. The corresponding stat maxima are removed, with only the numeric representation limit retained during initialization.

After defs and XML patches load, `StatPart_UncappedAccuracyCurve` moves each effective conversion curve into the end of its existing stat-parts list and clears its original runtime post-process curve. Source XML curve paths and all original curve points remain intact for XML patching before initialization. Existing stat parts keep their order, and remaining post-process factors remain attached to the stat. The new conversion is idempotent. It extends beyond the endpoint using the last positive segment slope; a curve with no positive segment uses a unit-slope continuation.

The conversion now follows stat-part evaluation semantics. Code explicitly requesting values without post-processing still receives stat-part transformations; it must not assume the former curve-free intermediate value. Stat explanations include a localized conversion note. No global stat-worker Harmony patch is added.

Installed Integrated Implants XML adds a mechanitor stat-offset part to weapon handling. Installing CE's conversion after XML loading preserves that list and places the conversion after the offset, without adding competing XML `parts` lists. The integration tests exercise both XML ordering arrangements with a minimal mechanitor-offset fixture; they do not load that mod or the game.

`AccuracyMath` supplies a shared positive tail:

```text
T(b, x, k) = b - x                              for x <= k
            (b-k)^2 / ((b-k) + x-k)              for x > k
```

It preserves the original value and first derivative at the transition, then decreases toward zero. Aiming accuracy `A` and handling `H` use these factors:

| Component | Factor |
|---|---|
| Base sway | `T(4.5, H, 4.0) * weaponSway` |
| Aimed sway | `T(1, A, 0.9375) / max(1, sightsEfficiency)` |
| Range and lead accuracy | `T(1.5, A, 1.25) / sightsEfficiency` |
| Visibility | `T(2, A, 1.5)` |
| Recoil base | `T(5, H, 4.5)` |

Small denominator guards prevent division by zero. Separate darkness/smoke lead-error terms retain their previous scale through 150% aiming, then multiply by `T(2, A, 1.5) / 0.5`. Existing sights, environmental weights, and suppression multipliers remain.

The first round retains zero recoil deviation. Later rounds use:

```text
expertControl(H) = 1 / (1 + 16 * max(0, H - 2.875)^2)
recoilMagnitude = T(5, H, 4.5)^(min(10, previousRounds) / 6.25)
                  * expertControl(H)
```

Handling through 287.5%, the unmodified skill-20 baseline, retains the previous recoil magnitude. Above that level, a smooth control factor reduces recoil for every weapon, including weapons without bipods. It equals one at the transition with zero additional slope and keeps decreasing without a cap. A further 25 percentage points of handling halves this factor. Applying control outside the fractional burst exponent prevents that exponent from pulling a tiny recoil base back toward one on the second round.

Horizontal recoil retains its uniform half-recoil range; vertical recoil retains its uniform range from minus one-third recoil to positive full recoil. The burst-length exponent and ten-round exponent limit remain. The same magnitude calculation supplies deterministic subsequent-shot recovery samples; see [Subsequent-shot rebalance](Subsequent-shot-rebalance.md).

Ordinary values retain their former linear calculations through the stated transitions. Near the former perfect-accuracy thresholds, positive tails replace abrupt zero errors, so the same old threshold value is not necessarily as accurate as before. Higher stats continue improving it. Floating-point underflow can eventually represent a vanishing error as zero; there is no balancing ceiling or fixed residual error floor.

**Mechanical spread is unchanged.** Neither handling, aiming, strength, nor tracking modifies the weapon's spread or its ammunition, quality, condition, and attachment factors. Existing instant-projectile spread/falloff handling is retained.

## Sway, preparation, and mode distinctions

Live sway and mode previews share `AccuracyMath.ModeSway`. Weapon sway remains a multiplier on handling-dependent sway, including its existing bipod and equipment modifiers. Aimed shots additionally multiply it by the aimed-sway factor and divide by sights efficiency, with the existing minimum divisor of one. Snapshot retains the unassisted sway; physical suppression retains its 1.5 multiplier when the shooter cannot aim. Hunting, moving-shooter, sight-capacity, and suppression eligibility rules remain in place.

Additional aim preparation now scales with control beyond the unmodified skill-20 baselines of 287.5% handling and 96.25% aiming accuracy:

```text
handlingScale = min(1, T(4.5, H, 4.0) / T(4.5, 2.875, 4.0))
aimingScale = min(1, T(1, A, 0.9375) / T(1, 0.9625, 0.9375))
extraAimTicks = floor((30 + 210 * clamp01(distance / 100))
                     * handlingScale * aimingScale)
```

Ordinary stats retain the original distance-dependent delay. Higher stats reduce the extra delay continuously, with game-tick quantization; a zero result skips the additional stance. Base weapon warmup, its modifiers, and the subsequent-shot phase multipliers are unchanged. Snapshot avoids the extra aiming phase. Aimed fire trades that phase for tighter sway, while suppressive fire keeps its existing targeting and interruption policies. Single, short-burst, and automatic fire retain their existing round counts and intervals: longer bursts emit more rounds but expose more rounds to recoil and changing conditions.

This preserves a speed-versus-precision choice without leaving enhanced shooters paying the full fixed aiming cost after their accuracy benefit has become small. At the limit where mechanical spread dominates, the modes can approach the same target-hit fraction; additional aiming time also approaches zero. There is no artificial spread penalty to force a difference.

For the reported 361.5% handling case, the expert recoil factor is approximately 0.1024. With 150% aiming, 150% sights efficiency, weapon sway 1.9, and a 0.9 deployed-bipod sway multiplier, snapshot sway is about 1.513 degrees and aimed sway about 0.0063 degrees. Extra preparation at 60 cells falls from 156 to 11 ticks before subsequent-shot reductions. With effective weapon recoil 2.52, round ten's recoil-only envelope at that distance is approximately 0.216 cells horizontally and 0.432 cells upward. These are component calculations, not an assurance of hits through cover or under all firing conditions.

## Burst hit estimate

Supported direct-fire tooltips show **Estimated burst hit chance**: the expected fraction of emitted rounds hitting the selected target, not the probability of at least one hit. The estimate starts a fresh burst from current conditions and models the selected fire and aiming modes. Magazine capacity and ammunition consumed per round limit the modeled burst. Unique-weapon burst-count alternatives are sampled locally rather than invoking the live getter that draws from gameplay RNG. A multi-pellet round counts as hitting when at least one pellet intersects the target.

`BurstHitEstimator` runs 512 deterministic sample bursts over a numeric snapshot. It shares tracking, positive-tail factors, lead prediction, target offset geometry, and ballistic aiming arithmetic with production. Samples include range estimation, visibility, leading error, sway phase, signed recoil, and independent mechanical spread. Target collision boxes and cover-clipped visible heights replace the former diagonal-width and centered-height approximation. The standard lerped path follows its sampled-tick parabola; the standard ballistic path includes sampled mass, diameter, coefficient, drag, and vertical gravity. Instant rays respect their live raycast range. Unattended turrets retain the previous locked direction and first-round positional-error basis in the estimate.

The simulation has no game-object or gameplay-RNG dependency. It does not spawn projectiles or advance live firing/targeting history. The adapter captures game state on the calling game thread. Results are cached for 15 game ticks; changed target, equipment, ammunition, fire/aim mode, relevant stats, geometry, or tracking configuration invalidates the result immediately. Position and sway-phase changes refresh at cache expiry.

The forecast assumes the target continues its current movement heading and speed, the shooter remains at the captured position, and current cover/visibility persists. It does not predict future path turns, teleports, new intervening blockers, shield interception, incidental hits on other enemies, penetration, damage, or armor outcomes. It estimates target intersection under those assumptions, not the complete future combat outcome. Moving targets and realistic ballistics still require in-game calibration.

The target is assumed to remain active throughout the modeled burst. Incapacitation, reaction delays, early stopping, and replacement targets are not forecast; the localized estimate explanation states this condition. Changing the new fire-control settings therefore does not change this conditional numerical estimate or consume gameplay RNG.

Custom trajectory workers, homing weapon traits, and fueled/accelerating projectiles show an unavailable estimate. Indirect-fire estimates stay on their existing path. Existing public hit-calculation signatures remain available.

## Validation and remaining acceptance

The current source passed the solution build against the existing pinned references and all 197 focused cases: burst-control math and production-adapter fixtures, accuracy math and curve migration, expert-recoil continuity and monotonicity, mode sway and preparation, tracking and lost-sight policy, movement inputs and interception, geometry and burst estimates, plus the existing subsequent-shot, bipod, source load-layout, and affected siege-builder regressions. Twelve estimator scenarios each agree within five percentage points with a separate 60,000-burst seeded simulation using an independent ray/plane calculation. These include stationary and moving targets, finite tracking, instantaneous rays, projectile flight, exceptional handling, and heavy-weapon angular fixtures at 60 cells.

The heavy-weapon fixtures combine recoil 2.52, 361.5% handling, 150% aiming, deployed-bipod sway, unchanged 0.03-degree spread, and a stationary human-sized collision box. Across weapon-sway factors of 0.6, 1.9, and 4, aimed ten-round bursts exceed an 80% modeled hit fraction and beat snapshot in expected hits per second after charging their extra preparation time. Single, three-round, and ten-round cases retain their expected accuracy ordering. These fixtures use instant rays to isolate angular errors; they do not reproduce the complete TMC projectile or constitute measured in-game hit rates.

Changed runtime XML parses, the original source conversion curves and the complete spread definition are unchanged, new English keys resolve, and existing Scribe keys are preserved. Standalone stat-part doubles check migration ownership and arithmetic; the actual game API is checked by compilation.

The preceding 105-case baseline's runtime package passed XML, metadata, load-path, content-hash, required-assembly, and source-artifact exclusion validation. All eleven packaged assembly hashes matched that baseline's compiled outputs and package manifest. The existing load-layout test also passed when run from the packaged mod root. Packaging remains deferred at the user's request; the existing package does not contain the current movement-prediction or burst-control changes.

The 21 new production-helper cases passed against rebuilt code. They cover eight movement directions and step timing, invalid step duration, crossing and approaching/receding interception, rotated firing lines, equal-speed interception, and unreachable-target fallback. The path-follower API inputs were checked by metadata and compilation against the pinned main-project reference. These checks validate the helper arithmetic and API usage; the reported in-game crossing-target behavior still requires playtesting.

Burst-control calibration uses 100,000 deterministic sample bursts at each of skills zero, five, ten, fifteen, and twenty. Production-preflight fixtures exercise eligibility filtering, hostile factions, cone/lead bounds, stable choice, frozen anchors, independent settings, immediate and delayed impacts, per-round/per-pellet reaction, awareness, saved intent reconstruction, restart guards, revival, and committed recovery. Tracking-clock cases cover different firing intervals. The fixtures link the actual preflight adapter with small game-API doubles; their emission/completion host is simulated and does not execute the game's ammunition preparation, projectile collision, scheduler callbacks, or cooldown stances. Those real integrations remain playtest acceptance, while compilation checks the actual API.

Remaining in-game acceptance:

- Compare rifles, DMRs, LMGs, and HMGs in single, short-burst, and automatic modes against stationary, crossing, approaching, and receding targets.
- Repeat the reported deployed Kord test at about 60 cells against a standing humanlike target, then compare aimed and snapshot fire by both hit fraction and hits per second. Check ordinary handling, the reported 361.5%, and much higher handling, including weapons with different sway factors and no bipod.
- Test room and open-field target groups with both fire-control settings independently, all aiming modes, pawn-versus-cell suppression, first-round kills, repeated replacements, heavy weapons, and nearby allies/neutrals. Measure wasted rounds at skills zero, ten, and twenty with impaired, normal, and enhanced consciousness. Confirm actual cooldown/completion behavior, magazine-less ammo preservation on cancellation, new finishing orders, and no automatic restart at a fallen target.
- Repeat with ordinary and heavily enhanced shooters, power armor, and both tracking settings. Compare carry-weight changes with bulk-only equipment.
- Exercise movement, retargeting, lost sight, suppressive fire, multi-pellet weapons, bipods, manned guns, unattended turrets, and CIWS where available.
- Compare tooltip predictions with observed selected-target hits, including cover, smoke, darkness, target-region selection, and both standard trajectory modes.
- Verify Integrated Implants mechanitor bonuses on actual stat cards, including the changed intermediate stat-part semantics.
- Save and reload during combat; confirm neutral tracking reconstruction, retained settings, reload behavior, and unchanged existing save data.

No game or multiplayer playtest was run.
