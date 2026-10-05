# Exusiai Humanoid Implementation Plan

**Goal:** Deliver independent Generic and Humanoid FBX and prefabs with explicit suffixes, retaining shared materials and original deformation data.

**Architecture:** Work in the existing model branch. Perform Unity import and humanoid avatar construction in /private/tmp/exusiai-playback-check. Keep the current Generic GUIDs when renaming assets. Create new GUIDs for Humanoid resources. Keep all 253 source bones; map the main weighted leg chains as human limbs and synchronize their Deform counterparts. Preserve current English names via prefab overrides and create matching English avatars.

**Tech Stack:** Unity 6000.3.25f1, URP 17.3, ModelImporter, AvatarBuilder, HumanPoseHandler, PlayableGraph.

## Constraints

- Shared material and texture assets must not change.
- Generic vertices and its 31 animation samples must remain unchanged.
- Humanoid avatar must be valid and human, all required bones mapped, finite mesh vertices, preserved blendshapes and complete skin bindings.
- Validate humanoid muscle animation with a clip authored against a distinct reference avatar and actual Play mode.
- Do not change gameplay or require downloaded commercial animations.

## Tasks

- [x] Verify absent Humanoid asset fails the validation gate; capture baseline Generic geometry and transforms.
- [x] Construct Humanoid importer mapping, reference T pose and English avatar; inspect geometry and muscle motion. Retain auxiliary bones without promising MMD runtime constraint solving.
- [x] Create suffixed Generic and Humanoid assets and preview scene, retain Generic GUIDs and source animation.
- [x] Verify isolated Play, render neutral/action views, copy validated assets to main project.
- [x] Record reports, paths, limitations and manual inspection procedure. Update tools and documentation for renamed Generic paths.

User approved the two-copy design and requested implementation in this conversation. Execute inline; no new review gate or Git commit is required.

## Execution findings

The leg weight audit found the majority of knee/foot weights on the main chains. A failing pose check reproduced 0.956385 m separation; the Humanoid-only 8-link follower reduced separation to 1.304e-6 m without touching bind data. Manual PlayableGraph initially enabled Foot IK and pulled feet to absent goal curves; disabling diagnostic Foot IK restored full leg geometry. Both true Play controllers pass; Generic after rename displacement 0.1526966 m, Humanoid about 0.467 m. Source copies, settings and verification helpers are documented in ArtSource/Exusiai/RigSource/README.md.
