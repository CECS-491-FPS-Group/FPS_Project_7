using UnityEngine;

public static class HeightMapGenerator {

	public static HeightMap GenerateHeightMap(int width, int height, HeightMapSettings settings, HeightMapContext context) {
		float[,] values = Noise.GenerateNoiseMap (width, height, settings.noiseSettings, context);

		AnimationCurve heightCurve_threadsafe = new AnimationCurve (settings.heightCurve.keys);

		bool applyFalloff = settings.useFalloff && context.Falloff.Enabled;
		LayoutCarver carver = context.Layout != null ? new LayoutCarver (context.Layout) : null;
		float[,] surfaceMask = carver != null ? new float[width, height] : null;

		float minValue = float.MaxValue;
		float maxValue = float.MinValue;

		for (int i = 0; i < width; i++) {
			for (int j = 0; j < height; j++) {
				Vector2 world = context.IndexToWorld (i, j);
				float falloff = applyFalloff ? context.Falloff.Evaluate (world) : 0f;

				float value = TerrainHeightField.Combine (values [i, j], falloff, applyFalloff, heightCurve_threadsafe, settings.heightMultiplier);

				if (carver != null) {
					float mask;
					value = carver.Apply (world, value, out mask);
					surfaceMask [i, j] = mask;
				}

				values [i, j] = value;

				if (value > maxValue) {
					maxValue = value;
				}
				if (value < minValue) {
					minValue = value;
				}
			}
		}

		return new HeightMap (values, surfaceMask, minValue, maxValue);
	}

}

public struct HeightMap {
	public readonly float[,] values;
	/// <summary>1 where the layout carved a road or building pad, 0 on natural terrain. Null when there is no layout.</summary>
	public readonly float[,] surfaceMask;
	public readonly float minValue;
	public readonly float maxValue;

	public HeightMap (float[,] values, float minValue, float maxValue)
		: this (values, null, minValue, maxValue)
	{
	}

	public HeightMap (float[,] values, float[,] surfaceMask, float minValue, float maxValue)
	{
		this.values = values;
		this.surfaceMask = surfaceMask;
		this.minValue = minValue;
		this.maxValue = maxValue;
	}
}
