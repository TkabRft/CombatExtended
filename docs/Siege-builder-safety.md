# Siege builder safety

code-synced: 2026-10-20

Status: implemented.

## Reported failure and scope

A debug-spawned Empire siege containing nonhuman units repeatedly threw a
`NullReferenceException` in `LordToil_Siege.SetAsBuilder`, reached through
`UpdateAllDuties` during the siege tick. The report identified the Empire as the
affected siege, but did not identify the selected pawn or the exact null object.

The guards address builder candidates without the trackers required by vanilla
builder assignment. They do not suppress exceptions or claim to repair every
possible null reference in the siege code. Faction neutrality is not an
eligibility condition.

## Implementation

The patches live in
[`Harmony_LordToil_Siege_Builders.cs`](../Source/CombatExtended/Harmony/Harmony_LordToil_Siege_Builders.cs).

- A postfix on `LordToil_Siege.CanBeBuilder` preserves a negative vanilla result
  and additionally requires `mindState`, `skills`, and `workSettings` to exist.
  There is no race or faction blacklist. Pawns with the required trackers remain
  subject to vanilla's work restrictions.
- A prefix on `LordToil_Siege.UpdateAllDuties` checks existing `Build` duties
  before vanilla remembers and counts builders. If a builder still has a duty
  but lacks the required trackers, vanilla's `SetAsDefender` assigns its defender
  duty, including the normal siege location and radius.
- Vanilla then selects replacement builders. When none are eligible, its normal
  `NoBuilders` handling remains responsible for the siege transition. The guards
  do not remove pawns from the group or replace the duty-update method.

CE's existing construction-skill and ammunition transpilers are unchanged. The
guards add no logging, saved fields, settings, or persistence keys. Eligibility
is checked again on each refresh, so a pawn is not permanently excluded if its
trackers later become available. Existing invalid builder duties can recover on
the next duty refresh after loading a save.

This does not recreate missing trackers, repair arbitrary pawn corruption, or
guarantee compatibility with another mod that replaces the siege duty methods.

## Validation

The required siege members were checked against the project's pinned references,
and compilation passed without changing dependency versions.

The automated test suite passed all 13 cases: the existing load-folder test and
12 guard regressions. The guard tests compile the production patch source with
small test doubles instead of executing reference-only game assemblies. They
cover missing trackers, preserved vanilla eligibility, existing invalid builder
duties, repeated refreshes, mixed groups, and groups without eligible builders.
These tests do not execute Harmony installation, vanilla builder selection, or
the `NoBuilders` transition inside RimWorld.

## In-game acceptance still required

- Reload the affected Empire siege and confirm that duty updates resume without
  the repeated builder exception.
- Check that a mixed group assigns eligible builders while trackerless units
  retain defender duties.
- Check that a group with no eligible builders follows vanilla's fallback
  without stalling.
- Check that an ordinary hostile siege still constructs and operates artillery.

If the original exception persists, identify the selected pawn and null object
before extending the guards. Compilation and the automated guard tests alone do
not establish that the reported in-game failure is resolved.
