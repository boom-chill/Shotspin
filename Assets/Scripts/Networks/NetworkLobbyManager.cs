// using Unity.Netcode;
// using UnityEngine;
// using UnityEngine.UI;
// using UnityEngine.SceneManagement;

// public class SimpleLobbyManager : MonoBehaviour
// {
//     [Header("UI References")]
//     public Button hostButton;
//     public Button joinButton;
//     public InputField ipInputField;
//     public Text statusText;
//     public GameObject lobbyPanel;
//     public GameObject gamePanel;

//     [Header("Lobby Settings")]
//     public int maxPlayers = 6;
//     public string gameplaySceneName = "GamePlay";

//     void Start()
//     {
//         // Setup button listeners
//         hostButton.onClick.AddListener(StartHost);
//         joinButton.onClick.AddListener(StartClient);

//         // Default IP
//         ipInputField.text = "127.0.0.1";

//         UpdateStatusText("Ready to connect");
//     }

//     public void StartHost()
//     {
//         UpdateStatusText("Starting host...");

//         // Configure transport
//         var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
//         transport.SetConnectionData("127.0.0.1", 7777);

//         // Start host
//         bool success = NetworkManager.Singleton.StartHost();

//         if (success)
//         {
//             UpdateStatusText($"Host started! Waiting for players... (0/{maxPlayers})");
//             ShowLobby();

//             // Subscribe to connection events
//             NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnected;
//             NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnected;
//         }
//         else
//         {
//             UpdateStatusText("Failed to start host!");
//         }
//     }

//     public void StartClient()
//     {
//         UpdateStatusText("Connecting to server...");

//         // Configure transport with input IP
//         var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
//         transport.SetConnectionData(ipInputField.text, 7777);

//         // Start client
//         bool success = NetworkManager.Singleton.StartClient();

//         if (success)
//         {
//             UpdateStatusText("Connecting...");

//             // Subscribe to events
//             NetworkManager.Singleton.OnClientConnectedCallback += OnConnectedToServer;
//             NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectedFromServer;
//         }
//         else
//         {
//             UpdateStatusText("Failed to start client!");
//         }
//     }

//     void OnPlayerConnected(ulong clientId)
//     {
//         if (NetworkManager.Singleton.IsHost)
//         {
//             int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
//             UpdateStatusText($"Player joined! ({connectedCount}/{maxPlayers})");

//             // Auto-start game when minimum players reached
//             if (connectedCount >= 2)
//             {
//                 StartCoroutine(StartGameCountdown());
//             }
//         }
//     }

//     void OnPlayerDisconnected(ulong clientId)
//     {
//         if (NetworkManager.Singleton.IsHost)
//         {
//             int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
//             UpdateStatusText($"Player left ({connectedCount}/{maxPlayers})");
//         }
//     }

//     void OnConnectedToServer(ulong clientId)
//     {
//         if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
//         {
//             UpdateStatusText("Connected to server! Waiting for game to start...");
//             ShowLobby();
//         }
//     }

//     void OnDisconnectedFromServer(ulong clientId)
//     {
//         if (NetworkManager.Singleton.IsClient)
//         {
//             UpdateStatusText("Disconnected from server!");
//             ShowMainMenu();
//         }
//     }

//     System.Collections.IEnumerator StartGameCountdown()
//     {
//         for (int i = 5; i > 0; i--)
//         {
//             UpdateStatusText($"Starting game in {i}...");
//             yield return new WaitForSeconds(1f);
//         }

//         // Load gameplay scene for all clients
//         if (NetworkManager.Singleton.IsHost)
//         {
//             NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
//         }
//     }

//     void ShowMainMenu()
//     {
//         lobbyPanel.SetActive(false);
//         gamePanel.SetActive(false);
//         hostButton.gameObject.SetActive(true);
//         joinButton.gameObject.SetActive(true);
//     }

//     void ShowLobby()
//     {
//         lobbyPanel.SetActive(true);
//         hostButton.gameObject.SetActive(false);
//         joinButton.gameObject.SetActive(false);
//     }

//     void UpdateStatusText(string message)
//     {
//         if (statusText != null)
//         {
//             statusText.text = message;
//             Debug.Log($"Lobby Status: {message}");
//         }
//     }

//     public void Disconnect()
//     {
//         NetworkManager.Singleton.Shutdown();
//         ShowMainMenu();
//         UpdateStatusText("Disconnected");
//     }

//     void OnDestroy()
//     {
//         // Cleanup event subscriptions
//         if (NetworkManager.Singleton != null)
//         {
//             NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerConnected;
//             NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerDisconnected;
//             NetworkManager.Singleton.OnClientConnectedCallback -= OnConnectedToServer;
//             NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromServer;
//         }
//     }
// }