# Upstream updates

## Combat Extended 16.7.4.0

This update keeps this fork's Rimsenal weapon balance, Hybrid Rifle grenade launcher, and reduced industrial-component cost for its grenade ammunition.

### Shooting and ammunition

- Aiming leads moving targets from their displayed position. Projectile collision checks also account for pawns moving between cells, including pawns crossing the firing line.
- Unique weapons receive ammunition support even when ordinary traders and pawn equipment generation do not offer that weapon. Newly generated unique weapons initialize their magazines; loading a saved game does not refill them.
- The ancient mercenary leader receives spare ammunition for the quest's unique weapon, subject to inventory capacity, replacing ammunition for the previous weapon.
- The magazine-size stat lists each applicable weapon trait's multiplier or additive modifier. Weapons without a magazine modifier do not show an empty modifier explanation.
- .44-40 and .45-70 ammunition have adjusted armor penetration. .45-70 ammunition recipes have higher material and work costs. Foam fuel cells and toxic mortar shells have corrected market values.
- Warhammer 40k ammunition recipes require Crafting 6. With Warcaskets - Adeptus Astartes active, these recipes also require Warcasket Weaponry research and can use the machining table through the ammunition crafting settings.

### Combat fixes

Shield protection checks ignore cells outside the map. Prometheum effects skip pawns that are not spawned on a map, avoiding errors while they are away.

### Optional mod support

New patches support Arsenal - Orbital Security, Arsenal - The Juggernaut, Arsenal - The New Republic, Eventide Warcasket, Faction - Elves, More Archotech Implants, The Thrumbo Horn Weapons, and Vanilla Quests Expanded - The Generator. These patches load when their corresponding mods are active.

Existing patches also update Anty weapons, ammunition, creatures and turrets; A Dog Said 2 prosthetic attacks; Alpha Animals; Machines of War; ReGrowth animals; Rimsenal Spacer mechanoids; Shambler Variants; Save Our Ship 2; Vanilla Animals Expanded; Vanilla Genetics Expanded; Vanilla Quests Expanded - Drone Factory; and Vanilla Weapons Expanded. Medieval Overhaul iron-bolt support is restored for Vanilla Factions Expanded - Medieval 2. Rimatomics masks use the overhead apparel layer so they draw correctly.

See the [supported mod list](../SupportedThirdPartyMods.md) for the wider compatibility list and the [main feature overview](../README.md#features) for general combat mechanics.
