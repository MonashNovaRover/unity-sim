// float _Y_MIN;
// float _Y_MAX;
// float _Y_COEF;

void GetSceneDepthUV_float(float2 uv, out float2 sceneDepthUV)
{
    sceneDepthUV = float2(uv.x, (uv.y - _Y_MIN) * _Y_COEF);
}
void GetOverlapAlpha_float(float2 uv, out float overlapAlpha)
{
    overlapAlpha = (uv.y >= _Y_MIN) * (uv.y <= _Y_MAX);
}

void Depth2Distance_float(float depth, float2 screenUV, out float distance)
{
    float near = _ProjectionParams.y;
    float far  = _ProjectionParams.x;

    // linearize depth
    float linearDepth = (2.0 * near) / (far + near - depth * (far - near));

    // convert to NDC
    float2 ndc = float2(screenUV.x * 2 - 1, 1 - screenUV.y * 2);

    // reconstruct view-space position
    float4 viewDir = mul(unity_CameraInvProjection, float4(ndc, 1, 1));
    float3 viewPos = viewDir.xyz / viewDir.w * linearDepth;

    distance = length(viewPos);
}


