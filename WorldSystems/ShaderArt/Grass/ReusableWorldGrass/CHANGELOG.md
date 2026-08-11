# Changelog

## 2.0.0

### Grass Tool 2.0

- Replaced the temporary EditorWindow-owned grass list with direct
  `GrassComputeScript` editing.
- Added reliable Undo, dirty marking, scene saving, and Prefab Mode saving.
- Removed automatic switching to an arbitrary `GrassComputeScript`.
- Removed the hard-coded Grass Tool Settings asset path.
- Added explicit source selection and Create Grass Child workflow.
- Added Paint, Sculpt, Style, Generate, and Utilities tabs.
- Added absolute Set Size sculpting with immediate visible preset clamping.
- Added signed relative sizing, smoothing, controlled randomization, and colour
  painting.
- Added live editing of the referenced `SO_GrassSettings` asset.
- Added debounced preview rebuilds.
- Added point, blade, and triangle estimates.
- Added setup validation and one-click material Blend correction.
- Improved mesh generation to use world-space surface area.
- Added local-space reprojecting and legacy world-to-local conversion.

### Renderer and prefab fixes

- Added complete local-to-world position and normal transformation in the
  compute renderer.
- Updated bounds and culling data to follow moved and rotated sources.
- Fixed repeated Scene View callback registration during renderer resets.
- Fixed dispatch-size integer division.
- Guarded missing interactor arrays during fast paint previews.
- Disabled Blend on all included procedural grass materials.
- Kept the optimized point-count-based draw-buffer allocation.

### Compatibility

- Preserved original script and asset `.meta` GUIDs.
- Preserved existing Grass Settings fields and the serialized `GrassData`
  layout.
- Added new tool settings fields without renaming or removing existing data.
- Kept `GrassSource`, `WorldGrassManager`, `WorldGrassCutter`, and the optional
  Above integration.
