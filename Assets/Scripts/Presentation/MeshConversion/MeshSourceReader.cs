using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Reads geometry only. Imported normals, UV seams and material boundaries are
    /// not mistaken for logical connectivity. The caller owns the source mesh.</summary>
    internal static class MeshSourceReader
    {
        public static MeshSurfaceData Read(Mesh mesh,Matrix4x4 transform,out bool quads)
        {
            if(mesh==null) throw new MeshConversionException("Input","Select a readable static Mesh asset or MeshFilter.");
            if(!mesh.isReadable) throw new MeshConversionException("MeshRead","Enable Read/Write in the model import settings and reimport the mesh.");
            if(mesh.vertexCount<4 || mesh.vertexCount>200000 || mesh.subMeshCount<1) throw new MeshConversionException("Input","Expected 4..200000 vertices and at least one submesh.");
            if(!MeshConversionSettings.Finite(transform.determinant) || Mathf.Abs(transform.determinant)<1e-10f) throw new MeshConversionException("Transform","The baked transform must be finite and invertible.");
            var vertices=new List<Vector3>(); foreach(var v in mesh.vertices) vertices.Add(transform.MultiplyPoint3x4(v));
            var faces=new List<MeshPolygon>(); quads=mesh.GetTopology(0)==MeshTopology.Quads;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var topology=mesh.GetTopology(s);
                if(topology!=(quads ? MeshTopology.Quads : MeshTopology.Triangles)) throw new MeshConversionException("Primitive","Use exclusively triangle submeshes or exclusively quad submeshes. Lines, points and mixed primitive types are unsupported.");
                int width=quads ? 4 : 3; var indices=mesh.GetIndices(s);
                if(indices.Length%width!=0) throw new MeshConversionException("Indices","Incomplete source face index list.");
                for(int i=0;i<indices.Length;i+=width) { var f=new int[width]; Array.Copy(indices,i,f,0,width); faces.Add(new MeshPolygon(f,s)); }
            }
            if(faces.Count==0 || faces.Count>20000) throw new MeshConversionException("Input","Use 1..20000 source faces; subdivide coarse patches during conversion.");
            return new MeshSurfaceData(vertices,faces);
        }
        public static MeshSurfaceData Weld(MeshSurfaceData input,MeshConversionSettings settings,MeshConversionReport report,out int[] map)
        {
            report.SourceVertices=input.Positions.Count; map=new int[input.Positions.Count]; var order=new List<int>();
            for(int i=0;i<input.Positions.Count;i++)
            {
                var p=input.Positions[i]; if(!MeshConversionSettings.Finite(p.x)||!MeshConversionSettings.Finite(p.y)||!MeshConversionSettings.Finite(p.z) || p.magnitude>1000000)
                    throw new MeshConversionException("Coordinates","Coordinates must be finite and within one million local units.",p);
                order.Add(i);
            }
            order.Sort((a,b)=> { var x=input.Positions[a]; var y=input.Positions[b]; int c=x.x.CompareTo(y.x); if(c==0) c=x.y.CompareTo(y.y); if(c==0) c=x.z.CompareTo(y.z); return c!=0 ? c : a.CompareTo(b); });
            var positions=new List<Vector3>(); float tolerance=settings.WeldTolerance; int moved=0;
            var exact=new Dictionary<Vector3,int>(); var bins=new Dictionary<(long,long,long),List<int>>();
            (long,long,long) Bin(Vector3 p) => ((long)Math.Floor((double)p.x/tolerance),(long)Math.Floor((double)p.y/tolerance),(long)Math.Floor((double)p.z/tolerance));
            if(tolerance>0 && tolerance<1e-9f) throw new MeshConversionException("WeldTolerance","Use zero for exact welding or a tolerance of at least 1e-9 local units.");
            foreach(int original in order)
            {
                var p=input.Positions[original]; int found=-1;
                if(tolerance==0) { if(exact.TryGetValue(p,out int existing)) found=existing; }
                else
                {
                    var b=Bin(p);
                    for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++)
                        if(bins.TryGetValue((b.Item1+x,b.Item2+y,b.Item3+z),out var candidates)) foreach(int candidate in candidates)
                            if((positions[candidate]-p).sqrMagnitude<=tolerance*tolerance)
                            { if(found>=0 && found!=candidate) throw new MeshConversionException("AmbiguousWeld","A vertex matches multiple weld anchors. Reduce the tolerance or clean up near-coincident vertices.",p); found=candidate; }
                }
                if(found<0)
                {
                    found=positions.Count; positions.Add(p);
                    if(tolerance==0) exact.Add(p,found);
                    else { var key=Bin(p); if(!bins.TryGetValue(key,out var list)) bins.Add(key,list=new List<int>()); list.Add(found); }
                }
                else if(!positions[found].Equals(p)) moved++;
                map[original]=found;
            }
            var faces=new List<MeshPolygon>();
            foreach(var f in input.Faces)
            {
                var ids=new int[f.Vertices.Length];
                for(int i=0;i<ids.Length;i++) { if(f.Vertices[i]<0 || f.Vertices[i]>=map.Length) throw new MeshConversionException("Indices","A face references an invalid vertex."); ids[i]=map[f.Vertices[i]]; }
                faces.Add(new MeshPolygon(ids,f.Submesh));
            }
            report.WeldedVertices=positions.Count;
            if(moved>0) report.Warnings.Add($"Snapped {moved} seam vertices to deterministic weld anchors. Review near-contact geometry in the preview.");
            return new MeshSurfaceData(positions,faces);
        }
    }
}
