Shader "ApplyEyeAdaptation"
{
   SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
       ZWrite Off Cull Off
       Pass
       {
           Name "BlitWithMaterialPass"

           HLSLPROGRAM
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
           #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

           #pragma vertex Vert
           #pragma fragment Frag

           TEXTURE2D_X(_CameraOpaqueTexture);
           float _TargetEyeAdaptation;

           half GammaCorrect_half(half linearColor)
           {
               return pow(linearColor,1/2.2);
           }
           
           // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
           float4 Frag(Varyings input) : SV_Target0
           {
               // this is needed so we account XR platform differences in how they handle texture arrays
               UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

               // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
               float2 uv = input.texcoord.xy;
               half4 color = SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
               float luminance = GammaCorrect_half(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, float2(0,0), _BlitMipLevel).x) ;
               
               
               // Modify the sampled color
               return color + ( _TargetEyeAdaptation - luminance);
           }

           ENDHLSL
       }
   }
}