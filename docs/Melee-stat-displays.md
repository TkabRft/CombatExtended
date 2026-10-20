# Melee stat displays

code-synced: 2026-10-20

Status: implemented.

## Integrated revision and scope

The fork integrates upstream [PR #4358](https://github.com/CombatExtended-Continued/CombatExtended/pull/4358), revision `677b84df0a26c05bb602e325365d6d52a8e18cf6`, in runtime merge commit `22a8b25d2`. The merge includes display corrections and regression coverage. The actual melee combat implementation and balance remain unchanged.

`Patches/Core/Stats/Stats.xml` assigns CE's damage-average worker to both pawn `MeleeDPS` and weapon `MeleeWeapon_AverageDPS`. Weapon DPS and average penetration appear in the Weapon category, after the damage entry. Existing stat identifiers are preserved.

## Damage and sustained DPS

`StatWorker_MeleeDamageBase.GetDamageReports` provides the attack data used by the damage and DPS workers. Pawn requests use their updated available melee verbs without terrain tools, with selection weights obtained for a null target. Single-weapon and weapon-def requests use `VerbUtility.GetAllVerbProperties`, including the verb giver supplied by an implant's installation recipe when applicable. The game API adjusts weapon damage, cooldown, and selection weight for the wielder, material, and weapon instance.

- Equipped attacks use the existing CE skill-dependent damage variation. Their unarmed bonus is not added.
- Natural attacks use fixed adjusted damage plus the pawn's enabled `UnarmedDamage` bonus. Weapon variation is not applied to these attacks.
- Pawns without a melee skill use fixed damage. Uninstalled implant previews also have no weapon variation or pawn bonus.
- Sustained DPS is weighted expected damage divided by weighted cooldown, multiplied by the wielder's melee hit chance once. It is not the weighted average of individual attacks' DPS ratios.

The explanation shows each attack's damage variation only when its damage actually varies, its average damage, cooldown, individual DPS, and normalized selection chance. This allows weapon and natural attacks to appear together without a misleading shared variation header. The hit-chance explanation follows the attack entries.

These are untargeted ordinary-attack estimates, before enemy armor and target-dependent defenses. They do not predict critical attacks or measured damage against a particular opponent.

## Armor penetration

For pawn requests, `StatWorker_MeleeArmorPenetration` averages each available CE melee verb's existing `ArmorPenetrationSharp` and `ArmorPenetrationBlunt` properties using its selection weight. The worker does not reapply skill, material, or other multipliers to those final values. This intentionally preserves the combat implementation's current skill-level convention.

Single-weapon previews calculate penetration from the tool's CE penetration, the current combat skill multiplier, the verb's damage-factor multiplier, and the weapon's penetration factor. Weapon-def requests resolve the factor through the stat API with the requested material. A missing equipment factor defaults to one before multiplication; it never replaces the entire penetration result with one. Non-CE verbs contribute zero CE penetration without an invalid tool cast.

The breakdown includes selection chance and relevant multipliers. Zero sharp penetration is labeled with its existing unit and has no multiplication chain. The same simplification applies to zero blunt penetration.

## Robustness and presentation

Implant lookup skips recipes without an added hediff and hediffs without a verb giver. Tool and verb lists are normalized independently, so a provider can contain only verbs or only tools. Lookup continues past empty or ranged-only providers and selects the first provider that supplies melee attacks.

Display-only arithmetic and provider selection live in `Utilities/MeleeStatMath.cs`. Invalid or nonpositive selection weights and cooldowns are excluded from damage reports. Empty or unusable attack sets produce zero DPS and no attack rows; empty damage ranges are blank rather than exposing a sentinel value. Penetration ignores nonfinite results and returns zero when no usable weighted results remain.

“Adjusted for weapon” appears only for a weapon whose adjusted damage differs from its base damage. Natural attacks and implants do not label pawn-derived changes as weapon adjustments. Tool/maneuver labels, units, and explanation text use existing localization facilities; existing translation keys are unchanged.

The public `StatWorker_MeleeDamageBase.GetAdjustedDamage(ToolCE, Thing)` method retains its original signature and implementation for existing callers. There are no new Harmony hooks, dependency changes, setting changes, renamed identifiers, or intentional save-data migrations. No third-party support status is changed by this integration.

## Validation

- The prescribed solution build passed against the existing frozen references.
- `Tests.MeleeStatTests`: 29 passed, zero failed. Coverage includes missing equipment factors, zero/non-unit penetration, weighted runtime penetration, natural and equipped damage variation, enabled unarmed bonuses, mixed attacks with unequal cooldowns, single hit-chance application, skillless-pawn inputs, invalid/empty attack sets, provider selection, and explanation-suppression decisions.
- `Tests.LoadFoldersXmlTests`: one passed, zero failed against the source load layout.
- Changed XML parsed successfully, and the three intended DPS/penetration worker bindings were checked.
- Static comparisons confirmed unchanged melee combat source, unchanged public damage-helper behavior, unchanged dependency pins, and unchanged fork-specific Rimsenal files. No save keys or Harmony patches were introduced.
- The runtime package passed XML, required-assembly, load-path, metadata, and SHA-256 validation.

The regression tests link production display helpers into the existing standalone test project. They do not instantiate RimWorld recipe or verb objects; compilation verifies the actual game API calls. No game, deployment, or publication was run. Vanilla stat XML was unavailable for checking the XML patch targets against the actual game defs.

## Remaining manual acceptance

Inspect pawn and weapon stat screens for unarmed humans at low and high melee skill, ordinary weapons and mixed weapon/natural attacks, different materials and qualities, installed and uninstalled melee implants, animals, and mechanoids. Check final values, tool-selection percentages, zero sharp penetration, and suppression of redundant adjustment/variation lines. Include a verb-only implant provider when an appropriate mod is available.

Confirm the vanilla stat patch operations apply without errors, and verify that a save reload preserves the same stat-screen behavior. These checks remain manual; compilation and helper tests are not in-game acceptance.
