using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Settings")]
    public int shopSlots = 4;
    public float shopTime = 3f; // Thời gian mua sắm

    [Header("Item Database")]
    public List<ItemData> tier1Items = new List<ItemData>();
    public List<ItemData> tier2Items = new List<ItemData>();
    public List<ItemData> tier3Items = new List<ItemData>();
    public List<ItemData> tier4Items = new List<ItemData>();

    [Header("UI References")]
    public GameObject shopPanel;
    public Transform shopSlotsParent;
    public UnityEngine.UI.Button[] shopButtons;
    public UnityEngine.UI.Text[] shopTexts;
    public UnityEngine.UI.Text[] shopPrices;
    public UnityEngine.UI.Text shopTimerText;
    public UnityEngine.UI.Text playerShellsText;

    private ItemData[] currentShopItems;
    private bool isShopOpen = false;
    private int currentShoppingPlayer = 0;

    void Awake()
    {
        currentShopItems = new ItemData[shopSlots];

        // Setup shop buttons
        for (int i = 0; i < shopButtons.Length && i < shopSlots; i++)
        {
            int slotIndex = i; // Capture for closure
            // shopButtons[i].onClick.AddListener(() => BuyItem(slotIndex));
        }

        // Hide shop initially
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    public IEnumerator OpenShop()
    {
        Debug.Log("=== Shop Phase ===");

        // Generate random shop items
        GenerateShopItems();

        // Mỗi player có lượt mua (theo thứ tự)
        List<PlayerController> alivePlayers = GameManager.Instance.GetAlivePlayers();

        foreach (var player in alivePlayers)
        {
            yield return StartCoroutine(PlayerShopTurn(player));
        }

        CloseShop();
    }

    IEnumerator PlayerShopTurn(PlayerController player)
    {
        Debug.Log($"Player {player.playerId} shopping turn - {player.shellCount} shells");

        currentShoppingPlayer = player.playerId;
        isShopOpen = true;

        int randomSlot = Random.Range(1, 5);
        BuyItem(randomSlot, player);

        // Show shop UI
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        UpdateShopUI(player);

        // Shopping timer
        float remainingTime = shopTime;

        while (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;

            // Update timer UI
            if (shopTimerText != null)
            {
                shopTimerText.text = $"Shopping: {remainingTime:F1}s";
            }

            // Update player shells UI
            if (playerShellsText != null)
            {
                playerShellsText.text = $"Player {player.playerId} Shells: {player.shellCount}";
            }


            yield return null;
        }

        isShopOpen = false;
    }

    void GenerateShopItems()
    {
        // 4 slots với 4 tiers khác nhau
        List<ItemData>[] tierLists = { tier1Items, tier2Items, tier3Items, tier4Items };

        for (int i = 0; i < shopSlots; i++)
        {
            int tierIndex = i; // Slot 0 = Tier 1, Slot 1 = Tier 2, etc.

            if (tierIndex < tierLists.Length && tierLists[tierIndex].Count > 0)
            {
                // Random item from tier
                List<ItemData> tierItems = tierLists[tierIndex];
                currentShopItems[i] = tierItems[Random.Range(0, tierItems.Count)];
            }
            else
            {
                currentShopItems[i] = null;
            }
        }

        Debug.Log("Shop items generated:");
        for (int i = 0; i < shopSlots; i++)
        {
            if (currentShopItems[i] != null)
            {
                Debug.Log($"Slot {i}: {currentShopItems[i].itemName} ({currentShopItems[i].shellCost} shells)");
            }
        }
    }

    void UpdateShopUI(PlayerController player)
    {
        for (int i = 0; i < shopSlots; i++)
        {
            if (i < shopButtons.Length && i < shopTexts.Length && i < shopPrices.Length)
            {
                if (currentShopItems[i] != null)
                {
                    // Update item info
                    shopTexts[i].text = currentShopItems[i].itemName;
                    shopPrices[i].text = $"{currentShopItems[i].shellCost} shells";

                    // Enable/disable button based on affordability and inventory space
                    bool canAfford = player.shellCount >= currentShopItems[i].shellCost;
                    bool hasSpace = player.items.Count < player.maxItems;

                    shopButtons[i].interactable = canAfford && hasSpace && isShopOpen;

                    // Visual feedback
                    if (!canAfford)
                    {
                        shopTexts[i].color = Color.red;
                        shopPrices[i].color = Color.red;
                    }
                    else if (!hasSpace)
                    {
                        shopTexts[i].color = Color.yellow;
                        shopPrices[i].color = Color.yellow;
                    }
                    else
                    {
                        shopTexts[i].color = Color.white;
                        shopPrices[i].color = Color.white;
                    }
                }
                else
                {
                    // Empty slot
                    shopTexts[i].text = "Empty";
                    shopPrices[i].text = "";
                    shopButtons[i].interactable = false;
                }
            }
        }
    }

    public void BuyItem(int slotIndex, PlayerController player)
    {
        if (!isShopOpen || slotIndex < 0 || slotIndex >= shopSlots) return;

        ItemData itemToBuy = currentShopItems[slotIndex];
        if (itemToBuy == null) return;

        PlayerController currentPlayer = GameManager.Instance.players[currentShoppingPlayer];

        // Check if can buy
        if (!currentPlayer.CanAffordItem(itemToBuy) || currentPlayer.items.Count >= currentPlayer.maxItems)
        {
            Debug.Log($"Player {currentShoppingPlayer} cannot buy {itemToBuy.itemName}");
            return;
        }

        // Purchase item
        player.BuyItem(itemToBuy);

        // Remove from shop
        currentShopItems[slotIndex] = null;

        // Update UI
        UpdateShopUI(currentPlayer);

        Debug.Log($"Player {currentShoppingPlayer} bought {itemToBuy.itemName}!");
    }

    void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        Debug.Log("Shop closed!");
    }

    // Methods để populate item databases
    void Start()
    {
        PopulateItemDatabases();
    }

    void PopulateItemDatabases()
    {
        // Create sample items for testing (in real game, load from Resources or ScriptableObjects)

        // Tier 1 (1 shell)
        if (tier1Items.Count == 0)
        {
            // Tạo sample items - trong thực tế sẽ load từ ScriptableObject
            Debug.Log("Creating sample Tier 1 items...");
        }

        // Tier 2 (2 shells)  
        if (tier2Items.Count == 0)
        {
            Debug.Log("Creating sample Tier 2 items...");
        }

        // Tier 3 (3 shells)
        if (tier3Items.Count == 0)
        {
            Debug.Log("Creating sample Tier 3 items...");
        }

        // Tier 4 (4 shells)
        if (tier4Items.Count == 0)
        {
            Debug.Log("Creating sample Tier 4 items...");
        }
    }
}