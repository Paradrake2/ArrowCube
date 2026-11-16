using System;
using System.Collections.Generic;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    public enum MovementOutcome { Removed, BlockedByPath, SelfCollision, NoExit }

    /// <summary>A read-only prediction; it does not change the grid or the path.</summary>
    public sealed class MovementPlan
    {
        internal SurfaceGrid Grid { get; }
        internal long Revision { get; }
        public ArrowPath Path { get; }
        public MovementOutcome Outcome { get; }
        public bool Succeeded => Outcome == MovementOutcome.Removed;
        public ExitRules Rules { get; }
        public ExitType? ExitType { get; }
        // Chronological tail-to-head route. Shared rail keeps plan storage O(body length + steps).
        public IReadOnlyList<PathPosition> Rail { get; }
        public int StepCount => Rail.Count - Path.Cells.Count;
        public int? BlockingPathId { get; }
        public SurfaceCoordinate? BlockedCell { get; }
        public PathPosition AttemptedHead { get; }

        internal MovementPlan(SurfaceGrid grid, ArrowPath path, MovementOutcome outcome,
            List<PathPosition> rail, PathPosition attemptedHead, int? blockingPathId = null,
            SurfaceCoordinate? blockedCell = null, ExitRules rules = null, ExitType? exitType = null)
        {
            Grid = grid; Revision = grid.Revision; Path = path; Outcome = outcome;
            Rail = rail.AsReadOnly(); AttemptedHead = attemptedHead;
            BlockingPathId = blockingPathId; BlockedCell = blockedCell;
            Rules = rules ?? ExitRules.Hybrid; ExitType = exitType;
        }

        public PathPosition GetPosition(int step, int headToTailIndex)
        {
            if (step < 0 || step > StepCount) throw new ArgumentOutOfRangeException(nameof(step));
            if (headToTailIndex < 0 || headToTailIndex >= Path.Cells.Count)
                throw new ArgumentOutOfRangeException(nameof(headToTailIndex));
            return Rail[Path.Cells.Count - 1 + step - headToTailIndex];
        }
    }

    /// <summary>Discrete snake motion with exhaustive head sweeps and no Unity/physics dependencies.</summary>
    public sealed class PathMovementSimulator
    {
        public MovementPlan Simulate(SurfaceGrid grid, int pathId, ExitMap exits, ExitRules rules = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (exits == null) throw new ArgumentNullException(nameof(exits));
            if (exits.GridSize != grid.Topology.GridSize) throw new ArgumentException("Exit map uses a different grid size.", nameof(exits));
            if (!grid.Paths.TryGetValue(pathId, out var path)) throw new ArgumentException("Path is not active.", nameof(pathId));
            rules = rules ?? ExitRules.Hybrid;
            ExitType? selectedExit = null;
            var rail = new List<PathPosition>(path.Cells.Count + grid.Topology.GridSize * 4);
            var body = new Queue<PathPosition>(path.Cells.Count);
            var bodyCells = new HashSet<SurfaceCoordinate>();
            for (int i = path.Cells.Count - 1; i >= 0; i--)
            {
                var position = new PathPosition(path.Cells[i]);
                rail.Add(position); body.Enqueue(position); bodyCells.Add(position.Cell);
            }
            var head = rail[rail.Count - 1];
            var direction = path.HeadDirection;
            var visitedHeadStates = new HashSet<ExitGate>();
            while (true)
            {
                PathPosition next;
                if (!head.IsOnSurface)
                    next = new PathPosition(head.Cell, head.ExitDistance + 1, head.ExitType.Value, head.ExitDirection);
                else
                {
                    var step = grid.Topology.Step(head.Cell, direction);
                    // Both mechanics are checked at this same logical step. An edge wins a tie
                    // with a gate on its boundary cell; an earlier compatible gate still wins normally.
                    if (rules.EnableCubeEdgeExits && step.CrossedEdge)
                    {
                        selectedExit = Movement.ExitType.CubeEdge;
                        next = new PathPosition(head.Cell, 1, selectedExit.Value, direction);
                    }
                    else if (rules.EnableDirectionalGates && exits.CanExit(head.Cell, direction))
                    {
                        selectedExit = Movement.ExitType.DirectionalGate;
                        next = new PathPosition(head.Cell, 1);
                    }
                    else
                    {
                        // A repeated directed head state cannot discover a new exit on a static surface.
                        if (!visitedHeadStates.Add(new ExitGate(head.Cell, direction)))
                            return Result(MovementOutcome.NoExit, head);
                        next = new PathPosition(step.Coordinate);
                        direction = step.Direction;
                    }
                }

                // Tail vacates simultaneously: entering its just-vacated cell is legal.
                var tail = body.Dequeue();
                if (tail.IsOnSurface) bodyCells.Remove(tail.Cell);
                if (next.IsOnSurface)
                {
                    if (grid.TryGetCellOwner(next.Cell, out int owner) && owner != pathId)
                        return Result(MovementOutcome.BlockedByPath, next, owner, next.Cell);
                    var segment = new SurfaceSegment(grid.Topology, head.Cell, next.Cell);
                    if (grid.TryGetSegmentOwner(segment, out owner) && owner != pathId)
                        return Result(MovementOutcome.BlockedByPath, next, owner, next.Cell);
                    if (!bodyCells.Add(next.Cell))
                        return Result(MovementOutcome.SelfCollision, next, pathId, next.Cell);
                }
                body.Enqueue(next); rail.Add(next); head = next;
                if (bodyCells.Count == 0) return Result(MovementOutcome.Removed, head);
            }

            MovementPlan Result(MovementOutcome outcome, PathPosition attempted, int? blocker = null,
                SurfaceCoordinate? blockedCell = null) => new MovementPlan(grid, path, outcome, rail, attempted,
                    blocker, blockedCell, rules, selectedExit);
        }
    }
}
