# Stage 4 verification

Unity 6000.6.7f1, verified 2026-11-15.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| EditMode | 164 | 0 | 0 |
| PlayMode | 19 | 0 | 0 |

The EditMode suite includes the 1,008-puzzle generation/replay regression. No recipes or solution validations failed.

PlayMode tests use isolated Input System Mouse, Keyboard, and Touchscreen devices and real queued device state events. Coverage includes clean and buffered clicks, long-press timing, path/empty/right drags, zoom, keyboard shortcuts, touch taps/drags, pinch and multi-touch transitions, UI capture, focus/reset/disable cancellation, hidden-face occlusion, cross-face selection, and an existing EventSystem.

Camera and layout tests cover every face, both poles, bounded zoom, inertia/reset, portrait/landscape safe areas, and high DPI.

Batch mode uses a null graphics device. Physical device feel, rendered appearance, fullscreen display transitions, and platform builds have not been hardware-tested.

Artifacts:

- [EditMode results](results-EditMode.xml)
- [PlayMode results](results-PlayMode.xml)
- [Generation regression CSV](stage3-generation-1008.csv)
- [Generation errors (empty)](stage3-generation-errors.txt)
