using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class MatchResultsUI : MonoBehaviour
{
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private Text winnerText;
    [SerializeField] private Text mvpText;
    [SerializeField] private Text scoreboardText;

    private NetworkMatchManager matchManager;

    private void OnEnable()
    {
        matchManager = FindObjectOfType<NetworkMatchManager>();
        if (matchManager != null)
        {
            matchManager.ResultsReady.OnValueChanged += OnResultsReadyChanged;
            if (matchManager.ResultsReady.Value)
            {
                RefreshResults();
            }
        }

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (matchManager != null)
        {
            matchManager.ResultsReady.OnValueChanged -= OnResultsReadyChanged;
        }
    }

    private void OnResultsReadyChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            RefreshResults();
        }
    }

    private void RefreshResults()
    {
        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (winnerText != null) winnerText.text = "Winner: " + matchManager.WinnerName.Value;
        if (mvpText != null) mvpText.text = "MVP: " + matchManager.MvpName.Value;

        if (scoreboardText == null)
        {
            return;
        }

        StringBuilder builder = new();
        for (int index = 0; index < matchManager.MatchResults.Count; index++)
        {
            MatchResultEntry entry = matchManager.MatchResults[index];
            string badges = entry.isWinner ? " [WINNER]" : entry.isMvp ? " [MVP]" : string.Empty;
            builder.Append(entry.playerName)
                .Append(badges)
                .Append(" | Kills: ").Append(entry.kills)
                .Append(" | Survival: ").Append(Mathf.FloorToInt(entry.survivalSeconds)).Append("s")
                .Append(" | Deliveries: ").Append(entry.deliveries)
                .Append('\n');
        }

        scoreboardText.text = builder.ToString();
    }
}
