using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    public readonly struct TrajectoryVisit
    {
        public SurfaceCoordinate Cell { get; }
        public int FirstStep { get; }
        public int LastStep { get; }
        internal TrajectoryVisit(SurfaceCoordinate cell,int first,int last) { Cell=cell; FirstStep=first; LastStep=last; }
    }

    public readonly struct CellTrajectoryVisit
    {
        public int CellId { get; }
        public int FirstStep { get; }
        public int LastStep { get; }
        internal CellTrajectoryVisit(int cellId,int first,int last) { CellId=cellId; FirstStep=first; LastStep=last; }
    }

    /// <summary>Exact compressed union of every segment's occupancy, including initial bends.
    /// A rail entry k occupies the moving body's window for steps k-length+1 through k.</summary>
    public sealed class RemovalTrajectory
    {
        private readonly MovementPlan plan;
        private readonly HashSet<int> swept=new HashSet<int>();
        private IReadOnlyCollection<SurfaceCoordinate> cubeCells;
        private IReadOnlyCollection<SurfaceSegment> cubeSegments;
        private IReadOnlyList<TrajectoryVisit> cubeVisits;
        public ISurfaceTopology Surface => plan.Path.Surface;
        public int PathId => plan.Path.Id;
        public bool HasValidExit => plan.Succeeded;
        public ExitType? ExitType => plan.ExitType;
        public MovementOutcome Outcome => plan.Outcome;
        public int StepCount => plan.StepCount;
        public IReadOnlyList<PathPosition> Rail => plan.Rail;
        public IReadOnlyList<CellPathPosition> CellRail => plan.CellRail;
        public IReadOnlyCollection<int> SweptCellIds { get; }
        public IReadOnlyList<CellTrajectoryVisit> CellVisits { get; }
        public IReadOnlyCollection<CellSegment> SweptCellSegments { get; }
        public IReadOnlyCollection<SurfaceCoordinate> SweptCells
        {
            get { if(cubeCells==null) { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<SurfaceCoordinate>(); foreach(int id in SweptCellIds) copy.Add(cube.GetCoordinate(id)); cubeCells=copy.AsReadOnly(); } return cubeCells; }
        }
        public IReadOnlyList<TrajectoryVisit> Visits
        {
            get { if(cubeVisits==null) { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<TrajectoryVisit>(); foreach(var visit in CellVisits) copy.Add(new TrajectoryVisit(cube.GetCoordinate(visit.CellId),visit.FirstStep,visit.LastStep)); cubeVisits=copy.AsReadOnly(); } return cubeVisits; }
        }
        public IReadOnlyCollection<SurfaceSegment> SweptSegments
        {
            get { if(cubeSegments==null) { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<SurfaceSegment>(); foreach(var segment in SweptCellSegments) copy.Add(new SurfaceSegment(cube,cube.GetCoordinate(segment.A),cube.GetCoordinate(segment.B))); cubeSegments=copy.AsReadOnly(); } return cubeSegments; }
        }
        public PathPosition GetBodyPosition(int step,int segment) => plan.GetPosition(step,segment);
        public CellPathPosition GetCellBodyPosition(int step,int segment) => plan.GetCellPosition(step,segment);
        public bool Contains(int cellId) => swept.Contains(cellId);
        public bool Contains(SurfaceCoordinate cell) => Contains(CubeTopologyAdapter.Require(Surface).GetCellIndex(cell));
        public static RemovalTrajectory Calculate(ISurfaceTopology topology,ArrowPath path,ExitMap exits,ExitRules rules,
            CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            return new RemovalTrajectory(new PathMovementSimulator().Simulate(new SurfaceGrid(topology,new[] { path }),path.Id,exits,rules),topology,token);
        }
        private RemovalTrajectory(MovementPlan plan,ISurfaceTopology topology,CancellationToken token)
        {
            this.plan=plan; int length=plan.Path.CellIds.Count;
            var visits=new List<CellTrajectoryVisit>(); var cells=new List<int>();
            var segments=new HashSet<CellSegment>(plan.Path.CellSegments);
            foreach(var segment in plan.TraversedSegments) segments.Add(segment);
            for(int k=0;k<plan.CellRail.Count;k++)
            {
                if((k&63)==0) token.ThrowIfCancellationRequested();
                var position=plan.CellRail[k]; if(!position.IsOnSurface) continue;
                if(swept.Add(position.CellId)) cells.Add(position.CellId);
                visits.Add(new CellTrajectoryVisit(position.CellId,Math.Max(0,k-length+1),Math.Min(plan.StepCount,k)));
            }
            SweptCellIds=cells.AsReadOnly(); CellVisits=visits.AsReadOnly(); SweptCellSegments=new List<CellSegment>(segments).AsReadOnly();
        }
    }

    /// <summary>Bounded per-search cache; immutable path/exit identities and rule flags prevent stale entries.</summary>
    public sealed class RemovalTrajectoryCache
    {
        private readonly int capacity;
        private readonly Dictionary<Key,RemovalTrajectory> entries=new Dictionary<Key,RemovalTrajectory>();
        private readonly Queue<Key> fifo=new Queue<Key>();
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public RemovalTrajectoryCache(int capacity=2048)
        { if(capacity<1) throw new ArgumentOutOfRangeException(nameof(capacity)); this.capacity=capacity; }
        public RemovalTrajectory Get(ISurfaceTopology topology,ArrowPath path,ExitMap exits,ExitRules rules,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested(); CubeTopologyAdapter.RequireCompatible(topology,path.Surface); CubeTopologyAdapter.RequireCompatible(topology,exits.Surface);
            rules=rules??ExitRules.Hybrid; var key=new Key(topology,path,exits,rules);
            if(entries.TryGetValue(key,out var value)) { Hits++; return value; }
            value=RemovalTrajectory.Calculate(topology,path,exits,rules,token); Misses++;
            if(entries.Count>=capacity) entries.Remove(fifo.Dequeue());
            entries.Add(key,value); fifo.Enqueue(key); return value;
        }
        private readonly struct Key : IEquatable<Key>
        {
            private readonly ISurfaceTopology surface; private readonly ArrowPath path; private readonly ExitMap exits; private readonly bool edges,gates;
            public Key(ISurfaceTopology surface,ArrowPath path,ExitMap exits,ExitRules rules) { this.surface=surface; this.path=path; this.exits=exits; edges=rules.EnableCubeEdgeExits; gates=rules.EnableDirectionalGates; }
            public bool Equals(Key other) => ReferenceEquals(surface,other.surface) && ReferenceEquals(path,other.path) && ReferenceEquals(exits,other.exits) && edges==other.edges && gates==other.gates;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => unchecked(((RuntimeHelpers.GetHashCode(path)*397)^RuntimeHelpers.GetHashCode(exits)) ^ RuntimeHelpers.GetHashCode(surface) ^ (edges?65536:0) ^ (gates?131072:0));
        }
    }
}
