using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

// ====================== LOBBY PLAYER DATA ======================
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public int SlotIndex;
    public FixedString64Bytes PlayerName;
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
               SlotIndex == other.SlotIndex &&
               PlayerName.Equals(other.PlayerName) &&
               IsReady == other.IsReady;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayerData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, SlotIndex, PlayerName, IsReady);
    }
}

// ====================== NETWORK LOBBY MANAGER ======================
public class NetworkLobbyManager : NetworkBehaviour
{
    [Header("Lobby UI")]
    public GameObject lobbyPanel;
    public GameObject gamePanel;

    [Header("Player Slots")]
    public Button hostButton;
    public Button JoinButton;
    public Button startGameButton;
    public Button leaveButton;

    public TextMeshProUGUI[] playerSlotTexts = new TextMeshProUGUI[4];
    public Image[] playerSlotImages = new Image[4];

    [Header("Connection Info")]
    public TMP_InputField ipAddressInput;
    public TextMeshProUGUI connectionStatusText;

    [Header("Player Info")]
    public TMP_InputField playerNameInput;
    private string localPlayerName = "Player";

    [Header("Colors")]
    public Color emptySlotColor = Color.gray;
    public Color occupiedSlotColor = Color.green;
    public Color localPlayerColor = Color.yellow;

    private NetworkList<LobbyPlayerData> lobbyPlayers;
    private bool isLoadingScene = false;

