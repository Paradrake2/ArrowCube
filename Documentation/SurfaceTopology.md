# Shape-independent logical surface topology

The traversal foundation now uses cell IDs and explicit boundary connections.
`SurfaceCell` and `CellBoundary` are immutable published objects. They do not use
`CubeFace`, `SurfaceCoordinate`, Unity types, geometry meshes, or directional gates.

## Construction and immutable traversal

`SurfaceTopologyBuilder.AddCell(id)` registers a unique nonnegative cell ID.
`SetBoundary(id, direction, boundary)` configures an individual directed boundary.
`Connect(id, direction, neighborId, newDirection, isGeometricEdge, allowsEdgeExit)`
sets both ends, including the inverse heading transformation. Builder connections
may be edited before publishing or between builds.

`Build()` validates the closed connection graph and returns an immutable
`SurfaceTopology`. Each build owns a new cell collection and copied boundary
arrays. Boundaries themselves are sealed immutable values exposed as objects.
Subsequent builder changes cannot change a previously published topology.

Validation rejects empty graphs, duplicate/negative IDs, missing boundary
configuration, nonexistent destinations, invalid directions/headings, and missing
or incorrectly oriented reverse connections. IDs need not be contiguous. Traversal
enumerates IDs in ascending order, independent of builder insertion order.

For a link from A in direction D to B with arrival heading H, the required reverse
is from B in direction `H.Opposite()` to A with heading `D.Opposite()`. This holds
across flat connections and both kinds of fold. The builder validates logical
connectivity; it does not infer a physically embeddable mesh, classify corners from
positions, or validate mesh manifoldness. Construction supplies the fold metadata.

`ISurfaceTopology` provides the common read-only cell/boundary contract.
`SurfaceTraversal.Step(topology, cellId, direction)` follows the selected boundary
and returns the next cell ID, arrival heading, geometric-fold flag, and ordinary
edge-exit eligibility. It always describes traversal, independently of puzzle rules.
The direction accessor preserves the existing serialized enum values:
Right=0, Up=1, Left=2, Down=3.

An existing cube can also be copied into a fully validated explicit graph:

```csharp
ISurfaceTopology cube = new CubeTopology(6);
var builder = new SurfaceTopologyBuilder();
foreach (int id in cube.EnumerateCellIds()) builder.AddCell(id);
foreach (int id in cube.EnumerateCellIds())
    for (int d = 0; d < 4; d++)
    {
        var direction = (SurfaceDirection)d;
        builder.SetBoundary(id, direction, cube.GetBoundary(id, direction));
    }
SurfaceTopology topology = builder.Build();
SurfaceCellStep next = topology.Step(0, SurfaceDirection.Right);
```

Normal cube gameplay uses its lazy adapter directly, avoiding this eager copy.

## Fold traversal and exits

| Connection | IsGeometricEdge | AllowsEdgeExit | Behavior |
| --- | --- | --- | --- |
| Flat | false | false | Continue to the connected cell |
| Convex fold | true | true | Eligible for an ordinary edge exit; otherwise continue |
| Concave fold | true | false | Continue through the inside corner |

The two flags are independent, rather than deriving eligibility from any change
of face or heading. A concave boundary must be authored with `AllowsEdgeExit=false`.
The walker transports the heading through its connection and never treats a fold
as a missing neighbor or an automatic end of the route.

`ExitRules.SelectExit(allowsEdgeExit, hasDirectionalGate)` is the shared movement
policy. It selects an ordinary edge exit only if both the level setting and the
boundary permit one. A compatible directional gate can select a gate exit when
gates are enabled, regardless of the geometric boundary's eligibility. An eligible
edge wins an exact tie with a gate, preserving the existing cube rule.
`PathMovementSimulator` uses this policy and keeps its existing snake simulation,
occupancy, collision checks, rail output, and tail-vacating behavior.

There is no gate flag or gate collection in topology. Existing `ExitGate` and
`ExitMap` remain unchanged. Future generic gameplay can look up a puzzle-owned gate
using `(cellId, SurfaceDirection)` before calling the same exit policy.

## Open versus unconfigured boundaries

