#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.UIElements;

public class BookmarkInspectorWindow : EditorWindow
{

  static readonly Dictionary<int, BookmarkInspectorWindow> openWindows = new Dictionary<int, BookmarkInspectorWindow>();

  int index = -1;
  TextField nameField;
  Vector3Field posField;
  Vector3Field eulerField;
  ColorField colorField;

  public static void Open(int index)
  {
    if (openWindows.TryGetValue(index, out var existing) && existing != null)
    {
      existing.Focus();
      existing.Rebind();
      return;
    }

    var w = CreateInstance<BookmarkInspectorWindow>();
    w.index = index;
    w.titleContent = new GUIContent("Bookmark Inspector");
    w.minSize = new Vector2(320, 180);
    openWindows[index] = w;
    w.ShowUtility();
  }

  void OnDestroy()
  {
    if (openWindows.ContainsKey(index))
    {
      openWindows.Remove(index);
    }
  }

  void CreateGUI()
  {
    BuildUI();
    Rebind();
  }

  void BuildUI()
  {
    var root = rootVisualElement;
    root.Clear();

    // Attach base bookmark style sheet + inspector USS
    StyleSheetHelper.AttatchStyleSheet(root, "Assets/Editor/UI/BookmarkStyleSheet.uss");
    StyleSheetHelper.AttatchStyleSheet(root, "Assets/Editor/UI/BookmarkInspectorStyleSheet.uss");

    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 8;
    root.style.paddingBottom = 8;
    root.style.flexDirection = FlexDirection.Column;

    // Title
    var title = new Label($"Edit Bookmark #{index}");
    title.AddToClassList("inspectorHeader");  
    title.AddToClassList("panelHeader");       
    title.style.unityFontStyleAndWeight = FontStyle.Bold;
    root.Add(title);

    // Group: name
    var nameGroup = new VisualElement();
    nameGroup.AddToClassList("inspectorFieldGroup");
    nameField = new TextField("Name") { isDelayed = true };
    nameField.RegisterValueChangedCallback(e =>
    {
      CameraBookmarkStore.instance.Rename(index, e.newValue);
    });
    nameGroup.Add(nameField);
    root.Add(nameGroup);

    // Group: position + rotation
    var transformGroup = new VisualElement();
    transformGroup.AddToClassList("fieldGroup");     
    transformGroup.AddToClassList("inspectorFieldGroup");
    transformGroup.Add(new Label("Transform").AddToClassReturning("sectionHeader"));
    // Helper extension (below) simply returns the same element after AddToClassList so you can inline-add
    posField = new Vector3Field("Position");
    posField.RegisterCallback<FocusOutEvent>(_ =>
    {
      CameraBookmarkStore.instance.SetPosition(index, posField.value);
    });
    posField.RegisterCallback<KeyDownEvent>(e =>
    {
      if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
      {
        CameraBookmarkStore.instance.SetPosition(index, posField.value);
        e.StopPropagation();
      }
    });

    eulerField = new Vector3Field("Rotation (Euler)");
    eulerField.RegisterCallback<FocusOutEvent>(_ =>
    {
      if (CameraBookmarkStore.instance.TryGet(index, out var cur))
      {
        cur.rotation = Quaternion.Euler(eulerField.value);
        CameraBookmarkStore.instance.Replace(index, cur);
      }
    });
    eulerField.RegisterCallback<KeyDownEvent>(e =>
    {
      if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
      {
        if (CameraBookmarkStore.instance.TryGet(index, out var cur))
        {
          cur.rotation = Quaternion.Euler(eulerField.value);
          CameraBookmarkStore.instance.Replace(index, cur);
        }
        e.StopPropagation();
      }
    });

    transformGroup.Add(posField);
    transformGroup.Add(eulerField);
    root.Add(transformGroup);

    // Group: color
    var colorGroup = new VisualElement();
    colorGroup.AddToClassList("fieldGroup");
    colorGroup.AddToClassList("inspectorFieldGroup");
    colorGroup.Add(new Label("Appearance").AddToClassReturning("sectionHeader"));
    colorField = new ColorField("Color");
    colorField.RegisterValueChangedCallback(e =>
    {
      if (CameraBookmarkStore.instance.TryGet(index, out var cur))
      {
        cur.color = e.newValue;
        CameraBookmarkStore.instance.Replace(index, cur);
      }
    });
    colorGroup.Add(colorField);
    root.Add(colorGroup);

    // Delete button (drop it in a card for emphasis)
    var deleteWrap = new VisualElement();
    deleteWrap.AddToClassList("card");
    var deleteBtn = new Button(() =>
    {
      if (!CameraBookmarkStore.instance.TryGet(index, out var bm))
      {
        Close();
        return;
      }

      string msg = $"Are you sure you want to delete Bookmark #{index} ({bm.name})?";
      bool ok = EditorUtility.DisplayDialog("Delete Bookmark", msg, "Delete", "Cancel");
      if (ok)
      {
        CameraBookmarkStore.instance.RemoveAt(index);
        Close();
      }
    })
    { text = "Delete" };

    // Add a class so we can style it in USS
    deleteBtn.AddToClassList("deleteButton");
    deleteWrap.Add(deleteBtn);
    root.Add(deleteWrap);
  }

  void Rebind()
  {
    if (!CameraBookmarkStore.instance.TryGet(index, out var bm))
    {
      rootVisualElement.Clear();
      rootVisualElement.Add(new Label("Bookmark not found."));
      return;
    }

    nameField?.SetValueWithoutNotify(bm.name);
    posField?.SetValueWithoutNotify(bm.position);
    eulerField?.SetValueWithoutNotify(bm.rotation.eulerAngles);
    colorField?.SetValueWithoutNotify(bm.color);
  }

}

// Small helper to chain AddToClassList during element construction
static class VEExt
{
  public static T AddToClassReturning<T>(this T ve, string className) where T : VisualElement
  {
    ve.AddToClassList(className);
    return ve;
  }
}

#endif
