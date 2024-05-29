using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class TintRendererFeature : ScriptableRendererFeature
{
    public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering;
    // Material with the Tint conversion shader 
    public Material passMaterial;
    private ProfilingSampler m_Sampler;
    // Needed requirements for the pass
    public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.Color;
    private static readonly int m_BlitTextureID = Shader.PropertyToID("_BlitTexture");
    private static readonly int m_BlitScaleBiasID = Shader.PropertyToID("_BlitScaleBias");

    private static MaterialPropertyBlock s_SharedPropertyBlock = null;

    class TintPass : ScriptableRenderPass
    {
        private Material m_Material;
        private string m_PassName;
        private ProfilingSampler m_Sampler;
        
        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData
        {
            internal Material material;
            internal TextureHandle source;
        }

        public TintPass(Material mat, string name)
        {
            m_PassName = name;
            m_Material = mat;
            m_Sampler ??= new ProfilingSampler(GetType().Name + "_" + name);
        }

        private static void ExecuteCopyColorPass(RasterCommandBuffer cmd, RTHandle sourceTexture)
        {
            Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
        }

        private static void ExecuteMainPass(RasterCommandBuffer cmd, Material material, RTHandle copiedColor)
        {
            s_SharedPropertyBlock.Clear();
            if (copiedColor != null)
                s_SharedPropertyBlock.SetTexture(m_BlitTextureID, copiedColor);

            // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
            s_SharedPropertyBlock.SetVector(m_BlitScaleBiasID, new Vector4(1, 1, 0, 0));

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
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
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                // Setting output attachment
                builder.SetRenderAttachment(copiedColorTexture, 0, AccessFlags.Write);

                // Execute step, simple copy
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    ExecuteCopyColorPass(rgContext.cmd, data.source);
                });
            }

            // Second blit with material, applying gray conversion
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName + "_FullScreenPass", out var passData, m_Sampler))
            {
                // Setting the temp color texture as the source for this pass
                passData.source = resourceData.activeColorTexture;
                // Setting the material
                passData.material = m_Material;

                // Setting input texture to sample
                builder.UseTexture(copiedColorTexture, AccessFlags.Read);
                // Setting output attachment
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                // Execute step, second blit with the gray scale conversion
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    ExecuteMainPass(rgContext.cmd, data.material, data.source);
                });
            }
        }
    }

    TintPass m_pass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_pass = new TintPass(passMaterial, name);
        m_pass.renderPassEvent = injectionPoint;
        m_pass.ConfigureInput(requirements);
    }

    public void OnEnable()
    {
        if(s_SharedPropertyBlock == null)
            s_SharedPropertyBlock = new MaterialPropertyBlock();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_pass);
    }
}
