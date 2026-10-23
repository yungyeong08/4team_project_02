using UnityEngine;
using UnityEngine.SceneManagement;

public class FishingTitleScreen : MonoBehaviour
{
    public Font customFont;
    public Texture2D backgroundImage;
    public AudioSource bgmAudioSource, sfxAudioSource;
    [Range(0f, 1f)] public float bgmVolume = 1f, sfxVolume = 1f;

    [Header("타이틀 설정")]
    public string gameTitle = "인생 2회차 : 어부";
    public int titleSize = 55;
    public float titleXOffset = 0f;    
    public float titleYOffset = -240f; 
    public bool enableTitleAnim = true; 

    [Header("메인 버튼")]
    public int btnSize = 26;
    public float btnW = 400f, btnH = 65f, btnGap = 75f;

    [Header("팝업 설정")]
    public float popW = 650f, popH = 420f;

    [Header("도움말 글씨 설정")]
    public int helpFontSize = 20;       
    public float helpXOffset = 35f;     
    public float helpYOffset = 70f;     

    [TextArea(3, 6)]
    public string helpMsg = "• 이동: 방향키 / WASD\n\n• 낚시: F키(찌) / Space(잡기)\n\n• 상점: 잡은 물고기를 팔고 낚싯대를 구하세요!";

    private bool showHelp, showSet;

    private void Update()
    {
        if (bgmAudioSource) bgmAudioSource.volume = bgmVolume;
        if (sfxAudioSource) sfxAudioSource.volume = sfxVolume;
    }

    private void OnGUI()
    {
        float cx = Screen.width / 2f, cy = Screen.height / 2f;

        
        if (backgroundImage) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundImage);

        
        float animY = enableTitleAnim ? Mathf.Sin(Time.time * 2f) * 10f : 0f;
        GUI.Label(new Rect(cx - 300f + titleXOffset, cy + titleYOffset + animY, 600f, 100f), gameTitle, Style(titleSize, TextAnchor.MiddleCenter, true));

        
        GUIStyle bStyle = Style(btnSize, TextAnchor.MiddleCenter, false, true);
        float bx = cx - (btnW / 2f), by = cy - 40f - btnGap;

        if (GUI.Button(new Rect(bx, by, btnW, btnH), "게임 시작", bStyle)) SceneManager.LoadScene("FishingDemo");
        if (GUI.Button(new Rect(bx, by + btnGap, btnW, btnH), "도움말", bStyle)) Toggle(ref showHelp, ref showSet);
        if (GUI.Button(new Rect(bx, by + (btnGap * 2), btnW, btnH), "환경 설정", bStyle)) Toggle(ref showSet, ref showHelp);
        if (GUI.Button(new Rect(bx, by + (btnGap * 3), btnW, btnH), "게임 종료", bStyle)) QuitGame();


        if (showHelp)
        {
            DrawBox(cx, cy, "\n[ 도 움 말 ]");
            GUI.Label(new Rect(cx - (popW / 2f) + helpXOffset, cy - (popH / 2f) + helpYOffset, popW - 70f, popH - 90f), helpMsg, Style(helpFontSize));
        }

        
        if (showSet)
        {
            DrawBox(cx, cy, "\n[ 환 경 설 정 ]");
            float sy = cy - (popH / 2f) + 80f;
            bgmVolume = DrawSlider(cx - 220f, sy, $"배경음 (BGM): {Mathf.RoundToInt(bgmVolume * 100)}%", bgmVolume);
            sfxVolume = DrawSlider(cx - 220f, sy + 110f, $"효과음 (SFX): {Mathf.RoundToInt(sfxVolume * 100)}%", sfxVolume);
        }
    }

  
    private GUIStyle Style(int sz, TextAnchor align = TextAnchor.UpperLeft, bool bold = false, bool isBtn = false) =>
        new GUIStyle(isBtn ? GUI.skin.button : GUI.skin.label) { fontSize = sz, alignment = align, font = customFont, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };

    private void DrawBox(float cx, float cy, string title) =>
        GUI.Box(new Rect(cx - (popW / 2f), cy - (popH / 2f), popW, popH), title, Style(28, TextAnchor.UpperCenter, true, true));

    private float DrawSlider(float x, float y, string label, float val)
    {
        GUI.Label(new Rect(x, y, 440f, 35f), label, Style(20));
        return GUI.HorizontalSlider(new Rect(x, y + 40f, 440f, 45f), val, 0f, 1f,
            new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 20f },
            new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 30f, fixedHeight = 35f });
    }

    private void Toggle(ref bool target, ref bool other) { target = !target; other = false; }


    private void QuitGame()
    {
#if UNITY_EDITOR
    // 유니티 에디터에서 플레이 중일 때: Play 모드를 중지시킴
    UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 진짜 게임 파일(.exe)일 때: 게임 완전히 종료
        Application.Quit();
#endif
    }
}