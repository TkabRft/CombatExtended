# Tactical lighting safety

code-synced: 2026-10-22

Status: implemented.

## Reported failure

A pawn starting `Wait_Combat` threw a `NullReferenceException` in `AI_Utility.VisibilityGoodAt`, reached through `CompOpportunisticSwitch.TryFlare` and CE's tactical cast checks. This happens while starting an attack, before the standard shot loop and burst-control preflight. The flare tactic checks visibility before checking inventory, so a pawn need not carry a flare gun to reach the failing code.

The original visibility helper dereferenced `map.GetLightingTracker()` without checking its result. That accessor explicitly returns null for a missing or unregistered map, and can receive a missing map component. Its cache also retained a null component lookup for that map indefinitely. The report identifies the unguarded lighting access; it does not establish which map lifecycle condition caused the unavailable lighting context. Other mods appearing as patched callers in the trace do not establish their responsibility.

## Implementation

`CE_Utility.Lighting.cs` retains the existing public `GetLightingTracker` signature and indexed cache fields. Cache hits require the same map, a non-null tracker, and matching tracker ownership. A missing component is queried again on a later lookup, allowing map initialization to recover. Reused map indices and larger indices retain their existing supported behavior. Null maps and negative indices still return null; the accessor does not create components or register maps.

`AI_Utility.Visibility.cs` retains the existing public `VisibilityGoodAt` signature. Missing/unregistered maps, uninitialized sky data, missing/despawned shooters, different shooter maps, invalid target cells, and missing or incorrectly owned trackers return true for this optional illumination decision. This means lighting is not a reason to replace the original attack with a flare job when the lighting context is unknown. It does not bypass weapon eligibility, visibility, or shooting-line checks in the firing path.

With a valid lighting context, existing day/night, close-range, combat-glow, night-vision, and roof decisions are unchanged. No exception is caught or suppressed, no logging is added, and no unrelated tactical policy is changed. The owning utility classes are partial to permit direct regression coverage of these methods; their public type identities and method signatures remain unchanged.

The fallback does not recreate a permanently missing CE map component, repair arbitrary tactical/pawn corruption, or make every other lighting caller safe against an invalid map. Its scope is the reported optional visibility decision and recovery from transient cache misses.

## Validation and remaining acceptance

The solution build passed against the existing pinned references. All 216 focused cases passed: 19 new production-linked lighting cache/visibility cases plus the existing 197 targeting, burst-control, subsequent-shot, bipod, load-layout, and affected siege-builder cases.

The new cases cover recovery after an early null lookup, positive-cache reuse, map-index reuse and growth, incorrect tracker ownership, invalid map contexts, ordinary visibility decisions, and explicit night-vision overrides without reading the pawn stat. Lightweight map/pawn/tracker doubles host the actual production methods; compilation checks the real game API. These are not a reproduction of the reported running game's lifecycle.

No settings, Scribe keys, dependency pins, or save format changed. Packaging remains deferred, so the existing release snapshot does not contain this fix. Documentation remains a separate future contribution group from runtime code and tests.

Remaining manual acceptance is to repeat Bree's combat entry and NPC auto-attacks, including after map loading, map changes, and with/without an available flare gun. Confirm that valid nighttime flare deployment still works and that an unavailable lighting context no longer interrupts the optional visibility check. No game was launched for validation.
