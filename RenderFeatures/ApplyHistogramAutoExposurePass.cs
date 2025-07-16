using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class ApplyHistogramAutoExposurePass : ScriptableRenderPass
{
    private static readonly int HistogramLuminanceTexture = Shader.PropertyToID( "_HistogramLuminanceTexture" );
    
    private int m_AutoExposurePingPong;

    private readonly ComputeShader _autoExposureComputeShader;
    private readonly Vector2 _filteringMinMax;
    private readonly Vector2 _minMaxLuminance;
    private readonly EyeAdaptationFeature.EyeAdaptationType _adaptationType;
    private readonly float _speedUp;
    private readonly float _speedDown;
    private readonly float _exposureCompensation;
    private readonly Material _eyeAdaptationMaterial;
    private RTHandle[] _autoExposureHandles;
    private TextureHandle _currentTexture;

    const int k_NumAutoExposureTextures = 2;

    
    public ApplyHistogramAutoExposurePass(ComputeShader autoExposureComputeShader, Vector2 filteringMinMax, Vector2 minMaxLuminance, EyeAdaptationFeature.EyeAdaptationType adaptationType, float speedUp, float speedDown, float exposureCompensation, Material eyeAdaptationMaterial)
    {
        _autoExposureComputeShader = autoExposureComputeShader;
        _filteringMinMax = filteringMinMax;
        _minMaxLuminance = minMaxLuminance;
        _adaptationType = adaptationType;
        _speedUp = speedUp;
        _speedDown = speedDown;
        _exposureCompensation = exposureCompensation;
        _eyeAdaptationMaterial = eyeAdaptationMaterial;

        var desc = new RenderTextureDescriptor
        (
            1,
            1,
            GraphicsFormat.R32_SFloat,
            0,
            1)
        {
            enableRandomWrite = true,
            msaaSamples = 1,
            useMipMap = false
        };

        _autoExposureHandles = new RTHandle[k_NumAutoExposureTextures];

        for (var i = 0; i < _autoExposureHandles.Length; i++)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(ref _autoExposureHandles[i], desc, name: $"AutoExposure_{i}");
        }
    }

    public class ApplyHistogramData : ContextItem
    {
        public TextureHandle eyeAdaptationTexture;
        
        public override void Reset()
        {
            eyeAdaptationTexture = TextureHandle.nullHandle;
        }
    }

    private class PassData
    {
        public TextureHandle[] textureHandles;
        public BufferHandle histogramHandle;
        public float lowPercent;
        public float highPercent;
        public float speedDown;
        public float speedUp;
        public Vector2 minMaxLuminance;
        public float exposureCompensation;
        public TextureHandle source;
        public TextureHandle destination;
    }
    
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );

        // Make sure filtering values are correct to avoid apocalyptic consequences
        float lowPercent = _filteringMinMax.x;
        float highPercent = _filteringMinMax.y;
        const float kMinDelta = 1e-2f;
        highPercent = Mathf.Clamp(highPercent, 1f + kMinDelta, 99f);
        lowPercent = Mathf.Clamp(lowPercent, 1f, highPercent - kMinDelta);

        // Clamp min/max adaptation values as well
        float minLum = _minMaxLuminance.x;
        float maxLum = _minMaxLuminance.y;
        minLum = Mathf.Min(minLum, maxLum);
        maxLum = Mathf.Max(minLum, maxLum);

        string adaptation = null;

        if (_adaptationType == EyeAdaptationFeature.EyeAdaptationType.Fixed)
            adaptation = "KAutoExposureAvgLuminance_fixed";
        else
            adaptation = "KAutoExposureAvgLuminance_progressive";

        var compute = _autoExposureComputeShader;
        int kernel = compute.FindKernel(adaptation);

        
        using (var builder = renderGraph.AddComputePass("ApplyHistogramAutoExposurePass",out PassData passData))
        {
            var histogramFrameData = frameData.Get<ComputeLuminanceHistogramPrePass.LuminanceHistogramFrameData>();

            passData.textureHandles = new TextureHandle[_autoExposureHandles.Length];
            
            for (var i = 0; i < _autoExposureHandles.Length; i++)
            {
                var rthandle = _autoExposureHandles[i];
                var handle = renderGraph.ImportTexture(rthandle);
                builder.UseTexture(handle,AccessFlags.ReadWrite); // this could be optimized as it pingpongs from source to destination
                passData.textureHandles[i] = handle;
            }
            
            builder.UseBuffer(histogramFrameData.histogramHandle);

            passData.histogramHandle = histogramFrameData.histogramHandle;

            passData.lowPercent = lowPercent;
            passData.highPercent = highPercent;
            passData.speedDown = Mathf.Max(0,_speedDown);
            passData.speedUp = Mathf.Max(0,_speedUp);
            passData.minMaxLuminance = new Vector2(minLum,maxLum);
            passData.exposureCompensation = _exposureCompensation;
            
            int pp = m_AutoExposurePingPong;
            var src = passData.textureHandles[++pp % 2];
            var dst = passData.textureHandles[++pp % 2];
            

            passData.source = src;
            passData.destination = dst;

            _currentTexture = dst;
            
            builder.SetRenderFunc<PassData>((data, ctx) =>
            {
                ctx.cmd.SetComputeBufferParam(compute, kernel, "_HistogramBuffer", data.histogramHandle);
                ctx.cmd.SetComputeVectorParam(compute, "_Params1", new Vector4(data.lowPercent * 0.01f, data.highPercent * 0.01f, Mathf.Pow(2,data.minMaxLuminance.x), Mathf.Pow(2,data.minMaxLuminance.y)));
                ctx.cmd.SetComputeVectorParam(compute, "_Params2", new Vector4(data.speedDown, data.speedUp, data.exposureCompensation, Time.deltaTime));
                ctx.cmd.SetComputeVectorParam(compute, "_ScaleOffsetRes", LuminanceHistogramUtils.GetHistogramScaleOffsetRes());

                ctx.cmd.SetComputeTextureParam(compute, kernel, "_Source", data.source);
                ctx.cmd.SetComputeTextureParam(compute, kernel, "_Destination", data.destination);
                ctx.cmd.DispatchCompute(compute, kernel, 1, 1, 1);

                m_AutoExposurePingPong = ++pp % 2;
            });
            
        }

        RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters( _currentTexture, resourceData.cameraColor,_eyeAdaptationMaterial,0 );
        renderGraph.AddBlitPass(blitParameters, passName:"Apply Eye Adaptation" );
        
        // renderGraph.AddBlitPass(_currentTexture, resourceData.cameraColor,Vector2.one, Vector2.zero);
    }

    public void Cleanup()
    {
        if (_autoExposureHandles != null)
        {
            foreach (var autoExposureHandle in _autoExposureHandles)
            {
                autoExposureHandle?.Release();
            }
            
            _autoExposureHandles = null;
        }

    }
}