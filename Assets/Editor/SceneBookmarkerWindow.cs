#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SceneBookmarkerWindow : EditorWindow
{
  // ui
  Label hdrTitle;
  Toggle tglGizmos;
  Toggle tglLabels;
  Toggle tglAnimate;
  TextField newNameField;
  Button addBtn;
  ListView listView;

  int lastCount = -1;

  [MenuItem("Tools/Scene Bookmarker")]
  public static void Open()
  {
    var w = GetWindow<SceneBookmarkerWindow>();
    w.titleContent = new GUIContent("Scene Bookmarker");
    w.minSize = new Vector2(420, 320);
    w.Show();
  }

  void OnEnable()
  {
    CameraBookmarkStore.instance.Changed += OnStoreChanged;
    EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

    // Listen to SceneView events every frame.
    SceneView.duringSceneGui += HandleSceneViewHotkeys;
  }

  void OnDisable()
  {
    var store = CameraBookmarkStore.instance;
    if (store != null)
    {
      store.Changed -= OnStoreChanged;
    }

    EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;

    // Stop listening when this window disables.
    SceneView.duringSceneGui -= HandleSceneViewHotkeys;
  }


  // layout
  public void CreateGUI()
  {
    var root = rootVisualElement;

    StyleSheetHelper.AttatchStyleSheet(root, "Assets/Editor/UI/BookmarkStyleSheet.uss");

    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 8;
    root.style.paddingBottom = 8;
    root.style.flexDirection = FlexDirection.Column;

    // header
    hdrTitle = new Label();
    hdrTitle.AddToClassList("bigTitle");     
    hdrTitle.AddToClassList("panelHeader"); 
    hdrTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
    hdrTitle.style.fontSize = 14;
    root.Add(hdrTitle);

    // toggles
    var toggles = new VisualElement();
    toggles.AddToClassList("fieldGroup");
    toggles.style.flexDirection = FlexDirection.Column;

    var store = CameraBookmarkStore.instance;

    tglGizmos = new Toggle("Show markers in scene") { value = store.GetGizmosEnabled() };
    tglGizmos.RegisterValueChangedCallback(e => { store.SetGizmosEnabled(e.newValue); });

    tglLabels = new Toggle("Show marker name in scene") { value = store.GetLabelsEnabled() };
    tglLabels.RegisterValueChangedCallback(e => { store.SetLabelsEnabled(e.newValue); });

    tglAnimate = new Toggle("Animate camera") { value = EditorPrefs.GetBool(CameraBookMarkPreferences.prefAnimate, true) };
    tglAnimate.RegisterValueChangedCallback(e => { EditorPrefs.SetBool(CameraBookMarkPreferences.prefAnimate, e.newValue); });

    toggles.Add(tglGizmos);
    toggles.Add(tglLabels);
    toggles.Add(tglAnimate);
    root.Add(toggles);

    // help + add row
    var help = new HelpBox("Add bookmarks from the current SceneView camera, then jump or edit.", HelpBoxMessageType.Info);
    root.Add(help);

    var addRow = new VisualElement();
    addRow.AddToClassList("fieldGroup");
    addRow.style.flexDirection = FlexDirection.Row;
    addRow.style.alignItems = Align.Center;

    var nameLbl = new Label("New Bookmark Name:");
    nameLbl.style.minWidth = 150;

    newNameField = new TextField { value = "" };
    newNameField.style.flexGrow = 1.0f;

    addBtn = new Button(OnAddBookmark) { text = "Add Bookmark @ Current SceneView Camera" };

    addRow.Add(nameLbl);
    addRow.Add(newNameField);
    root.Add(addRow);
    root.Add(addBtn);

    // list header
    var listHdr = new Label("Bookmarks");
    listHdr.AddToClassList("panelHeader");
    listHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
    listHdr.style.marginTop = 6;
    root.Add(listHdr);

    // list view
    listView = new ListView
    {
      virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
      fixedItemHeight = 28,
      selectionType = SelectionType.None,
      showBorder = true,
      reorderable = true, // enable drag-to-reorder
      itemsSource = CameraBookmarkStore.instance.Bookmarks
    };
    listView.makeItem = MakeRow;
    listView.bindItem = BindRow;
    listView.itemIndexChanged += OnListItemIndexChanged; // persist ordering
    listView.style.flexGrow = 1.0f;
    root.Add(listView);

    RefreshHeader();
    RefreshList(true);
  }

  // persist reorder into the store's active scene list
  void OnListItemIndexChanged(int oldIndex, int newIndex)
  {
    CameraBookmarkStore.instance.Reorder(oldIndex, newIndex);
  }

  // row visual
  VisualElement MakeRow()
  {
    var card = new VisualElement();
    card.AddToClassList("card");
    card.style.marginBottom = 4;
    card.style.paddingTop = 4;
    card.style.paddingBottom = 4;
    card.style.paddingLeft = 6;
    card.style.paddingRight = 6;
    card.style.borderTopWidth = 1;
    card.style.borderBottomWidth = 1;
    card.style.borderLeftWidth = 1;
    card.style.borderRightWidth = 1;
    var bc = new Color(0, 0, 0, 0.2f);
    card.style.borderTopColor = bc;
    card.style.borderBottomColor = bc;
    card.style.borderLeftColor = bc;
    card.style.borderRightColor = bc;

    var header = new VisualElement();
    header.style.flexDirection = FlexDirection.Row;
    header.style.alignItems = Align.Center;

    var dot = new VisualElement();
    dot.AddToClassList("pillDot");
    dot.name = "dot";
    dot.style.width = 12;
    dot.style.height = 12;
    dot.style.marginRight = 4;

    var idx = new Label { name = "idx" };
    idx.style.minWidth = 28;

    var nameLbl = new Label { name = "nameLbl" };
    nameLbl.style.flexGrow = 1.0f;

    var gotoBtn = new Button { name = "gotoBtn", text = "Go To" };
    gotoBtn.style.flexShrink = 0;
    gotoBtn.style.minWidth = 64;

    var editBtn = new Button { name = "editBtn", text = "Edit" };
    editBtn.style.flexShrink = 0;
    editBtn.style.minWidth = 60;

    header.Add(dot);
    header.Add(idx);
    header.Add(nameLbl);
    header.Add(gotoBtn);
    header.Add(editBtn);
    card.Add(header);

    return card;
  }

  // row bind
  void BindRow(VisualElement row, int i)
  {
    var list = CameraBookmarkStore.instance.Bookmarks;
    if (i < 0 || i >= list.Count)
    {
      return;
    }

    var bm = list[i];

    var dot = row.Q<VisualElement>("dot");
    if (dot != null)
    {
      dot.style.backgroundColor = bm.color;
    }

    var idx = row.Q<Label>("idx");
    if (idx != null)
    {
      idx.text = $"#{i + 1}"; // +1 to be human readable instead of index 0
    }

    var nameLbl = row.Q<Label>("nameLbl");
    if (nameLbl != null)
    {
      nameLbl.text = bm.name;
    }

    // Go To
    var gotoBtn = row.Q<Button>("gotoBtn");
    if (gotoBtn != null)
    {
      var old = gotoBtn.userData as Action;
      if (old != null)
      {
        gotoBtn.clicked -= old;
      }

      int rowIndex = i;
      Action click = () =>
      {
        if (rowIndex >= 0 && rowIndex < CameraBookmarkStore.instance.Bookmarks.Count &&
            CameraBookmarkStore.instance.TryGet(rowIndex, out var target))
        {
          bool animate = EditorPrefs.GetBool(CameraBookMarkPreferences.prefAnimate, true);
          CameraBookmarkStore.ApplyToSceneView(target, animate);
        }
      };

      gotoBtn.userData = click;
      gotoBtn.clicked += click;
    }

    // Edit
    var editBtn = row.Q<Button>("editBtn");
    if (editBtn != null)
    {
      var old = editBtn.userData as Action;
      if (old != null)
      {
        editBtn.clicked -= old;
      }

      int rowIndex = i;
      Action click = () => BookmarkInspectorWindow.Open(rowIndex);

      editBtn.userData = click;
      editBtn.clicked += click;
    }
  }

  // add bookmark
  void OnAddBookmark()
  {
    string name = newNameField != null ? newNameField.value : null;

    if (CameraBookmarkStore.TryCaptureFromSceneView(out var bm, name))
    {
      CameraBookmarkStore.instance.Add(bm);
      if (newNameField != null)
      {
        newNameField.value = "";
      }
    }
    else
    {
      EditorUtility.DisplayDialog("No SceneView", "Could not find an active SceneView to capture.", "OK");
    }
  }

  // store change
  void OnStoreChanged()
  {
    if (rootVisualElement != null)
    {
      rootVisualElement.schedule.Execute(() =>
      {
        RefreshHeader();
        RefreshList();
      });
    }
    else
    {
      RefreshHeader();
      RefreshList();
    }
  }

  void OnActiveSceneChanged(Scene from, Scene to)
  {
    RefreshHeader();
  }

  // helpers
  void RefreshHeader()
  {
    string sceneName = SceneManager.GetActiveScene().IsValid() ? SceneManager.GetActiveScene().name : "<no scene>";
    int count = CameraBookmarkStore.instance.Bookmarks.Count;
    if (hdrTitle != null)
    {
      hdrTitle.text = $"Scene {sceneName} ({count} Bookmark{(count == 1 ? "" : "s")})";
    }
  }

  void RefreshList(bool forceRebuild = false)
  {
    if (listView == null)
    {
      return;
    }

    int count = CameraBookmarkStore.instance.Bookmarks.Count;
    bool countChanged = (count != lastCount) || forceRebuild;
    lastCount = count;

    if (countChanged)
    {
      listView.Rebuild();
    }
    else
    {
      listView.RefreshItems();
    }
  }

  /// <summary>
  /// SceneView event hook that listens for Shift/Ctrl + number keys and
  /// jumps to the corresponding bookmark index.
  /// Requirements:
  /// - Only runs when the focused editor window is a SceneView
  /// - Ignores input if any control/UI has focus (keyboard/hot control)
  /// - No-ops if index is out of range
  /// </summary>
  private static void HandleSceneViewHotkeys(SceneView sv)
  {
    // Must be in the SceneView
    if (!(EditorWindow.focusedWindow is SceneView))
    {
      return;
    }

    Event e = Event.current;
    if (e == null || e.type != EventType.KeyDown)
    {
      return;
    }

    // Require Shift or Ctrl
    if (!(e.shift || e.control))
    {
      return;
    }

    // Also skip if SceneView's IMGUI control (overlay, toolbar input) has focus
    if (GUIUtility.keyboardControl != 0 && GUI.GetNameOfFocusedControl() != "")
    {
      return;
    }

    // Map number keys to bookmark indices
    int index = -1;
    switch (e.keyCode)
    {
      case KeyCode.Alpha1: case KeyCode.Keypad1: index = 0; break;
      case KeyCode.Alpha2: case KeyCode.Keypad2: index = 1; break;
      case KeyCode.Alpha3: case KeyCode.Keypad3: index = 2; break;
      case KeyCode.Alpha4: case KeyCode.Keypad4: index = 3; break;
      case KeyCode.Alpha5: case KeyCode.Keypad5: index = 4; break;
      case KeyCode.Alpha6: case KeyCode.Keypad6: index = 5; break;
      case KeyCode.Alpha7: case KeyCode.Keypad7: index = 6; break;
      case KeyCode.Alpha8: case KeyCode.Keypad8: index = 7; break;
      case KeyCode.Alpha9: case KeyCode.Keypad9: index = 8; break;
      case KeyCode.Alpha0: case KeyCode.Keypad0: index = 9; break;
      default: return;
    }

    var store = CameraBookmarkStore.instance;
    if (index < 0 || index >= store.Bookmarks.Count)
    {
      return;
    }

    bool animate = EditorPrefs.GetBool(CameraBookMarkPreferences.prefAnimate, true);
    if (store.TryGet(index, out var target))
    {
      CameraBookmarkStore.ApplyToSceneView(target, animate);
      e.Use();
      sv.Repaint();
    }
  }

}

#endif