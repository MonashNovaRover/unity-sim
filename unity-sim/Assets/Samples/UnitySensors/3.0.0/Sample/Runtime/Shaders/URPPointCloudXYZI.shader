Shader "UnitySensors/PointCloudXYZI_URP"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _PointSize ("Point Size", Range(0.01,0.5)) = 0.05
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest LEqual

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
                float2 uv : TEXCOORD0;   // quad UVs
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float intensity : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                struct Point { float3 position; float intensity; };
                StructuredBuffer<Point> _PointsBuffer;
                float4x4 _LocalToWorldMatrix;
                float _PointSize;
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);        // required

                uint id = UNITY_GET_INSTANCE_ID();  // get instance index

                Point p = _PointsBuffer[id];        // read the correct point
                float3 pointPos = mul(_LocalToWorldMatrix, float4(p.position, 1)).xyz;
                float3 worldPos = IN.positionOS * _PointSize + pointPos;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                OUT.intensity = p.intensity;
                return OUT;
            }

            sampler2D _MainTex;

            half4 frag(Varyings IN) : SV_Target
            {
                return tex2D(_MainTex, IN.uv) * IN.intensity;
            }

            ENDHLSL
        }
    }
}
