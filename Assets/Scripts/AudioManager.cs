using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    [Header("Mixer & Sliders")]
    public AudioMixer mainMixer;
    public Slider masterSlider; // Thêm thanh Slider cho Master
    public Slider musicSlider;
    public Slider sfxSlider;

    void Start()
    {
        // 1. LẤY DỮ LIỆU CŨ: Đọc giá trị đã lưu (nếu chưa lưu bao giờ thì mặc định là 1)
        float savedMasterVol = PlayerPrefs.GetFloat("SavedMasterVol", 1f);
        float savedMusicVol = PlayerPrefs.GetFloat("SavedMusicVol", 1f);
        float savedSFXVol = PlayerPrefs.GetFloat("SavedSFXVol", 1f);

        // 2. CẬP NHẬT UI MASTER
        if (masterSlider != null)
        {
            masterSlider.value = savedMasterVol;
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        // 3. CẬP NHẬT UI MUSIC
        if (musicSlider != null)
        {
            musicSlider.value = savedMusicVol;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        // 4. CẬP NHẬT UI SFX
        if (sfxSlider != null)
        {
            sfxSlider.value = savedSFXVol;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // 5. ÁP DỤNG ÂM THANH NGAY LẬP TỨC
        SetMasterVolume(savedMasterVol);
        SetMusicVolume(savedMusicVol);
        SetSFXVolume(savedSFXVol);
    }

    public void SetMasterVolume(float sliderValue)
    {
        // Chuyển đổi giá trị tuyến tính của Slider sang Logarit (dB)
        float dbValue = Mathf.Log10(sliderValue) * 20;

        // Gọi parameter "MasterVol" trong Audio Mixer
        mainMixer.SetFloat("MasterVol", dbValue);

        // LƯU LẠI DỮ LIỆU
        PlayerPrefs.SetFloat("SavedMasterVol", sliderValue);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float sliderValue)
    {
        float dbValue = Mathf.Log10(sliderValue) * 20;
        mainMixer.SetFloat("MusicVol", dbValue);

        PlayerPrefs.SetFloat("SavedMusicVol", sliderValue);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float sliderValue)
    {
        float dbValue = Mathf.Log10(sliderValue) * 20;
        mainMixer.SetFloat("SFXVol", dbValue);

        PlayerPrefs.SetFloat("SavedSFXVol", sliderValue);
        PlayerPrefs.Save();
    }
}