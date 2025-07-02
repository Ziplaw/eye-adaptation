using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class DebugHistogramLuminanceComputePass : ScriptableRenderPass
{
	private readonly ComputeShader _debugHistogramComputeShader;
	private RTHandle _debugTextureHandle;

	public DebugHistogramLuminanceComputePass(ComputeShader debugHistogramComputeShader)
	{
		_debugHistogramComputeShader = debugHistogramComputeShader;
		RenderTextureDescriptor desc = new RenderTextureDescriptor
		(
			ComputeLuminanceHistogramPrePass.k_Bins,
			ComputeLuminanceHistogramPrePass.k_Bins,
			GraphicsFormat.R32_SFloat,
			0,
			1)
		{
			enableRandomWrite = true,
			msaaSamples = 1,
			sRGB = false,
			useMipMap = false
		};

		RenderingUtils.ReAllocateHandleIfNeeded(ref _debugTextureHandle, desc,name:"_DebugHistogram");
	}
	
	private class PassData
	{
		public BufferHandle histogramHandle;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );

		var logHistogramFrameData = frameData.Get<ComputeLuminanceHistogramPrePass.LuminanceHistogramFrameData>();
		var textureHandle = renderGraph.ImportTexture(_debugTextureHandle);

		using (var builder = renderGraph.AddComputePass("Apply Luminance Histogram", out PassData passData))
		{
			builder.UseBuffer(logHistogramFrameData.histogramHandle);
			builder.UseTexture(textureHandle, AccessFlags.Write);

			passData.histogramHandle = logHistogramFrameData.histogramHandle;
			
			builder.SetRenderFunc<PassData>((data, context) =>
			{
				context.cmd.SetComputeBufferParam(_debugHistogramComputeShader,0,"_Histogram",data.histogramHandle);
				context.cmd.SetComputeTextureParam(_debugHistogramComputeShader,0,"_Output",textureHandle);
				context.cmd.DispatchCompute(_debugHistogramComputeShader,0,ComputeLuminanceHistogramPrePass.k_Bins/8,ComputeLuminanceHistogramPrePass.k_Bins/8,1);
			});
		}

		renderGraph.AddBlitPass(textureHandle, resourceData.cameraColor,Vector2.one, Vector2.zero);
	}
}