    public static NetworkLobbyManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        lobbyPlayers = new NetworkList<LobbyPlayerData>();
    }

    void Start()
    {
        SetupButtons();
        UpdateLobbyUI();

        if (ipAddressInput != null)
        {
            ipAddressInput.text = "127.0.0.1";
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Subscribe to scene load events
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!IsPlayerInLobby(client.ClientId))
                {
                    OnClientConnected(client.ClientId);
                }
            }
        }

        if (IsClient)
        {
            RegisterPlayerNameServerRpc(NetworkManager.Singleton.LocalClientId, localPlayerName);

            // Subscribe to scene events for clients
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }

        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;
        UpdateLobbyUI();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            if (IsServer)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete;
            }

            if (IsClient)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }

        if (lobbyPlayers != null)
        {
            lobbyPlayers.OnListChanged -= OnLobbyPlayersChanged;
        }
    }

    void SetupButtons()
    {
        hostButton.onClick.AddListener(StartHost);
        JoinButton.onClick.AddListener(StartClient);
        startGameButton.onClick.AddListener(StartGame);
        leaveButton.onClick.AddListener(LeaveLobby);

        startGameButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);
    }

    void StartHost()
    {
        localPlayerName = string.IsNullOrWhiteSpace(playerNameInput.text) ? "Host" : playerNameInput.text;

        NetworkManager.Singleton.StartHost();
        UpdateStatus("Started as Host");

        startGameButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);
        DisableSlotButtons();
    }

    void StartClient()
    {
        localPlayerName = string.IsNullOrWhiteSpace(playerNameInput.text) ? "Client" : playerNameInput.text;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddressInput.text, 7777);

        NetworkManager.Singleton.StartClient();
        UpdateStatus($"Connecting to {ipAddressInput.text}...");

        leaveButton.gameObject.SetActive(true);
        DisableSlotButtons();
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"[SERVER] Client {clientId} connected");

        if (IsPlayerInLobby(clientId))
        {
            Debug.LogWarning($"[SERVER] Client {clientId} already in lobby");
            return;
        }

        int slotIndex = FindEmptySlot();
        if (slotIndex == -1)
        {
            Debug.LogWarning($"[SERVER] No empty slots for client {clientId}");
            return;
        }

        lobbyPlayers.Add(new LobbyPlayerData
        {
            ClientId = clientId,
            SlotIndex = slotIndex,
            PlayerName = $"Player {clientId}",
            IsReady = false
        });

        Debug.Log($"[SERVER] Added client {clientId} to slot {slotIndex}");
        LogLobbyPlayers();
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"[SERVER] Client {clientId} disconnected");

        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
                Debug.Log($"[SERVER] Removed client {clientId} from lobby");
                break;
            }
        }

        UpdateStatus($"Client {clientId} disconnected");
    }

    void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        Debug.Log($"[CLIENT {NetworkManager.LocalClientId}] Lobby changed: {changeEvent.Type}");
        UpdateLobbyUI();
        LogLobbyPlayers();
    }

    void UpdateLobbyUI()
    {
        for (int i = 0; i < 4; i++)
        {
            playerSlotTexts[i].text = $"Player {i + 1}\nEmpty";
            if (playerSlotImages[i] != null)
            {
                playerSlotImages[i].color = emptySlotColor;
            }
        }

        foreach (var player in lobbyPlayers)
        {
            int slotIndex = player.SlotIndex;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                string readyText = player.IsReady ? "Ready" : "Not Ready";
                playerSlotTexts[slotIndex].text = $"{player.PlayerName}\n{readyText}";

                if (NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.LocalClientId == player.ClientId)
                {
                    playerSlotTexts[slotIndex].text = $"{player.PlayerName}\n(You)";
                    if (playerSlotImages[slotIndex] != null)
                    {
                        playerSlotImages[slotIndex].color = localPlayerColor;
                    }
                }
                else if (playerSlotImages[slotIndex] != null)
                {
                    playerSlotImages[slotIndex].color = occupiedSlotColor;
                }
            }
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            startGameButton.interactable = lobbyPlayers.Count >= 2;
        }
    }

    void StartGame()
    {
        if (!IsHost) return;

        if (lobbyPlayers.Count < 2)
        {
            UpdateStatus("Need at least 2 players to start!");
            return;
        }

        if (isLoadingScene)
        {
            Debug.LogWarning("[Lobby] Already loading scene!");
            return;
        }

        isLoadingScene = true;
        UpdateStatus("Loading Game Scene...");

        // Hide lobby UI on all clients
        HideLobbyUIClientRpc();

        // Load scene using NetworkSceneManager (this will sync to all clients)
        Debug.Log("[HOST] Starting scene load to GameScene");
        var status = NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[HOST] Failed to start scene load! Status: {status}");
            isLoadingScene = false;
            UpdateStatus("Failed to load scene!");
        }
    }

    [ClientRpc]
    void HideLobbyUIClientRpc()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }
        Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Lobby UI hidden");
    }

    // Server-side: called when scene load completes
    void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        Debug.Log($"[SERVER] Scene '{sceneName}' loaded. Completed: {clientsCompleted.Count}, TimedOut: {clientsTimedOut.Count}");

        if (sceneName == "GameScene")
        {
            isLoadingScene = false;

            // Initialize game after scene loads
            StartCoroutine(WaitForGameManagerThenInit());
        }
    }

    // Server-side: called when a client finishes synchronizing
    void OnSynchronizeComplete(ulong clientId)
    {
        Debug.Log($"[SERVER] Client {clientId} synchronized with scene");
    }

    // Client-side: called during scene loading process
    void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (!IsClient) return;

        var clientId = NetworkManager.Singleton.LocalClientId;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                Debug.Log($"[Client {clientId}] Loading scene: {sceneEvent.SceneName}");
                break;
            case SceneEventType.LoadComplete:
                Debug.Log($"[Client {clientId}] Scene load complete: {sceneEvent.SceneName}");
                break;
            case SceneEventType.Synchronize:
                Debug.Log($"[Client {clientId}] Synchronizing scene: {sceneEvent.SceneName}");
                break;
            case SceneEventType.SynchronizeComplete:
                Debug.Log($"[Client {clientId}] Scene synchronization complete: {sceneEvent.SceneName}");
                break;
        }
    }

    private IEnumerator WaitForGameManagerThenInit()
    {
        Debug.Log("[SERVER] Waiting for NetworkGameManager...");

        // Wait for NetworkGameManager to exist
        yield return new WaitUntil(() => NetworkGameManager.Instance != null);

        Debug.Log("[SERVER] NetworkGameManager found! Initializing game...");

        var playerSlots = new Dictionary<ulong, int>();
        foreach (var player in lobbyPlayers)
        {
            playerSlots[player.ClientId] = player.SlotIndex;
        }

        NetworkGameManager.Instance.InitializeGame(playerSlots);
    }

    void LeaveLobby()
    {
        NetworkManager.Singleton.Shutdown();

        if (IsServer)
        {
            lobbyPlayers.Clear();
        }

        lobbyPanel.SetActive(true);
        startGameButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        EnableSlotButtons();
        UpdateLobbyUI();
        UpdateStatus("Left lobby");
    }

    bool IsPlayerInLobby(ulong clientId)
    {
        foreach (var player in lobbyPlayers)
        {
            if (player.ClientId == clientId)
                return true;
        }
        return false;
    }

    int FindEmptySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            bool slotTaken = false;
            foreach (var player in lobbyPlayers)
            {
                if (player.SlotIndex == i)
                {
                    slotTaken = true;
                    break;
                }
            }
            if (!slotTaken)
                return i;
        }
        return -1;
    }

    void DisableSlotButtons()
    {
        hostButton.interactable = false;
        JoinButton.interactable = false;
    }

    void EnableSlotButtons()
    {
        hostButton.interactable = true;
        JoinButton.interactable = true;
    }

    void UpdateStatus(string message)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
        Debug.Log($"[Lobby] {message}");
    }

    void LogLobbyPlayers()
    {
        if (lobbyPlayers == null || lobbyPlayers.Count == 0)
        {
            Debug.Log("[LobbyPlayers] (empty)");
            return;
        }

        string log = "[LobbyPlayers] ";
        foreach (var player in lobbyPlayers)
        {
            log += $"[ClientId={player.ClientId}, Slot={player.SlotIndex}, Name={player.PlayerName}] ";
        }
        Debug.Log(log);
    }

    public void ToggleReady()
    {
        if (!NetworkManager.Singleton.IsConnectedClient) return;
        ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void ToggleReadyServerRpc(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                var player = lobbyPlayers[i];
                player.IsReady = !player.IsReady;
                lobbyPlayers[i] = player;
                Debug.Log($"[SERVER] Client {clientId} ready state: {player.IsReady}");
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterPlayerNameServerRpc(ulong clientId, string playerName)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                var player = lobbyPlayers[i];
                player.PlayerName = playerName;
                lobbyPlayers[i] = player;
                Debug.Log($"[SERVER] Updated name for {clientId}: {playerName}");
                return;
            }
        }
    }
}