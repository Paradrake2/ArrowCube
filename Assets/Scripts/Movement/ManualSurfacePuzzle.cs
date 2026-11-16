using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    /// <summary>Fixed non-cube logical authoring fixture: 16 closed cells with rotated local
    /// frames and concave/convex folds. No mesh or procedural shape construction.</summary>
    public static class ManualSurfacePuzzle
    {
        public static System.Collections.Generic.IReadOnlyList<int> RemovalOrder { get; }=System.Array.AsReadOnly(new[] { 30,20,10 });

        public static PuzzleDefinition Create(ExitRules rules=null)
        {
            // Authored records: ID, neighbor to the right, neighbor above, local frame rotation.
            // The periodic 4-by-4 connectivity has toroidal topology, not cube topology.
            int[,] records={
                {101,115,202,0},{115,129,216,1},{129,143,230,2},{143,101,244,3},
                {202,216,303,0},{216,230,317,0},{230,244,331,0},{244,202,345,0},
                {303,317,404,0},{317,331,418,0},{331,345,432,0},{345,303,446,0},
                {404,418,101,0},{418,432,115,0},{432,446,129,0},{446,404,143,0}
            };
            var builder=new SurfaceTopologyBuilder();
            var rotations=new System.Collections.Generic.Dictionary<int,int>();
            for(int i=0;i<16;i++) { builder.AddCell(records[i,0]); rotations.Add(records[i,0],records[i,3]); }
            for(int i=0;i<16;i++)
            {
                int id=records[i,0],right=records[i,1],up=records[i,2];
                builder.Connect(id,Rotate(SurfaceDirection.Right,rotations[id]),right,Rotate(SurfaceDirection.Right,rotations[right]));
                builder.Connect(id,Rotate(SurfaceDirection.Up,rotations[id]),up,Rotate(SurfaceDirection.Up,rotations[up]));
            }
            builder.Connect(101,SurfaceDirection.Right,115,SurfaceDirection.Up,true,false); // Inside fold.
            builder.Connect(115,SurfaceDirection.Up,129,SurfaceDirection.Left,true,true); // Outside fold.
            builder.Connect(115,SurfaceDirection.Left,216,SurfaceDirection.Up,true,false);
            builder.Connect(418,SurfaceDirection.Up,115,SurfaceDirection.Left,true,true);
            var surface=builder.Build();
            var paths=new[] {
                new ArrowPath(10,surface,new[] {101,143},SurfaceDirection.Right),
                new ArrowPath(20,surface,new[] {216,115},SurfaceDirection.Up),
                new ArrowPath(30,surface,new[] {418,317},SurfaceDirection.Up)
            };
            var gates=new[] { new CellGate(129,SurfaceDirection.Left),new CellGate(317,SurfaceDirection.Up),new CellGate(418,SurfaceDirection.Up) };
            return new PuzzleDefinition("Folded loop: remove 30, then 20, then 10",surface,paths,new ExitMap(surface,gates),rules??ExitRules.GateOnly);
        }
        private static SurfaceDirection Rotate(SurfaceDirection direction,int amount) => (SurfaceDirection)(((int)direction+amount)%4);
    }
}
