using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// public class DitherEffectFeature : ScriptableRendererFeature
// {
//     // We want to call the pass after post processing
//     public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering;
//     // Material with the DitherEffect conversion shader 
//     public Material passMaterial;
//     // Needed requirements for the pass
//     public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.Color;

//     private static MaterialPropertyBlock s_SharedPropertyBlock = null;

//     // The pass itself
//     private DitherEffectPass m_pass;

//     public override void Create()
//     {
//         m_pass = new DitherEffectPass(passMaterial, name);
//         m_pass.renderPassEvent = injectionPoint;
//         m_pass.ConfigureInput(requirements);
//     }

//     public void OnEnable()
//     {
//         if(s_SharedPropertyBlock == null)
//             s_SharedPropertyBlock = new MaterialPropertyBlock();
//     }
//     // Adding the render pass to the renderer
//     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//     {
//         renderer.EnqueuePass(m_pass);
//     }

//     // Class defining the pass
//     internal class DitherEffectPass : ScriptableRenderPass
//     {
//         private string m_PassName;
//         private ProfilingSampler m_Sampler;
//         private Material m_Material;
//         private static readonly int m_BlitTextureID = Shader.PropertyToID("_BlitTexture");
//         private static readonly int m_BlitScaleBiasID = Shader.PropertyToID("_BlitScaleBias");

//         /////// NON Render Graph ONLY ///////
//         private RTHandle m_CopiedColor;
//         /////////////////////////////////////


//         ///////// Render Graph ONLY /////////
//         private PassData m_PassData;
//         private class PassData
//         {
//             internal Material material;
//             internal TextureHandle source;
//         }
//         private static Material s_FrameBufferFetchMaterial;

//         /////////////////////////////////////
        
//         public DitherEffectPass(Material mat, string name)
//         {
//             m_PassName = name;
//             m_Material = mat;

//             m_Sampler ??= new ProfilingSampler(GetType().Name + "_" + name);

//             s_FrameBufferFetchMaterial ??= UnityEngine.Resources.Load("FrameBufferFetch") as Material;
//         }

//         [System.Obsolete]
//         // NON Render Graph ONLY
//         public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
//         {
//             ReAllocate(renderingData.cameraData.cameraTargetDescriptor);
//         }

//         internal void ReAllocate(RenderTextureDescriptor desc)
//         {
//             desc.msaaSamples = 1;
//             desc.depthBufferBits = (int)DepthBits.None;
//             RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, desc, name: "_FullscreenPassColorCopy");
//         }

//         public void Dispose()
//         {
//             m_CopiedColor?.Release();
//         }

//         private static void ExecuteCopyColorPass(RasterCommandBuffer cmd, RTHandle sourceTexture, bool useFrameBufferFetch = false)
//         {
//             // NON Render Graph and Render Graph with no optimization
//             if(!useFrameBufferFetch)
//             {
//                 Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
//             }
//             else // Render Graph with optimization
//             {
//                 cmd.DrawProcedural(Matrix4x4.identity, s_FrameBufferFetchMaterial, 1, MeshTopology.Triangles, 3, 1, null);
//             }
//         }

//         private static void ExecuteMainPass(RasterCommandBuffer cmd, Material material, RTHandle copiedColor)
//         {
//             s_SharedPropertyBlock.Clear();
//             if (copiedColor != null)
//                 s_SharedPropertyBlock.SetTexture(m_BlitTextureID, copiedColor);

//             // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
//             s_SharedPropertyBlock.SetVector(m_BlitScaleBiasID, new Vector4(1, 1, 0, 0));

//             cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
//         }

//         // NON Render Graph ONLY
//         [System.Obsolete]
//         public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//         {
//             var stack = VolumeManager.instance.stack;
//             var customEffect = stack.GetComponent<SphereVolumeComponent>();
//             // Only process if the effect is active
//             if (!customEffect.IsActive())
//                 return;

//             // Retrieve URP camera data
//             ref var cameraData = ref renderingData.cameraData;

//             // Get a command buffer from the pool and clear it
//             CommandBuffer cmd = CommandBufferPool.Get("Custom Post Processing - Dither Effect");
//             cmd.Clear();

//             using (new ProfilingScope(cmd, profilingSampler))
//             {
//                 // Get the raster command buffer
//                 RasterCommandBuffer rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);
                
//                 // Set the render target to the temporary color texture (created in OnCameraSetup())
//                 CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
//                 // Execute the copy pass, copying the URP camera color target to the temp color target
//                 ExecuteCopyColorPass(rasterCmd, cameraData.renderer.cameraColorTargetHandle);

//                 // Set the render target back to the URP camera color target
//                 CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
//                 // Execute the blit pass with the dither effect material, sampling the copied temp color texture
//                 ExecuteMainPass(rasterCmd, m_Material, m_CopiedColor);
//             }

//             // Execute the command buffer and release it
//             context.ExecuteCommandBuffer(cmd);
//             CommandBufferPool.Release(cmd);
//         }

//         // Render Graph ONLY - Recording the passes in the render graph
//         /*
//         public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//         {
//             var stack = VolumeManager.instance.stack;
//             var customEffect = stack.GetComponent<SphereVolumeComponent>();
//             // Only process if the effect is active
//             if (!customEffect.IsActive())
//                 return;
            
//             // UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

//             // // We need a copy of the color texture as input for the blit with material
//             // // Retrieving texture descriptor from active color texture after post process
//             // var colCopyDesc = renderGraph.GetTextureDesc(resourceData.afterPostProcessColor);
//             // // Changing the name
//             // colCopyDesc.name = "_TempColorCopy";
//             // // Requesting the creation of a texture to Render Graph, Render Graph will allocate when needed
//             // TextureHandle copiedColorTexture = renderGraph.CreateTexture(colCopyDesc);

//             // First blit, simply copying color to intermediary texture so it can be used as input in next pass
//             using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName + "_CopyPass", out var passData, m_Sampler))
//             {
//                 // // Setting the URP active color texture as the source for this pass
//                 // passData.source = resourceData.activeColorTexture;

//                 // // Setting input texture to sample
//                 // builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
//                 // // Setting output attachment
//                 // builder.SetRenderAttachment(copiedColorTexture, 0, AccessFlags.Write);

//                 // // Execute step, simple copy
//                 // builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
//                 // {
//                 //     ExecuteCopyColorPass(rgContext.cmd, data.source);
//                 // });
//             }

//             /// Second blit with material, applying gray conversion
//             using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName + "_FullScreenPass", out var passData, m_Sampler))
//             {
//                 // // Setting the temp color texture as the source for this pass
//                 // passData.source = resourceData.activeColorTexture;
//                 // // Setting the material
//                 // passData.material = m_Material;

//                 // // Setting input texture to sample
//                 // builder.UseTexture(copiedColorTexture, AccessFlags.Read);
//                 // // Setting output attachment
//                 // builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

//                 // // Execute step, second blit with the gray scale conversion
//                 // builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
//                 // {
//                 //     ExecuteMainPass(rgContext.cmd, data.material, data.source);
//                 // });
//             }
//         }
//         */
//     }
// }
