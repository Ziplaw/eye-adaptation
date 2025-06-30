using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class AverageLuminanceComputePass : LuminanceComputePass
{
	private readonly ComputeShader _averageLuminanceComputeShader;
	private RTHandle[] _luminanceTextures;
	private int _sizeReduction = 4;

	public class LuminanceFrameData : ContextItem
	{
		public TextureHandle[] luminanceTextures;

		public override void Reset( )
		{
			luminanceTextures = null;
		}
	}

	// This class stores the data needed by the RenderGraph pass.
	// It is passed as a parameter to the delegate function that executes the RenderGraph pass.
	private class PassData
	{
		public ComputeShader computeShader;
		public int cameraKernelIndex;
		public int luminanceMipsKernelIndex;
		public TextureHandle cameraTexture;
		public Vector2Int colorTextureSize;
		public TextureHandle[] luminanceHandles;
	}

	public AverageLuminanceComputePass( ComputeShader averageLuminanceComputeShader, int sizeReduction )
	{
		_averageLuminanceComputeShader = averageLuminanceComputeShader;
		_sizeReduction = sizeReduction;

		SetupLuminanceMips( );
	}

	void SetupLuminanceMips( )
	{
		var w = Screen.width;
		var h = Screen.height;
		var d = Mathf.Max( w, h );

		var divisions = Mathf.FloorToInt( Mathf.Log( d, _sizeReduction ) ) + 1;

		_luminanceTextures = new RTHandle[divisions];

		var xSize = w;
		var ySize = h;

		for ( var i = 0; i < _luminanceTextures.Length; i++ )
		{
			xSize = Mathf.Max( 1, xSize / _sizeReduction );
			ySize = Mathf.Max( 1, ySize / _sizeReduction );

			var descriptor = new RenderTextureDescriptor( xSize, ySize, GraphicsFormat.R32_SFloat, 0 )
			{
				enableRandomWrite = true,
				msaaSamples = 1,
				sRGB = false,
				useMipMap = false
			};

			RenderingUtils.ReAllocateHandleIfNeeded( ref _luminanceTextures[i], descriptor, name: $"LuminanceTexture_{i}" );
		}
	}

	// RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
	// FrameData is a context container through which URP resources can be accessed and managed.
	public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData )
	{
		if ( _luminanceTextures == null || _luminanceTextures.Length == 0 || _luminanceTextures[0].rt.width != Screen.width / _sizeReduction || _luminanceTextures[0].rt.height != Screen.height / _sizeReduction )
		{
			SetupLuminanceMips( );
		}

		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>( );

		// This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
		using ( var builder = renderGraph.AddComputePass<PassData>( "Average Luminance Compute", out var passData ) )
		{
			var activeColorDescriptor = resourceData.activeColorTexture.GetDescriptor( renderGraph );
			TextureHandle[] luminanceHandles = new TextureHandle[_luminanceTextures.Length];

			for ( var i = 0; i < _luminanceTextures.Length; i++ )
			{
				luminanceHandles[i] = renderGraph.ImportTexture( _luminanceTextures[i] );
				builder.UseTexture( luminanceHandles[i], AccessFlags.Write );
			}

			passData.cameraTexture = resourceData.activeColorTexture;
			passData.computeShader = _averageLuminanceComputeShader;
			passData.cameraKernelIndex = _averageLuminanceComputeShader.FindKernel( "luminance" );
			passData.luminanceMipsKernelIndex = _averageLuminanceComputeShader.FindKernel( "luminance_mips" );
			passData.colorTextureSize = new Vector2Int( activeColorDescriptor.width, activeColorDescriptor.height );
			passData.luminanceHandles = luminanceHandles;

			var customData = frameData.Create<LuminanceFrameData>( );
			customData.luminanceTextures = luminanceHandles;

			builder.UseTexture( resourceData.activeColorTexture, AccessFlags.Read );

			builder.SetRenderFunc( ( PassData data, ComputeGraphContext context ) =>
			{
				var textureSize = new Vector2Int( data.colorTextureSize.x, data.colorTextureSize.y );

				context.cmd.SetComputeFloatParam( data.computeShader, "_Size", _sizeReduction );
				context.cmd.SetComputeVectorParam( data.computeShader, "_PreviousTextureSize", (Vector2)textureSize );
				context.cmd.SetComputeTextureParam( data.computeShader, data.cameraKernelIndex, "_CameraTexture", data.cameraTexture );
				context.cmd.SetComputeTextureParam( data.computeShader, data.cameraKernelIndex, "_LuminanceTexture", data.luminanceHandles[0] );

				context.cmd.DispatchCompute( data.computeShader, data.cameraKernelIndex, textureSize.x / 8, textureSize.y / 8, 1 );

				for ( var i = 1; i < luminanceHandles.Length; i++ )
				{
					textureSize /= _sizeReduction;

					context.cmd.SetComputeVectorParam( data.computeShader, "_PreviousTextureSize", (Vector2)textureSize );
					context.cmd.SetComputeTextureParam( data.computeShader, data.luminanceMipsKernelIndex, "_PreviousLuminanceTexture", data.luminanceHandles[i - 1] );
					context.cmd.SetComputeTextureParam( data.computeShader, data.luminanceMipsKernelIndex, "_LuminanceTexture", data.luminanceHandles[i] );

					context.cmd.DispatchCompute( data.computeShader, data.luminanceMipsKernelIndex, Mathf.Max( 1, textureSize.x / 8 ), Mathf.Max( 1, textureSize.y / 8 ), 1 );
				}
			} );
		}
	}

	public override void Cleanup( )
	{
		for ( var i = 0; i < _luminanceTextures.Length; i++ )
		{
			_luminanceTextures[i]?.Release( );
			_luminanceTextures[i] = null;
		}

		_luminanceTextures = null;
	}
}