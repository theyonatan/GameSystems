# Above Island Route Generator

Version 1.5.3

Prefab-driven Unity route generation for:

```text
Island -> Connection -> Island -> Connection -> Island
```

The system generates the complete route as data first. It instantiates prefabs only after the main route, Beacon placement, island-group phases, collision checks and detours succeed. An optional child generator then decorates that successful route with background islands. Version 1.4 adds a non-destructive Island Forge catalog utility; 1.4.1 adds persistent island names/IDs; 1.5.0 adds biome/size-specific odds, actionable placement diagnostics, Forge exports, reliable detour fallbacks, and corrected scenic satellite clusters; 1.5.1 adds collision-safe standalone Small-island scale boosts; and 1.5.3 adds safe cleanup and removal controls for the grouped island pools.

No ScriptableObjects are used.

## Install or update

Copy `Runtime` anywhere under `Assets` and copy `Editor` beneath an `Editor` folder, for example:

```text
Assets/Game Systems/WorldSystems/IslandRouteGenerator/Runtime
Assets/Game Systems/WorldSystems/IslandRouteGenerator/Editor
```

Add `IslandRouteGenerator` to one scene GameObject.

The release zip intentionally contains **no `.meta` files**. When updating an
existing installation, merge/replace the included script and documentation
files over the existing folder. Do not delete the installed folder first, and
do not replace its existing `.meta` files. This preserves Unity script GUIDs,
component references, prefab references and serialized inspector data.

Version 1.5.3 does not rename or remove any 1.0.0, 1.1.0, 1.2.0, 1.2.1, 1.3.0, 1.4.0, 1.4.1, 1.5.0, or 1.5.1
serialized field.

## 1.5.3 workflow changes

- The grouped **Island Prefabs by Biome & Size** section now detects hidden empty rows and shows a warning with their exact count.
- Click **Remove Empty Rows** once to delete all null island-table entries and clear the validation errors they caused.
- Every visible island row now has a small `×` button. It removes that prefab from the complete island table, including each of its biome-specific chance overrides.
- Both removal actions support Unity Undo. When **Auto Even Chances** is enabled, affected biome/size pools are normalized again automatically.
- **Add Empty Island Row** was removed because the grouped Inspector could not display or fill the blank row. Add islands by dragging prefab assets onto the section instead.

## 1.5.1 workflow changes

`Distribution & Clearance` now includes **Boost Standalone Small Islands** plus
a minimum/maximum multiplier. The default is `1.2..1.6`. The boosted radius is
reserved during placement, so the enlarged result cannot overlap a route piece,
another background island, or an exclusion volume. If that Small island later
becomes a successful scenic-cluster center, the reservation is removed and the
center stays at normal scale. Satellites and Hero Landmarks are never boosted.

## 1.5.0 workflow changes

### Organized island odds

The route Inspector now presents islands as:

```text
Grass
  Medium — total 100%
  Small  — total 100%
  Detour Endpoints
Golden Trees
  Medium — total 100%
  Small  — total 100%
  Detour Endpoints
```

Shared islands may have an independent Chance % in every supported biome. The
old global `ChancePercent` remains as the migration fallback, so existing scene
data keeps its prior odds until the new Inspector creates biome overrides. Use
**Normalize 100%** on one bucket or **Normalize Every Biome/Size Pool**.

`Repeat Gap` means how many other islands must be selected before that same
prefab may repeat. Setting every island to 2 is safe only when every requested
biome/size/phase has enough alternative eligible prefabs; the 1.5 diagnostics
now report when Repeat Gap is the actual blocker.

Cluster `Topology Weights` are relative preferences, not percentages and not
island odds. `1 / 1 / 1 / 1` means equal 25% shares; a value of 0 disables that
topology. Island selection odds come only from the biome/size Chance % pools.

### Placement diagnostics and detours

Failed main-route slots now identify Linear versus Cluster Spine context and
report eligible prefab names, biome/size/phase filters, Repeat Gap and Max/Run
blocks, missing route sockets, disallowed connection types, overlaps, heading,
lateral drift, forward progress, height and cluster-envelope rejections.

Cluster failures summarize how many times each topology was attempted and the
last detailed rejection for each. The last lateral island no longer requires a
spare Detour exit, and optional Small/Medium fallback makes side groups robust
when one size cannot fit. A detour without a dedicated Detour Endpoint now ends
on a regular Small island instead of silently rolling back.

### Forge and configuration exports

