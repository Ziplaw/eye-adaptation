using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class EyeAdaptationFeature : ScriptableRendererFeature
{
    public enum EyeAdaptationTechnique {AverageLuminance, Histogram}

    [Header("Config"), SerializeField] private EyeAdaptationTechnique technique;
    [SerializeField] private ComputeShader luminanceComputeShader;
    [SerializeField] private ComputeShader computeHistogramComputeShader;
    [SerializeField] private ComputeShader debugHistogramComputeShader;
    [SerializeField] private Material eyeAdaptationMaterial;
    [SerializeField, Range(4,128)] private int sizeReduction = 4;
    [SerializeField, Range(0,1)] private float targetLuminance = .5f;
    
    [SerializeField, Header( "Debug" )] private bool debugLuminance;
    [SerializeField, Range(0,.9999f)] private float luminanceLevelToVisualize = 0;


    private LuminanceComputePass _luminanceComputePass;
    private DebugLuminancePass _debugLuminancePass;
    private ApplyEyeAdaptationPass _applyEyeAdaptationPass;
    private ComputeLuminanceHistogram _computeLuminanceHistogramPrePass;

    //TODO this would benefit from Strategy Pattern, leaving as switch statements for now
    public override void Create()
    { 
        switch (technique)
        {
            case EyeAdaptationTechnique.AverageLuminance:
                _luminanceComputePass = new AverageLuminanceComputePass(luminanceComputeShader, sizeReduction);
                _debugLuminancePass = new DebugLuminancePass( luminanceLevelToVisualize );
                _applyEyeAdaptationPass = new ApplyEyeAdaptationPass( eyeAdaptationMaterial, targetLuminance );
                
                _luminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                _debugLuminancePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                _applyEyeAdaptationPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                break;
            case EyeAdaptationTechnique.Histogram:
                _computeLuminanceHistogramPrePass = new ComputeLuminanceHistogram(computeHistogramComputeShader);
                _luminanceComputePass = new DebugHistogramLuminanceComputePass(debugHistogramComputeShader);
                
                _computeLuminanceHistogramPrePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                _luminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || !SystemInfo.supportsComputeShaders || !computeHistogramComputeShader || !debugHistogramComputeShader || !luminanceComputeShader || !eyeAdaptationMaterial) return;
        

        switch (technique)
        {
            case EyeAdaptationTechnique.AverageLuminance:
                renderer.EnqueuePass(_luminanceComputePass);
                renderer.EnqueuePass(_applyEyeAdaptationPass);
                if(debugLuminance) renderer.EnqueuePass(_debugLuminancePass);
                break;
            case EyeAdaptationTechnique.Histogram:
                renderer.EnqueuePass(_computeLuminanceHistogramPrePass);
                renderer.EnqueuePass(_luminanceComputePass);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void Dispose( bool disposing )
    {
        _computeLuminanceHistogramPrePass?.Cleanup();
        _luminanceComputePass?.Cleanup(  );
    }
}
