// Stores camera bookmarks per scene using a ScriptableSingleton.
// Uses fields that let us restore the SceneView viewpoint as closely as possible.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct CameraBookMarkPreferences
{
  const string ns = "CameraBookmarks";
  public const string prefGizmos = ns + ".DrawGizmos";
  public const string prefLabels = ns + ".DrawLabels";
  public const string prefAnimate = ns + ".AnimateCamera";
}

[Serializable]
public struct CameraBookmark
{
  public string name;
  public Vector3 pivot;
  public Quaternion rotation;
  public float size;
  public bool orthographic;
  public Color color;
  public float cameraDistance;
  public Vector3 position;
}

[FilePath("UserSettings/CameraBookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
public class CameraBookmarkStore : ScriptableSingleton<CameraBookmarkStore>
{
  // per-scene storage bucket
  [Serializable]
  private class SceneBookmarkBucket
  {
    public string key;
    public string scenePath;
    public List<CameraBookmark> bookmarks = new List<CameraBookmark>();
  }

  // per-scene buckets
  [SerializeField] private List<SceneBookmarkBucket> sceneBuckets = new List<SceneBookmarkBucket>();

  // legacy single-list support (migrated on enable)
  [SerializeField] private List<CameraBookmark> bookmarksLegacy = new List<CameraBookmark>();

  // stable view list for UI binding
  private readonly List<CameraBookmark> view = new List<CameraBookmark>();

  public event Action Changed;

  // UI and consumers should bind to this list; it stays the same instance across scene switches.
  public List<CameraBookmark> Bookmarks
  {
    get { return view; }
    private set { }
  }

  // add to active scene
  public void Add(CameraBookmark bm)
  {
    var bucket = GetOrCreateActiveListBucket();
    bucket.bookmarks.Add(bm);
    Save(true);
    SyncViewToActive();
  }

  // remove from active scene
  public void RemoveAt(int index)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (index >= 0 && index < list.Count)
    {
      list.RemoveAt(index);
      Save(true);
      SyncViewToActive();
    }
  }

  // rename in active scene
  public void Rename(int index, string newName)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (index >= 0 && index < list.Count)
    {
      var bm = list[index];
      bm.name = newName;
      list[index] = bm;
      Save(true);
      SyncViewToActive();
    }
  }

  // replace in active scene (no view refresh needed for non-name fields)
  public void Replace(int index, CameraBookmark bm)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (index >= 0 && index < list.Count)
    {
      list[index] = bm;
      Save(true);
    }
  }

  // try get from active scene
  public bool TryGet(int index, out CameraBookmark bookmark)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (index >= 0 && index < list.Count)
    {
      bookmark = list[index];
      return true;
    }

    bookmark = default;

    return false;
  }

  // set position in active scene
  public void SetPosition(int index, Vector3 newPos)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (index < 0 || index >= list.Count)
    {
      return;
    }

    var bm = list[index];
    bm.position = newPos;

    float d = Mathf.Max(0.0f, bm.cameraDistance);
    bm.pivot = bm.position + (bm.rotation * Vector3.forward) * d;

    list[index] = bm;
    Save(true);
    // No SyncViewToActive since list count and displayed name do not change.
  }

  // capture from SceneView
  public static bool TryCaptureFromSceneView(out CameraBookmark bookmark, string optionalName = null)
  {
    var sv = SceneView.lastActiveSceneView;
    if (sv == null || sv.camera == null)
    {
      bookmark = default;
      return false;
    }

    string finalName = string.IsNullOrEmpty(optionalName)
      ? DateTime.Now.ToString("MM/dd/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
      : optionalName;

    bookmark = new CameraBookmark
    {
      name = finalName,
      pivot = sv.pivot,
      rotation = sv.rotation,
      size = sv.size,
      orthographic = sv.orthographic,
      position = sv.camera.transform.position,
      cameraDistance = GetSceneViewCameraDistanceSafe(sv),
      color = ColorHelpers.RandomBrightColor()
    };

    return true;
  }

  public static bool ApplyToSceneView(CameraBookmark bookmark, bool animate)
  {
    var sv = SceneView.lastActiveSceneView;
    if (sv == null)
    {
      return false;
    }

    if (!animate)
    {
      float d = Mathf.Max(0.0f, bookmark.cameraDistance);
      Vector3 pivotFromPosition = bookmark.position + (bookmark.rotation * Vector3.forward) * d;
      sv.orthographic = bookmark.orthographic;
      sv.LookAt(pivotFromPosition, bookmark.rotation, bookmark.size, bookmark.orthographic, true);
      SetSceneViewCameraDistanceSafe(sv, bookmark.cameraDistance);
      SceneView.RepaintAll();
      return true;
    }

    StartTweenToBookmark(sv, bookmark, 0.4f);
    return true;
  }

  // tween state
  static bool tweenActive;
  static double tweenStartTime;
  static float tweenDuration;
  static Vector3 tweenStartPivot, tweenTargetPivot;
  static Quaternion tweenStartRot, tweenTargetRot;
  static float tweenStartSize, tweenTargetSize;
  static float tweenStartDist, tweenTargetDist;
  static bool tweenTargetOrtho;

  static void StartTweenToBookmark(SceneView sv, CameraBookmark bm, float duration)
  {
    tweenActive = true;
    tweenStartTime = EditorApplication.timeSinceStartup;
    tweenDuration = Mathf.Max(0.01f, duration);

    tweenStartPivot = sv.pivot;
    tweenStartRot = sv.rotation;
    tweenStartSize = sv.size;
    tweenStartDist = GetSceneViewCameraDistanceSafe(sv);

    tweenTargetRot = bm.rotation;
    tweenTargetSize = bm.size;
    tweenTargetOrtho = bm.orthographic;

    float d = Mathf.Max(0.0f, bm.cameraDistance);
    tweenTargetPivot = bm.position + (bm.rotation * Vector3.forward) * d;
    tweenTargetDist = d;

    EditorApplication.update -= OnTweenUpdate;
    EditorApplication.update += OnTweenUpdate;
  }

  static void OnTweenUpdate()
  {
    if (!tweenActive)
    {
      return;
    }

    var sv = SceneView.lastActiveSceneView;
    if (sv == null)
    {
      tweenActive = false;
      EditorApplication.update -= OnTweenUpdate;
      return;
    }

    double now = EditorApplication.timeSinceStartup;
    float t = Mathf.Clamp01((float)((now - tweenStartTime) / tweenDuration));
    float s = t * t * (3f - 2f * t);

    Vector3 pivot = Vector3.Lerp(tweenStartPivot, tweenTargetPivot, s);
    Quaternion rot = Quaternion.Slerp(tweenStartRot, tweenTargetRot, s);
    float size = Mathf.Lerp(tweenStartSize, tweenTargetSize, s);
    float dist = Mathf.Lerp(tweenStartDist, tweenTargetDist, s);

    sv.orthographic = tweenTargetOrtho;
    sv.LookAt(pivot, rot, size, tweenTargetOrtho, true);
    SetSceneViewCameraDistanceSafe(sv, dist);
    SceneView.RepaintAll();

    if (t >= 1f)
    {
      tweenActive = false;
      EditorApplication.update -= OnTweenUpdate;
      sv.orthographic = tweenTargetOrtho;
      sv.LookAt(tweenTargetPivot, tweenTargetRot, tweenTargetSize, tweenTargetOrtho, true);
      SetSceneViewCameraDistanceSafe(sv, tweenTargetDist);
      SceneView.RepaintAll();
    }
  }

  static float GetSceneViewCameraDistanceSafe(SceneView sv)
  {
    try
    {
      var prop = typeof(SceneView).GetProperty("cameraDistance",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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

  static void SetSceneViewCameraDistanceSafe(SceneView sv, float distance)
  {
    try
    {
      var prop = typeof(SceneView).GetProperty("cameraDistance",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
      if (prop != null && prop.CanWrite)
      {
        prop.SetValue(sv, distance, null);
        return;
      }
    }
    catch
    {
    }
  }

  // notify listeners
  void NotifyChanged()
  {
    var handler = Changed;
    if (handler != null)
    {
      EditorApplication.delayCall += () =>
      {
        handler();
      };
    }
  }

  // enable hooks and migrate legacy
  private void OnEnable()
  {
    SceneView.duringSceneGui -= OnDuringSceneGUI;
    SceneView.duringSceneGui += OnDuringSceneGUI;

    EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
    EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

    if (bookmarksLegacy != null && bookmarksLegacy.Count > 0)
    {
      var bucket = GetOrCreateActiveListBucket();
      bucket.bookmarks.AddRange(bookmarksLegacy);
      bookmarksLegacy.Clear();
      Save(true);
    }

    SyncViewToActive();
  }

  private void OnDisable()
  {
    SceneView.duringSceneGui -= OnDuringSceneGUI;
    EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
  }

  private void OnActiveSceneChanged(Scene from, Scene to)
  {
    SyncViewToActive();
  }

  // scene gizmos
  private void OnDuringSceneGUI(SceneView sv)
  {
    if (!GetGizmosEnabled())
    {
      return;
    }

    if (Event.current.type != EventType.Repaint)
    {
      return;
    }

    // draw from the live bucket to reflect position edits immediately
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    for (int i = 0; i < list.Count; i++)
    {
      var bm = list[i];
      Vector3 p = bm.position;
      float h = HandleUtility.GetHandleSize(p) * 0.15f;

      using (new Handles.DrawingScope(bm.color))
      {
        Handles.SphereHandleCap(0, p, Quaternion.identity, h, EventType.Repaint);

        Vector3 dir = bm.rotation * Vector3.forward;
        Handles.ArrowHandleCap(0, p, Quaternion.LookRotation(dir, Vector3.up), h * 1.6f, EventType.Repaint);

        if (GetLabelsEnabled())
        {
          var style = new GUIStyle(EditorStyles.boldLabel);
          style.normal.textColor = Color.white;
          style.alignment = TextAnchor.UpperLeft;
          Handles.Label(p + Vector3.up * (h * 0.8f), $"#{i} {bm.name}", style);
        }
      }
    }
  }

  public bool GetGizmosEnabled()
  {
    return EditorPrefs.GetBool(CameraBookMarkPreferences.prefGizmos, true);
  }

  public void SetGizmosEnabled(bool value)
  {
    EditorPrefs.SetBool(CameraBookMarkPreferences.prefGizmos, value);
    SceneView.RepaintAll();
    NotifyChanged();
  }

  public bool GetLabelsEnabled()
  {
    return EditorPrefs.GetBool(CameraBookMarkPreferences.prefLabels, true);
  }

  public void SetLabelsEnabled(bool value)
  {
    EditorPrefs.SetBool(CameraBookMarkPreferences.prefLabels, value);
    SceneView.RepaintAll();
    NotifyChanged();
  }

  // helpers

  private static string GetActiveSceneKey()
  {
    var s = EditorSceneManager.GetActiveScene();
    string path = s.path;

    if (string.IsNullOrEmpty(path))
    {
      return "__untitled__:" + s.name;
    }

    string guid = AssetDatabase.AssetPathToGUID(path);

    return string.IsNullOrEmpty(guid) ? path : guid;
  }

  private SceneBookmarkBucket GetOrCreateActiveListBucket()
  {
    string key = GetActiveSceneKey();

    for (int i = 0; i < sceneBuckets.Count; i++)
    {
      var b = sceneBuckets[i];
      if (b != null && b.key == key)
      {
        return b;
      }
    }

    var s = EditorSceneManager.GetActiveScene();
    var created = new SceneBookmarkBucket
    {
      key = key,
      scenePath = s.path,
      bookmarks = new List<CameraBookmark>()
    };

    sceneBuckets.Add(created);
    Save(false);

    return created;
  }

  private void SyncViewToActive()
  {
    var bucket = GetOrCreateActiveListBucket();
    view.Clear();

    if (bucket != null && bucket.bookmarks != null)
    {
      view.AddRange(bucket.bookmarks);
    }
    NotifyChanged();
  }

  // reorder within the active scene bucket
  public void Reorder(int oldIndex, int newIndex)
  {
    var bucket = GetOrCreateActiveListBucket();
    var list = bucket.bookmarks;

    if (oldIndex < 0 || oldIndex >= list.Count || newIndex < 0 || newIndex >= list.Count)
    {
      return;
    }

    var item = list[oldIndex];
    list.RemoveAt(oldIndex);
    if (newIndex > oldIndex)
    {
      newIndex--;
    }
    list.Insert(newIndex, item);

    Save(true);
    SyncViewToActive();
  }

}
#endif