Forge badges are displayed beside each island name. **Copy Complete Forge
Report** copies unique and per-biome totals plus every island's name, ID, biome,
size, role, phase usage, sockets, Placement Bounds and optional BackgroundIsland
settings. Route and Background generator Inspectors have their own copy buttons
for chances, global settings and last diagnostics.

### Scenic background clusters

Satellite placement now accounts for the center island explicitly instead of
applying ordinary layer spacing against it. Large center bounds therefore no
longer make a configured spread impossible. Cluster diagnostics distinguish
chance skips, the maximum-group cap, missing eligible prefabs, placement
failure and performance-budget blocks.

`Maximum Clusters Per Run` still caps a 100% Cluster Chance. Set it to 0 for
unlimited eligible centers. `Reserved Satellite Slots` prevents ordinary layer
islands from consuming the entire island-count budget before cluster members
are planned. Visual Cost is a performance-budget unit only: 1 is
normal and it never affects spawn odds. A prefab may carry both `AboveIsland`
and `BackgroundIsland`; Forge and validation support that workflow.

## Island Forge organizer (1.4)

`IslandForgeOrganizer` is an editor catalog for authored prefab instances. It
does not participate in runtime route generation.

### First setup

1. Use **GameObject > Above > Create Island Forge**, or create a scene GameObject
   named `Forge` and add `IslandForgeOrganizer` to it.
2. Keep Forge where you want the local catalog origin to live.
3. Place every authored island and connection prefab instance as a **direct
   child** of Forge. Nested helpers inside those prefabs are ignored.
4. Select Forge to see its live statistics, browser, filters and grid settings.
5. Press **Arrange Recognized Children** when the settings look right.

Only direct-child roots containing `AboveIsland` or `ConnectionIsland` are
recognized. Unrecognized children remain visible in the browser and statistics,
but grid arrangement never moves them.

### Island identity and top catalog (1.4.1)

Every `AboveIsland` now has two catalog fields:

- `Island Name`: the readable authored name, such as `Kobold Trap`.
- `Island ID`: a manually assigned project-wide unique integer. `-1` means the
  ID has not been assigned yet.

Existing prefabs keep all prior data. Until an identity is filled, the catalog
uses the scene/prefab instance name and displays `#---`, together with a warning
badge. IDs are displayed with at least three digits, such as `#017`, but are not
limited to 0-100.

The Island Catalog is the first Forge Inspector section. Each biome card shows:

```text
Golden Trees — Total Eligible: 5
  Medium — Total: 2  (Exclusive: 1 • Shared: 1)
    #020 Kobold Trap
    #024 Twin Cliffs        Shared: Grass
  Small — Total: 3  (Exclusive: 2 • Shared: 1)
    ...
```

A multi-biome island deliberately appears in every supported biome card because
it contributes to every corresponding generation pool. It carries a `Shared:
Other Biome` badge in each card and also appears once in the dedicated Shared /
Multi-Biome overview. All-Biome islands are similarly explicit and have their
own overview. Therefore the list beneath a biome always explains its eligibility
total instead of hiding duplicate membership.

Catalog entries can be searched by readable name, scene/prefab name, ID or tag.
Click an entry to select it, double-click to frame it in Scene View, or use its
Frame button. Hovering shows biomes, size, role, phase usage and cached socket
count. The Inspector warns about missing and duplicate IDs/names; these warnings
do not block route generation or grid arrangement.

Connections appear below the island sections, grouped by type and listed by
name. The compact statistics and advanced browsing/filtering/arrangement tools
remain below the catalog.

### Default grid order

The organizer lays out the catalog in Forge-local X/Z rows:

```text
Grass • Medium
Grass • Small
Golden Trees • Medium
Golden Trees • Small
Shared / Multi-Biome • Medium
Shared / Multi-Biome • Small
All Biomes • Medium
All Biomes • Small
Connections • Normal
Connections • Launch Pad
Connections • Drop Down
Connections • Zipline
```

Empty categories are skipped. Long categories wrap according to `Columns Per
Row`. Connection types can instead share one combined row.

The layout uses authored Placement Bounds when available and can fall back to
Renderer bounds. It writes only each recognized direct child's `localPosition`.
It preserves rotation, scale, active state and sibling index, and the complete
arrangement is registered as one Undo operation.

### Multi-biome islands

`Shared Rows` is the recommended layout rule:

- An island with one Allowed Biome appears in that biome's Medium or Small row.
- An island with multiple Allowed Biomes appears once in Shared / Multi-Biome.
- An empty Allowed Biomes list, meaning every biome, appears once in All Biomes.

