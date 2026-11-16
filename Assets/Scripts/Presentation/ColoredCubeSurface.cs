using System;
using ArrowCube.Topology;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    /// <summary>Six opaque, evenly visible face colors, independent of scene lighting.</summary>
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class ColoredCubeSurface : MonoBehaviour
    {

        public static Color FaceColor(CubeFace face)
        {
            switch(face)
            {
                case CubeFace.Front: return new Color(.72f,.84f,.98f);
                case CubeFace.Right: return new Color(.62f,.83f,.72f);
                case CubeFace.Back: return new Color(.99f,.76f,.63f);
                case CubeFace.Left: return new Color(.82f,.70f,.95f);
                case CubeFace.Top: return new Color(1f,.90f,.64f);
                case CubeFace.Bottom: return new Color(.94f,.69f,.80f);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        public void Initialize(CubeTopology topology,float sideLength,Material[] materials)
        {
            var shared=gameObject.AddComponent<SurfaceMeshRenderer>();
            shared.Initialize(new CubeSurfaceGeometry(topology,sideLength),materials);
        }
    }
}
