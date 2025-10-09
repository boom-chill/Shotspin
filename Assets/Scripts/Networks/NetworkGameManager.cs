using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    public int maxPlayers = 4;
    public int startingHP = 4;
    public int startingCards = 2;
    public float turnTimer = 25f;

    [Header("Prefab References")]
    public GameObject networkPlayerPrefab;
    public GameObject networkRevolverPrefab;
    public GameObject cardPrefab;

    [Header("Card Database")]
    public List<CardData> allCards = new List<CardData>();

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
    private NetworkShopManager networkShopManager;
    private NetworkDeckManager networkDeckManager;

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

        if (IsClient)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToGame;
            
            // ✅ THÊM: Subscribe to phase changes
            currentPhase.OnValueChanged += OnPhaseChangedClient;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToGame;
        }

        // ✅ THÊM: Unsubscribe
        currentPhase.OnValueChanged -= OnPhaseChangedClient;
    }

    void OnClientConnectedToGame(ulong clientId)
    {
        Debug.Log($"[GameManager] Client {clientId} connected to game scene");
    }

    // ✅ THÊM: Client-side phase change handler
    void OnPhaseChangedClient(GamePhase oldPhase, GamePhase newPhase)
    {
        if (!IsClient) return;

        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Phase changed: {oldPhase} → {newPhase}");

        // Show UI feedback based on phase
        switch (newPhase)
        {
            case GamePhase.Setup:
                ShowPhaseUI("Game Starting...", Color.white);
                break;

            case GamePhase.RevolverReveal:
                ShowPhaseUI("Revolver Reveal Phase", Color.yellow);
                break;

            case GamePhase.CardPlay:
                ShowPhaseUI("Card Play Phase", Color.cyan);
                break;

            case GamePhase.CardExecution:
                ShowPhaseUI("Card Execution Phase", Color.magenta);
                break;

            case GamePhase.Shop:
                ShowPhaseUI("Shop Phase", Color.green);
                break;

            case GamePhase.DrawCards:
                ShowPhaseUI("Draw Cards Phase", Color.blue);
                break;

            case GamePhase.GameOver:
                ShowPhaseUI("Game Over!", Color.red);
                break;
        }
    }

    void ShowPhaseUI(string phaseName, Color color)
    {
        // TODO: Update actual UI
        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>[PHASE] {phaseName}</color>");
        
        // Example: If you have a UI Text element
        // phaseText.text = phaseName;
        // phaseText.color = color;
    }

    // ====================== INITIALIZATION ======================

    public void InitializeGame(Dictionary<ulong, int> playerSlots)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[GameManager] InitializeGame called on non-server!");
            return;
        }

        Debug.Log($"[GameManager] InitializeGame called with {playerSlots.Count} players");

        if (networkRevolverPrefab == null || networkPlayerPrefab == null)
        {
            Debug.LogError("[GameManager] Missing prefab references!");
            return;
        }

        if (networkPlayers != null)
        {
            networkPlayers.Clear();
        }
        else
        {
            Debug.LogError("[GameManager] networkPlayers is NULL!");
            return;
        }

        playerControllers.Clear();

        // Create revolver
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

        // Find managers
        networkShopManager = FindObjectOfType<NetworkShopManager>();
        networkDeckManager = FindObjectOfType<NetworkDeckManager>();

        if (networkShopManager == null)
            Debug.LogWarning("[GameManager] NetworkShopManager not found!");
        if (networkDeckManager == null)
            Debug.LogWarning("[GameManager] NetworkDeckManager not found!");

        StartCoroutine(WaitForSpawnsThenStart());
    }

    System.Collections.IEnumerator WaitForSpawnsThenStart()
    {
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"[GameManager] All players spawned. Player count: {playerControllers.Count}");
        
        LogPlayers();

        // ✅ THÊM: Notify clients về game start
        NotifyGameStartClientRpc();

        yield return new WaitForSeconds(1f);
        
        // ✅ Server starts game loop
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

            float angle = (360f / 4) * slotIndex;
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
            NetworkPlayerController controller = playerObj.GetComponent<NetworkPlayerController>();

            if (netObj == null || controller == null)
            {
                Debug.LogError($"[GameManager] Missing components on player prefab!");
                Destroy(playerObj);
                continue;
            }

            netObj.SpawnAsPlayerObject(clientId, true);
            controller.Initialize(slotIndex, startingHP, startingCards);

            playerControllers[clientId] = controller;

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
        
        if (networkRevolver != null)
        {
            networkRevolver.SetTargetPlayer(0);
        }
    }

    // ====================== CLIENT SETUP ======================

    [ClientRpc]
    void NotifyGameStartClientRpc()
    {
        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Game started!");
        StartCoroutine(FindAndSetupLocalPlayer());
    }

    System.Collections.IEnumerator FindAndSetupLocalPlayer()
    {
        yield return new WaitForSeconds(0.5f);

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();

        Debug.Log($"[Client {localClientId}] Found {allPlayers.Length} player objects in scene");

        NetworkPlayerController localPlayer = null;

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
            Camera playerCamera = localPlayer.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
                playerCamera.enabled = true;
                Debug.Log($"[Client {localClientId}] ✓ Camera enabled");
            }

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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Debug.LogError($"[Client {localClientId}] Could not find local player object!");
        }
    }

    // ====================== GAME LOOP (SERVER ONLY) ======================

    System.Collections.IEnumerator NetworkGameLoop()
    {
        if (!IsServer) yield break;

        Debug.Log("[GameLoop] Starting game loop on SERVER");

        // ✅ Notify clients that game loop started
        AnnouncePhaseClientRpc("Game loop started!");

        while (GetAlivePlayers().Count > 1)
        {
            // Phase 1: Revolver Reveal
            currentPhase.Value = GamePhase.RevolverReveal;
            yield return StartCoroutine(RevolverPhase());

            // Phase 2: Card Play
            currentPhase.Value = GamePhase.CardPlay;
            yield return StartCoroutine(NetworkTurnPlayPhase());

            // Phase 3: Card Execution
            currentPhase.Value = GamePhase.CardExecution;
            yield return StartCoroutine(NetworkExecuteCardsPhase());

            // Phase 4: Shop
            currentPhase.Value = GamePhase.Shop;
            yield return StartCoroutine(NetworkShopPhase());

            // Phase 5: Draw Cards
            currentPhase.Value = GamePhase.DrawCards;
            DrawCardsForAllPlayers();
            
            RoundCleanup();

            yield return new WaitForSeconds(1f);
        }

        currentPhase.Value = GamePhase.GameOver;
        GameOver();
    }

    // ✅ THÊM: Announce messages to all clients
    [ClientRpc]
    void AnnouncePhaseClientRpc(string message)
    {
        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] 📢 {message}");
    }

    System.Collections.IEnumerator RevolverPhase()
    {
        if (!IsServer) yield break;

        Debug.Log("[Server] === Revolver Phase ===");
        
        // ✅ Announce to clients
        AnnouncePhaseClientRpc("Revolver revealing bullets...");
        
        if (networkRevolver != null)
        {
            networkRevolver.RevealAllSlotsClientRpc();
        }
        
        yield return new WaitForSeconds(2f);
    }

    System.Collections.IEnumerator NetworkTurnPlayPhase()
    {
        if (!IsServer) yield break;

        Debug.Log("[Server] === Network Turn Play Phase (Turn-Based) ===");
        
        // Bắt đầu từ người bị revolver chỉ vào
        int startIndex = networkRevolver.targetPlayerIndex.Value;

        // Announce to clients
        AnnouncePhaseClientRpc($"Turn-based card play starting from Player {startIndex}!");

        // Lần lượt từng người chơi
        for (int i = 0; i < networkPlayers.Count; i++)
        {
            int playerIndex = GetNextPlayerIndex(startIndex, i);
            var playerData = GetPlayerDataByIndex(playerIndex);

            if (!playerData.HasValue) continue;

            if (playerControllers.ContainsKey(playerData.Value.clientId))
            {
                var controller = playerControllers[playerData.Value.clientId];
                
                if (controller == null || !controller.IsAlive())
                {
                    Debug.Log($"[Server] Player {playerIndex} is dead, skipping turn");
                    continue;
                }

                // ✅ Announce current player's turn
                AnnouncePhaseClientRpc($"Player {playerIndex}'s turn!");
                NotifyPlayerTurnClientRpc(playerData.Value.clientId, true);

                Debug.Log($"[Server] --- Player {playerIndex} turn ---");

                // ✅ Chỉ enable cho người chơi hiện tại
                EnableCardSelectionForPlayerClientRpc(playerData.Value.clientId, true);

                // ✅ Countdown timer cho người chơi này
                float timer = turnTimer;
                while (timer > 0)
                {
                    timer -= Time.deltaTime;
                    UpdateTimerClientRpc(timer);
                    yield return null;
                }

                // ✅ Disable cho người chơi này
                EnableCardSelectionForPlayerClientRpc(playerData.Value.clientId, false);
                NotifyPlayerTurnClientRpc(playerData.Value.clientId, false);

                Debug.Log($"[Server] Player {playerIndex} turn ended");

                // ✅ Ngắt giữa các lượt
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ✅ Announce phase ended
        AnnouncePhaseClientRpc("All players have played their cards!");
    }

    [ClientRpc]
    void EnableCardSelectionForPlayerClientRpc(ulong targetClientId, bool enable)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        // Chỉ client được chỉ định mới xử lý
        if (localClientId != targetClientId) return;

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

    // ✅ NEW: Notify player về turn của họ (for UI highlight)
    [ClientRpc]
    void NotifyPlayerTurnClientRpc(ulong targetClientId, bool isTheirTurn)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (localClientId == targetClientId)
        {
            // Đây là turn của bạn!
            Debug.Log($"[Client {localClientId}] 🎯 IT'S YOUR TURN!");
            // TODO: Show "YOUR TURN" UI
            // turnIndicatorText.text = "YOUR TURN!";
            // turnIndicatorPanel.SetActive(isTheirTurn);
        }
        else
        {
            // Đang là turn của người khác
            Debug.Log($"[Client {localClientId}] Waiting for other player...");
            // TODO: Show "WAITING" UI
            // turnIndicatorText.text = "Waiting...";
        }
}


    [ClientRpc]
    void UpdateTimerClientRpc(float timeRemaining)
    {
        // TODO: Update UI timer
        // timerText.text = $"{timeRemaining:F1}s";
    }

    System.Collections.IEnumerator NetworkExecuteCardsPhase()
    {
        if (!IsServer) yield break;

        Debug.Log("[Server] === Network Execute Cards Phase ===");
        
        // ✅ Announce to clients
        AnnouncePhaseClientRpc("Executing cards...");

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
                        Debug.Log($"[Server] Executing cards for Player {playerIndex}");
                        
                        // ✅ Announce current player
                        AnnouncePhaseClientRpc($"Player {playerIndex} executing cards...");
                        
                        yield return StartCoroutine(controller.ExecutePlayedCards());
                    }
                }
            }
        }
    }

    System.Collections.IEnumerator NetworkShopPhase()
    {
        if (!IsServer) yield break;

        Debug.Log("[Server] === Network Shop Phase ===");
        
        // ✅ Announce to clients
        AnnouncePhaseClientRpc("Shop is opening...");
        
        if (networkShopManager != null)
        {
            yield return StartCoroutine(networkShopManager.OpenNetworkShop());
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }
        
        // ✅ Announce shop closed
        AnnouncePhaseClientRpc("Shop closed!");
    }

    // ====================== UTILITY METHODS ======================

    void DrawCardsForAllPlayers()
    {
        if (!IsServer) return;

        Debug.Log("[Server] Drawing cards for all players...");
        
        // ✅ Announce to clients
        AnnouncePhaseClientRpc("Drawing cards...");

        if (networkDeckManager == null)
        {
            Debug.LogError("[GameManager] NetworkDeckManager not found!");
            return;
        }

        foreach (var controller in playerControllers.Values)
        {
            if (controller != null && controller.IsAlive())
            {
                networkDeckManager.DrawCardServerRpc(controller.OwnerClientId);
            }
        }
    }

    void RoundCleanup()
    {
        if (!IsServer) return;

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
        if (!IsServer) return;

        var winners = GetAlivePlayers();
        if (winners.Count > 0)
        {
            Debug.Log($"[Server] Game Over! Winner: Player {winners[0].playerId.Value}");
            ShowGameOverClientRpc(winners[0].playerId.Value);
        }
    }

    [ClientRpc]
    void ShowGameOverClientRpc(int winnerPlayerId)
    {
        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] 🎉 Game Over! Winner: Player {winnerPlayerId}");
        // TODO: Show game over UI
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

    public void ChangeRotationDirection()
    {
        if (!IsServer) return;
        
        clockwiseRotation.Value = !clockwiseRotation.Value;
        Debug.Log($"[Server] Rotation direction changed to: {(clockwiseRotation.Value ? "Clockwise" : "Counter-clockwise")}");
    }

    public void LogPlayers()
    {
        if (!IsServer) return;
        
        Debug.Log("===== Network Player List =====");
        Debug.Log("Số lượng player: " + networkPlayers.Count);

        for (int i = 0; i < networkPlayers.Count; i++)
        {
            var playerData = networkPlayers[i];
            if (playerControllers.ContainsKey(playerData.clientId))
            {
                var controller = playerControllers[playerData.clientId];
                Debug.Log($"[{i}] ClientID: {playerData.clientId} | PlayerID: {playerData.playerId} | HP: {controller?.currentHP.Value} | Alive: {controller?.IsAlive()}");
            }
        }
        Debug.Log("=======================");
    }
}