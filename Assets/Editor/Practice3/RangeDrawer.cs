using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom drawer for a Range struct { float min; float max; } using UI Toolkit.
/// Layout: single row with a MinMaxSlider and two FloatFields ("Min", "Max").
/// Synchronization rules:
///  - Slider updates write back to both serialized properties (supports multi-object edit).
///  - FloatFields are bound to properties; slider is nudged to match on changes/undo.
/// Layout fixes:
///  - Row uses 'gap' spacing.
///  - FloatFields have flexShrink = 0 (so they don't collapse in narrow inspectors).
///  - Label elements get a fixed width so "Min"/"Max" text doesn't clip.
/// Undo:
///  - Uses Undo.RecordObjects when the slider changes.
/// </summary>
[CustomPropertyDrawer(typeof(Range))]
public class RangeDrawer : PropertyDrawer
{
  // Demo limits for the slider. If you need per-field limits, consider a custom attribute.
  private const float DefaultLowLimit = -1000f;
  private const float DefaultHighLimit = 1000f;

  public override VisualElement CreatePropertyGUI(SerializedProperty property)
  {
    // Child properties
    SerializedProperty minProp = property.FindPropertyRelative("min");
    SerializedProperty maxProp = property.FindPropertyRelative("max");

    // Root
    var root = new VisualElement();
    {
      root.style.flexDirection = FlexDirection.Column;
      root.style.marginTop = 2;
      root.style.marginBottom = 2;
      root.style.marginLeft = 2;
      root.style.marginRight = 2;
      root.AddToClassList("swim-range-root");
    }

    // Group box (simple border)
    var group = new VisualElement();
    {
      group.style.flexDirection = FlexDirection.Column;
      group.style.borderTopWidth = 1;
      group.style.borderBottomWidth = 1;
      group.style.borderLeftWidth = 1;
      group.style.borderRightWidth = 1;
      var border = new Color(0, 0, 0, 0.2f);
      group.style.borderTopColor = border;
      group.style.borderBottomColor = border;
      group.style.borderLeftColor = border;
      group.style.borderRightColor = border;
      group.style.paddingLeft = 6;
      group.style.paddingRight = 6;
      group.style.paddingTop = 4;
      group.style.paddingBottom = 6;
      group.style.marginBottom = 2;
      group.AddToClassList("swim-range-group");
      root.Add(group);
    }

    // Title
    var titleRow = new VisualElement();
    {
      titleRow.style.flexDirection = FlexDirection.Row;
      titleRow.style.alignItems = Align.Center;

      var title = new Label(ObjectNames.NicifyVariableName(property.name));
      title.style.unityFontStyleAndWeight = FontStyle.Bold;
      title.style.marginRight = 6;

      titleRow.Add(title);
      group.Add(titleRow);
    }

    // Main row
    var row = new VisualElement();
    {
      row.style.flexDirection = FlexDirection.Row;
      row.style.alignItems = Align.Center;
      row.style.paddingLeft = 6;
      // row.style.gap = 6; // space between slider and fields so elements don't collide
      row.AddToClassList("swim-range-row");
      group.Add(row);
    }

    // Slider (takes remaining space)
    float currentMin = minProp.floatValue;
    float currentMax = maxProp.floatValue;
    var slider = new MinMaxSlider(currentMin, currentMax, DefaultLowLimit, DefaultHighLimit);
    {
      slider.style.flexGrow = 1;     // let slider expand
      slider.style.marginTop = 2;
      slider.style.marginBottom = 2;
      row.Add(slider);
    }

    // Min field (fixed width; label width fixed to avoid clipping)
    var minField = new FloatField("Min");
    {
      minField.style.width = 120;
      minField.style.marginLeft = 2;
      minField.style.marginRight = 2;
      minField.style.flexShrink = 0; // do not allow collapse on narrow inspectors

      // Keep label readable; prevent "Mi" clipping
      minField.labelElement.style.width = 32;
      minField.labelElement.style.minWidth = 32;
      minField.labelElement.style.unityTextAlign = TextAnchor.MiddleRight;

      minField.AddToClassList("swim-range-min");
      row.Add(minField);
    }

    // Max field (same treatment as Min)
    var maxField = new FloatField("Max");
    {
      maxField.style.width = 120;
      maxField.style.marginLeft = 2;
      maxField.style.marginRight = 2;
      maxField.style.flexShrink = 0;

      maxField.labelElement.style.width = 32;
      maxField.labelElement.style.minWidth = 32;
      maxField.labelElement.style.unityTextAlign = TextAnchor.MiddleRight;

      maxField.AddToClassList("swim-range-max");
      row.Add(maxField);
    }

    // Bind float fields to properties; slider is manually synced.
    minField.bindingPath = "min";
    maxField.bindingPath = "max";
    root.Bind(property.serializedObject);

    // --- Synchronization helpers ---

    // Pulls min/max from properties and applies to the slider (without re-entrancy).
    void UpdateSliderFromProps()
    {
      float minVal = minProp.floatValue;
      float maxVal = maxProp.floatValue;

      if (maxVal < minVal)
      {
        float t = minVal;
        minVal = maxVal;
        maxVal = t;
      }

      slider.SetValueWithoutNotify(new Vector2(minVal, maxVal));
    }

    // When slider moves, write back to all selected objects' properties with Undo.
    slider.RegisterValueChangedCallback(evt =>
    {
      Undo.RecordObjects(property.serializedObject.targetObjects, "Adjust Range");

      float newMin = evt.newValue.x;
      float newMax = evt.newValue.y;

      if (newMax < newMin)
      {
        float t = newMin;
        newMin = newMax;
        newMax = t;
      }

      foreach (var target in property.serializedObject.targetObjects)
      {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property.propertyPath);
        var pMin = prop.FindPropertyRelative("min");
        var pMax = prop.FindPropertyRelative("max");

        pMin.floatValue = newMin;
        pMax.floatValue = newMax;
        so.ApplyModifiedProperties();
      }
    });

    // When number fields change (via binding), reflect into the slider.
    minField.RegisterValueChangedCallback(_ => UpdateSliderFromProps());
    maxField.RegisterValueChangedCallback(_ => UpdateSliderFromProps());

    // Keep slider in sync on undo/redo or external changes.
    root.schedule.Execute(UpdateSliderFromProps).Every(100);

    // Initial sync
    UpdateSliderFromProps();

    return root;
  }
}
