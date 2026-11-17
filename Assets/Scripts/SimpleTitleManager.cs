using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleTitleManager : MonoBehaviour
{
    public GameObject settingPanel; 
    public AudioSource bgmAudio;    
    public AudioSource sfxAudio;    


    public void StartGame()
    {
        SceneManager.LoadScene("FishingDemo");
    }

    public void ToggleSetting()
    {
        settingPanel.SetActive(!settingPanel.activeSelf);
    }

   
    public void SetBgmVolume(float volume)
    {
        if (bgmAudio != null) bgmAudio.volume = volume;
    }


    public void SetSfxVolume(float volume)
    {
        if (sfxAudio != null) sfxAudio.volume = volume;
    }

  
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