The live biome table reports **eligibility**, so a shared island is counted in
each biome it supports. The headline Islands / Small / Medium totals remain
unique counts. This distinction prevents double-counted biome eligibility from
being mistaken for the number of scene objects.

`First Supported Biome` is available as a more compact alternative. It places a
multi-biome prefab in only its first supported biome's row without changing the
prefab's actual biome eligibility.

### Browse, sort and filter

The Forge Inspector can search by object name or metadata and filter by:

- island, connection or unrecognized object;
- Small or Medium island;
- biome;
- exclusive, shared or all-biome membership;
- connection type.

Results may be displayed in hierarchy, name, type, biome or size order. This is
only an Inspector view; it never rearranges the Transform hierarchy.

**Show Only Matching In Scene** uses Unity's editor-only Scene Visibility. It
does not call `SetActive`, modify gameplay or affect a build. **Clear Scene
Filter** reveals only the objects hidden by the organizer during the current
editor session, preserving objects that were already manually hidden.

### Live statistics and Scene View guides

The Inspector reports:

- direct children, recognized islands, connections and unrecognized objects;
- unique Small and Medium island counts;
- per-biome Small, Medium, total, exclusive and shared eligibility;
- connection counts and percentages by type;
- missing Placement Bounds and missing cached socket warnings.

Optional Scene View guides label the planned rows while Forge is selected.
Individual child labels may also be enabled. Both are editor-only and do not add
scene objects.

## Socket convention

Add an `IslandSocket` component to every socket child.

- An entry socket's forward arrow points into the piece, following player travel.
- An exit socket's forward arrow points away from the piece, following player travel.
- Aligned entry and exit sockets therefore use the same forward direction.
- Use `Both` for a socket that may act as either entry or exit.
- Use `Main Route`, `Detour`, or `Both` to control which route may consume it.
- In island groups, the numbered spine consumes `Main Route` sockets. Lateral
  cluster paths consume `Detour` or `Both` sockets.
- Use `Allowed Connections` when a socket only accepts a launch, drop, zipline or normal connection.

Do not pitch an island's socket to express height. Keep its rotation level and put the connection's exit socket physically higher or lower. This keeps the next island level.

## Regular island prefab

1. Add `AboveIsland` to the prefab root.
2. Choose `Small` or `Medium`.
3. Choose `Regular`, `Junction`, or `Detour Endpoint`.
4. Choose `Linear`, `Cluster`, or `Both` under Phase Usage. Existing islands
   default to `Both` when upgrading.
5. Add the supported biomes. An empty biome list means every biome.
6. Add socket children and press **Refresh Socket References**.
7. Press **Add Placement Bounds Box**, then resize the disabled BoxCollider to reserve the island, trees, rocks and visual clearance.

Use multiple Placement Bounds boxes for a curved or irregular island. They are disabled in generated instances and are never used as gameplay colliders.

Junction islands need an unused detour-capable exit after the main route consumes its main exit. A Detour Endpoint may use one socket marked `Both`.

## Connection prefab

1. Add `ConnectionIsland` to the prefab root.
2. Choose `Normal`, `Launch Pad`, `Drop Down`, or `Zipline`.
3. Add at least one entry socket and one different exit socket.
4. Position the exit to author the connection's turn, distance and height change.
5. Add Placement Bounds for the bridge/path/zipline corridor.

Examples:

- Straight path: exit ahead at the same height.
- Correction curve: exit ahead and sideways, with a corrected forward rotation.
- Launch pad: exit ahead and higher.
- Drop: exit ahead and lower.
- Zipline: exit farther ahead and lower.

The generator derives left/right/up/down from the socket transforms. There are no directional prefab enums.

## Generator setup

### Generation

- `Route Start`: where island 1's entry is aligned. Its forward defines the overall main-route direction.
- `Generated Parent`: optional scene parent.
- `Generate On Start`: use only on the authority responsible for generating the world.
- `Use Random Seed`: disable for deterministic testing.
- `Maximum Route Attempts`: complete dry-plan retries before failure.
- `Beacon Island Prefab`: placed after all biome phases and excluded from island numbering.
- `Beacon Connection Override`: optional exact connection before the Beacon.

### Biome phases

Add list rows in route order:

| Biome | Minimum | Maximum |
| --- | ---: | ---: |
| Grass | 10 | 12 |
| Golden Trees | 3 | 5 |

Adding another enum value and another list row supports a future biome.

## Island Group Phases (1.2)

