using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ArrowCube.Presentation
{
    internal static class MeshSubdivision
    {
        public static MeshSurfaceData Refine(MeshSurfaceData parents,List<MeshBoundaryPair> pairs,MeshConversionSettings settings,MeshConversionReport report,CancellationToken token)
        {
            int Count(float length) { double ratio=length/(double)settings.TargetCellSize; if(ratio>64) throw new MeshConversionException("SubdivisionLimit","Target cell size requires more than 64 segments on a patch edge."); return Math.Max(1,(int)Math.Ceiling(ratio)); }
            int count=parents.Faces.Count; var desired=new int[count*2]; var union=new int[count*2];
            for(int i=0;i<count;i++)
            {
                var q=MeshGeometryValidation.Quad(parents,parents.Faces[i]);
                float u=Mathf.Max(Vector3.Distance(q.Corner(0),q.Corner(1)),Vector3.Distance(q.Corner(2),q.Corner(3)));
                float v=Mathf.Max(Vector3.Distance(q.Corner(1),q.Corner(2)),Vector3.Distance(q.Corner(3),q.Corner(0)));
                desired[i*2]=settings.TargetCellSize>0 ? Count(u) : settings.Subdivisions;
                desired[i*2+1]=settings.TargetCellSize>0 ? Count(v) : settings.Subdivisions;
            }
            foreach(var p in settings.PatchSubdivisions)
            { if(p.Patch>=count) throw new MeshConversionException("Subdivision","A subdivision override references a nonexistent canonical patch ID."); desired[p.Patch*2]=p.Columns; desired[p.Patch*2+1]=p.Rows; }
            for(int i=0;i<union.Length;i++) union[i]=i;
            int Root(int i) { while(union[i]!=i) { union[i]=union[union[i]]; i=union[i]; } return i; }
            foreach(var edge in pairs) { int a=Root(edge.A*2+(edge.EdgeA%2)),b=Root(edge.B*2+(edge.EdgeB%2)); union[b]=a; }
            var maxima=new int[union.Length]; var minima=new int[union.Length]; for(int i=0;i<minima.Length;i++) minima[i]=int.MaxValue;
            for(int i=0;i<union.Length;i++) { int root=Root(i); maxima[root]=Math.Max(maxima[root],desired[i]); minima[root]=Math.Min(minima[root],desired[i]); }
            long cells=0;
            for(int i=0;i<desired.Length;i++)
            {
                int root=Root(i);
                if(!settings.PropagateSubdivisions && maxima[root]!=minima[root]) throw new MeshConversionException("SubdivisionMismatch","Neighboring patches require different shared-edge segment counts. Enable propagation or supply compatible overrides.");
                if(desired[i]!=maxima[root]) report.PropagatedAxes++;
                desired[i]=maxima[root]; if(desired[i]>64) throw new MeshConversionException("SubdivisionLimit","A patch axis exceeds 64 cells. Increase target cell size or simplify the input.");
            }
            for(int i=0;i<count;i++) cells+=(long)desired[i*2]*desired[i*2+1];
            if(cells>settings.MaximumCells || cells<2) throw new MeshConversionException("CellLimit",$"Requested {cells} cells exceeds the configured maximum {settings.MaximumCells} (or is below two).");
            var positions=new List<Vector3>(parents.Positions); var faces=new List<MeshPolygon>((int)cells); var edgeVertices=new Dictionary<(int,int,int,int),int>();
            for(int patch=0;patch<count;patch++)
            {
                token.ThrowIfCancellationRequested(); var source=parents.Faces[patch]; int u=desired[patch*2],v=desired[patch*2+1]; var q=MeshGeometryValidation.Quad(parents,source); var grid=new int[u+1,v+1];
                int EdgePoint(int from,int to,int index,int segments)
                {
                    if(index==0) return from; if(index==segments) return to;
                    if(from>to) { int old=from; from=to; to=old; index=segments-index; }
                    var key=(from,to,index,segments); if(edgeVertices.TryGetValue(key,out int existing)) return existing;
                    int id=positions.Count; positions.Add(Vector3.Lerp(parents.Positions[from],parents.Positions[to],index/(float)segments)); edgeVertices.Add(key,id); return id;
                }
                for(int y=0;y<=v;y++) for(int x=0;x<=u;x++)
                {
                    int id;
                    if(y==0) id=EdgePoint(source.Vertices[0],source.Vertices[1],x,u);
                    else if(y==v) id=EdgePoint(source.Vertices[3],source.Vertices[2],x,u);
                    else if(x==0) id=EdgePoint(source.Vertices[0],source.Vertices[3],y,v);
                    else if(x==u) id=EdgePoint(source.Vertices[1],source.Vertices[2],y,v);
                    else { id=positions.Count; positions.Add(Vector3.Lerp(Vector3.Lerp(q.Corner(0),q.Corner(1),x/(float)u),Vector3.Lerp(q.Corner(3),q.Corner(2),x/(float)u),y/(float)v)); }
                    grid[x,y]=id;
                }
                for(int y=0;y<v;y++) for(int x=0;x<u;x++)
                {
                    var f=new MeshPolygon(new[] {grid[x,y],grid[x+1,y],grid[x+1,y+1],grid[x,y+1]},source.Submesh);
                    var cell=MeshGeometryValidation.Quad(new MeshSurfaceData(positions,faces),f);
                    if(cell.Size<settings.MinimumCellSize) throw new MeshConversionException("SmallCell",$"Patch {patch} produces a cell smaller than {settings.MinimumCellSize}. Reduce density or remove narrow/sliver patches.",cell.Center);
                    faces.Add(f);
                }
            }
            return new MeshSurfaceData(positions,faces);
        }
    }
}
