
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
  private List<CameraBookmark> _bookmarks = new List<CameraBookmark>();

  public IReadOnlyList<CameraBookmark> Bookmarks
  {
    get
    {
      return _bookmarks;
    }
  }

  public void Add(CameraBookmark bm)
  {
    _bookmarks.Add(bm);
    Save(true);
  }

  public void RemoveAt(int index)
  {
    if (index >= 0 && index < _bookmarks.Count)
    {
      _bookmarks.RemoveAt(index);
      Save(true);
    }
  }

  public void Rename(int index, string newName)
  {
    if (index >= 0 && index < _bookmarks.Count)
    {
      var bm = _bookmarks[index];
      bm.name = newName;
      _bookmarks[index] = bm;
      Save(true);
    }
  }

  public void Replace(int index, CameraBookmark bm)
  {
    if (index >= 0 && index < _bookmarks.Count)
    {
      _bookmarks[index] = bm;
      Save(true);
    }
  }

  // Utility to fetch a bookmark safely.
  public bool TryGet(int index, out CameraBookmark bookmark)
  {
    if (index >= 0 && index < _bookmarks.Count)
    {
      bookmark = _bookmarks[index];
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

    // Capture everything needed to reconstruct the scene view state.
    bookmark = new CameraBookmark
    {
      name = string.IsNullOrEmpty(optionalName) ? $"Bookmark {DateTime.Now:HHmmss}" : optionalName,
      pivot = sv.pivot,
      rotation = sv.rotation,
      size = sv.size,
      orthographic = sv.orthographic,
      cameraPositionAtSave = sv.camera.transform.position,
      // If cameraDistance is unavailable on this Unity version, this stays as 0; we’ll restore without it.
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

    // Ensure we’re focusing the same style of view (ortho vs perspective).
    sv.orthographic = bookmark.orthographic;

    // Apply rotation and pivot directly. This is the most robust cross-version approach.
    sv.rotation = bookmark.rotation;
    sv.pivot = bookmark.pivot;
    sv.size = bookmark.size;

    // If available in this Unity version, set cameraDistance (helps replicate exact camera pos in perspective).
    SetSceneViewCameraDistanceSafe(sv, bookmark.cameraDistance);

    // Force a repaint and optionally snap instantly.
    // LookAt with current values can "lock in" instantly; size is already set.
    sv.LookAt(sv.pivot, sv.rotation, sv.size, sv.orthographic, instant);

    SceneView.RepaintAll();
    return true;
  }

  private static float GetSceneViewCameraDistanceSafe(SceneView sv)
  {
    try
    {
      // If the property exists, use it.
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
    catch
    {
      // Ignore reflection issues; fallback below.
    }

    // Fallback: approximate distance along -forward between camera and pivot.
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
    catch
    {
      // Ignore; we’ll rely on rotation+pivot+size which is already set.
    }
  }

}
#endif
