// File: SelectionRenamerWindow.cs
// Location: Assets/Editor/

using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A UI Toolkit EditorWindow for renaming selected GameObjects by prefix and/or suffix.
/// - Launch via Tools/Selection Renamer
/// - Uses Undo
/// - Disables rename when invalid
/// - Shows HelpBox feedback
/// - Saves/loads last-used values via EditorPrefs
/// </summary>
public class SelectionRenamerWindow : EditorWindow
{
  private const string EditorPrefPrefixKey = "Swim.SelectionRenamer.LastPrefix";
  private const string EditorPrefSuffixKey = "Swim.SelectionRenamer.LastSuffix";

  private TextField _prefixField;
  private TextField _suffixField;
  private Button _renameButton;
  private HelpBox _helpBox;

  [MenuItem("Tools/Selection Renamer (UI Toolkit)")]
  public static void ShowWindow()
  {
    var win = GetWindow<SelectionRenamerWindow>();
    win.titleContent = new GUIContent("Selection Renamer");
    win.minSize = new Vector2(360, 160);
    win.Show();
  }

  private void OnEnable()
  {
    // React when the Unity selection changes
    Selection.selectionChanged += OnSelectionChanged;
  }

  private void OnDisable()
  {
    Selection.selectionChanged -= OnSelectionChanged;

    // Persist latest text values safely if fields exist.
    if (_prefixField != null)
    {
      EditorPrefs.SetString(EditorPrefPrefixKey, _prefixField.value ?? "");
    }
    if (_suffixField != null)
    {
      EditorPrefs.SetString(EditorPrefSuffixKey, _suffixField.value ?? "");
    }
  }

  private void OnSelectionChanged()
  {
    UpdateHelpAndButton();
    Repaint();
  }

  public void CreateGUI()
  {
    // Root styling
    var root = rootVisualElement;
    root.Clear();
    root.style.paddingLeft = 10;
    root.style.paddingRight = 10;
    root.style.paddingTop = 10;
    root.style.paddingBottom = 10;

    // Header
    var header = new Label("Selection Renamer");
    header.style.unityFontStyleAndWeight = FontStyle.Bold;
    header.style.fontSize = 13;
    header.style.marginBottom = 6;
    root.Add(header);

    // HelpBox for dynamic feedback (selection count, validation warnings)
    _helpBox = new HelpBox("No objects selected.", HelpBoxMessageType.Info);
    _helpBox.style.marginBottom = 8;
    root.Add(_helpBox);

    // Fields row
    var fieldRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
    fieldRow.style.paddingLeft = 8;
    root.Add(fieldRow);

    _prefixField = new TextField("Prefix")
    {
      tooltip = "Text to add at the beginning of each selected GameObject's name."
    };
    _prefixField.style.flexGrow = 1;
    fieldRow.Add(_prefixField);

    _suffixField = new TextField("Suffix")
    {
      tooltip = "Text to add at the end of each selected GameObject's name."
    };
    _suffixField.style.flexGrow = 1;
    fieldRow.Add(_suffixField);

    // Persist inputs as user types
    _prefixField.RegisterValueChangedCallback(e =>
    {
      EditorPrefs.SetString(EditorPrefPrefixKey, e.newValue ?? "");
      UpdateHelpAndButton();
    });
    _suffixField.RegisterValueChangedCallback(e =>
    {
      EditorPrefs.SetString(EditorPrefSuffixKey, e.newValue ?? "");
      UpdateHelpAndButton();
    });

    // Action button
    _renameButton = new Button(RenameSelection) { text = "Rename Selected" };
    _renameButton.style.marginTop = 8;
    _renameButton.style.height = 26;
    root.Add(_renameButton);

    // Restore EditorPrefs after fields exist.
    _prefixField.SetValueWithoutNotify(EditorPrefs.GetString(EditorPrefPrefixKey, ""));
    _suffixField.SetValueWithoutNotify(EditorPrefs.GetString(EditorPrefSuffixKey, ""));

    // Initial validation
    UpdateHelpAndButton();
  }

  /// <summary>
  /// Enables/disables the action button and updates help text based on validity:
  /// - Must have at least one GameObject selected
  /// - Must provide a non-empty prefix or suffix
  /// </summary>
  private void UpdateHelpAndButton()
  {
    if (_helpBox == null) return;

    var selectedGOs = Selection.objects.OfType<GameObject>().ToArray();

    bool hasSelection = selectedGOs.Length > 0;
    bool hasText = !string.IsNullOrEmpty(_prefixField?.value) || !string.IsNullOrEmpty(_suffixField?.value);

    if (!hasSelection && !hasText)
    {
      _helpBox.messageType = HelpBoxMessageType.Info;
      _helpBox.text = "Select one or more GameObjects, then enter a prefix and/or suffix.";
    }
    else if (!hasSelection)
    {
      _helpBox.messageType = HelpBoxMessageType.Warning;
      _helpBox.text = "No GameObjects selected.";
    }
    else if (!hasText)
    {
      _helpBox.messageType = HelpBoxMessageType.Warning;
      _helpBox.text = "Enter a prefix and/or suffix to proceed.";
    }
    else
    {
      _helpBox.messageType = HelpBoxMessageType.Info;
      _helpBox.text = $"Ready: {selectedGOs.Length} GameObject(s) will be renamed.";
    }

    _renameButton?.SetEnabled(hasSelection && hasText);
  }

  /// <summary>
  /// Performs the rename with full Undo support. Uses Selection.objects (filtered to GameObjects).
  /// Shows a completion dialog.
  /// </summary>
  private void RenameSelection()
  {
    var prefix = _prefixField.value ?? string.Empty;
    var suffix = _suffixField.value ?? string.Empty;

    var selected = Selection.objects; // as requested, use Selection.objects
    var selectedGOs = selected.OfType<GameObject>().ToList();

    if (selectedGOs.Count == 0)
    {
      EditorUtility.DisplayDialog("Rename", "No GameObjects selected.", "OK");
      return;
    }

    if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
    {
      EditorUtility.DisplayDialog("Rename", "Please enter a prefix and/or suffix.", "OK");
      return;
    }

    // Rename all, recording Undo for each object so the entire operation can be safely reverted.
    Undo.SetCurrentGroupName("Batch Rename Selected GameObjects");
    int undoGroup = Undo.GetCurrentGroup();

    foreach (var go in selectedGOs)
    {
      Undo.RecordObject(go, "Rename GameObject");
      go.name = $"{prefix}{go.name}{suffix}";
      EditorUtility.SetDirty(go);
    }

    Undo.CollapseUndoOperations(undoGroup);

    EditorUtility.DisplayDialog(
        "Rename Complete",
        $"Renamed {selectedGOs.Count} GameObject(s).",
        "OK"
    );

    // Refresh UI
    UpdateHelpAndButton();
  }

}

