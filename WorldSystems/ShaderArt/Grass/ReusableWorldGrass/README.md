# Grass Tool 2.0 + Reusable World Grass

This is a complete replacement Grass folder. It contains the original
Minions Art renderer assets, the optimized compute renderer, the prefab-safe
authoring tool, reusable world batching, and the optional Above integration.

## Clean installation

1. Close the Grass Tool window and exit Play Mode.
2. Delete the old Grass folder from the Unity project, including its `.meta`
   files.
3. Copy this package's complete `Grass` folder into `Assets/GameSystems` (or
   any other location under `Assets`).
4. Let Unity finish importing and compiling.
5. Open `Tools > Grass Tool 2.0`.

The editor no longer relies on a hard-coded installation path. Keep the
included `.meta` files: they preserve the GUIDs used by materials, shaders,
settings, and existing references.

## Author grass on a prefab

1. Open the prefab in Prefab Mode.
2. Select the prefab root and click **Create Grass Child**, or create a child
   named `Grass` and add `GrassComputeScript`.
3. Assign a `Grass Settings` asset. Its Compute Shader must be
   `GrassBlades.compute`, and its material should be one of the included
   procedural grass materials.
4. Add `GrassSource` to the prefab root and let it reference the Grass child.
5. Leave `GrassSource > Data Space` on `Local To Source`.
6. Assign the exact Grass child at the top of Grass Tool 2.0.
7. Set Hit Mask and Painting Mask to include the ground collider's layer.
8. Enable Scene Brush, then right-click and drag in Scene view.
9. Save the prefab normally.

Painted positions and normals are stored directly on `GrassComputeScript` in
its local coordinate space. They therefore follow moved, rotated, duplicated,
and procedurally spawned prefab instances.

## Tool tabs

- **Paint** adds or removes points and defines the initial visible size and
  colour. New sizes are constrained to the referenced preset's visible range.
- **Sculpt** can set an exact height/width, add or subtract size, smooth nearby
  points, randomize size, or paint colour. Stored data is clamped immediately;
  invisible accumulated values are not possible.
- **Style** edits the referenced `SO_GrassSettings` asset directly with Undo
  and a debounced live preview.
- **Generate** distributes points by actual mesh surface area or across a
  Terrain.
- **Utilities** floods size/colour, reprojects, removes steep-surface points,
  converts legacy world data, and clears the selected source.

The status area reports painted points and estimated maximum blades and
triangles. Setup Validation reports missing resources, masks, colliders, and
material Blend problems.

## Existing or legacy data

The `GrassData` structure and existing serialized field names are unchanged.
Prefab data painted at an identity transform remains compatible.

If an older source intentionally stores world-space positions, enable
`grassDataIsWorldSpace`, select that source in Grass Tool 2.0, and use
**Utilities > Convert World Data To Local Space** once. The conversion supports
Undo. Do not run it on data that is already local.

## Use in a scene

1. Create one GameObject named `World Grass Manager`.
2. Add `WorldGrassManager`.
3. Optionally assign Source Root to limit which spawned objects are gathered.
4. Use `WorldGrassCutter` when cuts must affect combined grass batches.

Sources sharing one `SO_GrassSettings` preset become one renderer. Different
presets become separate renderers, allowing different biome colours without
creating one GPU renderer per island.

## Above biome colours

1. Duplicate `Grass Settings.asset` for each visually distinct biome.
2. Configure Top Tint and Bottom Tint in each preset.
3. Add `IslandGrassRouteBridge` beside `IslandRouteGenerator`.
4. Assign the `WorldGrassManager`.
5. Add biome rows and assign their corresponding grass presets.

The optional Above folder is the only part coupled to the island generator.
For another game, delete `ReusableWorldGrass/OptionalAboveIntegration`; the
core grass system remains reusable.

## Performance starting point

Use roughly three blades and three segments per painted point as a practical
starting point. The renderer allocates according to the actual painted point
count rather than reserving the original fixed multi-million-triangle buffer
for every island.

Maximum blades are:

`painted points * allowed blades per point`

Maximum triangles are:

`painted points * blades * (((segments - 1) * 2) + 1)`

Profile on the final target hardware before substantially increasing density.
