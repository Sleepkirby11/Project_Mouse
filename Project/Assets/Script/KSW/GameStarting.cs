using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStarting : MonoBehaviour
{
    public GameObject settingPanel;

    // 게임 시작 버튼 누를 때 호출
    public void PlayGame()
    {
        SceneManager.LoadScene("Tutorial");
    }

    // 종료 버튼 누를 때 호출
    public void QuitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }

    public void OpenSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(true);
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
    }
}
