using System;
using UnityEngine;

/// <summary>
/// Simple serializable range with two floats.
/// Displayed via a custom PropertyDrawer using UI Toolkit.
/// </summary>
[Serializable]
public struct Range
{
  /// <summary>
  /// Lower bound of the range. No implicit clamping here; the drawer enforces min <= max.
  /// </summary>
  public float min;

  /// <summary>
  /// Upper bound of the range. The drawer enforces max >= min.
  /// </summary>
  public float max;
}

/// <summary>
/// Example MonoBehaviour that exposes a Range in the Inspector so you can interact with the custom drawer.
/// </summary>
public class RangeUser : MonoBehaviour
{
  [Header("Demo Range")]
  public Range damage;

  [Tooltip("Example Text")]
  public string note = "Editable field";
}