Island Group Phases are optional playable clusters inserted into the existing
numbered route. They do not replace the normal Biome Phases list.

```text
Linear route -> cluster entrance -> numbered cluster spine -> linear route
                                      |       |       |
                                  side island-link-side island
```

Each rule is configured on the same `IslandRouteGenerator` inspector:

- `Enabled` turns the rule on without deleting its values.
- `Biome` controls eligible islands, connections and grass integration.
- `Chance %` is rolled once for each allowed occurrence.
- `Minimum / Maximum Start Index` controls where the cluster may be inserted.
  `-1` for Maximum means any available position before the final base island.
- `Maximum Occurrences Per Run` caps repeated rolls of that rule.
- `Minimum / Maximum Spine Islands` inserts 3-5 numbered islands by default.
- `Centerpiece Prefab` optionally forces a landmark such as the Power Crystal
  Grove into the middle of the spine.
- `Minimum / Maximum Additional Islands` controls playable lateral islands.
- `Medium Additional Island Chance` adds size variety to those side islands.
- `Reward Endpoint Chance` may use a `Detour Endpoint` as the final side island.
- `Extra Link Chance` and `Maximum Extra Links` attempt loop/cross connections.
- `Maximum Width`, `Maximum Height Range`, and branch heading keep the group
  inside a readable envelope.
- `Maximum Cluster Attempts` retries only the current cluster before rejecting
  the complete dry-plan attempt.
- `Extra Link Position / Angle Tolerance` controls how accurately a connection
  prefab must meet an unused socket on an already placed island.
- Topology weights select `Hub`, `Diamond`, `Ring`, or `Braided`. Values are
  relative weights and do not need to total 100.

### Counting rule

The inserted spine islands are normal numbered main-route islands. They advance
Special Island ranges, biome/rhythm progression and Beacon timing.

Additional lateral islands and their connections do **not** advance those
systems. This keeps a 5-7 Special Island rule stable even when the cluster
grows extra side content.

### Cluster prefab setup

1. Set eligible island prefabs to Phase Usage `Cluster` or `Both`.
2. Give several possible spine islands three distinct sockets: a `Main Route`
   or `Both` entry, a different `Main Route` or `Both` continuation exit, and a
   different `Detour` or `Both` lateral exit.
3. Give additional cluster islands `Detour` or `Both` entries and exits. A
   single endpoint socket may use `Both` socket usage.
4. Give connection prefabs intended for lateral clusters `Detour` or `Both`
   entry and exit sockets.
5. Use Route Usage `Both` only when either the main spine or a lateral path may
   consume that socket. Once consumed, the socket cannot be reused.
6. Keep reward/challenge endpoints marked `Detour Endpoint`; they may have one
   socket marked `Both`.
7. Keep using the normal Island Prefabs and Connection Prefabs percentage
   tables. Cluster generation respects chance, repeat gap and Max / Run.

Cluster lateral paths use the same Detour-compatible socket category as normal
detours even when `Enable Detours` is off. The setting enables the older random
Junction detour feature; it does not disable sockets labelled Detour.

Hub/Diamond/Ring/Braided affect how branch anchors and closure targets are
preferred. A physical loop is created only when one of the eligible connection
prefabs actually lands within the configured position and angle tolerances.
If no connection fits, the playable side islands remain valid; the generator
does not stretch or deform authored prefabs.

### Recommended first test

Use one Island Group Phase with:

| Setting | Value |
| --- | ---: |
| Chance | 100% while testing |
| Start index | 4 to 8 |
| Max occurrences | 1 |
| Spine islands | 3 to 4 |
| Additional islands | 1 to 2 |
| Extra link chance | 25% |
| Max extra links | 1 |
| Cluster attempts | 20 |

Once the sockets and bounds validate reliably, lower Chance to the desired demo
frequency and increase Additional Islands if the group still feels too sparse.

### Island rhythm

The default rhythm is:

```text
Medium -> Small -> Small -> occasionally Small -> Medium
```

`Additional Small Island Chance` controls each optional small island above the minimum. With minimum 2 and maximum 3, it is exactly the chance of receiving the third small island.

### Percentage tables

Assign every regular island and connection in the generator's compact tables.

You can select multiple island prefab assets in the Project window and drag
them directly onto the **Island Prefabs** list title/header. The same works for
multiple `ConnectionIsland` prefabs and the **Connection Prefabs** list. Existing
entries are skipped instead of duplicated.

Chance is normalized among candidates that are currently valid. A prefab configured at 10% receives approximately 10% of selections when the eligible entries total 100% and it passes the biome, role, repeat, socket, collision and route-shape filters. If it cannot fit, its percentage is redistributed among remaining valid candidates.

