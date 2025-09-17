using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public class StyleSheetHelper
{

  public static void AttatchStyleSheet(VisualElement root, string path)
  {
    var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);

    if (ss != null)
    {
      root.styleSheets.Add(ss);
    }
    else
    {
      Debug.LogWarning($"{path} not found!");
    }
  }

}
