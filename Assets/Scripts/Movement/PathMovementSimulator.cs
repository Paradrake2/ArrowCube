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
        private IReadOnlyList<PathPosition> cubeRail;
        public IReadOnlyList<CellPathPosition> CellRail { get; }
        public IReadOnlyList<CellSegment> TraversedSegments { get; }
        public IReadOnlyList<PathPosition> Rail
        {
            get
            {
                if(cubeRail==null)
                { var cube=CubeTopologyAdapter.Require(Grid.Surface); var copy=new List<PathPosition>(CellRail.Count); foreach(var point in CellRail) copy.Add(point.ToCube(cube)); cubeRail=copy.AsReadOnly(); }
                return cubeRail;
            }
        }
        public int StepCount => CellRail.Count - Path.CellIds.Count;
        public int? BlockingPathId { get; }
        public int? BlockedCellId { get; }
        public SurfaceCoordinate? BlockedCell => BlockedCellId.HasValue ? CubeTopologyAdapter.Require(Grid.Surface).GetCoordinate(BlockedCellId.Value) : (SurfaceCoordinate?)null;
        public CellPathPosition AttemptedCellHead { get; }
        public PathPosition AttemptedHead => AttemptedCellHead.ToCube(CubeTopologyAdapter.Require(Grid.Surface));

        internal MovementPlan(SurfaceGrid grid, ArrowPath path, MovementOutcome outcome,
            List<CellPathPosition> rail,CellPathPosition attemptedHead,List<CellSegment> traversedSegments,int? blockingPathId = null,
            int? blockedCell = null, ExitRules rules = null, ExitType? exitType = null)
        {
            Grid = grid; Revision = grid.Revision; Path = path; Outcome = outcome;
            CellRail = rail.AsReadOnly(); AttemptedCellHead = attemptedHead; TraversedSegments=traversedSegments.AsReadOnly();
            BlockingPathId = blockingPathId; BlockedCellId = blockedCell;
            Rules = rules ?? ExitRules.Hybrid; ExitType = exitType;
        }

        public PathPosition GetPosition(int step, int headToTailIndex)
            => GetCellPosition(step,headToTailIndex).ToCube(CubeTopologyAdapter.Require(Grid.Surface));

        public CellPathPosition GetCellPosition(int step,int headToTailIndex)
        {
            if (step < 0 || step > StepCount) throw new ArgumentOutOfRangeException(nameof(step));
            if (headToTailIndex < 0 || headToTailIndex >= Path.CellIds.Count)
                throw new ArgumentOutOfRangeException(nameof(headToTailIndex));
            return CellRail[Path.CellIds.Count - 1 + step - headToTailIndex];
        }
    }

    /// <summary>Discrete snake motion with exhaustive head sweeps and no Unity/physics dependencies.</summary>
    public sealed class PathMovementSimulator
    {
        public MovementPlan Simulate(SurfaceGrid grid, int pathId, ExitMap exits, ExitRules rules = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (exits == null) throw new ArgumentNullException(nameof(exits));
            CubeTopologyAdapter.RequireCompatible(grid.Surface,exits.Surface);
            if (!grid.Paths.TryGetValue(pathId, out var path)) throw new ArgumentException("Path is not active.", nameof(pathId));
            rules = rules ?? ExitRules.Hybrid;
            ExitType? selectedExit = null;
            var rail = new List<CellPathPosition>(path.CellIds.Count + Math.Min(grid.Surface.CellCount,256));
            var traversed=new List<CellSegment>();
            var body = new Queue<CellPathPosition>(path.CellIds.Count);
            var bodyCells = new HashSet<int>();
            for (int i = path.CellIds.Count - 1; i >= 0; i--)
            {
                var position = new CellPathPosition(path.CellIds[i]);
                rail.Add(position); body.Enqueue(position); bodyCells.Add(position.CellId);
            }
            var head = rail[rail.Count - 1];
            var direction = path.HeadDirection;
            var visitedHeadStates = new HashSet<CellGate>();
            while (true)
            {
                CellPathPosition next; CellSegment? crossed=null;
                if (!head.IsOnSurface)
                    next = new CellPathPosition(head.CellId, head.ExitDistance + 1, head.ExitType.Value, head.ExitDirection);
                else
                {
                    var step = grid.Surface.Step(head.CellId, direction);
                    // Both mechanics are checked at this same logical step. An edge wins a tie
                    // with a gate on its boundary cell; an earlier compatible gate still wins normally.
                    selectedExit=rules.SelectExit(step.AllowsEdgeExit,rules.EnableDirectionalGates && exits.CanExit(head.CellId,direction));
                    if (selectedExit.HasValue)
                    {
                        next = new CellPathPosition(head.CellId, 1, selectedExit.Value, direction);
                    }
                    else
                    {
                        // A repeated directed head state cannot discover a new exit on a static surface.
                        if (!visitedHeadStates.Add(new CellGate(head.CellId, direction)))
                            return Result(MovementOutcome.NoExit, head);
                        crossed=new CellSegment(grid.Surface,head.CellId,direction);
                        next = new CellPathPosition(step.CellId);
                        direction = step.Direction;
                    }
                }

                // Tail vacates simultaneously: entering its just-vacated cell is legal.
                var tail = body.Dequeue();
                if (tail.IsOnSurface) bodyCells.Remove(tail.CellId);
                if (next.IsOnSurface)
                {
                    if (grid.TryGetCellOwner(next.CellId, out int owner) && owner != pathId)
                        return Result(MovementOutcome.BlockedByPath, next, owner, next.CellId);
                    if (grid.TryGetSegmentOwner(crossed.Value, out owner) && owner != pathId)
                        return Result(MovementOutcome.BlockedByPath, next, owner, next.CellId);
                    if (!bodyCells.Add(next.CellId))
                        return Result(MovementOutcome.SelfCollision, next, pathId, next.CellId);
                }
                if(crossed.HasValue) traversed.Add(crossed.Value);
                body.Enqueue(next); rail.Add(next); head = next;
                if (bodyCells.Count == 0) return Result(MovementOutcome.Removed, head);
            }

            MovementPlan Result(MovementOutcome outcome, CellPathPosition attempted, int? blocker = null,
                int? blockedCell = null) => new MovementPlan(grid, path, outcome, rail, attempted,traversed,
                    blocker, blockedCell, rules, selectedExit);
        }
    }
}
