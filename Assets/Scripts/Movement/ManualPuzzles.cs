using System;
using System.Collections.Generic;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    public sealed class ManualPuzzle : PuzzleDefinition
    {
        internal ManualPuzzle(string name, CubeTopology topology, ArrowPath[] paths, params ExitGate[] gates)
            : this(name, topology, paths, ExitRules.Hybrid, gates) { }
        internal ManualPuzzle(string name, CubeTopology topology, ArrowPath[] paths, ExitRules rules, params ExitGate[] gates)
            : base(name, topology, paths, new ExitMap(topology, gates), rules) { }
    }

    /// <summary>Hand-authored fixtures, deliberately independent of procedural generation.</summary>
    public static class ManualPuzzles
    {
        public const int Count = 7;
        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
        {
            "Straight departure", "Bent snake across three faces", "Remove blocker 20, then snake 10",
            "Rotated seam: Right to Top", "Closed circuit: no exit",
            "Diagram: ordinary edge exits", "Hybrid: edge exits + blue gate"
        });
        public static ManualPuzzle Create(int index)
        {
            if (index == 5 || index == 6) return CreateDiagram(index == 6);
            var t = new CubeTopology(6);
            var straight = new ArrowPath(10, t, new[] { C(CubeFace.Front, 2, 2), C(CubeFace.Front, 1, 2), C(CubeFace.Front, 0, 2) }, SurfaceDirection.Right);
            var bent = new ArrowPath(10, t, new[]
            {
                C(CubeFace.Right,1,2), C(CubeFace.Right,0,2), C(CubeFace.Front,5,2),
                C(CubeFace.Front,4,2), C(CubeFace.Front,4,1), C(CubeFace.Front,3,1), C(CubeFace.Front,3,0)
            }, SurfaceDirection.Right);
            var blocker = new ArrowPath(20, t, new[] { C(CubeFace.Back,0,2), C(CubeFace.Back,0,1) }, SurfaceDirection.Up);
            switch (index)
            {
                case 0: return new ManualPuzzle("Straight departure", t, new[] { straight }, ExitRules.GateOnly, G(CubeFace.Front,5,2,SurfaceDirection.Right));
                case 1: return new ManualPuzzle("Bent snake across three faces", t, new[] { bent }, ExitRules.GateOnly, G(CubeFace.Back,2,2,SurfaceDirection.Right));
                case 2: return new ManualPuzzle("Remove blocker 20, then snake 10", t, new[] { bent, blocker }, ExitRules.GateOnly,
                    G(CubeFace.Back,2,2,SurfaceDirection.Right), G(CubeFace.Back,0,4,SurfaceDirection.Up));
                case 3:
                    var rotated = new ArrowPath(10, t, new[]
                    {
                        C(CubeFace.Right,2,4), C(CubeFace.Right,2,3), C(CubeFace.Right,1,3),
                        C(CubeFace.Right,0,3), C(CubeFace.Front,5,3), C(CubeFace.Front,5,2)
                    }, SurfaceDirection.Up);
                    var topBlocker = new ArrowPath(20, t, new[] { C(CubeFace.Top,4,2), C(CubeFace.Top,4,1) }, SurfaceDirection.Up);
                    return new ManualPuzzle("Rotated seam: Right to Top", t, new[] { rotated, topBlocker }, ExitRules.GateOnly,
                        G(CubeFace.Top,1,2,SurfaceDirection.Left), G(CubeFace.Top,4,4,SurfaceDirection.Up));
                case 4: return new ManualPuzzle("Closed circuit: no exit", t, new[] { straight }, ExitRules.GateOnly);
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static ManualPuzzle CreateDiagram(bool hybrid)
        {
            // Diagram faces 1/2/3 are Front/Top/Right. IDs 10/20/30/40 are Red/Green/Orange/Blue.
            var t = new CubeTopology(8);
            var red = new List<SurfaceCoordinate>();
            for (int y = 6; y >= 0; y--) red.Add(C(CubeFace.Front,3,y));
            var green = new[]
            {
                C(CubeFace.Front,4,3), C(CubeFace.Front,5,3), C(CubeFace.Front,6,3), C(CubeFace.Front,7,3),
                C(CubeFace.Right,0,3), C(CubeFace.Right,1,3), C(CubeFace.Right,1,4), C(CubeFace.Right,1,5)
            };
            var orange = new List<SurfaceCoordinate>();
            for (int x = 2; x <= 6; x++) orange.Add(C(CubeFace.Right,x,4));
            var blue = new[] { C(CubeFace.Top,6,1), C(CubeFace.Top,7,1), C(CubeFace.Right,1,7), C(CubeFace.Right,1,6) };
            var paths = new[]
            {
                new ArrowPath(10,t,red,SurfaceDirection.Up), new ArrowPath(20,t,green,SurfaceDirection.Left),
                new ArrowPath(30,t,orange,SurfaceDirection.Left), new ArrowPath(40,t,blue,SurfaceDirection.Left)
            };
            return new ManualPuzzle(Names[hybrid ? 6 : 5],t,paths,hybrid ? ExitRules.Hybrid : ExitRules.EdgeOnly,
                hybrid ? new[] { G(CubeFace.Top,3,1,SurfaceDirection.Left) } : Array.Empty<ExitGate>());
        }
        private static SurfaceCoordinate C(CubeFace face, int x, int y) => new SurfaceCoordinate(face,x,y);
        private static ExitGate G(CubeFace face, int x, int y, SurfaceDirection direction) => new ExitGate(C(face,x,y), direction);
    }
}
