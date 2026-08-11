Shader "Game/Interactable_Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.35, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Extruding in view space keeps the outline a constant thickness on screen instead of
                // shrinking with distance the way an object-space offset would.
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), true);

                positionVS += normalVS * _OutlineWidth * -positionVS.z;
                output.positionCS = TransformWViewToHClip(positionVS);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
