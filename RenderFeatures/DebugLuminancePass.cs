using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

class DebugLuminancePass : ScriptableRenderPass
{
	private readonly float _luminanceToBlit;

	public DebugLuminancePass( float luminanceToBlit )
	{
		_luminanceToBlit = luminanceToBlit;
	}
	// RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
	// FrameData is a context container through which URP resources can be accessed and managed.
	public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData )
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );

		AverageLuminanceComputePass.LuminanceFrameData luminanceFrameData = frameData.Get<AverageLuminanceComputePass.LuminanceFrameData>( );

		var index = Mathf.FloorToInt( luminanceFrameData.luminanceTextures.Length * _luminanceToBlit );

		renderGraph.AddBlitPass( luminanceFrameData.luminanceTextures[index], resourceData.cameraColor, Vector2.one, Vector2.zero,passName:"Debug Luminance" );
	}
}