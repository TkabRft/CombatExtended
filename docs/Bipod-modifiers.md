# Bipod modifiers and stat previews

code-synced: 2026-10-20

Status: implemented.

## Integrated behavior

The fork integrates [upstream PR #4766](https://github.com/CombatExtended-Continued/CombatExtended/pull/4766) at `89754f0a7951e8e1ac1158d37e64313acf6a6136`, with additional baseline ownership and preview fixes.

Deployed recoil and warmup use only their deployed multipliers. Undeployed penalties no longer remain underneath those bonuses. Additional range applies only while deployed; resetting or unequipping restores the unmodified verb values. Re-equipping applies the undeployed state again.

For an LMG with a base warmup of one second, the bipod previews show 0.85 seconds deployed and 1.5 seconds undeployed. Both values remain the same when the weapon is deployed, retracted, freshly initialized, or reset after unequipping. These are bipod-adjusted verb values; the combat implementation still applies its other weapon, pawn, projectile, and aiming factors.

The upstream stat-part changes use existing localized bipod status labels and correctly describe the deployed sway multiplier for definition cards. Both item and definition cards show automatic setup modes using existing localized aiming-mode labels. Without a category-specific mode, automatic setup uses aimed shot and suppressive fire.

## Baseline ownership and lifecycle

[`BipodComp`](../Source/CombatExtended/CombatExtended/Comps/BipodComp.cs) owns a transient [`BipodVerbState`](../Source/CombatExtended/Utilities/BipodVerbState.cs). Before applying a state, the helper clones the current CE verb properties into an unmodified baseline. Each application creates a fresh copy of that baseline, so repeated deployment, retraction, spawn/load reconstruction, or reset cannot accumulate multipliers or range bonuses. Only the copied scalar recoil, range, and warmup values are changed.

The helper recognizes the last verb-properties object it assigned. A different live reference, such as a unique-trait override or an underbarrel selection, becomes a new baseline on the next lifecycle operation or existing `SetUpStart` tick. Its projectile, burst count, and other unrelated properties are retained. Switching back to a different main-weapon reference also establishes a new baseline. This mechanism handles reference replacements; arbitrary in-place edits by external mods are outside this integration's scope.

Spawning a fresh weapon initializes the undeployed state. Spawning after loading preserves the saved deployment flag. `PostLoadInit` reconstructs deployed or undeployed modifiers even when the weapon has no fire-mode component. The existing `SetupFromLoad` and `SetUpInvert` entry points remain in the load paths. Equipping refreshes the fire-mode component and retracts the bipod; unequipping restores the baseline. Initialization and lifecycle operations apply no modifiers when bipod mechanics are disabled. Live settings-toggle handling was not redesigned.

Deployment retains its existing setup job and sound. Existing multiplayer synchronization attributes are unchanged. All game-object access remains on the calling game thread.

## Read-only previews

[`StatWorker_BipodDisplay`](../Source/CombatExtended/CombatExtended/StatWorkers/StatWorker_BipodDisplay.cs) uses the same modifier calculation as the component for both displayed states. Definition requests use definition verb properties. Instance requests copy the component's effective baseline, including an unprocessed replacement reference when necessary.

Preview calls neither capture a new tracked baseline nor assign verb properties. They do not remove stat entries from shared definitions. Missing bipod components or CE verb properties hide the stat or fall back to the base explanation. Missing explicit sway values use the stat's default base value.

The display never divides by deployed or undeployed multipliers. Zero recoil or warmup multipliers therefore do not prevent the opposite state from being calculated correctly.

## Persistence and compatibility

The existing `isBipodSetUp` Scribe key is unchanged. Baselines and assigned-reference tracking are not serialized; they are reconstructed from the loaded weapon properties. There is no save-schema migration, new setting, renamed localization key, new Harmony patch, or dependency change. Existing saves receive the corrected deployed values when their bipod state is reconstructed.

The integration does not alter the subsequent-shot state machine. Its existing behavior and transient load reconstruction are described in [Subsequent-shot rebalance](Subsequent-shot-rebalance.md).

## Validation

The solution build passed against the existing pinned references. The standalone runner passed 48 focused cases: 14 in [`BipodTests`](../Source/Tests/BipodTests.cs), 33 subsequent-shot cases, and the source load-layout case. The runtime package passed XML, content-hash, metadata, and layout validation; the load-layout test also passed from the published package directory.

The bipod cases exercise production baseline tracking and arithmetic without loading the game: repeated transitions, independent weapon instances, neutral/reset state, fresh load reconstruction, replacement properties and unrelated fields, previews before initialization, preview isolation, zero multipliers, missing properties, and the LMG warmup regression. They do not execute RimWorld's component callback order or an actual unique-trait/underbarrel interaction.

Remaining in-game acceptance checks:

- Exercise manual and automatic deployment, aiming modes, movement cancellation, and setup sound.
- Transfer a weapon through equipment, inventory, and the ground; inspect both preview states before and after equipping.
- Save and reload deployed and undeployed weapons, including a representative unique-trait weapon and an underbarrel weapon.
- Switch underbarrel/main modes in both bipod states and verify projectile, burst count, recoil, warmup, and range after the next component update.
- Fire repeated bursts to verify the corrected bipod inputs alongside subsequent-shot warmup behavior.

No game, installation, deployment script, or multiplayer playtest was run.
