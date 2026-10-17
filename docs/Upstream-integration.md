# Upstream integration

code-synced: 2026-10-17

## Integrated revision

Status: implemented.

The fork branch `TkabRftPatches` integrates `CombatExtended-Continued/CombatExtended`'s `Development` revision `979aed6f0` (2026-10-12), fetched on 2026-10-17. This imports 95 commits beyond the previous fork head `b6dc8e334`, changing 86 upstream files. The mod version advances to 16.7.4.0; the supported game version remains 1.6.

The merge had no textual conflicts. The two files holding the fork's differences from the common ancestor remain byte-for-byte unchanged:

- `ModPatches/Rimsenal Core/Defs/Rimsenal Core/Ammo_GDGrenades.xml`
- `ModPatches/Rimsenal Core/Patches/Rimsenal Core/Weapons_GD_CE.xml`

## Source ownership and integration decisions

- Moving-target prediction and collision changes belong to `ShiftVecReport`, `Verb_LaunchProjectileCE`, and `ProjectileCE`. Collision scans reuse separate cell lists for the outer projectile scan and nested blocker scan.
- Unique-weapon ammunition changes belong to `AmmoInjector`, `Harmony_CompUniqueWeapon`, and the new `QuestNode_Root_AncientMercenaries_RunInt` postfix. The latter uses the quest slate's `LEADER` and `WEAPON` values to provision spare magazines after quest generation. Magazine initialization in `Harmony_CompUniqueWeapon` is guarded by `!fromSave`.
- `StatPart_ExtendedMagazine` reports individual trait effects. The integration uses localized trait labels with numeric modifiers instead of upstream's hardcoded English words, and formats negative offsets with a single minus sign. Existing localization keys are unchanged.
- `HediffComp_Prometheum` guards unspawned pawns before map access. `SuppressionUtility.InterceptorZonesFor` filters both vanilla and registered shield-zone cells to the map bounds.
- `Defs/Ammo/` owns the ammunition balance and Warhammer recipe-base changes. Optional content stays under its existing or newly registered `ModPatches/` folder; `LoadFolders.xml` gates it by package ID. Compatibility additions are listed in [SupportedThirdPartyMods.md](../SupportedThirdPartyMods.md).
- `BuildCompat.py` records already-built project outputs and passes matching project references to the existing compiler wrapper. The workspace validation continues to use the prescribed solution build.

No project files, package pins, framework targets, third-party reference DLLs, package identity, or existing def identifiers were changed by this integration. No changed C# lines introduce or rename Scribe keys or settings keys. There is no intentional save-format migration. Existing saves still need an in-game check because imported def balance and optional-mod patches affect runtime content.

Player-facing behavior is documented in [Upstream updates](../wiki/Upstream-updates.md).

## Validation

From the workspace root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Custom/Scripts/Build.ps1 -Target CombatExtended
```

Result: `Build is OK` against the existing frozen references. The initial sandboxed attempt could not reach NuGet; the same build succeeded with network access.

The built standalone test runner was invoked from the mod root:

```powershell
& .\Source\Tests\bin\Debug\net9.0\Tests.exe -class Tests.LoadFoldersXmlTests -noLogo -noColor
```

Result: 1 passed, 0 failed. The test checks load-folder existence, duplicate registrations, and relative XML path collisions. Additional focused checks parsed all 73 changed XML files, checked all 776 load-folder entries, confirmed that no existing defName disappeared across changed files, and compared both fork-specific files against the pre-merge HEAD contents. Eleven imported trailing-whitespace lines were trimmed across six XML files and one C# comment without changing their content beyond whitespace.

No game, deployment, or publication was run. Manual acceptance remains: moving and leaning target combat, unique-weapon generation and save reload, the ancient mercenary quest, shield behavior at map edges, and relevant optional-mod patch application. Def syntax and compilation do not establish that external mods' patch targets still match their installed versions.
