using UnityEngine;

public static class ColorHelpers
{

  /// <summary>
  /// Generates a bright, non-gray color.
  /// Strategy:
  ///  - Pick a random Hue across the full spectrum [0,1).
  ///  - Force high Saturation to avoid washed-out grays.
  ///  - Force high Value (brightness) to avoid dark colors.
  ///  - Convert HSV -> RGB using Unity's built-in utility.
  ///
  /// You can tweak the thresholds if you want even punchier colors.
  /// </summary>
  /// <param name="minSaturation">Lower bound for saturation (avoid gray). Recommended: 0.6–0.8.</param>
  /// <param name="minValue">Lower bound for value/brightness (avoid dark). Recommended: 0.8–0.95.</param>
  public static Color RandomBrightColor(float minSaturation = 0.65f, float minValue = 0.85f)
  {
    minSaturation = Mathf.Clamp01(minSaturation);
    minValue = Mathf.Clamp01(minValue);

    float h = Random.value;                            
    float s = Random.Range(minSaturation, 1.0f);       
    float v = Random.Range(minValue, 1.0f);            

    Color c = Color.HSVToRGB(h, s, v, false);

    if (GetSaturationApprox(c) < minSaturation || GetPerceivedLuminance(c) < minValue)
    {
      s = Mathf.Max(s, Mathf.Min(0.9f, minSaturation + 0.1f));
      v = Mathf.Max(v, Mathf.Min(0.95f, minValue + 0.1f));
      c = Color.HSVToRGB(h, s, v, false);
    }

    return c;
  }

  private static float GetSaturationApprox(Color c)
  {
    float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
    if (max <= 0.0001f)
    {
      return 0.0f;
    }
    return (max - min) / max;
  }

  private static float GetPerceivedLuminance(Color c)
  {
    float r = Mathf.GammaToLinearSpace(c.r);
    float g = Mathf.GammaToLinearSpace(c.g);
    float b = Mathf.GammaToLinearSpace(c.b);

    float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
    return Mathf.Clamp01(y); // Keep it in [0,1]
  }

}
