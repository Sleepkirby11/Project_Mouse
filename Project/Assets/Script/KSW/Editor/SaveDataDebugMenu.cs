using UnityEditor;
using UnityEngine;

public static class SaveDataDebugMenu
{
    [MenuItem("Tools/Save/세이브 데이터 삭제")]
    public static void ClearSaveDataMenu()
    {
        SaveManager.ClearSaveData();
        Debug.Log("[SaveManager] 디버그: 세이브 데이터가 삭제되었습니다.");

        if (Application.isPlaying)
        {
            GameStarting menu = Object.FindFirstObjectByType<GameStarting>();
            if (menu != null)
            {
                menu.RefreshMainMenuContinueState();
            }
        }
    }

    [MenuItem("Tools/Save/세이브 데이터 삭제", true)]
    private static bool ValidateClearSaveDataMenu()
    {
        return SaveManager.HasSaveData();
    }
}
