using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EyeAdaptationFeature : ScriptableRendererFeature
{
    [Header("Config")]
    [SerializeField] private ComputeShader averageLuminanceComputeShader;
    [SerializeField] private Material _eyeAdaptationMaterial;
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
        _luminanceComputePass = new LuminanceComputePass(averageLuminanceComputeShader, sizeReduction);
        _debugLuminancePass = new DebugLuminancePass( luminanceLevelToVisualize );
        _applyEyeAdaptationPass = new ApplyEyeAdaptationPass( _eyeAdaptationMaterial, targetLuminance );

        // Configures where the render pass should be injected.
        _luminanceComputePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        _debugLuminancePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        _applyEyeAdaptationPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if ( !SystemInfo.supportsComputeShaders || averageLuminanceComputeShader == null || _eyeAdaptationMaterial == null) return;
        
        renderer.EnqueuePass(_luminanceComputePass);
        renderer.EnqueuePass(_applyEyeAdaptationPass);
        if(debugLuminance) renderer.EnqueuePass(_debugLuminancePass);
    }

    protected override void Dispose( bool disposing )
    {
        _luminanceComputePass?.Cleanup(  );
    }
}
