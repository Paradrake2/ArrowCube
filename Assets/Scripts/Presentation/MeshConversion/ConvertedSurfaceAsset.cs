using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Self-contained, exact-bit baked surface. Runtime never reanalyzes the source mesh.</summary>
    [CreateAssetMenu(menuName="Arrow Cube/Converted Surface")]
    public sealed class ConvertedSurfaceAsset : ScriptableObject
    {
        [SerializeField,HideInInspector] private string packet;
        [SerializeField,HideInInspector] private string summary;
        private string cachedPacket;
        private ConvertedMeshSurface cached;
        public string Summary => summary;
        public string ShapeId => Build().Shape.Id;
        public void Initialize(ConvertedMeshSurface result)
        {
            if(result==null) throw new ArgumentNullException(nameof(result));
            using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                w.Write(1); w.Write(result.ContentDigest); result.Settings.Write(w); result.Shape.Write(w); w.Write(result.Shape.Topology.CellCount);
                foreach(int cell in result.Shape.Topology.EnumerateCellIds())
                {
                    w.Write(cell); var q=result.Geometry.GetCell(cell); for(int i=0;i<4;i++) MeshIdentity.Point(w,q.Corner(i)); w.Write(q.Color.r); w.Write(q.Color.g); w.Write(q.Color.b); w.Write(q.Color.a);
                }
                w.Flush(); packet=Convert.ToBase64String(stream.ToArray());
            }
            summary=result.Report.ToString(); cached=result; cachedPacket=packet; Register(this);
        }
        public ConvertedMeshSurface Build()
        {
            try { return ReadPacket(); }
            catch(MeshConversionException) { throw; }
            catch(Exception error) when(error is ArgumentException || error is IOException || error is InvalidOperationException || error is FormatException)
            { throw new MeshConversionException("Asset","Invalid converted-surface packet: "+error.Message); }
        }
        private ConvertedMeshSurface ReadPacket()
        {
            if(cached!=null && cachedPacket==packet) return cached;
            if(string.IsNullOrEmpty(packet) || packet.Length>12000000) throw new MeshConversionException("Asset","Missing or oversized converted-surface data. Bake the asset using the mesh conversion tool.");
            using(var stream=new MemoryStream(Convert.FromBase64String(packet))) using(var r=new BinaryReader(stream,Encoding.UTF8))
            {
                if(r.ReadInt32()!=1) throw new MeshConversionException("Asset","Unsupported converted surface packet version.");
                string digest=r.ReadString(); var settings=MeshConversionSettings.Read(r); var shape=SurfaceShapeDefinition.Read(r);
                if(shape.Version!=MeshSurfaceConverter.ConversionVersion || shape.Id!="arrowcube/mesh/"+digest) throw new MeshConversionException("Asset","Shape identity does not match the converted geometry.");
                int count=r.ReadInt32(); if(count!=shape.Topology.CellCount) throw new MeshConversionException("Asset","Geometry and topology have different cell counts.");
                var quads=new Dictionary<int,SurfaceQuad>(); Vector3 Point() => new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                for(int i=0;i<count;i++) { int cell=r.ReadInt32(); if(!shape.Topology.ContainsCell(cell)) throw new MeshConversionException("Asset","Geometry references an unknown logical cell."); var a=Point(); var b=Point(); var c=Point(); var d=Point(); quads.Add(cell,new SurfaceQuad(a,b,c,d,new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()))); }
                if(stream.Position!=stream.Length || MeshSurfaceConverter.ComputeDigest(shape.Topology,quads,settings)!=digest) throw new MeshConversionException("Asset","Converted surface content checksum is invalid.");
                var geometry=new QuadSurfaceGeometry(shape.Topology,quads);
                cached=new ConvertedMeshSurface(shape,geometry,new MeshConversionReport {Cells=count},digest,settings); cachedPacket=packet; return cached;
            }
        }
        private void OnValidate() { cached=null; cachedPacket=null; }
        private static readonly Dictionary<string,ConvertedSurfaceAsset> Registry=new Dictionary<string,ConvertedSurfaceAsset>();
        private static bool resourcesLoaded;
        public static void Register(ConvertedSurfaceAsset asset)
        { if(asset==null) throw new ArgumentNullException(nameof(asset)); Registry[asset.Build().Shape.Id]=asset; }
        public static ISurfaceGeometry Resolve(PuzzleDefinition puzzle)
        {
            if(!resourcesLoaded) { resourcesLoaded=true; foreach(var asset in Resources.LoadAll<ConvertedSurfaceAsset>("ArrowCubeSurfaces")) Register(asset); }
            if(!Registry.TryGetValue(puzzle.SurfaceAssetId??"",out var source) || source==null)
            { foreach(var asset in Resources.LoadAll<ConvertedSurfaceAsset>("ArrowCubeSurfaces")) Register(asset); Registry.TryGetValue(puzzle.SurfaceAssetId??"",out source); }
            if(source==null) throw new ArgumentException("The converted geometry asset is unavailable. Include it in Resources/ArrowCubeSurfaces or register a referenced Converted Surface asset before restoring this puzzle.");
            var result=source.Build();
            if(puzzle.SurfaceAssetVersion!=result.Shape.Version || new SurfaceShapeDefinition(result.Shape.Id,result.Shape.Version,puzzle.Surface).Digest!=result.Shape.Digest)
                throw new ArgumentException("Saved converted topology does not match its geometry asset.");
            var quads=new Dictionary<int,SurfaceQuad>(); foreach(int c in puzzle.Surface.EnumerateCellIds()) quads.Add(c,result.Geometry.GetCell(c));
            return new QuadSurfaceGeometry(puzzle.Surface,quads);
        }
    }
}
