// ====================== CORE MANAGERS ======================

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int maxPlayers = 4;
    public int startingHP = 4;
    public int startingCards = 2;
    public float turnTimer = 3f;

    [Header("Prefab References")]
    public GameObject playerPrefab;     // Prefab cho người chơi thực
    public GameObject botPrefab;
    public GameObject revolverPrefab;
    public GameObject cardPrefab;

    [Header("Card Database")]
    public List<CardData> allCards = new List<CardData>();

    public static GameManager Instance;
    public List<PlayerController> players = new List<PlayerController>();
    public RevolverManager revolver;
    public TurnManager turnManager;
    public ShopManager shopManager;

    private int currentPlayerIndex = 0;
    private bool clockwiseRotation = true;

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
        InitializeGame();
    }

    void InitializeGame()
    {
        // Tạo revolver ở giữa bàn
        // GameObject revolverObj = Instantiate(
        //     revolverPrefab,
        //     new Vector3(0f, 0.88f, 0f),
        //     Quaternion.Euler(0f, 0, 90f)
        // );
        // revolver = revolverObj.GetComponent<RevolverManager>();

        // Tạo players quanh bàn (demo với 4 người)
        CreatePlayers(4);

        // Khởi tạo turn manager
        turnManager = GetComponent<TurnManager>();
        shopManager = GetComponent<ShopManager>();

        // Bắt đầu game
        StartCoroutine(GameLoop());
    }

    void CreatePlayers(int playerCount)
    {
        float radius = 1.3f; // Bán kính quanh bàn

        for (int i = 0; i < playerCount; i++)
        {
            Debug.Log(i);
            float angle = (360f / playerCount) * i;
            Debug.Log(angle);

            // Tính vị trí theo vòng tròn
            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                -0.2f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Vector3 directionToCenter = (Vector3.zero - position).normalized;

            // Lấy góc Y duy nhất
            float targetY = Quaternion.LookRotation(directionToCenter, Vector3.up).eulerAngles.y;

            // Xoay chỉ quanh trục Y
            Quaternion rotation = Quaternion.Euler(0, targetY, 0);

            GameObject playerObj = null;
            if (i == 0)
            {
                // Instantiate player
                playerObj = Instantiate(playerPrefab, position, rotation);
            }
            else
            {
                playerObj = Instantiate(botPrefab, position, rotation);
            }

            // Lấy PlayerController
            PlayerController player = playerObj.GetComponent<PlayerController>();
            player.Initialize(i, startingHP, startingCards);
            players.Add(player);
        }

        LogPlayers();

        // Set revolver trỏ vào người chơi đầu tiên
        if (revolver != null)
        {
            revolver.SetTargetPlayer(0);
        }
        else
        {
            Debug.LogError("Revolver chưa được gán! Kiểm tra prefab hoặc GetComponent.");
        }
    }

    IEnumerator GameLoop()
    {
        while (GetAlivePlayers().Count > 1)
        {
            yield return StartCoroutine(RevolverPhase());

            yield return StartCoroutine(ActionPlayPhase());

            yield return StartCoroutine(ExecuteCardsPhase());

            yield return StartCoroutine(ShopPhase());

            yield return StartCoroutine(DrawNewCardPhase());

            // Round cleanup
            RoundCleanup();
        }

        // Game over
        GameOver();
    }

    IEnumerator RevolverPhase()
    {
        // Nhìn ổ đạn lần đầu
        revolver.RevealAllSlots();
        yield return null;
    }


    IEnumerator DrawNewCardPhase()
    {
        Debug.Log("=== Draw New Cards Phase ===");

        // Mỗi người rút 1 lá bài
        foreach (var player in GetAlivePlayers())
        {
            player.DrawCard();
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator ActionPlayPhase()
    {
        Debug.Log("=== Action Play Phase ===");

        float timer = turnTimer;

        // Enable card selection cho tất cả players
        foreach (var player in GetAlivePlayers())
        {
            player.EnableItemUsage(true);
            player.EnableCardSelection(true);
        }

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        // Disable card selection
        foreach (var player in GetAlivePlayers())
        {
            player.EnableItemUsage(false);
            player.EnableCardSelection(false);
        }

        Debug.Log("Action play phase ended!");
    }

    IEnumerator ExecuteCardsPhase()
    {
        Debug.Log("=== Execute Cards Phase ===");

        // Thực thi theo thứ tự xoay vòng bắt đầu từ người bị súng chỉ vào
        int startIndex = revolver.GetTargetPlayerIndex();

        for (int i = 0; i < players.Count; i++)
        {
            int playerIndex = GetNextPlayerIndex(startIndex, i);
            PlayerController player = players[playerIndex];

            if (!player.IsAlive()) continue;

            Debug.Log($"Executing turn for Player {playerIndex}");

            // Thực thi cards của player này
            yield return StartCoroutine(player.ExecutePlayedCards());
        }
    }

    IEnumerator ShopPhase()
    {
        Debug.Log("=== Shop Phase ===");
        yield return StartCoroutine(shopManager.OpenShop());
    }

    void RoundCleanup()
    {
        // Reset states, check win condition
        foreach (var player in players)
        {
            player.RoundCleanup();
        }
    }

    void GameOver()
    {
        var winner = GetAlivePlayers()[0];
        Debug.Log($"Game Over! Winner: Player {winner.playerId}");
    }

    public List<PlayerController> GetAlivePlayers()
    {
        return players.FindAll(p => p.IsAlive());
    }

    public int GetNextPlayerIndex(int startIndex, int offset)
    {
        if (clockwiseRotation)
        {
            return (startIndex + offset) % players.Count;
        }
        else
        {
            return (startIndex - offset + players.Count) % players.Count;
        }
    }

    public void ChangeRotationDirection()
    {
        clockwiseRotation = !clockwiseRotation;
        Debug.Log($"Rotation direction changed to: {(clockwiseRotation ? "Clockwise" : "Counter-clockwise")}");
    }

    public void LogPlayers()
    {
        Debug.Log("===== Player List =====");
        Debug.Log("Số lượng player: " + players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            PlayerController p = players[i];
            string playerName = (p != null) ? p.gameObject.name : "NULL";
            Debug.Log($"[{i}] PlayerID: {p?.playerId} | Name: {playerName} | HP: {p?.currentHP}");
        }
        Debug.Log("=======================");
    }
}