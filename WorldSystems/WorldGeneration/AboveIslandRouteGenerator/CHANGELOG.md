# Changelog

## 1.5.3

### Island pool cleanup

- Added a warning when the serialized island table contains hidden empty rows.
- Added **Remove Empty Rows**, including the number of rows that will be removed.
- Added a compact `×` button to every grouped island row. Removing a shared
  prefab removes its single source entry and all of its per-biome chance data.
- Added Unity Undo support to both cleanup paths.
- Automatically re-normalizes all affected biome/size pools after removal when
  **Auto Even Chances** is enabled.
- Removed the unusable **Add Empty Island Row** action; prefab assets are added
  through the grouped section's drag-and-drop area.

### Compatibility

- Runtime generation, validation rules and serialized data structures are
  unchanged.
- No existing serialized field or public API was renamed or removed.
- No ScriptableObjects or `.meta` files were added.

## 1.5.1

### Standalone Small background islands

- Added **Boost Standalone Small Islands** under Distribution & Clearance.
- Added an independently configurable scale range, defaulting to `1.2..1.6`.
- Reserved the boosted Placement Bounds radius before accepting a candidate, so
  enlarged islands cannot introduce overlaps after planning.
- Applied the boost only after scenic clusters are resolved.
- Excluded successful cluster centers, all satellites and Hero Landmarks.
- Added the active boost configuration to clipboard diagnostics and the number
  of boosted islands to the generation report.

### Compatibility

- No existing 1.5.0 serialized field or public API was renamed or removed.
- Route generation, Forge organization, scenic clusters and grass integration
  remain unchanged.
- No ScriptableObjects or `.meta` files were added.

## 1.5.0

### Forge catalog

- Moved Shared/Junction/metadata badges beside their island names.
- Added **Copy Complete Forge Report** with unique/per-biome totals, island
  names/IDs, sizes, roles, phase usage, socket capability, bounds, connections
  and attached BackgroundIsland metadata.
- Added BackgroundIsland awareness without duplicating scene children.

### Route pools and diagnostics

- Added backward-compatible per-biome island chances and a Biome > Medium / Small
  Inspector grouped view with independent 100% normalization.
- Added Linear/Cluster eligibility counts and clear phase badges in each pool.
- Clarified that topology weights are relative preferences: equal values mean
  equal shares, zero disables a topology, and they do not affect island odds.
- Added detailed main-route and cluster failure diagnostics covering eligible
  prefab names, biome/size/phase filtering, repeat/max limits, sockets,
  connection masks, overlap and route-shape/envelope rejection counts.
- Added route configuration/diagnostics clipboard export.
- The final cluster side island no longer requires a future Detour exit.
- Added optional Small/Medium fallback for cluster side islands.
- Detours now fall back to a regular Small finale when no dedicated Detour
  Endpoint can be placed, and skipped-detour reasons are reported.

### Background groups

- Fixed scenic satellites being rejected by normal center-island spacing.
- Automatically expands center-to-center spread enough to clear authored bounds.
- Added satellite surface gap, maximum size and independent Repeat Gap behavior.
- Added reserved satellite budget slots so regular singles cannot consume every
  island slot before scenic groups are planned.
- `Maximum Clusters Per Run = 0` now means unlimited eligible centers.
- Added explicit cluster counters for chance, cap, prefab, fit and budget blocks.
- Added background configuration/diagnostics clipboard export.
- Permitted one prefab root to carry both AboveIsland and BackgroundIsland.
- Clarified Visual Cost as performance-budget units, never selection weight.

### Compatibility

- All pre-1.5 serialized fields and grass integration APIs remain present.
- Existing global island chances remain the fallback until per-biome overrides
  are created, preserving upgrade data.
- No ScriptableObjects or `.meta` files were added.

## 1.4.1

### Added

- Added a manually authored `Island Name` and project-wide unique `Island ID`
  to `AboveIsland`. Existing prefabs remain compatible and temporarily fall
  back to their scene/prefab instance name until the new fields are filled.
- Moved the Island Catalog to the top of the Forge Inspector.
- Added a collapsible catalog card for every biome with Total Eligible, Medium
  Total and Small Total counts, each split into Exclusive and Shared.
- Listed every eligible island directly under its biome and size as `#ID Name`.
- Added prominent Shared, All Biomes, Junction, Detour Endpoint, missing-name,
  missing-ID, duplicate-name and duplicate-ID badges.
- Added dedicated Shared / Multi-Biome and All-Biome overview sections so every
  intentional cross-biome duplicate is easy to audit.
