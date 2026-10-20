using UnityEngine;
using UnityEngine.SceneManagement;

public class FishingTitleScreen : MonoBehaviour
{
    private bool showHelp = false;

    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 55, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(Screen.width / 2f - 300f, Screen.height / 2f - 240f, 600f, 90f),
            "인생 2회차 : 어부",
            titleStyle
        );


        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26 };

        if (GUI.Button(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 110f, 400f, 70f), "게임 시작", btnStyle))
        {
            SceneManager.LoadScene("FishingDemo");
        }

        if (GUI.Button(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 20f, 400f, 70f), "도움말", btnStyle))
        {
            showHelp = !showHelp;
        }

        if (GUI.Button(new Rect(Screen.width / 2f - 200f, Screen.height / 2f + 70f, 400f, 70f), "게임 종료", btnStyle))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        if (showHelp)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 24, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(Screen.width / 2f - 325f, Screen.height / 2f - 190f, 650f, 380f), "\n[ 도 움 말 ]", boxStyle);

            GUIStyle helpTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            helpTextStyle.normal.textColor = Color.white;

            GUI.Label(
                new Rect(Screen.width / 2f - 290f, Screen.height / 2f - 120f, 580f, 290f),
                "• 이동: 방향키 또는 W,A,S,D 키.\n" +
                "• 낚시하기: F키로 낚싯대 던지기 및 스페이스바(Space)로 물고기 잡기.\n" +
                "• 상점 : 내가 잡은 물고기를 확인할 수 있어요!\n" +
                "• 잡은 물고기는 상점에서 판매해보세요!\n" +
                "• 더 좋은 낚싯대를 구입할 수 있습니다!",
                helpTextStyle
            );
        }
    }
}