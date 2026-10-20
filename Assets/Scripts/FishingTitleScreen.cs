using UnityEngine;
using UnityEngine.SceneManagement;

public class FishingTitleScreen : MonoBehaviour
{
    [Header("--- 폰트 & 배경 설정 ---")]
    public Font customFont;
    public Texture2D backgroundImage; 

    [Header("--- 게임 제목 설정 ---")]
    public string gameTitle = "인생 2회차 : 어부";
    [Range(20, 100)] public int titleFontSize = 55;
    public float titleYOffset = -240f; 

    [Header("--- 버튼 크기 & 위치 설정 ---")]
    [Range(10, 60)] public int buttonFontSize = 26;
    public float buttonWidth = 400f;
    public float buttonHeight = 70f;
    public float buttonCenterY = -20f; 
    public float buttonSpacing = 90f;  

    [Header("--- 도움말 팝업 설정 ---")]
    public float helpBoxWidth = 650f;
    public float helpBoxHeight = 420f; 
    [Range(15, 50)] public int helpTitleFontSize = 28; 
    [Range(10, 40)] public int helpTextFontSize = 22; 

    [TextArea(5, 10)]
    public string helpMessage = "• 이동: 방향키 또는 W,A,S,D 키\n\n" +
                                "• 낚시하기: F키로 낚싯대 던지기 및 스페이스바(Space)로 물고기 잡기.\n\n" +
                                "• 상점 : 내가 잡은 물고기를 확인할 수 있어요!\n\n" +
                                "• 잡은 물고기는 상점에서 판매해보세요!\n\n" +
                                "• 더 좋은 낚싯대를 구입할 수 있습니다!";

    private bool showHelp = false;

    private void OnGUI()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

      
        if (backgroundImage != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundImage);
        }

  
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = titleFontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            font = customFont
        };
        titleStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(cx - 300f, cy + titleYOffset, 600f, 100f),
            gameTitle,
            titleStyle
        );


        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize,
            font = customFont
        };

        float btnX = cx - (buttonWidth / 2f);


        if (GUI.Button(new Rect(btnX, cy + buttonCenterY - buttonSpacing, buttonWidth, buttonHeight), "게임 시작", btnStyle))
        {
            SceneManager.LoadScene("FishingDemo");
        }

   
        if (GUI.Button(new Rect(btnX, cy + buttonCenterY, buttonWidth, buttonHeight), "도움말", btnStyle))
        {
            showHelp = !showHelp;
        }

    
        if (GUI.Button(new Rect(btnX, cy + buttonCenterY + buttonSpacing, buttonWidth, buttonHeight), "게임 종료", btnStyle))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

      
        if (showHelp)
        {
          
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = helpTitleFontSize,
                fontStyle = FontStyle.Bold,
                font = customFont
            };
            GUI.Box(new Rect(cx - (helpBoxWidth / 2f), cy - (helpBoxHeight / 2f), helpBoxWidth, helpBoxHeight), "\n[ 도 움 말 ]", boxStyle);

          
            GUIStyle helpTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = helpTextFontSize,
                font = customFont
            };
            helpTextStyle.normal.textColor = Color.white;

            GUI.Label(
                new Rect(cx - (helpBoxWidth / 2f) + 35f, cy - (helpBoxHeight / 2f) + 70f, helpBoxWidth - 70f, helpBoxHeight - 90f),
                helpMessage,
                helpTextStyle
            );
        }
    }
}