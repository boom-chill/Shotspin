// using Unity.Netcode;
// using Unity.Netcode.Transports.UTP;
// using UnityEngine;
// using System.Collections.Generic;
// using System.Collections;

// public class NetworkGameManager : NetworkBehaviour
// {
//     [Header("Network Settings")]
//     public int maxPlayers = 6;
//     public float connectionTimeout = 30f;

//     [Header("Prefabs")]
//     public GameObject networkPlayerPrefab;
//     public GameObject networkRevolverPrefab;
//     public GameObject networkCardPrefab;

//     // Network Variables for game state synchronization
//     private NetworkVariable<int> currentRound = new NetworkVariable<int>(1);
//     private NetworkVariable<int> currentPlayerTurn = new NetworkVariable<int>(0);
//     private NetworkVariable<GamePhase> currentPhase = new NetworkVariable<GamePhase>(GamePhase.Setup);
//     private NetworkVariable<float> phaseTimer = new NetworkVariable<float>(0f);

//     // Connected players tracking
//     private NetworkList<PlayerNetworkData> connectedPlayers;

//     public static NetworkGameManager Instance;

//     public override void OnNetworkSpawn()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//         }

//         // Initialize NetworkList on server
//         if (IsServer)
//         {
//             connectedPlayers = new NetworkList<PlayerNetworkData>();
//         }

//         // Subscribe to network events
//         NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
//         NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

//         // Subscribe to game state changes
//         currentPhase.OnValueChanged += OnGamePhaseChanged;
//         currentPlayerTurn.OnValueChanged += OnCurrentPlayerChanged;

//         Debug.Log($"NetworkGameManager spawned. IsServer: {IsServer}, IsClient: {IsClient}");
//     }

//     public override void OnNetworkDespawn()
//     {
//         if (NetworkManager.Singleton != null)
//         {
//             NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
//             NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
//         }

//         currentPhase.OnValueChanged -= OnGamePhaseChanged;
//         currentPlayerTurn.OnValueChanged -= OnCurrentPlayerChanged;
//     }

//     void OnClientConnected(ulong clientId)
//     {
//         if (IsServer)
//         {
//             Debug.Log($"Client {clientId} connected");

//             // Add player data
//             var playerData = new PlayerNetworkData
//             {
//                 clientId = clientId,
//                 playerId = (int)connectedPlayers.Count,
//                 isReady = false
//             };

//             connectedPlayers.Add(playerData);

//             // Spawn network player for this client
//             SpawnNetworkPlayerServerRpc(clientId);

//             // Start game if enough players
//             if (connectedPlayers.Count >= 2)
//             {
//                 CheckStartGame();
//             }
//         }
//     }

//     void OnClientDisconnected(ulong clientId)
//     {
//         if (IsServer)
//         {
//             Debug.Log($"Client {clientId} disconnected");

//             // Handle player disconnection
//             HandlePlayerDisconnection(clientId);
//         }
//     }

//     [ServerRpc(RequireOwnership = false)]
//     void SpawnNetworkPlayerServerRpc(ulong clientId)
//     {
//         if (!IsServer) return;

//         // Calculate position around table
//         int playerIndex = (int)connectedPlayers.Count - 1;
//         float radius = 3f;
//         float angle = (360f / maxPlayers) * playerIndex;
//         Vector3 position = new Vector3(
//             Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
//             0,
//             Mathf.Sin(angle * Mathf.Deg2Rad) * radius
//         );

//         // Spawn network player
//         GameObject playerObj = Instantiate(networkPlayerPrefab, position, Quaternion.identity);
//         NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

//         // Assign ownership to the specific client
//         netObj.SpawnWithOwnership(clientId);

//         // Initialize player data
//         NetworkPlayerController playerController = playerObj.GetComponent<NetworkPlayerController>();
//         if (playerController != null)
//         {
//             playerController.InitializeNetworkPlayerServerRpc(playerIndex, 4, 3); // HP=4, cards=3
//         }
//     }

