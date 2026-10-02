using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class KillFeedUI : MonoBehaviour
{
    [SerializeField] private Text feedText;
    [SerializeField] private int maxEntries = 5;
    [SerializeField] private float entryLifetimeSeconds = 6f;

    private readonly Queue<FeedEntry> entries = new();
    private NetworkMatchManager matchManager;

    private sealed class FeedEntry
    {
        public string message;
        public float remainingSeconds;
    }

    private void OnEnable()
    {
        BindManager();
    }

    private void OnDisable()
    {
        if (matchManager != null)
        {
            matchManager.LastEliminationMessage.OnValueChanged -= OnEliminationMessageChanged;
        }
    }

    private void Update()
    {
        if (matchManager == null)
        {
            BindManager();
        }

        bool changed = false;
        foreach (FeedEntry entry in entries)
        {
            entry.remainingSeconds -= Time.deltaTime;
            changed = true;
        }

        while (entries.Count > 0 && entries.Peek().remainingSeconds <= 0f)
        {
            entries.Dequeue();
            changed = true;
        }

        if (changed)
        {
            RefreshText();
        }
    }

    private void BindManager()
    {
        if (matchManager != null)
        {
            return;
        }

        matchManager = FindObjectOfType<NetworkMatchManager>();
        if (matchManager != null)
        {
            matchManager.LastEliminationMessage.OnValueChanged += OnEliminationMessageChanged;
        }
    }

    private void OnEliminationMessageChanged(Unity.Collections.FixedString128Bytes previousValue, Unity.Collections.FixedString128Bytes newValue)
    {
        string message = newValue.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        entries.Enqueue(new FeedEntry { message = message, remainingSeconds = entryLifetimeSeconds });
        while (entries.Count > Mathf.Max(1, maxEntries))
        {
            entries.Dequeue();
        }

        RefreshText();
    }

    private void RefreshText()
    {
        if (feedText == null)
        {
            return;
        }

        StringBuilder textBuilder = new();
        foreach (FeedEntry entry in entries)
        {
            if (textBuilder.Length > 0)
            {
                textBuilder.Append('\n');
            }

            textBuilder.Append(entry.message);
        }

        feedText.text = textBuilder.ToString();
    }
}
