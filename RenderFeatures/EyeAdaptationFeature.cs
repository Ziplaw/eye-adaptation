using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class EyeAdaptationFeature : ScriptableRendererFeature
{
    public enum EyeAdaptationTechnique {AverageLuminance, Histogram}

    [Header("Config"), SerializeField] private EyeAdaptationTechnique technique;
    [SerializeField] private ComputeShader luminanceComputeShader;
    [SerializeField] private Material eyeAdaptationMaterial;
    [SerializeField, Range(4,128)] private int sizeReduction = 4;
    [SerializeField, Range(0,1)] private float targetLuminance = .5f;
    
    [SerializeField, Header( "Debug" )] private bool debugLuminance;
    [SerializeField, Range(0,.9999f)] private float luminanceLevelToVisualize = 0;


    LuminanceComputePass _luminanceComputePass;
    DebugLuminancePass _debugLuminancePass;
    ApplyEyeAdaptationPass _applyEyeAdaptationPass;

    /// <inheritdoc/>
    public override void Create()
    { 
        _luminanceComputePass = technique switch
        {
            EyeAdaptationTechnique.AverageLuminance => new AverageLuminanceComputePass(luminanceComputeShader, sizeReduction),
            EyeAdaptationTechnique.Histogram => new HistogramLuminanceComputePass(luminanceComputeShader),
            _ => throw new ArgumentOutOfRangeException()
        };
        _debugLuminancePass = new DebugLuminancePass( luminanceLevelToVisualize );
        _applyEyeAdaptationPass = new ApplyEyeAdaptationPass( eyeAdaptationMaterial, targetLuminance );

        // Configures where the render pass should be injected.
        _luminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        _debugLuminancePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        _applyEyeAdaptationPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || !SystemInfo.supportsComputeShaders || luminanceComputeShader == null || eyeAdaptationMaterial == null) return;
        
        renderer.EnqueuePass(_luminanceComputePass);

        switch (technique)
        {
            case EyeAdaptationTechnique.AverageLuminance:
                renderer.EnqueuePass(_applyEyeAdaptationPass);
                if(debugLuminance) renderer.EnqueuePass(_debugLuminancePass);
                break;
            case EyeAdaptationTechnique.Histogram:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void Dispose( bool disposing )
    {
        _luminanceComputePass?.Cleanup(  );
    }
}
