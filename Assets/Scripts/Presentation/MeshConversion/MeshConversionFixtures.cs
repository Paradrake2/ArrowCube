using System.Collections.Generic;
using ArrowCube.Topology;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    public enum MeshConversionFixture { RectangularPrism, L, T, AngularPrism }
    /// <summary>Static test-model geometry. Only vertex/index buffers enter conversion;
    /// none of the fixtures' authored topology metadata is supplied to the converter.</summary>
    public static class MeshConversionFixtures
    {
        public static Mesh Create(MeshConversionFixture shape,bool triangles=false,bool splitSubmeshes=false)
        {
            ISurfaceGeometry geometry=shape==MeshConversionFixture.RectangularPrism ? (ISurfaceGeometry)new CubeSurfaceGeometry(new CubeTopology(1),4)
                : AuthoredGenerationSurfaces.Create(shape==MeshConversionFixture.L ? AuthoredGenerationShape.L : shape==MeshConversionFixture.T ? AuthoredGenerationShape.T : AuthoredGenerationShape.ObliquePrism,1).Geometry;
            var vertices=new List<Vector3>(); var first=new List<int>(); var second=new List<int>(); int face=0;
            foreach(var q in geometry.RenderQuads)
            {
                int offset=vertices.Count;
                for(int i=0;i<4;i++) { var p=q.Corner(i); if(shape==MeshConversionFixture.RectangularPrism) p=Vector3.Scale(p,new Vector3(1.4f,.65f,1.05f)); vertices.Add(p); }
                var list=splitSubmeshes && face++%2==1 ? second : first;
                if(triangles) { list.AddRange(new[] {offset,offset+1,offset+2,offset,offset+2,offset+3}); }
                else list.AddRange(new[] {offset,offset+1,offset+2,offset+3});
            }
            var mesh=new Mesh {name="ArrowCube input - "+shape,indexFormat=IndexFormat.UInt32}; mesh.SetVertices(vertices); mesh.subMeshCount=splitSubmeshes ? 2 : 1;
            mesh.SetIndices(first,triangles ? MeshTopology.Triangles : MeshTopology.Quads,0);
            if(splitSubmeshes) mesh.SetIndices(second,triangles ? MeshTopology.Triangles : MeshTopology.Quads,1);
            mesh.RecalculateBounds(); mesh.RecalculateNormals(); return mesh;
        }
    }
}
