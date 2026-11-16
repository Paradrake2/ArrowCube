using System;
using System.Collections.Generic;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    /// <summary>Immutable authoring data shared by manual and generated puzzles.</summary>
    public class PuzzleDefinition
    {
        public string Name { get; }
        public CubeTopology Topology { get; }
        public IReadOnlyList<ArrowPath> Paths { get; }
        public ExitMap Exits { get; }
        public ExitRules Rules { get; }

        public PuzzleDefinition(string name, CubeTopology topology, IEnumerable<ArrowPath> paths,
            ExitMap exits, ExitRules rules = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            Exits = exits ?? throw new ArgumentNullException(nameof(exits));
            if (exits.GridSize != topology.GridSize) throw new ArgumentException("Exit map has a different grid size.", nameof(exits));
            Paths = new List<ArrowPath>(paths).AsReadOnly();
            Rules = rules ?? ExitRules.Hybrid;
        }

        public SurfaceGrid CreateGrid() => new SurfaceGrid(Topology, Paths);
    }
}