A builder slot starts unconfigured (`null`). A connected `CellBoundary` has
`Kind=Connected`, a destination ID, and an arrival heading. `CellBoundary.Open()`
explicitly represents an intentional opening with `Kind=Open` and no destination.
These states are distinct; no sentinel ID or missing link silently grants an exit.

This phase's `Build()` rejects intentionally open boundaries with an explicit
`NotSupportedException`; generic traversal also rejects an open boundary if a
provider supplies one. Open-surface traversal and exit behavior remain future work.
The representation already distinguishes an opening from incomplete construction.

## Cube compatibility

`CubeTopology` implements `ISurfaceTopology`. Its unchanged integer cube mapping
formulas and edge/orientation tables construct the immutable cell connections.
Interior boundaries disallow ordinary edge exits; cube seams permit them.
Its coordinate-based `Step` converts the source coordinate to an ID, calls the
shared traversal core, and converts the result back to `SurfaceCoordinate`.
`CrossedEdge` continues reporting a geometric cube seam, while `AllowsEdgeExit`
now carries exit eligibility explicitly.

Cell views are constructed lazily and memoized in a thread-safe private cache;
their IDs, links, headings, and permissions never change. This avoids materializing
every cell in very large cube topologies merely to construct a topology instance.
Existing coordinate, index, orientation, seam midpoint, and edge-mapping APIs stay
available. Topology remains free of Unity references.

Generation algorithms, random-call order, recipe versions, save formats, scene
assets, and rendering have not been changed in this phase.

## Verification

`SurfaceTopologyTests` uses small hand-connected fixtures with successive concave,
convex, and flat transitions. It checks heading transport, round trips, immutable
snapshots, direction indexing, exit/gate policy, deterministic enumeration, and
invalid/open construction. These are logical fixtures, without L/T construction.

`TopologyCompatibilityTests` compares cell-ID and coordinate traversal through
grids of sizes 1, 2, 6, 29, and 64, checks lazy large-grid support, and pins six
version-5 fingerprints captured before this refactor. Existing cube tests continue
checking mappings independently against integer face geometry. Existing historical
generation golden tests cover earlier recipe versions.

Before and after CSVs for 54 puzzles (six profiles, three exit modes, three seeds)
are in `TestResults/SurfaceTopology`. Every fingerprint and every deterministic
CSV statistic matches; generation time is excluded from equality comparisons.
The full Unity run's logs and XML results are in `TestResults/SurfaceTopology/Unity`.

The completed run passed **304 EditMode + 45 PlayMode tests** with zero failures
or skips. This includes the existing 1,008-puzzle generation/replay regression,
historical recipe goldens, cooperative generation, save restoration, cube geometry,
input, scene handoff, and blocked-arrow animations. All **1,008** generated puzzle
fingerprints also match the existing `TestResults/Stage5/Final` regression dataset.

## Remaining cube-specific systems

The generic logical traversal core is ready to consume other authored cell graphs.
The current game still uses cube coordinates in `ArrowPath`, `SurfaceSegment`,
`SurfaceGrid`, `PathPosition`, `PuzzleDefinition`, and the existing gate/save types.
Generation helpers, difficulty tools, camera/picking, seam animation geometry, and
the colored surface renderer also retain their cube assumptions. Migrating those
systems, caching trajectories by topology identity for different shapes, constructing
L/T surfaces, importing meshes, and generic surface serialization belong to later
phases. No non-cube production puzzle can be generated or rendered by this change
alone.

## Files

Added runtime files:

- `Assets/Scripts/Topology/SurfaceCell.cs`
- `Assets/Scripts/Topology/CellBoundary.cs`
- `Assets/Scripts/Topology/SurfaceTopology.cs`
- `Assets/Scripts/Topology/SurfaceTopologyBuilder.cs`

Modified runtime files:

- `Assets/Scripts/Topology/CubeTopology.cs`
- `Assets/Scripts/Movement/ExitRules.cs`
- `Assets/Scripts/Movement/PathMovementSimulator.cs`

Added tests:

- `Assets/Tests/EditMode/SurfaceTopologyTests.cs`
- `Assets/Tests/EditMode/TopologyCompatibilityTests.cs`

New source/test files also include Unity `.meta` files. Documentation adds this
guide and links it from `Stage1-CubeTopology.md`. Validation artifacts are under
`TestResults/SurfaceTopology`.
