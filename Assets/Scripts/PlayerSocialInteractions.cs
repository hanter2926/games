using UnityEngine;
using UnityEngine.Events;

public sealed class PlayerSocialInteractions : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string laughTrigger = "Laugh";
    [SerializeField] private string waveTrigger = "Wave";
    [SerializeField] private string speakTrigger = "Speak";

    [Header("Optional Events")]
    public UnityEvent laughPerformed;
    public UnityEvent voiceLinePerformed;
    public UnityEvent goodbyeWavePerformed;

    [Header("PC Shortcuts")]
    [SerializeField] private KeyCode laughShortcut = KeyCode.Alpha1;
    [SerializeField] private KeyCode speakShortcut = KeyCode.Alpha2;
    [SerializeField] private KeyCode waveShortcut = KeyCode.Alpha3;

    private void Update()
    {
        if (Input.GetKeyDown(laughShortcut)) Laugh();
        if (Input.GetKeyDown(speakShortcut)) Speak();
        if (Input.GetKeyDown(waveShortcut)) WaveBye();
    }

    public void Laugh()
    {
        PlayTrigger(laughTrigger);
        laughPerformed?.Invoke();
        Debug.Log("Social action: laugh");
    }

    public void Speak()
    {
        PlayTrigger(speakTrigger);
        voiceLinePerformed?.Invoke();
        Debug.Log("Social action: speak voice line");
    }

    public void WaveBye()
    {
        PlayTrigger(waveTrigger);
        goodbyeWavePerformed?.Invoke();
        Debug.Log("Social action: wave goodbye");
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
}