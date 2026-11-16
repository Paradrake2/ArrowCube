Shader "ArrowCube/OpaqueSurface"
{
    Properties
    {
        [MainColor] _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OpaqueSurfaceURP"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return float4(_Color.rgb, 1.0); }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OpaqueSurfaceBuiltIn"
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct Attributes { float4 vertex : POSITION; };
            struct Varyings { float4 position : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return float4(_Color.rgb, 1.0); }
            ENDCG
        }
    }
    FallBack Off
}
