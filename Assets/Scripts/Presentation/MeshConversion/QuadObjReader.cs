using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    /// <summary>Reliable explicit-quad interchange. UV/normal/material indices are ignored;
    /// polygon vertex order and original quad boundaries are preserved.</summary>
    public static class QuadObjReader
    {
        public static Mesh Read(string text,string name="Quad OBJ")
        {
            if(text==null || text.Length>16000000) throw new MeshConversionException("OBJ","Missing or oversized OBJ input (maximum 16 MB of text).");
            var vertices=new List<Vector3>(); var indices=new List<int>(); int lineNumber=0;
            using(var reader=new StringReader(text))
            {
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    lineNumber++; int comment=line.IndexOf('#'); if(comment>=0) line=line.Substring(0,comment);
                    var words=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries); if(words.Length==0) continue;
                    if(words[0]=="v")
                    {
                        if(words.Length!=4) throw new MeshConversionException("OBJ",$"Line {lineNumber}: use three-coordinate vertices without homogeneous weights or vertex colors.");
                        float Value(int i) { if(!float.TryParse(words[i],NumberStyles.Float,CultureInfo.InvariantCulture,out float value) || !MeshConversionSettings.Finite(value)) throw new MeshConversionException("OBJ",$"Line {lineNumber}: invalid vertex coordinate."); return value; }
                        vertices.Add(new Vector3(Value(1),Value(2),Value(3)));
                    }
                    else if(words[0]=="f")
                    {
                        if(words.Length!=5) throw new MeshConversionException("OBJ",$"Line {lineNumber}: expected a quad face with exactly four vertices. Export quads without triangulation; triangles/n-gons need preprocessing.");
                        for(int i=1;i<5;i++)
                        {
                            string part=words[i].Split('/')[0];
                            if(!int.TryParse(part,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value) || value==0) throw new MeshConversionException("OBJ",$"Line {lineNumber}: invalid face index.");
                            int index=value>0 ? value-1 : vertices.Count+value;
                            if(index<0 || index>=vertices.Count) throw new MeshConversionException("OBJ",$"Line {lineNumber}: face index is out of range."); indices.Add(index);
                        }
                    }
                    if(vertices.Count>200000 || indices.Count>80000) throw new MeshConversionException("OBJ","Use at most 200000 vertices and 20000 coarse quads.");
                }
            }
            if(vertices.Count<4 || indices.Count==0) throw new MeshConversionException("OBJ","No quad surface was found.");
            var mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32}; mesh.SetVertices(vertices); mesh.SetIndices(indices,MeshTopology.Quads,0); mesh.RecalculateBounds(); return mesh;
        }
    }
}
