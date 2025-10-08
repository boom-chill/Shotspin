using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class NetworkShopManager : NetworkBehaviour
{
    [Header("Shop Settings")]
    public int shopSlots = 4;
    public float shopTime = 15f;
    public float globalShopTime = 60f;

    [Header("Item Database")]
    public List<ItemData> tier1Items = new List<ItemData>();
    public List<ItemData> tier2Items = new List<ItemData>();
    public List<ItemData> tier3Items = new List<ItemData>();
    public List<ItemData> tier4Items = new List<ItemData>();

    [Header("UI References")]
    public GameObject shopPanel;
    public Transform shopSlotsParent;
    public Button[] shopButtons;
    public TextMeshProUGUI[] shopTexts;
    public TextMeshProUGUI[] shopPrices;
    public TextMeshProUGUI shopTimerText;
    public TextMeshProUGUI playerShellsText;
    public TextMeshProUGUI shopStatusText;

    // Network state
    private NetworkList<NetworkItemData> currentShopItems  = new NetworkList<NetworkItemData>();
    public NetworkVariable<float> shopTimer = new NetworkVariable<float>(0f);
    public NetworkVariable<bool> isShopOpen = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentShoppingPlayerId = new NetworkVariable<int>(-1);

    public static NetworkShopManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // currentShopItems = new NetworkList<NetworkItemData>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        PopulateItemDatabases();
        SetupShopButtons();
        
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isShopOpen.OnValueChanged += OnShopStateChanged;
        shopTimer.OnValueChanged += OnShopTimerChanged;
        currentShoppingPlayerId.OnValueChanged += OnCurrentShopperChanged;
        currentShopItems.OnListChanged += OnShopItemsChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        isShopOpen.OnValueChanged -= OnShopStateChanged;
        shopTimer.OnValueChanged -= OnShopTimerChanged;
        currentShoppingPlayerId.OnValueChanged -= OnCurrentShopperChanged;
        
        if (currentShopItems != null)
        {
            currentShopItems.OnListChanged -= OnShopItemsChanged;
        }
    }

    void SetupShopButtons()
    {
        for (int i = 0; i < shopButtons.Length && i < shopSlots; i++)
        {
            int slotIndex = i;
            shopButtons[i].onClick.AddListener(() => RequestBuyItemServerRpc(slotIndex));
        }
    }

    void PopulateItemDatabases()
    {
        Debug.Log("[ShopManager] Item databases populated");
    }

    // ====================== SHOP PHASE ======================

    public IEnumerator OpenNetworkShop()
    {
        if (!IsServer) yield break;

        Debug.Log("[ShopManager] === Shop Phase Started ===");

        GenerateShopItems();
        isShopOpen.Value = true;
        shopTimer.Value = globalShopTime;
        OpenShopClientRpc();

        while (shopTimer.Value > 0)
        {
            shopTimer.Value -= Time.deltaTime;
            yield return null;
        }

        CloseShop();
    }

    void GenerateShopItems()
    {
        if (!IsServer) return;

        currentShopItems.Clear();
        List<ItemData>[] tierLists = { tier1Items, tier2Items, tier3Items, tier4Items };

        Debug.Log("[ShopManager] Generating shop items...");

        for (int i = 0; i < shopSlots; i++)
        {
            int tierIndex = i;

            if (tierIndex < tierLists.Length && tierLists[tierIndex].Count > 0)
            {
                List<ItemData> tierItems = tierLists[tierIndex];
                ItemData randomItem = tierItems[Random.Range(0, tierItems.Count)];

                NetworkItemData networkItem = new NetworkItemData
                {
                    itemType = randomItem.itemType,
                    shellCost = randomItem.shellCost,
                    tier = randomItem.tier
                };

                currentShopItems.Add(networkItem);
                Debug.Log($"[ShopManager] Slot {i}: {randomItem.itemName} (Tier {randomItem.tier}, {randomItem.shellCost} shells)");
            }
            else
            {
                NetworkItemData emptyItem = new NetworkItemData
                {
                    itemType = ItemType.None,
                    shellCost = 0,
                    tier = 0
                };
                currentShopItems.Add(emptyItem);
            }
        }
    }

    // ====================== SERVER RPC ======================

    [ServerRpc(RequireOwnership = false)]
    void RequestBuyItemServerRpc(int slotIndex, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer || !isShopOpen.Value) return;
        if (slotIndex < 0 || slotIndex >= currentShopItems.Count) return;

        ulong buyerClientId = serverRpcParams.Receive.SenderClientId;
        NetworkItemData itemToBuy = currentShopItems[slotIndex];

        if (itemToBuy.itemType == ItemType.None)
        {
            Debug.Log($"[ShopManager] Slot {slotIndex} is empty");
            return;
        }

        NetworkPlayerController buyer = GetPlayerByClientId(buyerClientId);
        if (buyer == null)
        {
            Debug.LogError($"[ShopManager] Could not find player for client {buyerClientId}");
            return;
        }

        if (buyer.shellCount.Value < itemToBuy.shellCost)
        {
            Debug.Log($"[ShopManager] Player {buyer.playerId.Value} cannot afford item");
            PurchaseFailedClientRpc("Not enough shells!", new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { buyerClientId }
                }
            });
            return;
        }

        if (buyer.items.Count >= buyer.maxItems)
        {
            Debug.Log($"[ShopManager] Player {buyer.playerId.Value} inventory full");
            PurchaseFailedClientRpc("Inventory full!", new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { buyerClientId }
                }
            });
            return;
        }

        ItemData fullItemData = GetItemDataByType(itemToBuy.itemType);
        if (fullItemData != null)
        {
            Debug.Log($"[ShopManager] ✓ Player {buyer.playerId.Value} bought {fullItemData.itemName}!");

            NetworkItemData emptyItem = new NetworkItemData
            {
                itemType = ItemType.None,
                shellCost = 0,
                tier = 0
            };
            currentShopItems[slotIndex] = emptyItem;

            PurchaseSuccessClientRpc(itemToBuy.itemType, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { buyerClientId }
                }
            });
        }
    }

    NetworkPlayerController GetPlayerByClientId(ulong clientId)
    {
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                return player;
            }
        }
        return null;
    }

    ItemData GetItemDataByType(ItemType itemType)
    {
        List<ItemData>[] allTiers = { tier1Items, tier2Items, tier3Items, tier4Items };
        
        foreach (var tier in allTiers)
        {
            foreach (var item in tier)
            {
                if (item.itemType == itemType)
                {
                    return item;
                }
            }
        }
        
        return null;
    }

    void CloseShop()
    {
        if (!IsServer) return;

        isShopOpen.Value = false;
        CloseShopClientRpc();
        Debug.Log("[ShopManager] Shop closed!");
    }

    // ====================== CLIENT RPC ======================

    [ClientRpc]
    void OpenShopClientRpc()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        Debug.Log("[ShopManager] Shop opened on client");
        UpdateShopUILocal();
    }

    [ClientRpc]
    void CloseShopClientRpc()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        Debug.Log("[ShopManager] Shop closed on client");
    }

    [ClientRpc]
    void PurchaseSuccessClientRpc(ItemType itemType, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[ShopManager] Successfully purchased {itemType}!");
        
        if (shopStatusText != null)
        {
            shopStatusText.text = $"Purchased {itemType}!";
            shopStatusText.color = Color.green;
        }

        UpdateShopUILocal();
    }

    [ClientRpc]
    void PurchaseFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[ShopManager] Purchase failed: {reason}");
        
        if (shopStatusText != null)
        {
            shopStatusText.text = reason;
            shopStatusText.color = Color.red;
        }
    }

    // ====================== CALLBACKS ======================

    void OnShopStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[ShopManager] Shop state: {oldValue} → {newValue}");
        
        if (shopPanel != null)
        {
            shopPanel.SetActive(newValue);
        }
    }

    void OnShopTimerChanged(float oldValue, float newValue)
    {
        if (shopTimerText != null)
        {
            shopTimerText.text = $"Shop Time: {newValue:F1}s";
        }
    }

    void OnCurrentShopperChanged(int oldValue, int newValue)
    {
        Debug.Log($"[ShopManager] Current shopper: {oldValue} → {newValue}");
    }

    void OnShopItemsChanged(NetworkListEvent<NetworkItemData> changeEvent)
    {
        Debug.Log($"[ShopManager] Shop items changed: {changeEvent.Type}");
        UpdateShopUILocal();
    }

    // ====================== UI UPDATE ======================

    void UpdateShopUILocal()
    {
        if (!IsClient) return;

        NetworkPlayerController localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;

        int playerShells = localPlayer.shellCount.Value;

        if (playerShellsText != null)
        {
            playerShellsText.text = $"Your Shells: {playerShells}";
        }

        for (int i = 0; i < shopSlots && i < currentShopItems.Count; i++)
        {
            if (i >= shopButtons.Length || i >= shopTexts.Length || i >= shopPrices.Length)
                continue;

            NetworkItemData item = currentShopItems[i];

            if (item.itemType != ItemType.None)
            {
                ItemData fullItemData = GetItemDataByType(item.itemType);
                
                if (fullItemData != null)
                {
                    shopTexts[i].text = fullItemData.itemName;
                    shopPrices[i].text = $"{item.shellCost} shells";

                    bool canAfford = playerShells >= item.shellCost;
                    bool hasSpace = localPlayer.items.Count < localPlayer.maxItems;
                    bool shopIsOpen = isShopOpen.Value;

                    shopButtons[i].interactable = canAfford && hasSpace && shopIsOpen;

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
            }
            else
            {
                shopTexts[i].text = "SOLD OUT";
                shopPrices[i].text = "";
                shopButtons[i].interactable = false;
                shopTexts[i].color = Color.gray;
            }
        }
    }

    NetworkPlayerController GetLocalPlayer()
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        
        foreach (var player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == localClientId)
            {
                return player;
            }
        }
        
        return null;
    }

    public bool IsShopOpen() => isShopOpen.Value;
    public float GetShopTimer() => shopTimer.Value;
}