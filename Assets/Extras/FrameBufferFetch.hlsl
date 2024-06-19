#ifndef FRAMEBUFFER_FETCH
#define FRAMEBUFFER_FETCH

#ifdef SHADERGRAPH_PREVIEW
#else
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// Declare the framebuffer input as a texture 2d containing float.
FRAMEBUFFER_INPUT_X_FLOAT(0);
#endif

void FragFrameBufferFetch_float(float2 clipPos, out float3 outFbf)
{
#ifdef SHADERGRAPH_PREVIEW
    float4 color = float4(1,1,1,1);
#else
	// Read previous subpass result directly from the framebuffer.
    float4 color = LOAD_FRAMEBUFFER_X_INPUT(0, clipPos);
#endif

    outFbf = color.xyz;
}

#endif
