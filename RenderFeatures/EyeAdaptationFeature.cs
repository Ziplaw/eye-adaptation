using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class EyeAdaptationFeature : ScriptableRendererFeature
{
    public enum EyeAdaptationTechnique {AverageLuminance, Histogram}
    public enum EyeAdaptationType {Fixed, Progressive}

    [Header("Config"), SerializeField] private EyeAdaptationTechnique technique;
    [Header("Histogram")]
    [MinMax(1f, 99f), SerializeField] private Vector2 filtering = new(0,99); 
    [MinMax(-9, 9), SerializeField] private float minEV = -9; 
    [MinMax(-9, 9), SerializeField] private float maxEV = 9;
    [SerializeField] private EyeAdaptationType _eyeAdaptationType;
    [SerializeField] private ComputeShader _computeHistogramComputeShader;
    [SerializeField] private ComputeShader _debugHistogramComputeShader;
    [SerializeField] private ComputeShader _autoExposureComputeShader;
    [SerializeField] private float _speedUp = 5;
    [SerializeField] private float _speedDown = 5;
    [SerializeField, Range(-9,9)] private float _exposureCompensation;
    [Header("Average Luminance")]
    [SerializeField] private ComputeShader luminanceComputeShader;
    [SerializeField] private Material eyeAdaptationMaterial;
    [SerializeField, Range(4,128)] private int sizeReduction = 4;
    [SerializeField, Range(0,1)] private float targetLuminance = .5f;
    
    [SerializeField, Header( "Debug" )] private bool debugLuminance;
    [SerializeField, Range(0,.9999f)] private float luminanceLevelToVisualize = 0;


    private LuminanceComputePass _luminanceComputePass;
    private DebugLuminancePass _debugLuminancePass;
    private ApplyEyeAdaptationPass _applyEyeAdaptationPass;
    private ComputeLuminanceHistogramPrePass _computeLuminanceHistogramPrePassPrePass;
    private DebugHistogramLuminanceComputePass _debugHistogramLuminanceComputePass;


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
                _computeLuminanceHistogramPrePassPrePass = new ComputeLuminanceHistogramPrePass(_computeHistogramComputeShader);
                _luminanceComputePass = new ApplyHistogramAutoExposurePass(_autoExposureComputeShader,filtering,new Vector2(minEV,maxEV),_eyeAdaptationType,_speedUp, _speedDown, _exposureCompensation, eyeAdaptationMaterial);
                _debugHistogramLuminanceComputePass = new DebugHistogramLuminanceComputePass(_debugHistogramComputeShader);
                
                _computeLuminanceHistogramPrePassPrePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                _luminanceComputePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                _debugHistogramLuminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || !SystemInfo.supportsComputeShaders || !_computeHistogramComputeShader || !_debugHistogramComputeShader || !_autoExposureComputeShader || !luminanceComputeShader || !eyeAdaptationMaterial) return;
        

        switch (technique)
        {
            case EyeAdaptationTechnique.AverageLuminance:
                renderer.EnqueuePass(_luminanceComputePass);
                renderer.EnqueuePass(_applyEyeAdaptationPass);
                if(debugLuminance) renderer.EnqueuePass(_debugLuminancePass);
                break;
            case EyeAdaptationTechnique.Histogram:
                renderer.EnqueuePass(_computeLuminanceHistogramPrePassPrePass);
                renderer.EnqueuePass(_luminanceComputePass);
                if(debugLuminance) renderer.EnqueuePass(_debugHistogramLuminanceComputePass);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void Dispose( bool disposing )
    {
        _computeLuminanceHistogramPrePassPrePass?.Cleanup();
        _luminanceComputePass?.Cleanup(  );
    }
}
