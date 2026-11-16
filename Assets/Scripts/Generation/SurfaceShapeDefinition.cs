using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Immutable, self-contained authored topology identity. Safe to use on a worker.
    /// Geometry is deliberately a separate registered asset, identified by ID and version.</summary>
    public sealed class SurfaceShapeDefinition
    {
        public string Id { get; }
        public int Version { get; }
        public ISurfaceTopology Topology { get; }
        public string Digest { get; }
        public SurfaceShapeDefinition(string id,int version,ISurfaceTopology topology)
        {
            if(string.IsNullOrWhiteSpace(id) || id.Length>128 || version<1 || topology==null || topology.CellCount<2 || topology.CellCount>65536)
                throw new ArgumentException("A shape needs a stable ID, positive version and 2..65536 cells.");
            Id=id; Version=version;
            var ids=new List<int>(topology.EnumerateCellIds()); ids.Sort(); var builder=new SurfaceTopologyBuilder();
            foreach(int cell in ids) builder.AddCell(cell);
            foreach(int cell in ids) for(int d=0;d<4;d++) builder.SetBoundary(cell,(SurfaceDirection)d,topology.GetBoundary(cell,(SurfaceDirection)d));
            Topology=builder.Build();
            var seen=new HashSet<int>(); var queue=new Queue<int>(); queue.Enqueue(ids[0]); seen.Add(ids[0]);
            while(queue.Count>0) { int c=queue.Dequeue(); for(int d=0;d<4;d++) { int n=Topology.Step(c,(SurfaceDirection)d).CellId; if(seen.Add(n)) queue.Enqueue(n); } }
            if(seen.Count!=Topology.CellCount) throw new ArgumentException("Generation requires one connected closed surface.");
            using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            { Write(writer); writer.Flush(); using(var sha=SHA256.Create()) Digest=BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant(); }
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(Id); writer.Write(Version); writer.Write(Topology.CellCount);
            foreach(int cell in Topology.EnumerateCellIds())
            {
                writer.Write(cell);
                for(int d=0;d<4;d++) { var b=Topology.GetBoundary(cell,(SurfaceDirection)d); writer.Write(b.NeighborCellId.Value); writer.Write((int)b.NewDirection); writer.Write(b.IsGeometricEdge); writer.Write(b.AllowsEdgeExit); }
            }
        }
        public static SurfaceShapeDefinition Read(BinaryReader reader)
        {
            string id=reader.ReadString(); int version=reader.ReadInt32(),count=reader.ReadInt32();
            if(count<2 || count>65536) throw new ArgumentException("Invalid shape snapshot size.");
            var builder=new SurfaceTopologyBuilder(); var cells=new int[count]; var boundaries=new CellBoundary[count,4];
            for(int i=0;i<count;i++)
            { cells[i]=reader.ReadInt32(); builder.AddCell(cells[i]); for(int d=0;d<4;d++) boundaries[i,d]=new CellBoundary(reader.ReadInt32(),(SurfaceDirection)reader.ReadInt32(),reader.ReadBoolean(),reader.ReadBoolean()); }
            for(int i=0;i<count;i++) for(int d=0;d<4;d++) builder.SetBoundary(cells[i],(SurfaceDirection)d,boundaries[i,d]);
            return new SurfaceShapeDefinition(id,version,builder.Build());
        }
    }
}
