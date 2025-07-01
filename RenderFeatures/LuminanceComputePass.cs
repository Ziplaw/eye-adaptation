using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LuminanceComputePass : ScriptableRenderPass
{
	public const int rangeMin = -9; // ev
	public const int rangeMax = 9; // ev
	
	protected Vector4 GetHistogramScaleOffsetRes()
	{
		float diff = rangeMax - rangeMin;
		float scale = 1f / diff;
		float offset = -rangeMin * scale;
		return new Vector4(scale, offset, Screen.width, Screen.height);
	}
	
	public virtual void Cleanup()
	{
		
	}

}