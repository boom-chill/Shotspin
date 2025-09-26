using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    [Header("Turn Settings")]
    public float cardPlayTime = 25f;
    public float itemUseTime = 3f;

    [Header("UI References")]
    public UnityEngine.UI.Text timerText;
    public UnityEngine.UI.Text currentPlayerText;

    private bool isCardPlayPhase = false;
    private bool isItemPhase = false;
    private List<int> skippedPlayers = new List<int>();

    public IEnumerator CardPlayPhase(List<PlayerController> alivePlayers)
    {
        Debug.Log("=== Card Play Phase Started ===");
        isCardPlayPhase = true;

        // Enable card selection cho tất cả players
        foreach (var player in alivePlayers)
        {
            player.EnableCardSelection(true);
        }

        // Countdown timer
        float remainingTime = cardPlayTime;

        while (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;

            // Update timer UI
            if (timerText != null)
            {
                timerText.text = $"Card Play: {remainingTime:F1}s";
            }

            yield return null;
        }

        // Disable card selection
        foreach (var player in alivePlayers)
        {
            player.EnableCardSelection(false);
        }

        isCardPlayPhase = false;

        if (timerText != null)
        {
            timerText.text = "Card Play: Complete";
        }

        Debug.Log("=== Card Play Phase Ended ===");
    }

    public IEnumerator ExecutePlayerTurns(List<PlayerController> alivePlayers)
    {
        Debug.Log("=== Executing Player Turns ===");

        // Bắt đầu từ người bị súng chỉ vào
        int startPlayerIndex = GameManager.Instance.revolver.GetTargetPlayerIndex();

        for (int i = 0; i < GameManager.Instance.players.Count; i++)
        {
            int playerIndex = GetNextPlayerIndex(startPlayerIndex, i);
            PlayerController currentPlayer = GameManager.Instance.players[playerIndex];

            // Skip dead players
            if (!currentPlayer.IsAlive()) continue;

            // Check if player is skipped this round
            if (skippedPlayers.Contains(playerIndex))
            {
                Debug.Log($"Player {playerIndex} is skipped this turn!");

                // Player still loses their cards but doesn't execute
                currentPlayer.RoundCleanup();
                continue;
            }

            Debug.Log($"=== Player {playerIndex} Turn ===");

            // Update current player UI
            if (currentPlayerText != null)
            {
                currentPlayerText.text = $"Current: Player {playerIndex}";
            }

            // Item use phase (3 seconds)
            yield return StartCoroutine(ItemUsePhase(currentPlayer));

            // Execute played cards
            yield return StartCoroutine(currentPlayer.ExecutePlayedCards());

            // Small delay between players
            yield return new WaitForSeconds(0.5f);
        }

        // Clear skipped players for next round
        skippedPlayers.Clear();

        if (currentPlayerText != null)
        {
            currentPlayerText.text = "Turn Complete";
        }

        Debug.Log("=== All Player Turns Complete ===");
    }

    IEnumerator ItemUsePhase(PlayerController player)
    {
        if (player.items.Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        Debug.Log($"Player {player.playerId} - Item use phase");
        isItemPhase = true;

        // Enable item usage for this player only
        player.EnableItemUsage(true);

        float remainingTime = itemUseTime;
        bool itemUsed = false;

        while (remainingTime > 0 && !itemUsed)
        {
            remainingTime -= Time.deltaTime;

            // Update timer UI
            if (timerText != null)
            {
                timerText.text = $"Item Use: {remainingTime:F1}s";
            }

            // Check if item was used (simplified - in real game track via events)
            // itemUsed = player.hasUsedItemThisRound;

            yield return null;
        }

        // Disable item usage
        player.EnableItemUsage(false);
        isItemPhase = false;

        if (timerText != null)
        {
            timerText.text = "";
        }
    }

    public void SkipPlayer(int playerIndex)
    {
        if (!skippedPlayers.Contains(playerIndex))
        {
            skippedPlayers.Add(playerIndex);
            Debug.Log($"Player {playerIndex} will be skipped next turn!");
        }
    }

    int GetNextPlayerIndex(int startIndex, int offset)
    {
        return GameManager.Instance.GetNextPlayerIndex(startIndex, offset);
    }

    // UI Methods
    public void UpdateTimerUI(string text)
    {
        if (timerText != null)
        {
            timerText.text = text;
        }
    }

    public void UpdateCurrentPlayerUI(int playerIndex)
    {
        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"Current: Player {playerIndex}";
        }
    }
}
