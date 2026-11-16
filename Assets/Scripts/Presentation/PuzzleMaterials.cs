using System;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Resources keeps the shader in player builds. Cube, arrows and gates
    /// always use opaque depth-tested rendering, including when URP shaders are stripped.</summary>
    public static class PuzzleMaterials
    {
        public const string ShaderName="ArrowCube/OpaqueSurface";
        public static Material Create(Color color)
        {
            var shader=Resources.Load<Shader>("ArrowCubeOpaque");
            if(shader==null) throw new InvalidOperationException("The required ArrowCubeOpaque shader resource is missing.");
            return new Material(shader) { name="Arrow Cube opaque surface",color=color };
        }
    }
}
