using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;
using static UnityEditor.ShaderData;

//This example blits the active CameraColor to a new texture. It shows how to do a blit with material, and how to use the ResourceData to avoid another blit back to the active color target.
//This example is for API demonstrative purposes. 


// This pass blits the whole screen for a given material to a temp texture, and swaps the UniversalResourceData.cameraColor to this temp texture.
// Therefor, the next pass that references the cameraColor will reference this new temp texture as the cameraColor, saving us a blit. 
// Using the ResourceData, you can manage swapping of resources yourself and don't need a bespoke API like the SwapColorBuffer API that was specific for the cameraColor. 
// This allows you to write more decoupled passes without the added costs of avoidable copies/blits.
public class DitherEffectPass : ScriptableRenderPass
{
    const string m_PassName = "DitherEffectPass";

    // Material used in the blit operation.
    Material m_BlitMaterial;

    // Function used to transfer the material from the renderer feature to the render pass.
    public void Setup(Material mat)
    {
        m_BlitMaterial = mat;

        //The pass will read the current color texture. That needs to be an intermediate texture. It's not supported to use the BackBuffer as input texture. 
        //By setting this property, URP will automatically create an intermediate texture. 
        //It's good practice to set it here and not from the RenderFeature. This way, the pass is selfcontaining and you can use it to directly enqueue the pass from a monobehaviour without a RenderFeature.
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var stack = VolumeManager.instance.stack;
            var customEffect = stack.GetComponent<SphereVolumeComponent>();
            // Only process if the effect is active
            if (!customEffect.IsActive())
                return;

        // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
        // The active color and depth textures are the main color and depth buffers that the camera renders into
        var resourceData = frameData.Get<UniversalResourceData>();

        //This should never happen since we set m_Pass.requiresIntermediateTexture = true;
        //Unless you set the render event to AfterRendering, where we only have the BackBuffer. 
        if (resourceData.isActiveTargetBackBuffer)
        {
            Debug.LogError($"Skipping render pass. DitherEffectRendererFeature requires an intermediate ColorTexture, we can't use the BackBuffer as a texture input.");
            return;
        }

        // The destination texture is created here, 
        // the texture is created with the same dimensions as the active color texture
        var source = resourceData.activeColorTexture;

        var destinationDesc = renderGraph.GetTextureDesc(source);
        destinationDesc.name = $"CameraColor-{m_PassName}";
        destinationDesc.clearBuffer = false;

        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

        RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_BlitMaterial, 0);
        renderGraph.AddBlitPass(para, passName: m_PassName);

        //FrameData allows to get and set internal pipeline buffers. Here we update the CameraColorBuffer to the texture that we just wrote to in this pass. 
        //Because RenderGraph manages the pipeline resources and dependencies, following up passes will correctly use the right color buffer.
        //This optimization has some caveats. You have to be careful when the color buffer is persistent across frames and between different cameras, such as in camera stacking.
        //In those cases you need to make sure your texture is an RTHandle and that you properly manage the lifecycle of it.
        resourceData.cameraColor = destination;
    }
}

public class DitherEffectRendererFeature : ScriptableRendererFeature
{    
    [Tooltip("The material used when making the blit operation.")]
    public Material material;

    [Tooltip("The event where to inject the pass.")]
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    DitherEffectPass m_Pass;

    // Here you can create passes and do the initialization of them. This is called everytime serialization happens.
    public override void Create()
    {
        m_Pass = new DitherEffectPass();

        // Configures where the render pass should be injected.
        m_Pass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Early exit if there are no materials.
        if (material == null)
        {
            Debug.LogWarning("DitherEffectRendererFeature material is null and will be skipped.");
            return;
        }

        m_Pass.Setup(material);
        renderer.EnqueuePass(m_Pass);        
    }
}



