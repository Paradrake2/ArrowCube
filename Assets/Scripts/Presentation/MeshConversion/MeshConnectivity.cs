using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ArrowCube.Presentation
{
    internal readonly struct MeshBoundaryPair
    {
        public readonly int A,EdgeA,B,EdgeB;
        public MeshBoundaryPair(int a,int edgeA,int b,int edgeB) { A=a; EdgeA=edgeA; B=b; EdgeB=edgeB; }
    }
    /// <summary>Exact shared endpoint connectivity after welding. Also rejects open edges,
    /// inconsistent winding, disconnected shells and pinched/non-manifold vertices.</summary>
    internal static class MeshConnectivity
    {
        public static List<MeshBoundaryPair> Build(MeshSurfaceData data,CancellationToken token,bool validateVertexFans=true)
        {
            var edges=new SortedDictionary<MeshEdge,List<(int face,int edge,int from,int to)>>();
            var duplicate=new HashSet<string>(); var incident=new Dictionary<int,HashSet<int>>();
            for(int f=0;f<data.Faces.Count;f++)
            {
                token.ThrowIfCancellationRequested(); var p=data.Faces[f]; var unique=new HashSet<int>(p.Vertices);
                if(unique.Count!=p.Vertices.Length) throw new MeshConversionException("Degenerate","A face repeats welded vertices.",data.Positions[p.Vertices[0]]);
                var key=new int[p.Vertices.Length]; p.Vertices.CopyTo(key,0); Array.Sort(key);
                if(!duplicate.Add(string.Join(",",key))) throw new MeshConversionException("DuplicateFace","Coincident or duplicate faces are unsupported.",data.Positions[p.Vertices[0]]);
                foreach(int v in p.Vertices) { if(!incident.TryGetValue(v,out var faces)) incident.Add(v,faces=new HashSet<int>()); faces.Add(f); }
                for(int e=0;e<p.Vertices.Length;e++)
                {
                    int a=p.Vertices[e],b=p.Vertices[(e+1)%p.Vertices.Length]; var edge=new MeshEdge(a,b);
                    if(!edges.TryGetValue(edge,out var list)) edges.Add(edge,list=new List<(int,int,int,int)>()); list.Add((f,e,a,b));
                }
            }
            var result=new List<MeshBoundaryPair>(); var neighbors=new List<int>[data.Faces.Count]; for(int i=0;i<neighbors.Length;i++) neighbors[i]=new List<int>();
            foreach(var pair in edges)
            {
                var list=pair.Value; var a=data.Positions[pair.Key.A]; var b=data.Positions[pair.Key.B];
                if(list.Count!=2) throw new MeshConversionException(list.Count<2 ? "OpenBoundary" : "NonManifoldEdge",$"Edge has {list.Count} incident faces; exactly two are required. Repair holes, overlaps or T-junctions before conversion.",a,b);
                var x=list[0]; var y=list[1];
                if(x.from!=y.to || x.to!=y.from) throw new MeshConversionException("Winding","Adjacent faces traverse a shared edge in the same direction. Recalculate consistent outward winding.",a,b);
                result.Add(new MeshBoundaryPair(x.face,x.edge,y.face,y.edge)); neighbors[x.face].Add(y.face); neighbors[y.face].Add(x.face);
            }
            var seen=new HashSet<int>(); var pending=new Queue<int>(); seen.Add(0); pending.Enqueue(0);
            while(pending.Count>0) { int f=pending.Dequeue(); foreach(int n in neighbors[f]) if(seen.Add(n)) pending.Enqueue(n); }
            if(seen.Count!=data.Faces.Count) throw new MeshConversionException("Disconnected","Use a single connected shell. Separate closed components cannot share a puzzle surface.");
            if(validateVertexFans) foreach(var vertex in incident)
            {
                token.ThrowIfCancellationRequested(); seen.Clear(); int first=-1; foreach(int f in vertex.Value) { first=f; break; } seen.Add(first); pending.Enqueue(first);
                while(pending.Count>0) { int f=pending.Dequeue(); foreach(int n in neighbors[f]) if(vertex.Value.Contains(n) && seen.Add(n)) pending.Enqueue(n); }
                if(seen.Count!=vertex.Value.Count) throw new MeshConversionException("NonManifoldVertex","A pinched vertex has disconnected incident face fans.",data.Positions[vertex.Key]);
            }
            return result;
        }
    }
    internal static class MeshGeometryValidation
    {
        public static void ValidateFaces(MeshSurfaceData data)
        {
            foreach(var f in data.Faces)
            {
                if(f.Vertices.Length==4) Quad(data,f);
                else
                {
                    var a=data.Positions[f.Vertices[0]]; var b=data.Positions[f.Vertices[1]]; var c=data.Positions[f.Vertices[2]];
                    float length=Mathf.Max((a-b).magnitude,(b-c).magnitude,(c-a).magnitude);
                    if(length<=1e-12f || Vector3.Cross(b-a,c-a).magnitude<=length*length*1e-7f) throw new MeshConversionException("Degenerate","A triangle is collinear or has zero area.",a,b,c);
                }
            }
        }
        public static SurfaceQuad Quad(MeshSurfaceData data,MeshPolygon f)
        {
            try { return new SurfaceQuad(data.Positions[f.Vertices[0]],data.Positions[f.Vertices[1]],data.Positions[f.Vertices[2]],data.Positions[f.Vertices[3]],Color.white); }
            catch(ArgumentException error) { throw new MeshConversionException("InvalidQuad","A quad must be planar, convex and nondegenerate: "+error.Message,data.Positions[f.Vertices[0]]); }
        }
        public static void Orient(MeshSurfaceData data,MeshConversionSettings settings,MeshConversionReport report)
        {
            double volume=0; var origin=data.Positions[data.Faces[0].Vertices[0]];
            foreach(var f in data.Faces) for(int i=1;i+1<f.Vertices.Length;i++)
            {
                var a=data.Positions[f.Vertices[0]]-origin; var b=data.Positions[f.Vertices[i]]-origin; var c=data.Positions[f.Vertices[i+1]]-origin;
                volume+=((double)a.x*((double)b.y*c.z-(double)b.z*c.y)+(double)a.y*((double)b.z*c.x-(double)b.x*c.z)+(double)a.z*((double)b.x*c.y-(double)b.y*c.x))/6;
            }
            if(Math.Abs(volume)<1e-15) throw new MeshConversionException("Volume","The closed shell has zero or indeterminate enclosed volume.");
            if(volume<0)
            {
                if(!settings.RepairUniformInwardWinding) throw new MeshConversionException("InwardWinding","The shell is consistently inward-facing. Recalculate outside normals, or explicitly enable uniform inward-winding repair.");
                foreach(var f in data.Faces) Array.Reverse(f.Vertices); report.Warnings.Add("Reversed a uniformly inward-facing shell. Mixed winding is never repaired automatically.");
            }
            MeshIdentity.Sort(data.Faces);
        }
    }
}
