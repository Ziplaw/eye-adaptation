using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class EyeAdaptationFeature : ScriptableRendererFeature
{
    public enum EyeAdaptationType {Fixed, Progressive}
    [Header("Config")]
    [MinMax(1f, 99f), SerializeField] private Vector2 filtering = new(0,99); 
    [MinMax(-9, 9), SerializeField] private float minEV = -9; 
    [MinMax(-9, 9), SerializeField] private float maxEV = 9;
    [SerializeField] private EyeAdaptationType _eyeAdaptationType;
    [SerializeField] private Material eyeAdaptationMaterial;
    [SerializeField] private ComputeShader _computeHistogramComputeShader;
    [SerializeField] private ComputeShader _debugHistogramComputeShader;
    [SerializeField] private ComputeShader _autoExposureComputeShader;
    [SerializeField] private float _speedUp = 5;
    [SerializeField] private float _speedDown = 5;
    [SerializeField, Range(-9,9)] private float _exposureCompensation;
    
    [SerializeField, Header( "Debug" )] private bool debugLuminance;


    private ComputeLuminanceHistogramPrePass _computeLuminanceHistogramPrePassPrePass;
    private ApplyHistogramAutoExposurePass _applyHistogramAutoExposurePass;
    private DebugHistogramLuminanceComputePass _debugHistogramLuminanceComputePass;


    //TODO this would benefit from Strategy Pattern, leaving as switch statements for now
    public override void Create()
    {
        _computeLuminanceHistogramPrePassPrePass = new ComputeLuminanceHistogramPrePass(_computeHistogramComputeShader);
        _applyHistogramAutoExposurePass = new ApplyHistogramAutoExposurePass(_autoExposureComputeShader, filtering, new Vector2(minEV, maxEV), _eyeAdaptationType, _speedUp, _speedDown, _exposureCompensation, eyeAdaptationMaterial);
        _debugHistogramLuminanceComputePass = new DebugHistogramLuminanceComputePass(_debugHistogramComputeShader);

        _computeLuminanceHistogramPrePassPrePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        _applyHistogramAutoExposurePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        _debugHistogramLuminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || !SystemInfo.supportsComputeShaders || !_computeHistogramComputeShader || !_debugHistogramComputeShader || !_autoExposureComputeShader || !eyeAdaptationMaterial) return;

        renderer.EnqueuePass(_computeLuminanceHistogramPrePassPrePass);
        renderer.EnqueuePass(_applyHistogramAutoExposurePass);
        if (debugLuminance) renderer.EnqueuePass(_debugHistogramLuminanceComputePass);
    }

    protected override void Dispose( bool disposing )
    {
        _computeLuminanceHistogramPrePassPrePass?.Cleanup();
        _applyHistogramAutoExposurePass?.Cleanup(  );
    }
}
