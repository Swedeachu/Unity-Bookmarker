
// IMGUI-based tool for placing and managing camera bookmarks at the current SceneView camera.
/*
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

      for (int i = 0; i < list.Count; i++)
      {
        if (nameEdits.ContainsKey(i) == false)
        {
          nameEdits[i] = list[i].name;
        }
      }
    }

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

            string cachedName = nameEdits.TryGetValue(i, out var val) ? val : bm.name;
            string newName = EditorGUILayout.DelayedTextField(cachedName);
            if (!ReferenceEquals(newName, cachedName))
            {
              nameEdits[i] = newName;

              if (newName != bm.name)
              {
                store.Rename(i, newName);
              }
            }

            if (GUILayout.Button("Go To", GUILayout.Width(60)))
            {
              CameraBookmarkStore.TryApplyToSceneView(bm, instant: true);
            }

            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
              removeIndex = i;
            }
          }
          EditorGUILayout.EndHorizontal();

          // ---------- Editable Properties ----------
          EditorGUI.indentLevel++;

          // Position (eye point). Changing this should also move the pivot forward by cameraDistance.
          EditorGUI.BeginChangeCheck();
          Vector3 newPos = EditorGUILayout.Vector3Field("Position", bm.position);
          if (EditorGUI.EndChangeCheck())
          {
            store.SetPosition(i, newPos); // keeps pivot synced with position
            store.TryGet(i, out bm);
          }

          // Rotation (Euler)
          Vector3 euler = bm.rotation.eulerAngles;
          EditorGUI.BeginChangeCheck();
          Vector3 newEuler = EditorGUILayout.Vector3Field("Rotation (Euler)", euler);
          if (EditorGUI.EndChangeCheck())
          {
            bm.rotation = Quaternion.Euler(newEuler);
            store.Replace(i, bm);
          }

          EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
      }
    }

    EditorGUILayout.EndScrollView();

    if (removeIndex.HasValue)
    {
      store.RemoveAt(removeIndex.Value);
      nameEdits.Clear();
      GUIUtility.ExitGUI();
    }
  }

}
#endif
*/