- Added a connection catalog grouped by connection type and showing each name.
- Added catalog search by island name, scene/prefab name, numeric ID or tag.
- Added click-to-select, double-click-to-frame, Frame buttons and metadata
  tooltips containing size, role, biomes, phase usage and socket count.
- Added Junction, Shared Island and Missing ID headline metrics.
- Added Island ID/name support to the existing advanced browser and Scene View
  labels.
- Explicitly listing every current biome is treated as Shared / Multi-Biome in
  the catalog; only an empty Allowed Biomes list uses the All Biomes category.

### Counting behavior

- Unique Islands, Small and Medium remain non-overlapping scene-object totals.
- Biome totals are eligibility totals: an island supporting two biomes appears
  in both cards and is marked Shared in both places.
- All-Biome islands appear in each biome where they are eligible and in their
  dedicated overview, always with an All Biomes badge.
- Missing or duplicate metadata never blocks arrangement or generation.

### Compatibility

- No existing 1.4.0 serialized field or public API was renamed or removed.
- All route, cluster, background-island, Forge arrangement and grass-integration
  behavior remains available.
- No ScriptableObjects were introduced.
- The update archive intentionally contains no `.meta` files.

## 1.4.0

### Added

- Added `IslandForgeOrganizer`, a separate scene-catalog component for a Forge
  parent containing authored island and connection prefab instances.
- Added **GameObject > Above > Create Island Forge** for one-click setup.
- Added a polished Forge Inspector with headline metric cards, per-biome Small
  and Medium eligibility, exclusive/shared counts, connection-type statistics,
  and catalog-health warnings.
- Added name and metadata search plus kind, size, biome, biome-membership and
  connection-type filters.
- Added Inspector-only display sorting by hierarchy order, name, type, biome or
  size without changing Transform sibling order.
- Added editor-only Scene Visibility filtering with session tracking, so clearing
  the organizer filter does not reveal objects that were already manually hidden.
- Added an Undoable scene grid layout that organizes biome Medium/Small rows,
  shared/all-biome rows, and connection rows.
- Added Placement Bounds-aware cell sizing with Renderer-bounds fallback,
  configurable wrapping, gaps, centering, local origin and height preservation.
- Added optional colored Scene View row guides and direct-child labels.

### Multi-biome behavior

- A one-biome island is placed in that biome's Medium or Small row.
- A multi-biome island is placed once in a dedicated Shared / Multi-Biome row by
  default, while statistics count it as eligible for every supported biome.
- An island with an empty Allowed Biomes list is placed once in All Biomes and
  counted as eligible for every enum biome.
- An optional First Supported Biome mode provides a more compact grid without
  changing actual generation eligibility.

### Safety and compatibility

- Arrangement touches only `localPosition` on recognized direct children and is
  recorded as one Undo step. It does not change sibling indices, rotation,
  scale, active state, prefab assets or unrecognized children.
- Filtering uses editor Scene Visibility rather than `SetActive`.
- No 1.0.0 through 1.3.0 serialized field or public API was renamed or removed.
- The grass integration context API remains present and unchanged.
- No ScriptableObjects were introduced.
- The update archive intentionally contains no `.meta` files.

## 1.3.0

### Added

- Added a separate `BackgroundIslandGenerator` designed for a child object of
  the playable `IslandRouteGenerator`.
- Added a one-click **Create Background Islands Child (1.3)** setup button to
  the main route inspector.
- Added route-relative Near, Middle and Far background layers with independent
  count, density, lateral distance, height, spacing, scale, size, scenic-gap
  and shadow controls.
- Added dedicated `BackgroundIsland` prefab metadata with biome/layer filters,
  size, visual cost, random yaw, scale range and Placement Bounds.
- Added weighted background prefab tables with Chance %, Repeat Gap, Max / Run,
  Auto Even editing and multi-prefab drag-and-drop.
- Added deterministic background seeds derived from the successful route seed
  plus a separate offset.
- Added rare Hero Landmark rules with route-index ranges, chance, side,
  distance, height, scale and run limits.
- Added optional scenic clusters that place smaller satellite islands around
  selected background centers.
- Added `BackgroundExclusionVolume` and `BackgroundDensityVolume` scene tools.
- Added hard island-count and visual-cost budgets, automatic collider disabling,
  Far shadow disabling and optional missing-LOD warnings.
- Added independent Validate, Generate and Clear controls plus generated and
  failed UnityEvents.
- Added `RouteCleared` to the playable generator's public event API so the child
  background pass can clean up with its parent.

### Generation behavior

- Background positions are sampled along the actual generated route spine and
  offset into side/height bands instead of filling a global random box.
- Empty distribution cells create deliberate scenic gaps; side balancing keeps
  the vista from accumulating entirely on one side.
