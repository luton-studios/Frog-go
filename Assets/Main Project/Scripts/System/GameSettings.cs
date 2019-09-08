using UnityEngine;

public class GameSettings : MonoBehaviour {
    public static GameSettings instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;
    public UIReferences[] uiReferences;

    public bool vibrationEnabled { get; private set; }

    private float defMusicVolume;

    private void Awake() {
        instance = this;
        defMusicVolume = musicSource.volume;

        // Load and apply settings.
        OnMusicVolumeUpdated(PlayerPrefs.GetFloat("MusicVolume", 1f));
        OnSfxVolumeUpdated(PlayerPrefs.GetFloat("SFXVolume", 1f));
        OnBatterySaverUpdated(PlayerPrefs.GetInt("BatterySaver", 0) == 1);
        OnVibrationUpdated(PlayerPrefs.GetInt("Vibration", 1) == 1);
    }

    public void OnMusicVolumeUpdated(float value) {
        PlayerPrefs.SetFloat("MusicVolume", value);

        for(int i = 0; i < uiReferences.Length; i++)
            uiReferences[i].musicVolumeSlider.value = value;

        float targetSrcVolume = defMusicVolume * value;

        if(musicSource.volume != targetSrcVolume) {
            musicSource.volume = targetSrcVolume;
        }
    }

    public void OnSfxVolumeUpdated(float value) {
        PlayerPrefs.SetFloat("SFXVolume", value);

        for(int i = 0; i < uiReferences.Length; i++)
            uiReferences[i].sfxVolumeSlider.value = value;

        if(sfxSource.volume != value) {
            sfxSource.volume = value;
            NGUITools.soundVolume = value;
        }
    }

    public void OnBatterySaverBack() {
        OnBatterySaverUpdated(false);
    }

    public void OnBatterySaverFwd() {
        OnBatterySaverUpdated(true);
    }

    private void OnBatterySaverUpdated(bool on) {
        if(on) {
            PlayerPrefs.SetInt("BatterySaver", 1);
            Application.targetFrameRate = 30;

            for(int i = 0; i < uiReferences.Length; i++) {
                uiReferences[i].batterySaverLabel.text = "On";
                uiReferences[i].batterySaverBack.isEnabled = true;
                uiReferences[i].batterySaverFwd.isEnabled = false;
            }
        }
        else {
            PlayerPrefs.SetInt("BatterySaver", 0);
            Application.targetFrameRate = 60;

            for(int i = 0; i < uiReferences.Length; i++) {
                uiReferences[i].batterySaverLabel.text = "Off";
                uiReferences[i].batterySaverBack.isEnabled = false;
                uiReferences[i].batterySaverFwd.isEnabled = true;
            }
        }
    }

    public void OnVibrationBack() {
        OnVibrationUpdated(false);
    }

    public void OnVibrationFwd() {
        OnVibrationUpdated(true);
    }

    private void OnVibrationUpdated(bool on) {
        vibrationEnabled = on;

        if(on) {
            PlayerPrefs.SetInt("Vibration", 1);

            for(int i = 0; i < uiReferences.Length; i++) {
                uiReferences[i].vibrationLabel.text = "On";
                uiReferences[i].vibrationBack.isEnabled = true;
                uiReferences[i].vibrationFwd.isEnabled = false;
            }
        }
        else {
            PlayerPrefs.SetInt("Vibration", 0);

            for(int i = 0; i < uiReferences.Length; i++) {
                uiReferences[i].vibrationLabel.text = "Off";
                uiReferences[i].vibrationBack.isEnabled = false;
                uiReferences[i].vibrationFwd.isEnabled = true;
            }
        }
    }

    [System.Serializable]
    public class UIReferences {
        public UILabel signedInAs;
        public UIButton signInOutButton;
        public UILabel signInOutButtonLabel;
        public UISlider musicVolumeSlider;
        public UISlider sfxVolumeSlider;
        public UILabel batterySaverLabel;
        public UIButton batterySaverBack;
        public UIButton batterySaverFwd;
        public UILabel vibrationLabel;
        public UIButton vibrationBack;
        public UIButton vibrationFwd;
    }
}