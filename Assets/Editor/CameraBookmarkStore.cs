
// Stores camera bookmarks as a ScriptableSingleton so they persist between editor sessions.
// Uses fields that let us restore the SceneView viewpoint as closely as possible.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public struct CameraBookmark
{

  public string name;

  // We capture the SceneView viewpoint in a way we can restore it:
  public Vector3 pivot;
  public Quaternion rotation;
  public float size;
  public bool orthographic;

  // Visual bookmark gizmo color
  public Color color;

  // Newer Unity versions expose cameraDistance on SceneView; we store a copy for fidelity.
  public float cameraDistance;

  // World-space camera position that was present when saved.
  // This is helpful for "nearest to look ray" calculations and for showing where it was.
  public Vector3 cameraPositionAtSave;

}

// Saves into UserSettings (typically not committed). Ideal so bookmarks remain local and not committed files in a project repository.
[FilePath("UserSettings/CameraBookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
public class CameraBookmarkStore : ScriptableSingleton<CameraBookmarkStore>
{

  [SerializeField]
  private List<CameraBookmark> bookmarks = new List<CameraBookmark>();

  // Event so UI can update immediately when this store changes.
  // We keep this simple: no args; listeners just re-pull Bookmarks.
  public event Action Changed;

  public IReadOnlyList<CameraBookmark> Bookmarks
  {
    get
    {
      return bookmarks;
    }
  }

  public void Add(CameraBookmark bm)
  {
    bookmarks.Add(bm);
    Debug.Log($"Creating \"{bm.name}\" at #{bookmarks.Count - 1}");
    Save(true);
    NotifyChanged();
  }

  public void RemoveAt(int index)
  {
    if (index >= 0 && index < bookmarks.Count)
    {
      CameraBookmark at = bookmarks[index];
      Debug.Log($"Removing \"{at.name}\" at #{index}");
      bookmarks.RemoveAt(index);
      Save(true);
      NotifyChanged();
    }
  }

  public void Rename(int index, string newName)
  {
    if (index >= 0 && index < bookmarks.Count)
    {
      CameraBookmark bm = bookmarks[index];
      Debug.Log($"Renaming \"{bm.name}\" to \"{newName}\" at #{index}");
      bm.name = newName;
      bookmarks[index] = bm;
      Save(true);
      NotifyChanged();
    }
  }

  public void Replace(int index, CameraBookmark bm)
  {
    if (index >= 0 && index < bookmarks.Count)
    {
      bookmarks[index] = bm;
      Save(true);
      NotifyChanged();
    }
  }

  // Utility to fetch a bookmark safely.
  public bool TryGet(int index, out CameraBookmark bookmark)
  {
    if (index >= 0 && index < bookmarks.Count)
    {
      bookmark = bookmarks[index];
      return true;
    }

    bookmark = default;
    return false;
  }

  // Capture the current SceneView camera as a bookmark.
  // Default name uses timestamp formatted like "10/20/2025 3:02 PM".
  public static bool TryCaptureFromSceneView(out CameraBookmark bookmark, string optionalName = null)
  {
    var sv = SceneView.lastActiveSceneView;

    if (sv == null || sv.camera == null)
    {
      bookmark = default;
      return false;
    }

    string finalName = "";

    if (string.IsNullOrEmpty(optionalName))
    {
      // Formats as: "10/20/2025 3:02 PM"
      finalName = DateTime.Now.ToString("MM/dd/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
    }
    else
    {
      finalName = optionalName;
    }

    bookmark = new CameraBookmark
    {
      name = finalName,
      pivot = sv.pivot,
      rotation = sv.rotation,
      size = sv.size,
      orthographic = sv.orthographic,
      cameraPositionAtSave = sv.camera.transform.position,
      cameraDistance = GetSceneViewCameraDistanceSafe(sv),
      color = ColorHelpers.RandomBrightColor()
    };

    return true;
  }

  // Set the SceneView to a bookmark's viewpoint.
  public static bool TryApplyToSceneView(CameraBookmark bookmark, bool instant = true)
  {
    var sv = SceneView.lastActiveSceneView;

    if (sv == null)
    {
      return false;
    }

    sv.orthographic = bookmark.orthographic;
    sv.rotation = bookmark.rotation;
    sv.pivot = bookmark.pivot;
    sv.size = bookmark.size;

    SetSceneViewCameraDistanceSafe(sv, bookmark.cameraDistance);

    sv.LookAt(sv.pivot, sv.rotation, sv.size, sv.orthographic, instant);
    SceneView.RepaintAll();
    return true;
  }

  private static float GetSceneViewCameraDistanceSafe(SceneView sv)
  {
    try
    {
      var prop = typeof(SceneView).GetProperty("cameraDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
      if (prop != null && prop.CanRead)
      {
        var val = prop.GetValue(sv, null);
        if (val is float f)
        {
          return f;
        }
      }
    }
    catch { }

    if (sv.camera != null)
    {
      Vector3 camPos = sv.camera.transform.position;
      Vector3 toPivot = sv.pivot - camPos;
      float dist = Vector3.Dot(toPivot, sv.camera.transform.forward);
      return Mathf.Max(dist, 0.0f);
    }

    return 0.0f;
  }

  private static void SetSceneViewCameraDistanceSafe(SceneView sv, float distance)
  {
    try
    {
      var prop = typeof(SceneView).GetProperty("cameraDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
      if (prop != null && prop.CanWrite)
      {
        prop.SetValue(sv, distance, null);
        return;
      }
    }
    catch { }
  }

  // Notify listeners (other windows) to refresh.
  // Using delayCall ensures we fire outside the current IMGUI layout event, which avoids edge-cases.
  private void NotifyChanged()
  {
    Action handler = Changed;
    if (handler != null)
    {
      EditorApplication.delayCall += () =>
      {
        handler();
      };
    }
  }

  // Subscribe to SceneView drawing when the store is loaded.
  private void OnEnable()
  {
    SceneView.duringSceneGui -= OnDuringSceneGUI;
    SceneView.duringSceneGui += OnDuringSceneGUI;
  }

  // Unsubscribe cleanly.
  private void OnDisable()
  {
    SceneView.duringSceneGui -= OnDuringSceneGUI;
  }

  // Draw simple 3D markers (sphere + forward arrow) and optional billboarded text in the Scene view only.
  private void OnDuringSceneGUI(SceneView sv)
  {
    // Only draw when enabled.
    if (GetGizmosEnabled() == false)
    {
      return;
    }

    // Draw during repaint to avoid handling input.
    if (Event.current.type != EventType.Repaint)
    {
      return;
    }

    var list = Bookmarks;

    for (int i = 0; i < list.Count; i++)
    {
      var bm = list[i];
      Vector3 p = bm.cameraPositionAtSave;

      // Handle size scales with distance so markers stay readable.
      float h = HandleUtility.GetHandleSize(p) * 0.15f;

      // Draw a small sphere marker.
      using (new Handles.DrawingScope(bm.color))
      {
        Handles.SphereHandleCap(0, p, Quaternion.identity, h, EventType.Repaint);

        // Draw a forward-direction arrow based on saved rotation.
        Vector3 dir = bm.rotation * Vector3.forward;
        Handles.ArrowHandleCap(0, p, Quaternion.LookRotation(dir, Vector3.up), h * 1.6f, EventType.Repaint);

        // Billboarded text label: "#index name"
        if (GetLabelsEnabled())
        {
          var style = new GUIStyle(EditorStyles.boldLabel);
          style.normal.textColor = Color.white;
          style.alignment = TextAnchor.UpperLeft;

          // Nudge upward so text doesn't overlap the sphere.
          Handles.Label(p + Vector3.up * (h * 0.8f), $"#{i} {bm.name}", style);
        }
      }
    }
  }

  // Toggle getters/setters (stored in EditorPrefs so no extra fields are required).
  public bool GetGizmosEnabled()
  {
    return EditorPrefs.GetBool("CameraBookmarks.DrawGizmos", true);
  }

  public void SetGizmosEnabled(bool value)
  {
    EditorPrefs.SetBool("CameraBookmarks.DrawGizmos", value);
    SceneView.RepaintAll();
    NotifyChanged();
  }

  public bool GetLabelsEnabled()
  {
    return EditorPrefs.GetBool("CameraBookmarks.DrawLabels", true);
  }

  public void SetLabelsEnabled(bool value)
  {
    EditorPrefs.SetBool("CameraBookmarks.DrawLabels", value);
    SceneView.RepaintAll();
    NotifyChanged();
  }

}

#endif