- Hero landmarks are planned first, regular layer islands second, and scenic
  satellites last. Every stage respects playable Placement Bounds, route
  clearance, background spacing, volumes and performance budgets.
- Biomes are inherited from the sampled route with an optional transition blend.
- Background shortages are best-effort warnings. They never reject or rebuild a
  valid playable route.

### Compatibility

- No existing serialized field was renamed or removed.
- `AboveRoutePiece.HasGeneratedContext`, `GeneratedBiome`, and
  `InitializeGeneratedContext` remain available for the grass integration.
- No ScriptableObjects were introduced.
- The update archive intentionally contains no `.meta` files.

## 1.2.1

### Changed

- Numbered cluster spines continue to consume `Main Route` sockets.
- Lateral cluster branches, additional island chains, reward endpoints and
  physical cross-links now consume only `Detour` or `Both` route sockets.
- Main Route-only sockets can no longer be selected for lateral cluster paths.
- Cluster validation now checks for three distinct usable sockets: a Main Route
  entry, a different Main Route continuation exit, and a different Detour/Both
  branch exit.
- Validation also reports missing Detour/Both entry/exit pairs on eligible
  lateral island and connection prefabs.
- Cluster failure messages and Inspector guidance now describe the exact socket
  categories required.

### Compatibility

- No serialized fields or public grass-integration APIs were removed or
  renamed.
- Existing components and generator settings are preserved when scripts are
  merged over 1.2.0.
- No ScriptableObjects were introduced.
- The update archive intentionally contains no `.meta` files.

## 1.2.0

### Added

- Added optional `Island Group Phases` to the central generator inspector.
- Added chance, one-based start range and maximum occurrences per group rule.
- Added a numbered 3-5 island cluster spine with configurable range.
- Added an optional forced Centerpiece prefab in the middle of a cluster spine.
- Added 0-3 non-numbered lateral islands with independent Medium chance.
- Added optional reward/challenge endpoints using existing `Detour Endpoint`
  prefabs.
- Added Hub, Diamond, Ring and Braided topology weights.
- Added optional physical cross-links between unused island sockets when an
  authored connection prefab fits the configured position/angle tolerances.
- Added cluster width, height and branch-heading envelopes.
- Added cluster-only retries and precise cluster failure messages.
- Added `Linear / Cluster / Both` Phase Usage to `AboveIsland`.
- Added `IsGeneratedClusterPiece` runtime context to generated route pieces.
- Added validation for cluster ranges, topology weights, centerpiece biome,
  branch-capable sockets and reward endpoint availability.

### Generation behavior

- Cluster spine islands advance normal one-based main-route numbering.
- Lateral cluster islands and cross-link connections do not affect Special
  Island indexes, rhythm or Beacon timing.
- Existing island and connection percentage, repeat-gap and Max / Run rules
  are reused inside clusters.
- Existing Placement Bounds checks protect the complete main route, Beacon,
  earlier clusters and later detours.
- Failed cluster attempts roll back their pieces, socket reservations and run
  usage before trying again.

### Compatibility

- No 1.0.0 or 1.1.0 serialized field was renamed or removed.
- Existing islands default to Phase Usage `Both`.
- No ScriptableObjects were introduced.
- The update archive omits `.meta` files. Merge/replace it over the installed
  package so Unity keeps existing GUIDs, component references and inspector
  data.

## 1.1.0

### Added

- Added `Auto Even Chances` to the generator inspector.
- Editing a chance locks that row and all rows above it, then proportionally
  redistributes the remaining percentage across lower rows.
- Added exact two-decimal percentage allocation so Auto Even tables total 100%.
- Added multi-prefab drag-and-drop onto the Island Prefabs and Connection
  Prefabs list titles/headers.
- Added multi-object inspector editing for `AboveIsland` and
  `ConnectionIsland` prefabs.
- Added multi-object support to **Refresh Socket References** and **Add
  Placement Bounds Box**.
- Added Unity `SelectionBase` behavior to `AboveIsland`.
- Added validation errors when the Beacon or Beacon connection override does
  not support the final biome.
- Added validation warnings for entries whose Max / Run is `0`.

### Changed

- Newly added Island and Connection table rows now reliably initialize Max /
  Run to `-1` (unlimited), including rows created through multi-drag.
- Percentage validation ignores entries disabled by 0% chance or Max / Run 0
  when determining whether a usable category exists.
- Generated update archives omit `.meta` files so merging scripts preserves
  the installed Unity GUIDs and serialized component data.

### Compatibility

- No 1.0.0 serialized field was renamed or removed.
- Install by merging/replacing files over the existing package. Do not delete
  the existing package folder or its `.meta` files before updating.
