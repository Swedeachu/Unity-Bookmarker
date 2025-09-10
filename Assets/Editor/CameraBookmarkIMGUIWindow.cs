
// IMGUI-based tool for placing and managing camera bookmarks at the current SceneView camera.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class CameraBookmarkIMGUIWindow : EditorWindow
{

  // Menu path and window name are distinct from the UI Toolkit window.
  [MenuItem("Tools/Camera Bookmarks/Marker Placer (IMGUI)")]
  public static void Open()
  {
    var window = GetWindow<CameraBookmarkIMGUIWindow>("Camera Bookmarks (IMGUI)");
    window.minSize = new Vector2(340, 260);
    window.Show();
  }

  private Vector2 _scroll;
  private string _newBookmarkName = "";

  private void OnGUI()
  {
    EditorGUILayout.LabelField("Marker Placer (IMGUI)", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
        "Click the button below to save a bookmark from the current SceneView camera. " +
        "You can rename or remove bookmarks in the list.", MessageType.Info);

    // Optional name entry for the new bookmark.
    EditorGUILayout.BeginHorizontal();
    {
      EditorGUILayout.LabelField("New Bookmark Name:", GUILayout.Width(150));
      _newBookmarkName = EditorGUILayout.TextField(_newBookmarkName);
    }
    EditorGUILayout.EndHorizontal();

    // Button: Add current SceneView camera bookmark.
    if (GUILayout.Button("Add Bookmark @ Current SceneView Camera"))
    {
      if (CameraBookmarkStore.TryCaptureFromSceneView(out var bm, _newBookmarkName))
      {
        CameraBookmarkStore.instance.Add(bm);
        _newBookmarkName = "";
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

    _scroll = EditorGUILayout.BeginScrollView(_scroll);
    {
      for (int i = 0; i < list.Count; i++)
      {
        EditorGUILayout.BeginVertical("box");
        {
          var bm = list[i];

          // Name row with rename field + apply.
          EditorGUILayout.BeginHorizontal();
          {
            EditorGUILayout.LabelField($"#{i}", GUILayout.Width(30));

            string newName = EditorGUILayout.TextField(bm.name);
            if (newName != bm.name)
            {
              // Only rename when user hits the apply button to avoid constant saves each keystroke.
              if (GUILayout.Button("Apply Rename", GUILayout.Width(110)))
              {
                store.Rename(i, newName);
              }
            }
            else
            {
              GUILayout.Space(114); // keep layout consistent with button width
            }

            // Show quick "Go To" button to jump to this bookmark from IMGUI window too.
            if (GUILayout.Button("Go To", GUILayout.Width(60)))
            {
              CameraBookmarkStore.TryApplyToSceneView(bm, instant: true);
            }

            // Remove button.
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
              store.RemoveAt(i);
              // After removal, continue loop safely.
              continue;
            }
          }
          EditorGUILayout.EndHorizontal();

          // Small info block.
          EditorGUI.indentLevel++;
          EditorGUILayout.Vector3Field("Pivot", bm.pivot);
          EditorGUILayout.Vector3Field("Saved Cam Pos", bm.cameraPositionAtSave);
          EditorGUILayout.Vector4Field("Rotation (xyzw)", new Vector4(bm.rotation.x, bm.rotation.y, bm.rotation.z, bm.rotation.w));
          EditorGUILayout.LabelField("Orthographic", bm.orthographic ? "Yes" : "No");
          EditorGUILayout.LabelField("Size", bm.size.ToString("F3"));
          EditorGUILayout.LabelField("Camera Distance", bm.cameraDistance.ToString("F3"));
          EditorGUI.indentLevel--;

        }
        EditorGUILayout.EndVertical();
      }
    }
    EditorGUILayout.EndScrollView();
  }

}
#endif
