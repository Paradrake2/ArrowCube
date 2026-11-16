# TODO — Tomorrow, November 17, 2026

- [x] Add color or contrasting shades to the cube faces so adjacent faces and cube edges are easy to distinguish while rotating the puzzle. Keep arrows clearly visible against each face. All six faces use distinct light colors and an unlit opaque shader, so scene lighting and shadows cannot darken a side.
- [x] When a player tries to clear a blocked arrow, animate it forward as far as it can travel before hitting the blocker, then return it to its original position. Mark the contact point and highlight the blocker while preserving the puzzle state and existing blocked-attempt rules.
