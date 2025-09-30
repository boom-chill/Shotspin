using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

// ====================== NETWORK LOBBY MANAGER ======================
public class NetworkLobbyManager : MonoBehaviour
{
    [Header("Lobby UI")]
    public GameObject lobbyPanel;
    public GameObject gamePanel;

    [Header("Player Slots")]
    public Button player1Button; // Host
    public Button player2Button; // Client
    public Button player3Button; // Client
    public Button player4Button; // Client
    public Button startGameButton;
    public Button leaveButton;

    public TextMeshProUGUI[] playerSlotTexts = new TextMeshProUGUI[4];
    public Image[] playerSlotImages = new Image[4];

    [Header("Connection Info")]
    public TMP_InputField ipAddressInput;
    public TextMeshProUGUI connectionStatusText;

    [Header("Colors")]
    public Color emptySlotColor = Color.gray;
    public Color occupiedSlotColor = Color.green;
    public Color localPlayerColor = Color.yellow;

    private Dictionary<ulong, int> playerSlots = new Dictionary<ulong, int>();
    private int localPlayerSlot = -1;

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
        }
    }

    void Start()
    {
        SetupButtons();
        UpdateLobbyUI();

        // Set default IP
        if (ipAddressInput != null)
        {
            ipAddressInput.text = "127.0.0.1";
        }

        // Subscribe to network events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void SetupButtons()
    {
        player1Button.onClick.AddListener(() => JoinSlot(0, true));  // Host
        player2Button.onClick.AddListener(() => JoinSlot(1, false)); // Client
        player3Button.onClick.AddListener(() => JoinSlot(2, false)); // Client
        player4Button.onClick.AddListener(() => JoinSlot(3, false)); // Client

        startGameButton.onClick.AddListener(StartGame);
        leaveButton.onClick.AddListener(LeaveLobby);

        startGameButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);
    }

    void JoinSlot(int slotIndex, bool isHost)
    {
        // Check if slot is occupied
        foreach (var slot in playerSlots)
        {
            if (slot.Value == slotIndex)
            {
                UpdateStatus("Slot already occupied!");
                return;
            }
        }

        localPlayerSlot = slotIndex;

        if (isHost)
        {
            StartHost();
        }
        else
        {
            StartClient();
        }
    }

    void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        UpdateStatus("Started as Host");

        // Host takes slot 0
        RequestSlotServerRpc(0);

        startGameButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        DisableSlotButtons();
    }

    void StartClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddressInput.text, 7777);

        NetworkManager.Singleton.StartClient();
        UpdateStatus($"Connecting to {ipAddressInput.text}...");

        // Request slot after connection
        StartCoroutine(RequestSlotAfterConnection());

        leaveButton.gameObject.SetActive(true);
        DisableSlotButtons();
    }

    System.Collections.IEnumerator RequestSlotAfterConnection()
    {
        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return null;
        }

        RequestSlotServerRpc(localPlayerSlot);
        UpdateStatus($"Connected as Player {localPlayerSlot + 1}");
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSlotServerRpc(int requestedSlot, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Check if slot is available
        bool slotAvailable = true;
        foreach (var slot in playerSlots)
        {
            if (slot.Value == requestedSlot)
            {
                slotAvailable = false;
                break;
            }
        }

        if (slotAvailable)
        {
            playerSlots[clientId] = requestedSlot;
            UpdateLobbyClientRpc(clientId, requestedSlot, true);
        }
        else
        {
            // Find next available slot
            for (int i = 0; i < 4; i++)
            {
                bool isOccupied = false;
                foreach (var slot in playerSlots)
                {
                    if (slot.Value == i)
                    {
                        isOccupied = true;
                        break;
                    }
                }

                if (!isOccupied)
                {
                    playerSlots[clientId] = i;
                    UpdateLobbyClientRpc(clientId, i, true);
                    break;
                }
            }
        }
    }

    [ClientRpc]
    void UpdateLobbyClientRpc(ulong clientId, int slotIndex, bool joined)
    {
        if (joined)
        {
            playerSlots[clientId] = slotIndex;
        }
        else
        {
            playerSlots.Remove(clientId);
        }

        UpdateLobbyUI();
    }

    void UpdateLobbyUI()
    {
        // Reset all slots
        for (int i = 0; i < 4; i++)
        {
            playerSlotTexts[i].text = $"Player {i + 1}\nEmpty";
            playerSlotImages[i].color = emptySlotColor;
        }

        // Update occupied slots
        foreach (var slot in playerSlots)
        {
            int index = slot.Value;
            playerSlotTexts[index].text = $"Player {index + 1}\nReady";

            if (NetworkManager.Singleton.LocalClientId == slot.Key)
            {
                playerSlotImages[index].color = localPlayerColor;
                playerSlotTexts[index].text = $"Player {index + 1}\n(You)";
            }
            else
            {
                playerSlotImages[index].color = occupiedSlotColor;
            }
        }

        // Update start button visibility (only for host)
        if (NetworkManager.Singleton.IsHost)
        {
            startGameButton.interactable = playerSlots.Count >= 2;
        }
    }

    void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (playerSlots.Count < 2)
        {
            UpdateStatus("Need at least 2 players to start!");
            return;
        }

        // Send game start signal to all clients
        StartGameClientRpc();
    }

    [ClientRpc]
    void StartGameClientRpc()
    {
        lobbyPanel.SetActive(false);
        gamePanel.SetActive(true);

        // Initialize game for all clients
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkGameManager.Instance.InitializeGame(playerSlots);
        }
    }

    void LeaveLobby()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
        }
        else
        {
            NetworkManager.Singleton.Shutdown();
        }

        playerSlots.Clear();
        localPlayerSlot = -1;

        lobbyPanel.SetActive(true);
        gamePanel.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        EnableSlotButtons();
        UpdateLobbyUI();
        UpdateStatus("Left lobby");
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        UpdateStatus($"Client {clientId} connected");
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");

        if (playerSlots.ContainsKey(clientId))
        {
            int slot = playerSlots[clientId];
            playerSlots.Remove(clientId);
            UpdateLobbyClientRpc(clientId, slot, false);
        }

        UpdateStatus($"Client {clientId} disconnected");
    }

    void DisableSlotButtons()
    {
        player1Button.interactable = false;
        player2Button.interactable = false;
        player3Button.interactable = false;
        player4Button.interactable = false;
    }

    void EnableSlotButtons()
    {
        player1Button.interactable = true;
        player2Button.interactable = true;
        player3Button.interactable = true;
        player4Button.interactable = true;
    }

    void UpdateStatus(string message)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
        Debug.Log($"[Lobby] {message}");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}