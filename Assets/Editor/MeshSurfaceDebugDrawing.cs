using ArrowCube.Presentation;
using ArrowCube.Topology;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshSurfaceDebugDrawing
{
    public static void Draw(ConvertedMeshSurface converted,bool ids,bool directions,bool normals,bool connections,int maximum)
    {
        var geometry=converted.Geometry; var surface=converted.Shape.Topology; var settings=converted.Settings; int drawn=0;
        var old=Handles.zTest; Handles.zTest=CompareFunction.LessEqual;
        try
        {
            foreach(int cell in surface.EnumerateCellIds())
            {
                if(drawn++>=maximum) break; var q=geometry.GetCell(cell); float lift=geometry.ReferenceCellSize*.02f; var center=q.Center+q.Normal*lift;
                if(ids) { Handles.color=Color.white; Handles.Label(center,cell.ToString()); }
                if(normals) { Handles.color=Color.cyan; Handles.ArrowHandleCap(0,center,Quaternion.LookRotation(q.Normal),q.Size*.35f,EventType.Repaint); }
                for(int d=0;d<4;d++)
                {
                    var direction=(SurfaceDirection)d; var boundary=surface.GetBoundary(cell,direction); q.Boundary(direction,out var a,out var b);
                    var other=geometry.GetCell(boundary.NeighborCellId.Value);
                    var kind=MeshSurfaceConverter.Classify(q,other,direction,settings);
                    Handles.color=kind==MeshFoldKind.Convex ? new Color(.1f,.85f,.3f) : kind==MeshFoldKind.Concave ? new Color(1,.2f,.8f) : new Color(.45f,.5f,.6f);
                    Handles.DrawAAPolyLine(2,a+q.Normal*lift,b+q.Normal*lift);
                    if(boundary.AllowsEdgeExit) { Handles.color=new Color(1,.7f,.1f); Handles.DrawLine((a+b)*.5f+q.Normal*lift,(a+b)*.5f+q.Normal*lift-q.Direction(direction)*q.Size*.10f); }
                    if(directions)
                    {
                        Handles.color=d==0 ? Color.red : d==1 ? Color.green : d==2 ? Color.magenta : Color.blue;
                        Handles.ArrowHandleCap(0,center,Quaternion.LookRotation(q.Direction(direction),q.Normal),q.Size*.28f,EventType.Repaint);
                        Handles.Label(center+q.Direction(direction)*q.Size*.3f,direction.ToString());
                    }
                    if(connections && cell<boundary.NeighborCellId.Value)
                    {
                        Handles.color=Color.cyan; var seam=geometry.GetLiftedBoundary(cell,direction,lift);
                        Handles.DrawDottedLine(center,seam,3); Handles.DrawDottedLine(seam,other.Center+other.Normal*lift,3);
                    }
                }
            }
        }
        finally { Handles.zTest=old; }
    }
    public static void Source(Mesh mesh,Matrix4x4 transform,int maximum)
    {
        if(mesh==null || !mesh.isReadable) return; var vertices=mesh.vertices; int drawn=0; Handles.color=new Color(.5f,.55f,.6f,.5f);
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            var topology=mesh.GetTopology(s); int width=topology==MeshTopology.Quads ? 4 : topology==MeshTopology.Triangles ? 3 : 0; if(width==0) continue; var indices=mesh.GetIndices(s);
            for(int i=0;i+width<=indices.Length && drawn<maximum;i+=width,drawn++) for(int e=0;e<width;e++)
            {
                var a=transform.MultiplyPoint3x4(vertices[indices[i+e]]); var b=transform.MultiplyPoint3x4(vertices[indices[i+(e+1)%width]]);
                if(!float.IsNaN(a.x+a.y+a.z+b.x+b.y+b.z) && !float.IsInfinity(a.x+a.y+a.z+b.x+b.y+b.z)) Handles.DrawLine(a,b);
            }
        }
    }
    public static void Error(MeshConversionException error)
    {
        if(error==null || error.Locations.Count==0) return;
        Handles.color=Color.red; Vector3? previous=null;
        foreach(var p in error.Locations)
        {
            if(float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude)) continue;
            Handles.SphereHandleCap(0,p,Quaternion.identity,.06f,EventType.Repaint);
            if(previous.HasValue) Handles.DrawAAPolyLine(5,previous.Value,p); else Handles.Label(p,error.Code);
            previous=p;
        }
    }
}
