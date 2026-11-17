using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ArrowCube.Presentation
{
    internal static class MeshQuadExtraction
    {
        public static MeshSurfaceData Extract(MeshSurfaceData data,bool nativeQuads,MeshQuadDefinition companion,int[] originalMap,MeshConversionSettings settings,CancellationToken token)
        {
            if(nativeQuads)
            {
                if(companion!=null) throw new MeshConversionException("QuadDefinition","Native quad input already supplies face grouping; remove its triangle companion.");
                MeshIdentity.Sort(data.Faces); return data;
            }
            MeshIdentity.Sort(data.Faces);
            if(companion!=null) return Explicit(data,companion,originalMap);
            var pairs=MeshConnectivity.Build(data,token); var choices=new List<int>[data.Faces.Count]; var quads=new Dictionary<MeshEdge,MeshPolygon>();
            for(int i=0;i<choices.Length;i++) choices[i]=new List<int>();
            foreach(var edge in pairs)
                if(TryPair(data,edge.A,edge.B,out var quad)) { choices[edge.A].Add(edge.B); choices[edge.B].Add(edge.A); quads.Add(new MeshEdge(edge.A,edge.B),quad); }
            foreach(var options in choices) options.Sort();
            var selected=new List<MeshPolygon>(); var visited=new bool[choices.Length]; int work=0;
            for(int start=0;start<choices.Length;start++)
            {
                if(visited[start]) continue; var component=new List<int>(); var queue=new Queue<int>(); queue.Enqueue(start); visited[start]=true;
                while(queue.Count>0) { int id=queue.Dequeue(); component.Add(id); foreach(int n in choices[id]) if(!visited[n]) { visited[n]=true; queue.Enqueue(n); } }
                component.Sort();
                if(component.Count%2!=0 || choices[start].Count==0) throw new MeshConversionException("UnpairedTriangle","Triangles cannot be completely paired into planar convex quads. Supply native quads, a companion grouping, or preprocess the model.",data.Positions[data.Faces[start].Vertices[0]]);
                if(component.Count>512) throw new MeshConversionException("PairingBudget","A triangle pairing component exceeds 512 triangles. Supply an explicit quad definition.");
                int solutions=0; var matched=new bool[choices.Length]; var current=new List<MeshEdge>(); List<MeshEdge> solution=null;
                void Search()
                {
                    token.ThrowIfCancellationRequested();
                    if(++work>settings.PairingSearchBudget) throw new MeshConversionException("PairingBudget","Triangle pairing uniqueness could not be proven within the search budget. Supply explicit quad grouping.");
                    int next=-1,degree=int.MaxValue;
                    foreach(int id in component) if(!matched[id])
                    {
                        int count=0; foreach(int n in choices[id]) if(!matched[n]) count++;
                        if(count==0) return;
                        if(count<degree) { degree=count; next=id; }
                    }
                    if(next<0) { solutions++; if(solution==null) solution=new List<MeshEdge>(current); return; }
                    matched[next]=true;
                    foreach(int n in choices[next])
                    {
                        if(matched[n]) continue; matched[n]=true; current.Add(new MeshEdge(next,n)); Search(); current.RemoveAt(current.Count-1); matched[n]=false;
                        if(solutions>1) break;
                    }
                    matched[next]=false;
                }
                Search();
                if(solutions==0) throw new MeshConversionException("UnpairedTriangle","No complete quad pairing exists. Supply explicit quad grouping or preprocess the mesh.");
                if(solutions>1) throw new MeshConversionException("AmbiguousPairing","Multiple complete triangle-to-quad pairings are valid. The converter will not guess; supply native quads or a Mesh Quad Definition.");
                foreach(var edge in solution) selected.Add(quads[edge]);
            }
            MeshIdentity.Sort(selected); return new MeshSurfaceData(data.Positions,selected);
        }
        private static MeshSurfaceData Explicit(MeshSurfaceData data,MeshQuadDefinition companion,int[] map)
        {
            var indices=companion.quadVertices;
            if(indices==null || indices.Length==0 || indices.Length%4!=0 || indices.Length/4*2!=data.Faces.Count) throw new MeshConversionException("QuadDefinition","A companion needs four indices per quad and must cover every source triangle exactly once.");
            var used=new HashSet<int>(); var result=new List<MeshPolygon>();
            for(int i=0;i<indices.Length;i+=4)
            {
                var vertices=new int[4];
                for(int j=0;j<4;j++) { int id=indices[i+j]; if(id<0 || id>=map.Length) throw new MeshConversionException("QuadDefinition","A companion references an invalid source vertex index."); vertices[j]=map[id]; }
                var target=new MeshPolygon(vertices); MeshIdentity.Canonicalize(target); MeshGeometryValidation.Quad(data,target);
                var candidates=new List<int>(); var set=new HashSet<int>(vertices);
                for(int t=0;t<data.Faces.Count;t++) if(!used.Contains(t)) { bool inside=true; foreach(int v in data.Faces[t].Vertices) if(!set.Contains(v)) inside=false; if(inside) candidates.Add(t); }
                if(candidates.Count!=2 || !TryPair(data,candidates[0],candidates[1],out var paired)) throw new MeshConversionException("QuadDefinition","A companion quad must match exactly two compatible source triangles.");
                MeshIdentity.Canonicalize(paired);
                for(int j=0;j<4;j++) if(paired.Vertices[j]!=target.Vertices[j]) throw new MeshConversionException("QuadDefinition","Companion winding or boundary ordering disagrees with the source triangles.");
                used.Add(candidates[0]); used.Add(candidates[1]); result.Add(paired);
            }
            MeshIdentity.Sort(result); return new MeshSurfaceData(data.Positions,result);
        }
        private static bool TryPair(MeshSurfaceData data,int a,int b,out MeshPolygon quad)
        {
            quad=null; var first=data.Faces[a]; var second=data.Faces[b]; if(first.Submesh!=second.Submesh) return false;
            var edges=new Dictionary<MeshEdge,(int from,int to)>(); var counts=new Dictionary<MeshEdge,int>();
            foreach(var p in new[] {first,second}) for(int i=0;i<3;i++)
            { int from=p.Vertices[i],to=p.Vertices[(i+1)%3]; var key=new MeshEdge(from,to); counts.TryGetValue(key,out int n); counts[key]=n+1; edges[key]=(from,to); }
            var next=new Dictionary<int,int>();
            foreach(var pair in counts) if(pair.Value==1) { var edge=edges[pair.Key]; if(next.ContainsKey(edge.from)) return false; next.Add(edge.from,edge.to); }
            if(next.Count!=4) return false;
            int start=int.MaxValue; foreach(int v in next.Keys) start=Math.Min(start,v);
            var cycle=new int[4]; int current=start; var seen=new HashSet<int>();
            for(int i=0;i<4;i++) { if(!seen.Add(current) || !next.TryGetValue(current,out int n)) return false; cycle[i]=current; current=n; }
            if(current!=start) return false;
            var candidate=new MeshPolygon(cycle,first.Submesh);
            try { MeshGeometryValidation.Quad(data,candidate); } catch(MeshConversionException) { return false; }
            // A quad's two source triangles must face the same way, not form a folded flap.
            var p0=data.Positions[first.Vertices[0]]; var n0=Vector3.Cross(data.Positions[first.Vertices[1]]-p0,data.Positions[first.Vertices[2]]-p0).normalized;
            var p1=data.Positions[second.Vertices[0]]; var n1=Vector3.Cross(data.Positions[second.Vertices[1]]-p1,data.Positions[second.Vertices[2]]-p1).normalized;
            if(Vector3.Dot(n0,n1)<.999999f) return false;
            quad=candidate; return true;
        }
    }
}
