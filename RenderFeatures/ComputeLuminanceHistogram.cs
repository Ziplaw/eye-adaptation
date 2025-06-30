using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ComputeLuminanceHistogram : LuminanceComputePass
{
    private readonly ComputeShader _exposureHistogramComputeShader;
    private GraphicsBuffer _histogramBuffer;
    public const int rangeMin = -9; // ev
    public const int rangeMax = 9; // ev
    public const int k_Bins = 128; // number of bins for the histogram

    public ComputeLuminanceHistogram(ComputeShader exposureHistogramComputeShader)
    {
        _exposureHistogramComputeShader = exposureHistogramComputeShader;
        _histogramBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured,k_Bins, sizeof(uint));
    }

    public class LuminanceHistogramFrameData : ContextItem
    {
        public BufferHandle histogramHandle;

        public override void Reset( )
        {
            histogramHandle = BufferHandle.nullHandle;
        }
    }

    private class PassData
    {
        public BufferHandle histogramHandle;
        public TextureHandle cameraColorHandle;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        using var builder = renderGraph.AddComputePass<PassData>("Compute Exposure Histogram", out var passData);

        var histogramHandle = renderGraph.ImportBuffer(_histogramBuffer);
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );
        
        builder.UseBuffer(histogramHandle,AccessFlags.Write);
        builder.UseTexture(resourceData.cameraColor);
        
        frameData.Create<LuminanceHistogramFrameData>().histogramHandle = histogramHandle;

        passData.histogramHandle = histogramHandle;
        passData.cameraColorHandle = resourceData.cameraColor;

        //based on Built-In PostProcessing/LogHistogram.cs
        builder.SetRenderFunc<PassData>((data, context) =>
        {
            var scaleOffsetRes = GetHistogramScaleOffsetRes();

            // Clear the buffer on every frame as we use it to accumulate luminance values on each frame
            int kernel = _exposureHistogramComputeShader.FindKernel("KEyeHistogramClear");
            context.cmd.SetComputeBufferParam(_exposureHistogramComputeShader, kernel, "_HistogramBuffer", data.histogramHandle);
            _exposureHistogramComputeShader.GetKernelThreadGroupSizes(kernel, out var threadX, out var threadY, out var threadZ);
            context.cmd.DispatchCompute(_exposureHistogramComputeShader, kernel, Mathf.CeilToInt(k_Bins / (float)threadX), 1, 1);

            // Get a log histogram
            kernel = _exposureHistogramComputeShader.FindKernel("KEyeHistogram");
            context.cmd.SetComputeBufferParam(_exposureHistogramComputeShader, kernel, "_HistogramBuffer", data.histogramHandle);
            context.cmd.SetComputeTextureParam(_exposureHistogramComputeShader, kernel, "_Source", data.cameraColorHandle);
            context.cmd.SetComputeVectorParam(_exposureHistogramComputeShader, "_ScaleOffsetRes", scaleOffsetRes);

            _exposureHistogramComputeShader.GetKernelThreadGroupSizes(kernel, out threadX, out threadY, out threadZ);

            context.cmd.DispatchCompute(_exposureHistogramComputeShader, kernel,
                Mathf.CeilToInt(scaleOffsetRes.z / 2f / threadX),
                Mathf.CeilToInt(scaleOffsetRes.w / 2f / threadY),
                1
            );
        });
    }
    
    public Vector4 GetHistogramScaleOffsetRes()
    {
        float diff = rangeMax - rangeMin;
        float scale = 1f / diff;
        float offset = -rangeMin * scale;
        return new Vector4(scale, offset, Screen.width, Screen.height);
    }

    public override void Cleanup()
    {
        _histogramBuffer?.Release();
        _histogramBuffer = null;
    }
}