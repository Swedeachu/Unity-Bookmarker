
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
  public static bool TryCaptureFromSceneView(out CameraBookmark bookmark, string optionalName = null)
  {
    var sv = SceneView.lastActiveSceneView;

    if (sv == null || sv.camera == null)
    {
      bookmark = default;
      return false;
    }

    bookmark = new CameraBookmark
    {
      name = string.IsNullOrEmpty(optionalName) ? $"Bookmark {DateTime.Now:HHmmss}" : optionalName,
      pivot = sv.pivot,
      rotation = sv.rotation,
      size = sv.size,
      orthographic = sv.orthographic,
      cameraPositionAtSave = sv.camera.transform.position,
      cameraDistance = GetSceneViewCameraDistanceSafe(sv)
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

}

#endif
