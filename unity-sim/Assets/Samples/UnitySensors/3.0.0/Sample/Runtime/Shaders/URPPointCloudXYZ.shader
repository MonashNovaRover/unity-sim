Shader "UnitySensors/PointCloudXYZ_URP"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PointSize ("Point Size", Range(0.01, 0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                StructuredBuffer<float3> _PointsBuffer;
                float4x4 _LocalToWorldMatrix;
                float _PointSize;
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 worldPos = IN.positionOS;
                float4 color = float4(1,1,1,1);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    Point p = _PointsBuffer[unity_InstanceID];
                    float3 pointPos = mul(_LocalToWorldMatrix, float4(p.position, 1)).xyz;
                    worldPos = IN.positionOS * _PointSize + pointPos;
                #endif

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                return OUT;
            }

            sampler2D _MainTex;

            half4 frag(Varyings IN) : SV_Target
            {
                return tex2D(_MainTex, IN.uv); // simple white
            }
            ENDHLSL
        }
    }
    FallBack "Unlit/Texture"
}
