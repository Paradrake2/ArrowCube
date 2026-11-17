using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    public enum MeshEdgeExitPolicy { ConvexOnly, Disabled }
    public enum MeshFoldKind { Coplanar, Convex, Concave }
    [Serializable]
    public sealed class PatchSubdivision
    {
        public int Patch;
        [Min(1)] public int Columns=4,Rows=4;
    }
    [Serializable]
    public sealed class MeshConversionSettings
    {
        [Range(1,64)] public int Subdivisions=4;
        [Min(0)] public float TargetCellSize;
        [Min(0)] public float WeldTolerance=.00001f;
        [Min(.000001f)] public float MinimumCellSize=.005f;
        [Range(.001f,.5f)] public float CoplanarAngleDegrees=.05f;
        [Range(10,175)] public float MaximumFoldDegrees=170;
        [Range(2,65536)] public int MaximumCells=16384;
        public bool PropagateSubdivisions=true;
        public bool RepairUniformInwardWinding;
        public MeshEdgeExitPolicy EdgeExits=MeshEdgeExitPolicy.ConvexOnly;
        public List<PatchSubdivision> PatchSubdivisions=new List<PatchSubdivision>();
        public int PairingSearchBudget=100000;
        public MeshConversionSettings Copy()
        {
            var c=(MeshConversionSettings)MemberwiseClone(); c.PatchSubdivisions=new List<PatchSubdivision>();
            if(PatchSubdivisions!=null) foreach(var p in PatchSubdivisions) c.PatchSubdivisions.Add(p==null ? null : new PatchSubdivision {Patch=p.Patch,Columns=p.Columns,Rows=p.Rows});
            return c;
        }
        internal void Validate()
        {
            if(Subdivisions<1 || Subdivisions>64 || !Finite(TargetCellSize) || TargetCellSize<0 || !Finite(WeldTolerance) || WeldTolerance<0
                || !Finite(MinimumCellSize) || MinimumCellSize<.000001f || WeldTolerance>MinimumCellSize*.01f
                || !Finite(CoplanarAngleDegrees) || CoplanarAngleDegrees<.001f || CoplanarAngleDegrees>.5f
                || !Finite(MaximumFoldDegrees) || MaximumFoldDegrees<10 || MaximumFoldDegrees>175
                || MaximumCells<2 || MaximumCells>65536 || PairingSearchBudget<1 || PairingSearchBudget>2000000 || !Enum.IsDefined(typeof(MeshEdgeExitPolicy),EdgeExits))
                throw new MeshConversionException("Settings","Invalid conversion limits. Welding must be at most 1% of minimum cell size; subdivisions are 1..64, cells 2..65536, and coplanar tolerance .001..0.5 degrees.");
            var seen=new HashSet<int>();
            foreach(var p in PatchSubdivisions??new List<PatchSubdivision>())
                if(p==null || p.Patch<0 || !seen.Add(p.Patch) || p.Columns<1 || p.Columns>64 || p.Rows<1 || p.Rows>64)
                    throw new MeshConversionException("Subdivision","Patch overrides require unique nonnegative patch IDs and 1..64 rows/columns.");
        }
        internal static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        internal static MeshConversionSettings Read(BinaryReader r)
        {
            var s=new MeshConversionSettings { Subdivisions=r.ReadInt32(),TargetCellSize=r.ReadSingle(),WeldTolerance=r.ReadSingle(),MinimumCellSize=r.ReadSingle(),
                CoplanarAngleDegrees=r.ReadSingle(),MaximumFoldDegrees=r.ReadSingle(),MaximumCells=r.ReadInt32(),PropagateSubdivisions=r.ReadBoolean(),RepairUniformInwardWinding=r.ReadBoolean(),EdgeExits=(MeshEdgeExitPolicy)r.ReadInt32(),PairingSearchBudget=r.ReadInt32() };
            int count=r.ReadInt32(); if(count<0 || count>20000) throw new MeshConversionException("Asset","Invalid stored patch override count.");
            for(int i=0;i<count;i++) s.PatchSubdivisions.Add(new PatchSubdivision {Patch=r.ReadInt32(),Columns=r.ReadInt32(),Rows=r.ReadInt32()}); s.Validate(); return s;
        }
        internal void Write(BinaryWriter w)
        {
            w.Write(Subdivisions); w.Write(TargetCellSize); w.Write(WeldTolerance); w.Write(MinimumCellSize); w.Write(CoplanarAngleDegrees); w.Write(MaximumFoldDegrees);
            w.Write(MaximumCells); w.Write(PropagateSubdivisions); w.Write(RepairUniformInwardWinding); w.Write((int)EdgeExits); w.Write(PairingSearchBudget);
            var items=new List<PatchSubdivision>(PatchSubdivisions??new List<PatchSubdivision>()); items.Sort((a,b)=>a.Patch.CompareTo(b.Patch)); w.Write(items.Count);
            foreach(var p in items) { w.Write(p.Patch); w.Write(p.Columns); w.Write(p.Rows); }
        }
    }
    public sealed class MeshConversionException : ArgumentException
    {
        public string Code { get; }
        public IReadOnlyList<Vector3> Locations { get; }
        public MeshConversionException(string code,string message,params Vector3[] locations) : base(code+": "+message)
        { Code=code; Locations=Array.AsReadOnly(locations??Array.Empty<Vector3>()); }
    }
    public sealed class MeshConversionReport
    {
        public int SourceVertices,WeldedVertices,Triangles,ParentQuads,Cells,CoplanarBoundaries,ConvexBoundaries,ConcaveBoundaries,PropagatedAxes;
        public double ConversionMilliseconds,ValidationMilliseconds;
        public long ManagedMemoryDelta;
        public readonly List<string> Warnings=new List<string>();
        public override string ToString() => $"{ParentQuads} patches ? {Cells} cells; flat {CoplanarBoundaries}, convex {ConvexBoundaries}, concave {ConcaveBoundaries}. Conversion {ConversionMilliseconds:F1} ms, validation {ValidationMilliseconds:F1} ms.";
    }
    public sealed class ConvertedMeshSurface
    {
        public SurfaceShapeDefinition Shape { get; }
        public QuadSurfaceGeometry Geometry { get; }
        public MeshConversionReport Report { get; }
        public string ContentDigest { get; }
        private readonly MeshConversionSettings settings;
        public MeshConversionSettings Settings => settings.Copy();
        internal ConvertedMeshSurface(SurfaceShapeDefinition shape,QuadSurfaceGeometry geometry,MeshConversionReport report,string digest,MeshConversionSettings settings)
        { Shape=shape; Geometry=geometry; Report=report; ContentDigest=digest; this.settings=settings.Copy(); }
    }
    internal readonly struct MeshEdge : IEquatable<MeshEdge>,IComparable<MeshEdge>
    {
        public readonly int A,B;
        public MeshEdge(int a,int b) { A=Math.Min(a,b); B=Math.Max(a,b); }
        public bool Equals(MeshEdge other) => A==other.A && B==other.B;
        public override bool Equals(object obj) => obj is MeshEdge e && Equals(e);
        public override int GetHashCode() => unchecked(A*397^B);
        public int CompareTo(MeshEdge other) => A!=other.A ? A.CompareTo(other.A) : B.CompareTo(other.B);
    }
    internal sealed class MeshPolygon
    {
        public int[] Vertices;
        public int Submesh;
        public MeshPolygon(int[] vertices,int submesh=0) { Vertices=vertices; Submesh=submesh; }
    }
    internal sealed class MeshSurfaceData
    {
        public List<Vector3> Positions;
        public List<MeshPolygon> Faces;
        public MeshSurfaceData(List<Vector3> positions,List<MeshPolygon> faces) { Positions=positions; Faces=faces; }
    }
    internal static class MeshIdentity
    {
        public static string Hash(Action<BinaryWriter> write)
        { using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream,Encoding.UTF8,true)) { write(w); w.Flush(); using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant(); } }
        public static void Point(BinaryWriter w,Vector3 p) { w.Write(p.x==0 ? 0 : p.x); w.Write(p.y==0 ? 0 : p.y); w.Write(p.z==0 ? 0 : p.z); }
        public static void Canonicalize(MeshPolygon p)
        { int first=0; for(int i=1;i<p.Vertices.Length;i++) if(p.Vertices[i]<p.Vertices[first]) first=i; var a=new int[p.Vertices.Length]; for(int i=0;i<a.Length;i++) a[i]=p.Vertices[(first+i)%a.Length]; p.Vertices=a; }
        public static void Sort(List<MeshPolygon> faces)
        { foreach(var p in faces) Canonicalize(p); faces.Sort((a,b)=> { for(int i=0;i<a.Vertices.Length;i++) { int c=a.Vertices[i].CompareTo(b.Vertices[i]); if(c!=0) return c; } return 0; }); }
    }
}
