using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public enum SocialEmote : byte
{
    Laugh,
    Speak,
    WaveBye
}

[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerSocialInteractions : NetworkBehaviour
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
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        if (Input.GetKeyDown(laughShortcut)) Laugh();
        if (Input.GetKeyDown(speakShortcut)) Speak();
        if (Input.GetKeyDown(waveShortcut)) WaveBye();
    }

    public void Laugh()
    {
        PerformEmote(SocialEmote.Laugh);
    }

    public void Speak()
    {
        PerformEmote(SocialEmote.Speak);
    }

    public void WaveBye()
    {
        PerformEmote(SocialEmote.WaveBye);
    }

    private void PerformEmote(SocialEmote emote)
    {
        if (IsSpawned)
        {
            if (IsOwner)
            {
                RequestEmoteServerRpc(emote);
            }

            return;
        }

        PlayEmoteLocally(emote);
    }

    [ServerRpc]
    private void RequestEmoteServerRpc(SocialEmote emote)
    {
        PlayEmoteClientRpc(emote);
    }

    [ClientRpc]
    private void PlayEmoteClientRpc(SocialEmote emote)
    {
        PlayEmoteLocally(emote);
    }

    private void PlayEmoteLocally(SocialEmote emote)
    {
        switch (emote)
        {
            case SocialEmote.Laugh:
                PlayTrigger(laughTrigger);
                laughPerformed?.Invoke();
                Debug.Log("Social action: laugh");
                break;
            case SocialEmote.Speak:
                PlayTrigger(speakTrigger);
                voiceLinePerformed?.Invoke();
                Debug.Log("Social action: speak voice line");
                break;
            case SocialEmote.WaveBye:
                PlayTrigger(waveTrigger);
                goodbyeWavePerformed?.Invoke();
                Debug.Log("Social action: wave goodbye");
                break;
        }
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
}