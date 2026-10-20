using UnityEngine;
using UnityEngine.SceneManagement;

public class FishingTitleScreen : MonoBehaviour
{
    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 36;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(Screen.width / 2f - 200f,
            Screen.height / 2f - 120f, 400f, 70f),
            "나의 낚시 게임",
            titleStyle
        );

        if (GUI.Button(
            new Rect(Screen.width / 2f - 160f,
            Screen.height / 2f, 320f, 70f),
            "게임 시작"))
        {
            SceneManager.LoadScene("FishingDemo");
        }
    }
}
