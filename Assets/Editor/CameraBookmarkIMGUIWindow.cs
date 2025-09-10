
// IMGUI-based tool for placing and managing camera bookmarks at the current SceneView camera.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CameraBookmarkIMGUIWindow : EditorWindow
{

  [MenuItem("Tools/Camera Bookmarks/Marker Placer (IMGUI)")]
  public static void Open()
  {
    var window = GetWindow<CameraBookmarkIMGUIWindow>("Camera Bookmarks (IMGUI)");
    window.minSize = new Vector2(380, 300);
    window.Show();
  }

  private Vector2 scroll = Vector2.zero;
  private string newBookmarkName = "";

  // Optional local cache for names so typing feels smooth even before delayed commit fires.
  private readonly Dictionary<int, string> nameEdits = new Dictionary<int, string>();

  private void OnGUI()
  {
    EditorGUILayout.LabelField("Marker Placer (IMGUI)", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
        "Save bookmarks from the current SceneView camera, then edit or jump to them below. " +
        "Name edits commit when you press Enter or the field loses focus.",
        MessageType.Info);

    // --- Add new bookmark row ---
    EditorGUILayout.BeginHorizontal();
    {
      EditorGUILayout.LabelField("New Bookmark Name:", GUILayout.Width(150));
      newBookmarkName = EditorGUILayout.TextField(newBookmarkName);
    }
    EditorGUILayout.EndHorizontal();

    if (GUILayout.Button("Add Bookmark @ Current SceneView Camera"))
    {
      if (CameraBookmarkStore.TryCaptureFromSceneView(out var bm, newBookmarkName))
      {
        CameraBookmarkStore.instance.Add(bm);
        newBookmarkName = "";
        Repaint();
      }
      else
      {
        EditorUtility.DisplayDialog("No SceneView", "Could not find an active SceneView to capture.", "OK");
      }
    }

    EditorGUILayout.Space(8);
    EditorGUILayout.LabelField("Bookmarks", EditorStyles.boldLabel);

    var store = CameraBookmarkStore.instance;
    var list = store.Bookmarks;

    // Keep name cache in sync with count; do not overwrite existing edits.
    if (nameEdits.Count != list.Count)
    {
      // Remove stale keys
      var toRemove = new List<int>();
      foreach (var k in nameEdits.Keys)
      {
        if (k < 0 || k >= list.Count)
        {
          toRemove.Add(k);
        }
      }

      foreach (var k in toRemove)
      {
        nameEdits.Remove(k);
      }

      // Add missing keys
      for (int i = 0; i < list.Count; i++)
      {
        if (nameEdits.ContainsKey(i) == false)
        {
          nameEdits[i] = list[i].name;
        }
      }
    }

    // Defer state mutations that change layout.
    int? removeIndex = null;

    scroll = EditorGUILayout.BeginScrollView(scroll);
    {
      for (int i = 0; i < list.Count; i++)
      {
        EditorGUILayout.BeginVertical("box");
        {
          var bm = list[i];

          // ---------- Header Row (Index, Name, GoTo, Remove) ----------
          EditorGUILayout.BeginHorizontal();
          {
            EditorGUILayout.LabelField($"#{i}", GUILayout.Width(30));

            // Name field:
            // Use DelayedTextField so we commit once on enter/focus-loss instead of every keystroke.
            // Feed from a local cache so the user sees their current typing even before the delayed commit.
            string cachedName = nameEdits.TryGetValue(i, out var val) ? val : bm.name;
            string newName = EditorGUILayout.DelayedTextField(cachedName);
            if (!ReferenceEquals(newName, cachedName))
            {
              nameEdits[i] = newName;

              // Only write to the store when the delayed field reports a change.
              if (newName != bm.name)
              {
                store.Rename(i, newName);
                // No layout change; safe to continue.
              }
            }

            if (GUILayout.Button("Go To", GUILayout.Width(60)))
            {
              CameraBookmarkStore.TryApplyToSceneView(bm, instant: true);
            }

            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
              // Defer removal until after layout closes to keep Begin/End symmetric.
              removeIndex = i;
            }
          }

          EditorGUILayout.EndHorizontal();

          // ---------- Editable Properties ----------
          // We allow live editing of numbers; we commit immediately when a field changes.

          EditorGUI.indentLevel++;

          // Pivot
          EditorGUI.BeginChangeCheck();
          Vector3 newPivot = EditorGUILayout.Vector3Field("Pivot", bm.pivot);
          if (EditorGUI.EndChangeCheck())
          {
            bm.pivot = newPivot;
            store.Replace(i, bm);
          }

          // Rotation (Euler) - friendlier than raw quaternion editing.
          // We still show quaternion below for completeness (read-only or editable if you insist).
          Vector3 euler = bm.rotation.eulerAngles;
          EditorGUI.BeginChangeCheck();
          Vector3 newEuler = EditorGUILayout.Vector3Field("Rotation (Euler)", euler);

          if (EditorGUI.EndChangeCheck())
          {
            bm.rotation = Quaternion.Euler(newEuler);
            store.Replace(i, bm);
          }

          // Orthographic toggle
          EditorGUI.BeginChangeCheck();
          bool newOrtho = EditorGUILayout.Toggle("Orthographic", bm.orthographic);

          if (EditorGUI.EndChangeCheck())
          {
            bm.orthographic = newOrtho;
            store.Replace(i, bm);
          }

          // Size (delayed float to avoid thrashing while typing)
          EditorGUI.BeginChangeCheck();
          float newSize = EditorGUILayout.DelayedFloatField("Size", bm.size);

          if (EditorGUI.EndChangeCheck())
          {
            bm.size = Mathf.Max(0.0001f, newSize);
            store.Replace(i, bm);
          }

          // Camera Distance (optional fidelity)
          EditorGUI.BeginChangeCheck();
          float newCamDist = EditorGUILayout.DelayedFloatField("Camera Distance", bm.cameraDistance);

          if (EditorGUI.EndChangeCheck())
          {
            bm.cameraDistance = Mathf.Max(0.0f, newCamDist);
            store.Replace(i, bm);
          }

          // Saved camera position (world-space). Editable in case you want to nudge it.
          EditorGUI.BeginChangeCheck();
          Vector3 newSavedPos = EditorGUILayout.Vector3Field("Saved Cam Pos", bm.cameraPositionAtSave);
          if (EditorGUI.EndChangeCheck())
          {
            bm.cameraPositionAtSave = newSavedPos;
            store.Replace(i, bm);
          }

          // Raw quaternion view (read-only, since editing quats directly is unfriendly).
          using (new EditorGUI.DisabledScope(true))
          {
            Vector4 q = new Vector4(bm.rotation.x, bm.rotation.y, bm.rotation.z, bm.rotation.w);
            EditorGUILayout.Vector4Field("Rotation (xyzw)", q);
          }

          EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
      }
    }

    EditorGUILayout.EndScrollView();

    // Apply deferred mutations that change the layout tree 
    if (removeIndex.HasValue)
    {
      store.RemoveAt(removeIndex.Value);
      nameEdits.Clear(); // force rebuild next frame
      GUIUtility.ExitGUI(); // abort current IMGUI event to avoid layout mismatch
    }
  }

}
#endif
