using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Conversion orchestration only. Mesh reading belongs on Unity's main thread;
    /// the returned immutable topology is the ordinary Phase 4 generator input.</summary>
    public static class MeshSurfaceConverter
    {
        public const int ConversionVersion=1;
        public static ConvertedMeshSurface Convert(Mesh mesh,MeshConversionSettings settings=null,MeshQuadDefinition companion=null,
            CancellationToken cancellationToken=default) => Convert(mesh,Matrix4x4.identity,settings,companion,cancellationToken);
        public static ConvertedMeshSurface Convert(Mesh mesh,Matrix4x4 transform,MeshConversionSettings settings=null,MeshQuadDefinition companion=null,CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested(); settings=(settings??new MeshConversionSettings()).Copy(); settings.Validate();
            if(companion!=null && companion.sourceMesh!=null && companion.sourceMesh!=mesh) throw new MeshConversionException("QuadDefinition","The companion belongs to a different source mesh.");
            long memory=GC.GetTotalMemory(false); var watch=Stopwatch.StartNew(); var report=new MeshConversionReport();
            var input=MeshSourceReader.Read(mesh,transform,out bool nativeQuads);
            var welded=MeshSourceReader.Weld(input,settings,report,out var originalMap);
            MeshGeometryValidation.ValidateFaces(welded); MeshConnectivity.Build(welded,cancellationToken);
            MeshIntersectionValidation.Validate(welded,cancellationToken);
            report.Triangles=nativeQuads ? welded.Faces.Count*2 : welded.Faces.Count;
            var parents=MeshQuadExtraction.Extract(welded,nativeQuads,companion,originalMap,settings,cancellationToken);
            MeshGeometryValidation.Orient(parents,settings,report); var parentPairs=MeshConnectivity.Build(parents,cancellationToken);
            report.ParentQuads=parents.Faces.Count;
            var cells=MeshSubdivision.Refine(parents,parentPairs,settings,report,cancellationToken);
            var pairs=MeshConnectivity.Build(cells,cancellationToken,false);
            var builder=new SurfaceTopologyBuilder(); var quads=new Dictionary<int,SurfaceQuad>();
            for(int i=0;i<cells.Faces.Count;i++) { builder.AddCell(i); quads.Add(i,MeshGeometryValidation.Quad(cells,cells.Faces[i])); }
            foreach(var edge in pairs)
            {
                cancellationToken.ThrowIfCancellationRequested(); var from=quads[edge.A]; var to=quads[edge.B]; var direction=Direction(edge.EdgeA);
                var kind=Classify(from,to,direction,settings);
                bool fold=kind!=MeshFoldKind.Coplanar,exit=kind==MeshFoldKind.Convex && settings.EdgeExits==MeshEdgeExitPolicy.ConvexOnly;
                builder.Connect(edge.A,direction,edge.B,Direction(edge.EdgeB).Opposite(),fold,exit);
                if(kind==MeshFoldKind.Coplanar) report.CoplanarBoundaries++; else if(kind==MeshFoldKind.Convex) report.ConvexBoundaries++; else report.ConcaveBoundaries++;
            }
            var topology=builder.Build(); ColorRegions(topology,quads,cancellationToken); report.Cells=topology.CellCount;
            string digest=ComputeDigest(topology,quads,settings);
            var validation=Stopwatch.StartNew();
            var shape=new SurfaceShapeDefinition("arrowcube/mesh/"+digest,ConversionVersion,topology);
            var geometry=new QuadSurfaceGeometry(shape.Topology,quads);
            report.ValidationMilliseconds=validation.Elapsed.TotalMilliseconds; report.ConversionMilliseconds=watch.Elapsed.TotalMilliseconds;
            report.ManagedMemoryDelta=GC.GetTotalMemory(false)-memory;
            return new ConvertedMeshSurface(shape,geometry,report,digest,settings);
        }
        internal static string ComputeDigest(ISurfaceTopology topology,IReadOnlyDictionary<int,SurfaceQuad> quads,MeshConversionSettings settings)
        {
            return MeshIdentity.Hash(w=>
            {
                w.Write(ConversionVersion); settings.Write(w); w.Write(topology.CellCount);
                foreach(int c in topology.EnumerateCellIds())
                {
                    var q=quads[c]; for(int i=0;i<4;i++) MeshIdentity.Point(w,q.Corner(i)); w.Write(q.Color.r); w.Write(q.Color.g); w.Write(q.Color.b); w.Write(q.Color.a);
                    for(int d=0;d<4;d++) { var b=topology.GetBoundary(c,(SurfaceDirection)d); w.Write(b.NeighborCellId.Value); w.Write((int)b.NewDirection); w.Write(b.IsGeometricEdge); w.Write(b.AllowsEdgeExit); }
                }
            });
        }
        public static MeshFoldKind Classify(SurfaceQuad from,SurfaceQuad to,SurfaceDirection direction,MeshConversionSettings settings)
        {
            float dot=Mathf.Clamp(Vector3.Dot(from.Normal,to.Normal),-1,1); var cross=Vector3.Cross(from.Normal,to.Normal); float angle=Mathf.Atan2(cross.magnitude,dot)*Mathf.Rad2Deg;
            if(angle<=settings.CoplanarAngleDegrees) return MeshFoldKind.Coplanar;
            if(angle>settings.MaximumFoldDegrees) throw new MeshConversionException("SharpFold",$"Fold angle {angle:F3} exceeds {settings.MaximumFoldDegrees} degrees. Add a bridge/bevel patch for reliable arrow geometry.",from.BoundaryCenter(direction));
            from.Boundary(direction,out var a,out var b);
            float sign=Vector3.Dot((b-a).normalized,Vector3.Cross(from.Normal,to.Normal));
            if(Mathf.Abs(sign)<1e-7f) throw new MeshConversionException("AmbiguousFold","The oriented fold sign is indeterminate.",a,b);
            return sign>0 ? MeshFoldKind.Convex : MeshFoldKind.Concave;
        }
        private static SurfaceDirection Direction(int edge) => (SurfaceDirection)((edge+3)%4);
        private static void ColorRegions(ISurfaceTopology surface,Dictionary<int,SurfaceQuad> quads,CancellationToken token)
        {
            var region=new Dictionary<int,int>(); var queue=new Queue<int>(); int count=0;
            foreach(int c in surface.EnumerateCellIds())
            {
                if(region.ContainsKey(c)) continue; region.Add(c,count); queue.Enqueue(c);
                while(queue.Count>0) { int id=queue.Dequeue(); for(int d=0;d<4;d++) { var b=surface.GetBoundary(id,(SurfaceDirection)d); int n=b.NeighborCellId.Value; if(!b.IsGeometricEdge && !region.ContainsKey(n)) { region.Add(n,count); queue.Enqueue(n); } } }
                count++;
            }
            var palette=new[] {new Color(.73f,.85f,.98f),new Color(.98f,.88f,.63f),new Color(.67f,.85f,.73f),new Color(.84f,.73f,.96f),new Color(.97f,.73f,.79f),new Color(.68f,.9f,.93f),new Color(.94f,.8f,.66f),new Color(.81f,.89f,.64f)};
            var neighbors=new HashSet<int>[count]; for(int r=0;r<count;r++) neighbors[r]=new HashSet<int>();
            foreach(int c in surface.EnumerateCellIds()) { token.ThrowIfCancellationRequested(); int r=region[c]; for(int d=0;d<4;d++) { int n=region[surface.Step(c,(SurfaceDirection)d).CellId]; if(n!=r) neighbors[r].Add(n); } }
            var colors=new int[count]; for(int i=0;i<count;i++) colors[i]=-1;
            for(int r=0;r<count;r++)
            {
                var used=new bool[palette.Length]; foreach(int n in neighbors[r]) if(colors[n]>=0) used[colors[n]]=true;
                int choice=0; while(choice<palette.Length-1 && used[choice]) choice++; colors[r]=choice;
            }
            foreach(int c in surface.EnumerateCellIds()) { var q=quads[c]; quads[c]=new SurfaceQuad(q.Corner(0),q.Corner(1),q.Corner(2),q.Corner(3),palette[colors[region[c]]]); }
        }
    }
}
