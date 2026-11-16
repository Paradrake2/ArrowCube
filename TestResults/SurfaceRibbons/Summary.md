# Surface ribbon rendering fix

Replaced camera-facing arrow-body LineRenderers with flat, surface-aligned meshes. Constant-width face segments use bounded miter joins; cross-face and gate-exit segments use explicit transported surface frames. The filled head and body share the same join geometry.

Verified in Unity 6000.6.7f1 on 2026-11-15:

- 174 EditMode tests passed.
- 22 PlayMode tests passed.
- 1,008 generated puzzles still generated and replayed successfully.
- Graphics renders visually checked from front, grazing, and corner views.

## Rendered views

- [Front](ribbon-front.png)
- [Grazing angle](ribbon-grazing.png)
- [Cube corner](ribbon-corner.png)

## Test results

- [EditMode XML](results-EditMode.xml)
- [PlayMode XML](results-PlayMode.xml)
- [Generation regression](stage3-generation-1008.csv)
