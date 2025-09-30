using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    public int maxPlayers = 4;
    public int startingHP = 4;
    public int startingCards = 2;

    [Header("Prefab References")]
    public GameObject networkPlayerPrefab;
    public GameObject networkRevolverPrefab;
    public GameObject cardPrefab;

    [Header("Network State")]
    public NetworkVariable<GamePhase> currentPhase = new NetworkVariable<GamePhase>(
        GamePhase.Setup,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkList<PlayerNetworkData> networkPlayers;
    public NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);
    public NetworkVariable<bool> clockwiseRotation = new NetworkVariable<bool>(true);

    private Dictionary<ulong, NetworkPlayerController> playerControllers = new Dictionary<ulong, NetworkPlayerController>();
    private NetworkRevolverManager networkRevolver;

    public static NetworkGameManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            networkPlayers = new NetworkList<PlayerNetworkData>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeGame(Dictionary<ulong, int> playerSlots)
    {
        if (!IsServer) return;

        // Create revolver at center
        GameObject revolverObj = Instantiate(
            networkRevolverPrefab,
            new Vector3(0f, 0.88f, 0f),
            Quaternion.Euler(0f, 0, 90f)
        );

        NetworkObject revolverNetObj = revolverObj.GetComponent<NetworkObject>();
        revolverNetObj.Spawn();
        networkRevolver = revolverObj.GetComponent<NetworkRevolverManager>();

        // Create players
        CreateNetworkPlayers(playerSlots);

        // Start game loop
        StartCoroutine(NetworkGameLoop());
    }

    void CreateNetworkPlayers(Dictionary<ulong, int> playerSlots)
    {
        float radius = 1.3f;

        foreach (var slot in playerSlots)
        {
            ulong clientId = slot.Key;
            int slotIndex = slot.Value;

            float angle = (360f / 4) * slotIndex; // Always use 4 slots for consistent positioning

            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                -0.2f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Vector3 directionToCenter = (Vector3.zero - position).normalized;
            float targetY = Quaternion.LookRotation(directionToCenter, Vector3.up).eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0, targetY, 0);

            GameObject playerObj = Instantiate(networkPlayerPrefab, position, rotation);
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId);

            NetworkPlayerController controller = playerObj.GetComponent<NetworkPlayerController>();
            controller.Initialize(slotIndex, startingHP, startingCards);
            playerControllers[clientId] = controller;

            // Add to network list
            PlayerNetworkData data = new PlayerNetworkData
            {
                clientId = clientId,
                playerId = slotIndex,
                isReady = false
            };
            networkPlayers.Add(data);
        }
    }

    System.Collections.IEnumerator NetworkGameLoop()
    {
        while (GetAlivePlayers().Count > 1)
        {
            currentPhase.Value = GamePhase.CardPlay;
            yield return StartCoroutine(NetworkTurnPlayPhase());

            currentPhase.Value = GamePhase.CardExecution;
            yield return StartCoroutine(NetworkExecuteCardsPhase());

            currentPhase.Value = GamePhase.Shop;
            yield return StartCoroutine(NetworkShopPhase());

            // Draw cards
            DrawCardsForAllPlayers();

            // Round cleanup
            RoundCleanup();

            yield return new WaitForSeconds(1f);
        }

        currentPhase.Value = GamePhase.GameOver;
        GameOver();
    }

    System.Collections.IEnumerator NetworkTurnPlayPhase()
    {
        Debug.Log("=== Network Turn Play Phase ===");

        // Enable card selection for all players
        EnableCardSelectionClientRpc(true);

        float timer = 25f;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            UpdateTimerClientRpc(timer);
            yield return null;
        }

        // Disable card selection
        EnableCardSelectionClientRpc(false);
    }

    [ClientRpc]
    void EnableCardSelectionClientRpc(bool enable)
    {
        // Each client enables their own card selection
        if (playerControllers.ContainsKey(NetworkManager.Singleton.LocalClientId))
        {
            var controller = playerControllers[NetworkManager.Singleton.LocalClientId];
            controller.EnableCardSelection(enable);
            controller.EnableItemUsage(enable);
        }
    }

    [ClientRpc]
    void UpdateTimerClientRpc(float timeRemaining)
    {
        // Update UI timer on each client
        // if (TurnManager.Instance != null)
        // {
        //     TurnManager.Instance.UpdateTimerUI($"Time: {timeRemaining:F1}s");
        // }
    }

    System.Collections.IEnumerator NetworkExecuteCardsPhase()
    {
        Debug.Log("=== Network Execute Cards Phase ===");

        int startIndex = networkRevolver.targetPlayerIndex.Value;

        for (int i = 0; i < networkPlayers.Count; i++)
        {
            int playerIndex = GetNextPlayerIndex(startIndex, i);

            var playerData = GetPlayerDataByIndex(playerIndex);
            if (playerData.HasValue)
            {
                var controller = playerControllers[playerData.Value.clientId];
                if (controller != null && controller.IsAlive())
                {
                    yield return StartCoroutine(controller.ExecutePlayedCards());
                }
            }
        }
    }

    System.Collections.IEnumerator NetworkShopPhase()
    {
        Debug.Log("=== Network Shop Phase ===");

        // Simple shop phase - each player gets time to buy
        foreach (var player in GetAlivePlayers())
        {
            yield return new WaitForSeconds(3f);
        }
    }

    void DrawCardsForAllPlayers()
    {
        foreach (var player in GetAlivePlayers())
        {
            player.DrawCard();
        }
    }

    void RoundCleanup()
    {
        foreach (var controller in playerControllers.Values)
        {
            controller.RoundCleanup();
        }
    }

    void GameOver()
    {
        var winners = GetAlivePlayers();
        if (winners.Count > 0)
        {
            Debug.Log($"Game Over! Winner: Player {winners[0].playerId.Value}");
            ShowGameOverClientRpc(winners[0].playerId.Value);
        }
    }

    [ClientRpc]
    void ShowGameOverClientRpc(int winnerPlayerId)
    {
        Debug.Log($"Game Over! Winner: Player {winnerPlayerId}");
        // Show game over UI
    }

    List<NetworkPlayerController> GetAlivePlayers()
    {
        List<NetworkPlayerController> alive = new List<NetworkPlayerController>();
        foreach (var controller in playerControllers.Values)
        {
            if (controller != null && controller.IsAlive())
            {
                alive.Add(controller);
            }
        }
        return alive;
    }

    PlayerNetworkData? GetPlayerDataByIndex(int playerIndex)
    {
        foreach (var data in networkPlayers)
        {
            if (data.playerId == playerIndex)
                return data;
        }
        return null;
    }

    public int GetNextPlayerIndex(int startIndex, int offset)
    {
        if (clockwiseRotation.Value)
        {
            return (startIndex + offset) % networkPlayers.Count;
        }
        else
        {
            return (startIndex - offset + networkPlayers.Count) % networkPlayers.Count;
        }
    }
}