//     void CheckStartGame()
//     {
//         if (IsServer && connectedPlayers.Count >= 2)
//         {
//             StartCoroutine(StartGameCountdown());
//         }
//     }

//     IEnumerator StartGameCountdown()
//     {
//         // Wait for all players to be ready (simplified - auto-ready after 5 seconds)
//         yield return new WaitForSeconds(5f);

//         InitializeNetworkGameServerRpc();
//     }

//     [ServerRpc(RequireOwnership = false)]
//     void InitializeNetworkGameServerRpc()
//     {
//         if (!IsServer) return;

//         // Spawn revolver
//         GameObject revolverObj = Instantiate(networkRevolverPrefab, Vector3.zero, Quaternion.identity);
//         NetworkObject revolverNetObj = revolverObj.GetComponent<NetworkObject>();
//         revolverNetObj.Spawn();

//         // Set initial game state
//         currentPhase.Value = GamePhase.CardPlay;
//         currentPlayerTurn.Value = 0;
//         phaseTimer.Value = 25f; // Card play timer

//         // Notify all clients to start game
//         StartGameClientRpc();
//     }

//     [ClientRpc]
//     void StartGameClientRpc()
//     {
//         Debug.Log("=== NETWORK GAME STARTED ===");
//         UIManager.Instance?.UpdateGameStatus("Network Game Started!");

//         // Start local UI updates
//         StartCoroutine(NetworkGameLoop());
//     }

//     IEnumerator NetworkGameLoop()
//     {
//         while (currentPhase.Value != GamePhase.GameOver)
//         {
//             // Update timer UI
//             if (phaseTimer.Value > 0)
//             {
//                 UIManager.Instance?.UpdateTimerCountdown(phaseTimer.Value, currentPhase.Value.ToString());
//             }

//             yield return null;
//         }
//     }

//     // ====================== GAME PHASE MANAGEMENT ======================

//     void OnGamePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
//     {
//         Debug.Log($"Game phase changed: {oldPhase} → {newPhase}");

//         switch (newPhase)
//         {
//             case GamePhase.CardPlay:
//                 HandleCardPlayPhase();
//                 break;
//             case GamePhase.CardExecution:
//                 HandleCardExecutionPhase();
//                 break;
//             case GamePhase.Shop:
//                 HandleShopPhase();
//                 break;
//             case GamePhase.GameOver:
//                 HandleGameOverPhase();
//                 break;
//         }
//     }

//     void OnCurrentPlayerChanged(int oldPlayer, int newPlayer)
//     {
//         UIManager.Instance?.UpdateCurrentPlayer(newPlayer);
//     }

//     void HandleCardPlayPhase()
//     {
//         // Enable card selection for all players
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         foreach (var player in networkPlayers)
//         {
//             if (player.IsOwner) // Only for local player
//             {
//                 player.EnableCardSelection(true);
//             }
//         }

//         // Start timer countdown on server
//         if (IsServer)
//         {
//             StartCoroutine(PhaseTimerCountdown(25f));
//         }
//     }

//     void HandleCardExecutionPhase()
//     {
//         // Disable card selection
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         foreach (var player in networkPlayers)
//         {
//             if (player.IsOwner)
//             {
//                 player.EnableCardSelection(false);
//             }
//         }

//         // Start card execution on server
//         if (IsServer)
//         {
//             StartCoroutine(ExecuteNetworkCards());
//         }
//     }

//     void HandleShopPhase()
//     {
//         if (IsServer)
//         {
//             StartCoroutine(NetworkShopPhase());
//         }
//     }

//     void HandleGameOverPhase()
//     {
//         Debug.Log("Network game over!");
//     }

//     // ====================== NETWORK GAME EXECUTION ======================

//     IEnumerator PhaseTimerCountdown(float duration)
//     {
//         if (!IsServer) yield break;

//         phaseTimer.Value = duration;