Example straight-connection variants:

| Prefab | Chance |
| --- | ---: |
| Straight Common A | 40% |
| Straight Common B | 40% |
| Straight Rare A | 10% |
| Straight Rare B | 10% |

`Minimum Repeat Gap` is the number of other pieces required between repetitions. `Maximum Per Run` uses `-1` for unlimited.

New table rows default `Maximum Per Run` to `-1`. A value of `0` deliberately
disables that prefab for normal generation, and validation reports it.

#### Auto Even Chances

Enable `Auto Even Chances` on the generator to keep a table total at exactly
100% while editing:

1. Rows above the edited Chance remain unchanged.
2. The edited row keeps the entered value, clamped to the remaining budget.
3. Only rows below it are recalculated.
4. Lower rows retain their relative proportions.
5. If every lower value is zero, the remainder is divided equally.
6. Values are rounded to two decimal places using largest-remainder allocation,
   so the table still totals exactly 100%.

Example:

```text
25, 25, 25, 25
Edit row 1 to 40 -> 40, 20, 20, 20
Edit row 2 to 30 -> 40, 30, 15, 15
```

Adding or removing rows while Auto Even is enabled proportionally normalizes
the table. Auto Even is an authoring convenience; runtime selection still
normalizes only the candidates eligible for the current biome, size, sockets
and placement.

The validator reports percentage totals separately for each biome and island size, and for connections in each biome.

### Special islands

Special rules reserve an inclusive, one-based main-route index:

| Prefab | Minimum index | Maximum index |
| --- | ---: | ---: |
| Tutorial | 1 | 1 |
| Falling Bridge | 5 | 7 |
| Future Event | 9 | 13 |

The generator chooses a unique random index inside each range. It only considers indexes whose active biome is supported by that prefab. Special islands override normal percentages but must still fit spatially.

Connections, lateral cluster islands, detours and the Beacon do not count
toward these indexes. Numbered cluster-spine islands do count.

## Route correction and height

The route is scored against the Route Start's forward line:

- Maximum heading angle prevents spiraling/backwards exits.
- Maximum lateral drift keeps the journey inside a broad corridor.
- Minimum forward progress prevents folding backward.
- Relative height limits keep launches and drops inside the playable cloud layer.
- Candidate score tolerance decides how close a piece must be to the best correction before its percentage participates.

Low score tolerance produces a straighter, more aggressively corrected route. Higher tolerance gives percentages more influence and produces more variety.

## Detours

After the complete main route, Beacon and island groups are safe, junction islands may generate side routes. Detour length includes the final `Detour Endpoint`.

```text
Junction -> 0-2 small regular islands -> Detour Endpoint
```

Branches are generated after the main path, so they cannot steal the Beacon corridor. A failed optional detour is rolled back without affecting the valid main route.

## Multiplayer

The generator intentionally has no FishNet dependency.

Call `GenerateRoute` only from the authority responsible for world construction, or generate the same deterministic seed before enabling players on every peer. `PieceInstantiated` is exposed for a project-specific FishNet adapter if generated prefabs must be explicitly network-spawned.

Use `On Route Generated` to enable/spawn players after the world is ready.

## Background islands (1.3)

Background islands are a separate, one-way decoration pass. They read the
finished playable route but never move it, consume sockets, or affect whether
route generation succeeds.

Recommended hierarchy:

```text
Island Route Generator
├── [Generated Above Island Route]
└── Background Islands
    ├── BackgroundIslandGenerator
    └── [Generated Background Islands]
```

### First setup

1. On `IslandRouteGenerator`, press **Create Background Islands Child (1.3)**.
   You may instead create a child named `Background Islands` manually.
2. Add `BackgroundIslandGenerator` to the child. It automatically finds the
   route generator on its parent; assigning it explicitly is optional.
3. Make dedicated scenery prefabs. Add `BackgroundIsland` to each root. These
   prefabs should not contain sockets, enemies, pickups, grass managers, or
   required gameplay scripts.
4. Choose allowed biomes, allowed Near/Middle/Far layers, approximate size and
   Visual Cost on each prefab.
5. Press **Add Placement Bounds Box** and resize it around the complete visible
   silhouette, including trees and tall rocks. Multiple bounds are supported.
6. Drag one or many prefab assets onto the **Background Prefabs** foldout name
   or table header. Set Chance %, Repeat Gap and Max / Run.
