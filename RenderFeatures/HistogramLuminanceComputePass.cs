using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class HistogramLuminanceComputePass : LuminanceComputePass
{
	private readonly ComputeShader _luminanceComputeShader;
	private GraphicsBuffer _buffer;
	
	public class HistogramLuminanceFrameData : ContextItem
	{
		public BufferHandle graphicsBuffer;

		public override void Reset( )
		{
			graphicsBuffer = BufferHandle.nullHandle;
		}
	}

	public HistogramLuminanceComputePass(ComputeShader luminanceComputeShader)
	{
		_luminanceComputeShader = luminanceComputeShader;
		
		_buffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured,1024, sizeof(int));
		_buffer.SetData(new int[1024]);
	}
	
	private class PassData
	{
		public BufferHandle bufferHandle;
	}

	public override async void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		using var builder = renderGraph.AddComputePass<PassData>("HistogramLuminanceCompute",out var data);

		var bufferHandle = renderGraph.ImportBuffer(_buffer);
		builder.UseBuffer(bufferHandle, AccessFlags.Write);
		
		var histogramLuminanceFrameData = frameData.Create<HistogramLuminanceFrameData>();
		histogramLuminanceFrameData.graphicsBuffer = bufferHandle;

		data.bufferHandle = bufferHandle;

		builder.SetRenderFunc<PassData>((passData, context) =>
		{
			context.cmd.SetComputeBufferParam(_luminanceComputeShader, _luminanceComputeShader.FindKernel("luminance_histogram"),"test_output",passData.bufferHandle);
			context.cmd.DispatchCompute(_luminanceComputeShader, 0,1024/8,1,1);
		});
	}

	public override void Cleanup() 
	{
		_buffer?.Release();
		_buffer = null;
	}
}