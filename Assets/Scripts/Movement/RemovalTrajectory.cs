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

    /// <summary>Exact compressed union of every segment's occupancy, including initial bends.
    /// A rail entry k occupies the moving body's window for steps k-length+1 through k.</summary>
    public sealed class RemovalTrajectory
    {
        private readonly MovementPlan plan;
        private readonly HashSet<SurfaceCoordinate> swept=new HashSet<SurfaceCoordinate>();
        public int PathId => plan.Path.Id;
        public bool HasValidExit => plan.Succeeded;
        public ExitType? ExitType => plan.ExitType;
        public MovementOutcome Outcome => plan.Outcome;
        public int StepCount => plan.StepCount;
        public IReadOnlyList<PathPosition> Rail => plan.Rail;
        public IReadOnlyCollection<SurfaceCoordinate> SweptCells { get; }
        public IReadOnlyList<TrajectoryVisit> Visits { get; }
        public IReadOnlyCollection<SurfaceSegment> SweptSegments { get; }
        public PathPosition GetBodyPosition(int step,int segment) => plan.GetPosition(step,segment);
        public bool Contains(SurfaceCoordinate cell) => swept.Contains(cell);
        public static RemovalTrajectory Calculate(CubeTopology topology,ArrowPath path,ExitMap exits,ExitRules rules,
            CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            return new RemovalTrajectory(new PathMovementSimulator().Simulate(new SurfaceGrid(topology,new[] { path }),path.Id,exits,rules),topology,token);
        }
        private RemovalTrajectory(MovementPlan plan,CubeTopology topology,CancellationToken token)
        {
            this.plan=plan; int length=plan.Path.Cells.Count;
            var visits=new List<TrajectoryVisit>(); var cells=new List<SurfaceCoordinate>();
            var segments=new HashSet<SurfaceSegment>(plan.Path.Segments);
            for(int k=0;k<plan.Rail.Count;k++)
            {
                if((k&63)==0) token.ThrowIfCancellationRequested();
                var position=plan.Rail[k]; if(!position.IsOnSurface) continue;
                if(swept.Add(position.Cell)) cells.Add(position.Cell);
                visits.Add(new TrajectoryVisit(position.Cell,Math.Max(0,k-length+1),Math.Min(plan.StepCount,k)));
                if(k>=length && plan.Rail[k-1].IsOnSurface) segments.Add(new SurfaceSegment(topology,plan.Rail[k-1].Cell,position.Cell));
            }
            SweptCells=cells.AsReadOnly(); Visits=visits.AsReadOnly(); SweptSegments=new List<SurfaceSegment>(segments).AsReadOnly();
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
        public RemovalTrajectory Get(CubeTopology topology,ArrowPath path,ExitMap exits,ExitRules rules,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested(); rules=rules??ExitRules.Hybrid; var key=new Key(topology.GridSize,path,exits,rules);
            if(entries.TryGetValue(key,out var value)) { Hits++; return value; }
            value=RemovalTrajectory.Calculate(topology,path,exits,rules,token); Misses++;
            if(entries.Count>=capacity) entries.Remove(fifo.Dequeue());
            entries.Add(key,value); fifo.Enqueue(key); return value;
        }
        private readonly struct Key : IEquatable<Key>
        {
            private readonly int size; private readonly ArrowPath path; private readonly ExitMap exits; private readonly bool edges,gates;
            public Key(int size,ArrowPath path,ExitMap exits,ExitRules rules) { this.size=size; this.path=path; this.exits=exits; edges=rules.EnableCubeEdgeExits; gates=rules.EnableDirectionalGates; }
            public bool Equals(Key other) => size==other.size && ReferenceEquals(path,other.path) && ReferenceEquals(exits,other.exits) && edges==other.edges && gates==other.gates;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => unchecked(((RuntimeHelpers.GetHashCode(path)*397)^RuntimeHelpers.GetHashCode(exits)) ^ size ^ (edges?65536:0) ^ (gates?131072:0));
        }
    }
}