7. Generate the playable route, then press **Generate Background**. Once the
   layout is working, keep `Generate With Route` enabled for automatic builds.

The generator uses the route's last seed combined with `Seed Offset` by
default. The same playable route and settings therefore reproduce the same
background layout without changing the route's own random sequence.

### How the spread works

The generated main islands form a sampled route spine. Each enabled layer
chooses positions along that spine, then offsets candidates sideways and
vertically:

| Layer | Intended use | Default character |
| --- | --- | --- |
| Near | Strong readable silhouettes beside play | Fewer, smaller, casts shadows |
| Middle | Main sense of world depth | Moderate groups and height variety |
| Far | Hazy skyline and landmarks | More distance, no shadows by default |

`Empty Cell Chance` deliberately leaves gaps along the journey. Left/Right
weights and automatic side balancing prevent every island from collecting on
one side. Scenic Clusters optionally place one or two smaller satellites around
some successful islands. They still obey collision and performance budgets.

### Important settings

- `Start / End Island Index` restricts scenery to part of the route. `-1`
  includes its end and the Beacon.
- `Extend Before / Beyond Route` lets the vista continue past the selected
  route endpoints.
- `Route Corridor Clearance` protects the readable playable path.
- `Playable Bounds Clearance` protects every generated route-piece Placement
  Bounds volume.
- `Background Bounds Clearance` controls separation among scenery prefabs.
- `Boost Standalone Small Islands` enlarges only Small layer islands that do not
  become scenic-cluster centers. Its default range is `1.2..1.6`; satellites and
  landmarks keep their authored scale.
- `Candidates Per Island` lets density volumes influence several valid
  alternatives; it does not increase the final island count.
- Each layer controls count, density, distance, height, spacing, scale, maximum
  prefab size, empty cells and shadows.
- `Maximum Background Islands` is the hard instance budget.
- `Maximum Visual Cost` sums each prefab's relative Visual Cost. Set it to 0
  only when no cost budget is wanted.
- Generated colliders are disabled by default. Far shadows are disabled and
  missing Far `LODGroup` components can produce warnings.

Start conservatively. The default cap is 24 background islands; large islands
should usually be rare landmarks rather than ordinary Far-layer filler.

### Hero landmarks

Landmark rules place a specific `BackgroundIsland` within a one-based route
index range. Each rule has its own chance, layer, side preference, distance,
height, scale and Max / Run. Landmarks are planned before filler islands, so a
rare large silhouette receives space before smaller scenery is distributed.

### Exclusion and density volumes

- Add `BackgroundExclusionVolume` plus its BoxCollider wherever scenery must
  never appear, such as a camera vista, boss arena sightline, tall cloud wall,
  or future hand-authored structure.
- Add `BackgroundDensityVolume` to attract or discourage candidates in a box.
  A multiplier of 1 is neutral, above 1 attracts, and 0 blocks candidates.

Both volume types may live anywhere in the scene. They affect only background
generation and do not alter the playable route.

### Biomes and grass

Every candidate receives the nearest sampled route biome. A background prefab
with an empty Allowed Biomes list works everywhere; otherwise it appears only
in its allowed biomes. `Biome Transition Blend Distance` softens abrupt borders
by allowing either neighboring biome close to a boundary.

Background prefabs should normally be lightweight baked scenery. Do not attach
the playable-island grass bridge or a world grass manager to every distant
island. If a few Near landmarks need procedural grass, budget those prefabs
explicitly and let the single scene `WorldGrassManager` remain authoritative.

### Independent controls and safety

`BackgroundIslandGenerator` has its own **Validate**, **Generate Background**
and **Clear Background** buttons. Clearing the playable route can automatically
clear its background child. A background placement shortage logs a warning and
keeps every placement that safely fit; it never invalidates the playable route.

## Important prefab rules

- Use uniform scale on prefab roots.
- Use dedicated Placement Bounds colliders, not gameplay colliders.
- Keep Placement Bounds disabled; generated instances disable them automatically as well.
- Make connection entry and exit sockets different children.
- Give every main-route island at least one usable exit besides its chosen entry.
- Keep island entry rotations level when vertical travel should not tilt the destination island.

## Editing multiple prefabs

`AboveIsland` and `ConnectionIsland` support Unity multi-object editing. Select
several prefab assets to change shared component settings together. **Refresh
Socket References** and **Add Placement Bounds Box** also operate on every
selected route piece.

`AboveIsland` uses Unity's `SelectionBase`, so clicking one of its children in
the Scene view selects the island root by default.
