# Zero-armor utility apparel

code-synced: 2026-10-20

Status: implemented.

## Cause and correction

`ArmorUtilityCE.TryPenetrateArmor` processes worn apparel covering the hit part, including utility apparel without armor protection. The Biotech tox pack (`Apparel_PackTox`) is one such item: its definition supplies no armor ratings, and CE's Biotech pack patch adds shoulder coverage without adding armor protection.

The hard-armor durability path previously logged `penAmount or armorAmount are zero` when either value was zero and all three apparel armor ratings were zero. This reported a valid utility-apparel state as an error during ordinary combat.

The hard-armor ratio calculation now explicitly runs only when both penetration and the applicable armor rating are nonzero. Otherwise its durability damage remains zero, as before, without the erroneous diagnostic or the diagnostic's extra stat lookups. The existing heat and soft-armor paths are unchanged.

## Behavior and compatibility

Damage reduction, penetration loss, deflection, and durability calculations retain their previous behavior. A utility item with zero armor does not gain protection from this fix. The correction applies to this calculation for all apparel, without a tox-pack-specific patch.

No defs, identifiers, save fields, settings, dependency versions, public signatures, or Harmony hooks changed. Existing saves require no migration, and no third-party support status changes.

## Validation

- The prescribed solution build passed with the existing frozen dependencies.
- The existing source load-folder test passed: one passed, zero failed.
- Diff review confirmed that the change is limited to removing the invalid diagnostic and expressing the existing nonzero ratio guard directly. The ratio formula and surrounding damage calculations are unchanged.

## Remaining manual acceptance

With Biotech active, reproduce a sharp hit to a pawn wearing a tox pack on a covered body part. Confirm that the zero-armor error no longer appears and that damage continues through to the pawn. Compare against a pawn wearing protective armor and check a save reload. Compilation and layout validation do not replace in-game acceptance.

The player wiki is unchanged because combat behavior is unchanged; this is a diagnostic correction.
