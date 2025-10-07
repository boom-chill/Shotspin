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
        Debug.Log($"[GameManager] Awake called. Instance exists: {Instance != null}");

        if (Instance == null)
        {
            Instance = this;
            networkPlayers = new NetworkList<PlayerNetworkData>();
            Debug.Log("[GameManager] Instance created and networkPlayers initialized");
        }
        else
        {
            Debug.LogWarning($"[GameManager] Duplicate instance destroyed. Existing instance: {Instance.name}");
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Listen for spawned objects to track player controllers
        if (IsClient)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToGame;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToGame;
        }
    }

    void OnClientConnectedToGame(ulong clientId)
    {
        Debug.Log($"[GameManager] Client {clientId} connected to game scene");
    }

    public void InitializeGame(Dictionary<ulong, int> playerSlots)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[GameManager] InitializeGame called on non-server!");
            return;
        }

        Debug.Log($"[GameManager] InitializeGame called with {playerSlots.Count} players");

        // Validate prefabs
        if (networkRevolverPrefab == null)
        {
            Debug.LogError("[GameManager] networkRevolverPrefab is NULL! Assign it in Inspector.");
            return;
        }

        if (networkPlayerPrefab == null)
        {
            Debug.LogError("[GameManager] networkPlayerPrefab is NULL! Assign it in Inspector.");
            return;
        }

        // Clear previous data
        if (networkPlayers != null)
        {
            networkPlayers.Clear();
            Debug.Log("[GameManager] Cleared networkPlayers list");
        }
        else
        {
            Debug.LogError("[GameManager] networkPlayers is NULL!");
            return;
        }

        playerControllers.Clear();
        Debug.Log("[GameManager] Cleared playerControllers dictionary");

        // Create revolver at center
        Debug.Log("[GameManager] Creating revolver...");
        GameObject revolverObj = Instantiate(
            networkRevolverPrefab,
            new Vector3(0f, 0.88f, 0f),
            Quaternion.Euler(0f, 0, 90f)
        );

        NetworkObject revolverNetObj = revolverObj.GetComponent<NetworkObject>();
        if (revolverNetObj == null)
        {
            Debug.LogError("[GameManager] Revolver prefab missing NetworkObject component!");
            Destroy(revolverObj);
            return;
        }

        revolverNetObj.Spawn();
        networkRevolver = revolverObj.GetComponent<NetworkRevolverManager>();

        if (networkRevolver == null)
        {
            Debug.LogError("[GameManager] Revolver prefab missing NetworkRevolverManager component!");
        }
        else
        {
            Debug.Log("[GameManager] ✓ Revolver spawned successfully");
        }

        // Create players
        Debug.Log("[GameManager] Creating players...");
        CreateNetworkPlayers(playerSlots);

        // Wait a frame for all spawns to complete, then notify clients
        StartCoroutine(WaitForSpawnsThenStart());
    }

    System.Collections.IEnumerator WaitForSpawnsThenStart()
    {
        // Wait for all network spawns to complete
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"[GameManager] All players spawned. Player count: {playerControllers.Count}");

        // Notify all clients that setup is complete
        NotifyGameStartClientRpc();

        // Start game loop
        yield return new WaitForSeconds(1f);
        StartCoroutine(NetworkGameLoop());
    }

    void CreateNetworkPlayers(Dictionary<ulong, int> playerSlots)
    {
        float radius = 1.3f;

        foreach (var slot in playerSlots)
        {
            ulong clientId = slot.Key;
            int slotIndex = slot.Value;

            Debug.Log($"[GameManager] Creating player for Client {clientId} in slot {slotIndex}");

            // Calculate position
            float angle = (360f / 4) * slotIndex;
            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                -0.2f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            // Calculate rotation facing center
            Vector3 directionToCenter = (Vector3.zero - position).normalized;
            float targetY = Quaternion.LookRotation(directionToCenter, Vector3.up).eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0, targetY, 0);

            // Instantiate player
            GameObject playerObj = Instantiate(networkPlayerPrefab, position, rotation);

            // Get components before spawning
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            NetworkPlayerController controller = playerObj.GetComponent<NetworkPlayerController>();

            if (netObj == null)
            {
                Debug.LogError($"[GameManager] NetworkObject component missing on player prefab!");
                Destroy(playerObj);
                continue;
            }

            if (controller == null)
            {
                Debug.LogError($"[GameManager] NetworkPlayerController component missing on player prefab!");
                Destroy(playerObj);
                continue;
            }

            // Spawn as player object for specific client
            netObj.SpawnAsPlayerObject(clientId, true);

            // Initialize controller
            controller.Initialize(slotIndex, startingHP, startingCards);

            // Track controller
            playerControllers[clientId] = controller;

            // Add to network list
            PlayerNetworkData data = new PlayerNetworkData
            {
                clientId = clientId,
                playerId = slotIndex,
                isReady = false
            };
            networkPlayers.Add(data);

            Debug.Log($"[GameManager] ✓ Player spawned for Client {clientId} at position {position}");
        }

        Debug.Log($"[GameManager] Total players created: {playerControllers.Count}");
    }

    [ClientRpc]
    void NotifyGameStartClientRpc()
    {
        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Game started!");

        // Find and setup local player
        StartCoroutine(FindAndSetupLocalPlayer());
    }

    System.Collections.IEnumerator FindAndSetupLocalPlayer()
    {
        // Wait for player objects to be spawned on client
        yield return new WaitForSeconds(0.5f);

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        // Find all NetworkPlayerController objects in scene
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();

        Debug.Log($"[Client {localClientId}] Found {allPlayers.Length} player objects in scene");

        NetworkPlayerController localPlayer = null;

        // Find the player that belongs to this client
        foreach (var player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == localClientId)
            {
                localPlayer = player;
                Debug.Log($"[Client {localClientId}] Found local player! PlayerId: {player.playerId.Value}");
                break;
            }
        }

        if (localPlayer != null)
        {
            // Enable camera for local player only
            Camera playerCamera = localPlayer.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
                playerCamera.enabled = true;
                Debug.Log($"[Client {localClientId}] ✓ Camera enabled");
            }
            else
            {
                Debug.LogWarning($"[Client {localClientId}] Camera not found in player hierarchy!");
            }

            // Disable cameras on other players
            foreach (var player in allPlayers)
            {
                if (player != localPlayer)
                {
                    Camera otherCamera = player.GetComponentInChildren<Camera>(true);
                    if (otherCamera != null)
                    {
                        otherCamera.gameObject.SetActive(false);
                        otherCamera.enabled = false;
                    }
                }
            }

            // Setup cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Debug.LogError($"[Client {localClientId}] Could not find local player object!");
        }
    }

    System.Collections.IEnumerator NetworkGameLoop()
    {
        Debug.Log("[GameLoop] Starting game loop");

        while (GetAlivePlayers().Count > 1)
        {
            currentPhase.Value = GamePhase.CardPlay;
            yield return StartCoroutine(NetworkTurnPlayPhase());

            currentPhase.Value = GamePhase.CardExecution;
            yield return StartCoroutine(NetworkExecuteCardsPhase());

            currentPhase.Value = GamePhase.Shop;
            yield return StartCoroutine(NetworkShopPhase());

            DrawCardsForAllPlayers();
            RoundCleanup();

            yield return new WaitForSeconds(1f);
        }

        currentPhase.Value = GamePhase.GameOver;
        GameOver();
    }

    System.Collections.IEnumerator NetworkTurnPlayPhase()
    {
        Debug.Log("=== Network Turn Play Phase ===");
        EnableCardSelectionClientRpc(true);

        float timer = 25f;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            UpdateTimerClientRpc(timer);
            yield return null;
        }

        EnableCardSelectionClientRpc(false);
    }

    [ClientRpc]
    void EnableCardSelectionClientRpc(bool enable)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        // Find local player controller
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == localClientId)
            {
                player.EnableCardSelection(enable);
                player.EnableItemUsage(enable);
                Debug.Log($"[Client {localClientId}] Card selection: {enable}");
                break;
            }
        }
    }

    [ClientRpc]
    void UpdateTimerClientRpc(float timeRemaining)
    {
        // Update UI timer (implement as needed)
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
                if (playerControllers.ContainsKey(playerData.Value.clientId))
                {
                    var controller = playerControllers[playerData.Value.clientId];
                    if (controller != null && controller.IsAlive())
                    {
                        yield return StartCoroutine(controller.ExecutePlayedCards());
                    }
                }
            }
        }
    }

    System.Collections.IEnumerator NetworkShopPhase()
    {
        Debug.Log("=== Network Shop Phase ===");
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
            if (controller != null)
            {
                controller.RoundCleanup();
            }
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