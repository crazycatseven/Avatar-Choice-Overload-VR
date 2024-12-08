using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Sound Effects")]
    [SerializeField] private AudioClip[] movementSounds;  // Avatar移动时的音效列表
    [SerializeField] private AudioClip confirmSound;      // 确认选择时的音效
    [SerializeField] private AudioClip buttonClickSound;  // 按钮点击音效
    
    [Header("Volume Settings")]
    [SerializeField] private float movementVolume = 0.5f;  // 移动音效的音量
    [SerializeField] private float confirmVolume = 1f;     // 确认音效的音量
    [SerializeField] private float buttonVolume = 1f;      // 按钮音效的音量
    
    private AudioSource audioSource;
    private System.Random random;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        random = new System.Random();
    }

    public void PlayMovementSound()
    {
        if (movementSounds != null && movementSounds.Length > 0)
        {
            int index = random.Next(movementSounds.Length);
            PlaySound(movementSounds[index], movementVolume);
        }
    }

    public void PlayConfirmSound()
    {
        PlaySound(confirmSound, confirmVolume);
    }

    public void PlayButtonClickSound()
    {
        PlaySound(buttonClickSound, buttonVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}