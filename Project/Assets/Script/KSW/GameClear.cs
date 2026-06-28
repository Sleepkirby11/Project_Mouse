using UnityEngine;
using UnityEngine.SceneManagement;

public class GameClear : MonoBehaviour
{
    public string titleSceneName = "KSW_Scene";

    public void GoToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
    }
}