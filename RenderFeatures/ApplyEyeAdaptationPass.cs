using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

class ApplyEyeAdaptationPass : ScriptableRenderPass
{
	private readonly Material _eyeAdaptationMaterial;
	private readonly float _targetLuminance;
	
	private static readonly int TargetEyeAdaptation = Shader.PropertyToID( "_TargetEyeAdaptation" );

	public ApplyEyeAdaptationPass( Material eyeAdaptationMaterial, float targetLuminance )
	{
		_eyeAdaptationMaterial = eyeAdaptationMaterial;
		_targetLuminance = targetLuminance;
	}

	public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData )
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );

		AverageLuminanceComputePass.LuminanceFrameData luminanceFrameData = frameData.Get<AverageLuminanceComputePass.LuminanceFrameData>( );

		_eyeAdaptationMaterial.SetFloat( TargetEyeAdaptation, _targetLuminance ); 
		
		RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters( luminanceFrameData.luminanceTextures[^1], resourceData.cameraColor,_eyeAdaptationMaterial,0 );
		renderGraph.AddBlitPass(blitParameters, passName:"Apply Eye Adaptation" );
	}
}