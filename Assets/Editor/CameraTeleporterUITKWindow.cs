
// UI Toolkit-based tool for teleporting the SceneView camera to:
// 1) The bookmark closest to the camera's current look ray, or
// 2) A bookmark selected from a list.
// Uses a robust geometric test: minimal perpendicular distance from bookmark pivot to camera's forward ray.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraTeleporterUITKWindow : EditorWindow
{

  private ListView listView;
  private Button btnTeleportSelected;
  private Button btnTeleportNearest;
  private Label status;

  [MenuItem("Tools/Camera Bookmarks/Teleporter (UI Toolkit)")]
  public static void Open()
  {
    var window = GetWindow<CameraTeleporterUITKWindow>("Camera Teleporter (UI Toolkit)");
    window.minSize = new Vector2(360, 280);
    window.Show();
  }

  // Subscribe/unsubscribe to store changes so we stay live-synced with the IMGUI window.
  private void OnEnable()
  {
    var store = CameraBookmarkStore.instance;
    store.Changed += OnStoreChanged;
  }

  private void OnDisable()
  {
    // Guard in case domain-reload timing makes instance unavailable.
    var store = CameraBookmarkStore.instance;
    if (store != null)
    {
      store.Changed -= OnStoreChanged;
    }
  }

  // CreateGUI is the UI Toolkit entry point for EditorWindow
  public void CreateGUI()
  {
    // Root layout
    var root = rootVisualElement;
    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 8;
    root.style.paddingBottom = 8;

    var title = new Label("Camera Teleporter (UI Toolkit)");
    title.style.unityFontStyleAndWeight = FontStyle.Bold;
    title.style.marginBottom = 6;
    root.Add(title);

    var help = new HelpBox(
        "Use the buttons below to teleport the SceneView camera. " +
        "\"Teleport To Nearest Look Target\" finds the bookmark whose position is closest to the camera's forward ray. " +
        "You can also select a bookmark and click \"Teleport To Selected\".",
        HelpBoxMessageType.Info);
    root.Add(help);

    // Gizmo toggles row (SceneView only visuals)
    var togglesRow = new VisualElement();
    togglesRow.style.flexDirection = FlexDirection.Row;
    togglesRow.style.marginBottom = 6;
    togglesRow.style.flexWrap = Wrap.Wrap;

    var store = CameraBookmarkStore.instance;

    var toggleGizmos = new Toggle("Show Markers in Scene View")
    {
      value = store.GetGizmosEnabled()
    };

    toggleGizmos.RegisterValueChangedCallback(evt =>
    {
      store.SetGizmosEnabled(evt.newValue);
    });

    var toggleLabels = new Toggle("Show Billboarded Text")
    {
      value = store.GetLabelsEnabled()
    };

    toggleLabels.RegisterValueChangedCallback(evt =>
    {
      store.SetLabelsEnabled(evt.newValue);
    });

    togglesRow.Add(toggleGizmos);
    togglesRow.Add(toggleLabels);
    root.Add(togglesRow);

    // Buttons row
    var buttonsRow = new VisualElement();
    buttonsRow.style.flexDirection = FlexDirection.Row;
    buttonsRow.style.marginBottom = 6;

    btnTeleportNearest = new Button(OnTeleportNearest)
    {
      text = "Teleport To Nearest Look Target"
    };

    btnTeleportSelected = new Button(OnTeleportSelected)
    {
      text = "Teleport To Selected"
    };

    buttonsRow.Add(btnTeleportNearest);
    buttonsRow.Add(btnTeleportSelected);
    root.Add(buttonsRow);

    // ListView of bookmarks for manual selection
    var data = new List<CameraBookmark>(store.Bookmarks);

    listView = new ListView(
      data,
      itemHeight: 20,
      makeItem: MakeItem,
      bindItem: (ve, i) =>
      {
        var label = ve as Label;
        if (label != null)
        {
          var src = listView.itemsSource as List<CameraBookmark>;
          if (src != null && i >= 0 && i < src.Count)
          {
            label.text = FormatBookmarkLine(src[i], i);
          }
          else
          {
            label.text = "<empty>";
          }
        }
      });

    listView.selectionType = SelectionType.Single;
    listView.style.flexGrow = 1.0f;

    // Double-click selection to teleport immediately.
    listView.itemsChosen += objects =>
    {
      foreach (var obj in objects)
      {
        if (obj is CameraBookmark chosen)
        {
          CameraBookmarkStore.TryApplyToSceneView(chosen, instant: true);
          UpdateStatus($"Teleported to \"{chosen.name}\".");
          break;
        }
      }
    };

    root.Add(listView);

    // Status label
    status = new Label("Ready.");
    status.style.marginTop = 6;
    root.Add(status);

    // Toolbar for refresh
    var toolbar = new Toolbar();
    var refreshBtn = new ToolbarButton(RefreshData)
    {
      text = "Refresh"
    };
    toolbar.Add(refreshBtn);
    root.Add(toolbar);

    // Initial populate
    RefreshData();
  }

  private void OnStoreChanged()
  {
    // Keep selection stable if possible
    int keepIndex = listView != null ? listView.selectedIndex : -1;

    // Schedule the refresh on the UI Toolkit scheduler to play nicely with ongoing layout.
    if (rootVisualElement != null)
    {
      rootVisualElement.schedule.Execute(() =>
      {
        RefreshData();

        // Restore selection if still valid
        if (keepIndex >= 0)
        {
          var data = listView.itemsSource as List<CameraBookmark>;
          if (data != null && keepIndex < data.Count)
          {
            listView.selectedIndex = keepIndex;
          }
          else
          {
            listView.selectedIndex = -1;
          }
        }
      });
    }
    else
    {
      RefreshData();
    }
  }

  private VisualElement MakeItem()
  {
    var lbl = new Label();
    lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
    return lbl;
  }

  private string FormatBookmarkLine(CameraBookmark bm, int index)
  {
    return $"#{index} {bm.name} (pos: {bm.pivot.ToString("F1")})";
  }

  private void RefreshData()
  {
    var store = CameraBookmarkStore.instance;
    var data = new List<CameraBookmark>(store.Bookmarks);
    listView.itemsSource = data;
    listView.Rebuild();
    UpdateStatus($"Loaded {data.Count} bookmark(s).");
  }

  private void OnTeleportSelected()
  {
    var data = listView.itemsSource as List<CameraBookmark>;
    if (data == null || data.Count == 0)
    {
      UpdateStatus("No bookmarks available.");
      return;
    }

    int index = listView.selectedIndex;
    if (index < 0 || index >= data.Count)
    {
      UpdateStatus("No bookmark selected.");
      return;
    }

    var bm = data[index];
    if (CameraBookmarkStore.TryApplyToSceneView(bm, instant: true))
    {
      UpdateStatus($"Teleported to \"{bm.name}\".");
    }
    else
    {
      UpdateStatus("Failed to teleport: no active SceneView?");
    }
  }

  private void OnTeleportNearest()
  {
    var sv = SceneView.lastActiveSceneView;

    if (sv == null || sv.camera == null)
    {
      UpdateStatus("No active SceneView to measure from.");
      return;
    }

    var cam = sv.camera;
    Vector3 rayOrigin = cam.transform.position;
    Vector3 rayDir = cam.transform.forward;

    var store = CameraBookmarkStore.instance;
    var list = store.Bookmarks;

    if (list.Count == 0)
    {
      UpdateStatus("No bookmarks to evaluate.");
      return;
    }

    int bestIndex = -1;
    float bestScore = float.PositiveInfinity;

    for (int i = 0; i < list.Count; i++)
    {
      var bm = list[i];
      Vector3 toP = bm.pivot - rayOrigin;

      float t = Vector3.Dot(toP, rayDir);
      Vector3 closestOnLine = rayOrigin + rayDir * t;
      float perpDist = Vector3.Distance(bm.pivot, closestOnLine);

      float behindPenalty = (t < 0.0f) ? 2.0f : 1.0f;
      float score = perpDist * behindPenalty;

      if (score < bestScore)
      {
        bestScore = score;
        bestIndex = i;
      }
    }

    if (bestIndex >= 0 && bestIndex < list.Count)
    {
      var target = list[bestIndex];
      if (CameraBookmarkStore.TryApplyToSceneView(target, instant: true))
      {
        UpdateStatus($"Teleported to nearest look target: \"{target.name}\" (score={bestScore:F3}).");
      }
      else
      {
        UpdateStatus("Failed to teleport: no active SceneView?");
      }
    }
    else
    {
      UpdateStatus("Could not determine a nearest bookmark.");
    }
  }

  private void UpdateStatus(string msg)
  {
    if (status != null)
    {
      status.text = msg;
    }
    else
    {
      Debug.Log(msg);
    }
  }

}
#endif