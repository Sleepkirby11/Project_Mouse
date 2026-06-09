using UnityEngine;
using UnityEditor;

public class SpriteAnchorFixer : EditorWindow
{
    [MenuItem("Tools/Fix Sprite Pivot")]
    public static void ShowWindow()
    {
        GetWindow<SpriteAnchorFixer>("Fix Sprite Pivot");
    }

    private PivotMode pivotMode = PivotMode.Bottom;

    public enum PivotMode
    {
        Center, Bottom, Top, Left, Right,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    private void OnGUI()
    {
        GUILayout.Label("선택한 스프라이트의 피벗을 일괄 변경", EditorStyles.boldLabel);
        pivotMode = (PivotMode)EditorGUILayout.EnumPopup("피벗 위치", pivotMode);

        if (GUILayout.Button("적용"))
        {
            ApplyPivot();
        }
    }

    private void ApplyPivot()
    {
        Object[] selected = Selection.objects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("스프라이트를 선택해주세요.");
            return;
        }

        foreach (Object obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = GetPivotVector(pivotMode);
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        Debug.Log($"{selected.Length}개 스프라이트 피벗 변경 완료");
    }

    private Vector2 GetPivotVector(PivotMode mode)
    {
        return mode switch
        {
            PivotMode.Center => new Vector2(0.5f, 0.5f),
            PivotMode.Bottom => new Vector2(0.5f, 0f),
            PivotMode.Top => new Vector2(0.5f, 1f),
            PivotMode.Left => new Vector2(0f, 0.5f),
            PivotMode.Right => new Vector2(1f, 0.5f),
            PivotMode.TopLeft => new Vector2(0f, 1f),
            PivotMode.TopRight => new Vector2(1f, 1f),
            PivotMode.BottomLeft => new Vector2(0f, 0f),
            PivotMode.BottomRight => new Vector2(1f, 0f),
            _ => new Vector2(0.5f, 0.5f)
        };
    }
}