//         while (phaseTimer.Value > 0)
//         {
//             phaseTimer.Value -= Time.deltaTime;
//             yield return null;
//         }

//         // Move to next phase
//         switch (currentPhase.Value)
//         {
//             case GamePhase.CardPlay:
//                 currentPhase.Value = GamePhase.CardExecution;
//                 break;
//             case GamePhase.CardExecution:
//                 currentPhase.Value = GamePhase.Shop;
//                 break;
//             case GamePhase.Shop:
//                 currentPhase.Value = GamePhase.CardPlay;
//                 currentRound.Value++;
//                 break;
//         }
//     }

//     IEnumerator ExecuteNetworkCards()
//     {
//         if (!IsServer) yield break;

//         Debug.Log("=== Network Card Execution Phase ===");

//         // Get all network players and execute their cards in order
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         System.Array.Sort(networkPlayers, (a, b) => a.playerId.Value.CompareTo(b.playerId.Value));

//         for (int i = 0; i < networkPlayers.Length; i++)
//         {
//             currentPlayerTurn.Value = networkPlayers[i].playerId.Value;

//             // Execute this player's cards
//             yield return StartCoroutine(networkPlayers[i].ExecuteNetworkCards());

//             yield return new WaitForSeconds(0.5f);
//         }

//         // Move to shop phase
//         currentPhase.Value = GamePhase.Shop;
//     }

//     IEnumerator NetworkShopPhase()
//     {
//         if (!IsServer) yield break;

//         Debug.Log("=== Network Shop Phase ===");

//         // Each player gets a turn to shop
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();

//         foreach (var player in networkPlayers)
//         {
//             if (player.currentHP.Value > 0) // Only alive players
//             {
//                 yield return StartCoroutine(player.NetworkShopTurn());
//             }
//         }

//         // Check win condition
//         int alivePlayers = 0;
//         foreach (var player in networkPlayers)
//         {
//             if (player.currentHP.Value > 0) alivePlayers++;
//         }

//         if (alivePlayers <= 1)
//         {
//             currentPhase.Value = GamePhase.GameOver;
//         }
//         else
//         {
//             // Next round
//             currentPhase.Value = GamePhase.CardPlay;
//             phaseTimer.Value = 25f;
//         }
//     }

//     void HandlePlayerDisconnection(ulong clientId)
//     {
//         // Find and remove disconnected player
//         for (int i = 0; i < connectedPlayers.Count; i++)
//         {
//             if (connectedPlayers[i].clientId == clientId)
//             {
//                 connectedPlayers.RemoveAt(i);
//                 break;
//             }
//         }

//         // Handle game state if needed (pause, end game, etc.)
//         if (connectedPlayers.Count < 2)
//         {
//             Debug.Log("Not enough players, ending game");
//             currentPhase.Value = GamePhase.GameOver;
//         }
//     }

//     // ====================== PUBLIC NETWORK METHODS ======================

//     [ServerRpc(RequireOwnership = false)]
//     public void PlayCardServerRpc(ulong clientId, int cardIndex, ServerRpcParams rpcParams = default)
//     {
//         if (!IsServer) return;

//         // Validate and process card play
//         var player = GetPlayerByClientId(clientId);
//         if (player != null)
//         {
//             player.ProcessCardPlayServerRpc(cardIndex);
//         }
//     }

//     [ServerRpc(RequireOwnership = false)]
//     public void UseItemServerRpc(ulong clientId, int itemIndex, ServerRpcParams rpcParams = default)
//     {
//         if (!IsServer) return;

//         var player = GetPlayerByClientId(clientId);
//         if (player != null)
//         {
//             player.ProcessItemUseServerRpc(itemIndex);
//         }
//     }

//     NetworkPlayerController GetPlayerByClientId(ulong clientId)
//     {
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         foreach (var player in networkPlayers)
//         {
//             if (player.OwnerClientId == clientId)
//             {
//                 return player;
//             }
//         }
//         return null;
//     }
// }