/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DitherEffectFeature : ScriptableRendererFeature
{
    // We want to call the pass after post processing
    public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering;
    // Material with the DitherEffect conversion shader 
    public Material passMaterial;
    // Needed requirements for the pass
    public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.Color;

    public bool optimise = false;

    private static MaterialPropertyBlock s_SharedPropertyBlock = null;

    // The pass itself
    private DitherEffectPass m_pass;

    public override void Create()
    {
        m_pass = new DitherEffectPass(passMaterial, name, optimise);
        m_pass.renderPassEvent = injectionPoint;
        m_pass.ConfigureInput(requirements);
    }

    public void OnEnable()
    {
        if(s_SharedPropertyBlock == null)
            s_SharedPropertyBlock = new MaterialPropertyBlock();
    }
    // Adding the render pass to the renderer
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_pass);
    }

    // Class defining the pass
    internal class DitherEffectPass : ScriptableRenderPass
    {
        private string m_PassName;
        private ProfilingSampler m_Sampler;
        private Material m_Material;
        private bool m_Optimise;
        //private static readonly int m_BlitTextureID = Shader.PropertyToID("_BlitTexture");
        private static readonly int m_BlitScaleBiasID = Shader.PropertyToID("_BlitScaleBias");

        /////// NON Render Graph ONLY ///////
        private RTHandle m_CopiedColor;
        /////////////////////////////////////


        ///////// Render Graph ONLY /////////
        private PassData m_PassData;
        private class PassData
        {
            internal Material material;
            internal TextureHandle source;
        }
        private static Material s_FrameBufferFetchMaterial;
        /////////////////////////////////////
        
        public DitherEffectPass(Material mat, string name, bool optimise)
        {
            m_PassName = name;
            m_Material = mat;
            m_Optimise = optimise;

            m_Sampler ??= new ProfilingSampler(GetType().Name + "_" + name);
            s_FrameBufferFetchMaterial ??= UnityEngine.Resources.Load("FrameBufferFetch") as Material;
        }

        [System.Obsolete]
        // NON Render Graph ONLY
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ReAllocate(renderingData.cameraData.cameraTargetDescriptor);
        }

        internal void ReAllocate(RenderTextureDescriptor desc)
        {
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_CopiedColor, desc, name: "_FullscreenPassColorCopy");
        }

        public void Dispose()
        {
            m_CopiedColor?.Release();
        }

        private static void ExecuteCopyColorPass(RasterCommandBuffer cmd, RTHandle sourceTexture, bool optimise)
        {
            if(!optimise)
            {
                Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
            }
            else
            {
                cmd.DrawProcedural(Matrix4x4.identity, s_FrameBufferFetchMaterial, 1, MeshTopology.Triangles, 3, 1, null);
            }
        }

        private static void ExecuteMainPass(RasterCommandBuffer cmd, Material material, RTHandle copiedColor)
        {
            s_SharedPropertyBlock.Clear();
            //if (copiedColor != null)
            //    s_SharedPropertyBlock.SetTexture(m_BlitTextureID, copiedColor);

            // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
            s_SharedPropertyBlock.SetVector(m_BlitScaleBiasID, new Vector4(1, 1, 0, 0));

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 4, 1, s_SharedPropertyBlock);
        }

        // NON Render Graph ONLY
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var stack = VolumeManager.instance.stack;
            var customEffect = stack.GetComponent<SphereVolumeComponent>();
            // Only process if the effect is active
            if (!customEffect.IsActive())
                return;

            // Retrieve URP camera data
            ref var cameraData = ref renderingData.cameraData;

            // Get a command buffer from the pool and clear it
            CommandBuffer cmd = CommandBufferPool.Get("Custom Post Processing - Dither Effect");
            cmd.Clear();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                // Get the raster command buffer
                RasterCommandBuffer rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);
                
                // Set the render target to the temporary color texture (created in OnCameraSetup())
                CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
                // Execute the copy pass, copying the URP camera color target to the temp color target
                ExecuteCopyColorPass(rasterCmd, cameraData.renderer.cameraColorTargetHandle, m_Optimise);

                // Set the render target back to the URP camera color target
                CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                // Execute the blit pass with the dither effect material, sampling the copied temp color texture
                ExecuteMainPass(rasterCmd, m_Material, m_CopiedColor);
            }

            // Execute the command buffer and release it
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Render Graph ONLY - Recording the passes in the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var stack = VolumeManager.instance.stack;
            var customEffect = stack.GetComponent<SphereVolumeComponent>();
            // Only process if the effect is active
            if (!customEffect.IsActive())
                return;
            
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // We need a copy of the color texture as input for the blit with material
            // Retrieving texture descriptor from active color texture after post process
            var colCopyDesc = renderGraph.GetTextureDesc(resourceData.afterPostProcessColor);
            // Changing the name
            colCopyDesc.name = "_TempColorCopy";
            // Requesting the creation of a texture to Render Graph, Render Graph will allocate when needed
            TextureHandle copiedColorTexture = renderGraph.CreateTexture(colCopyDesc);

            // First blit, simply copying color to intermediary texture so it can be used as input in next pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName + "_CopyPass", out var passData, m_Sampler))
            {
                // Setting the URP active color texture as the source for this pass
                passData.source = resourceData.activeColorTexture;
                // Setting input texture to sample
                if (m_Optimise){
                    builder.SetInputAttachment(resourceData.activeColorTexture, 0);
                }else{
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                }
                // Setting output attachment
                builder.SetRenderAttachment(copiedColorTexture, 0, AccessFlags.Write);

                // Execute step, simple copy
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    ExecuteCopyColorPass(rgContext.cmd, data.source, m_Optimise);
                });
            }

            // Second blit with material, applying gray conversion
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName + "_FullScreenPass", out var passData, m_Sampler))
            {
                // Setting the material
                passData.source = resourceData.activeColorTexture;
                passData.material = m_Material;

                // Setting input texture to sample
                if (m_Optimise){
                    builder.SetInputAttachment( copiedColorTexture, 0);
                }else{
                    // Setting the temp color texture as the source for this pass
                    //passData.source = copiedColorTexture;
                    builder.UseTexture(copiedColorTexture, AccessFlags.Read);
                }
                // Setting output attachment
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                // Execute step, second blit with the gray scale conversion
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    ExecuteMainPass(rgContext.cmd, data.material, m_Optimise ? null : data.source);
                });
            }
        }
    }
}*/
