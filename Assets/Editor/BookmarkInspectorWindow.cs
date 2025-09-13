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

    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 8;
    root.style.paddingBottom = 8;
    root.style.flexDirection = FlexDirection.Column;

    var title = new Label($"Edit Bookmark #{index}");
    title.style.unityFontStyleAndWeight = FontStyle.Bold;
    root.Add(title);

    nameField = new TextField("Name") { isDelayed = true };
    nameField.RegisterValueChangedCallback(e =>
    {
      CameraBookmarkStore.instance.Rename(index, e.newValue);
    });

    root.Add(nameField);

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

    root.Add(posField);

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

    root.Add(eulerField);

    colorField = new ColorField("Color");
    colorField.RegisterValueChangedCallback(e =>
    {
      if (CameraBookmarkStore.instance.TryGet(index, out var cur))
      {
        cur.color = e.newValue;
        CameraBookmarkStore.instance.Replace(index, cur);
      }
    });

    root.Add(colorField);


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

    root.Add(deleteBtn);
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
#endif
