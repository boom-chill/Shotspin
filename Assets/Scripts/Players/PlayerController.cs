using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Player Stats")]
    public int playerId;
    public int currentHP;
    public int maxHP = 4;
    public int shellCount = 0;

    [Header("Hand & Items")]
    public List<Card> playedCards = new List<Card>(); // Cards đã đánh trong turn
    public List<Card> handCards = new List<Card>();
    public List<Item> items = new List<Item>();
    public int maxHandSize = 7;
    public int maxItems = 3;
    public int maxPlayedCards = 3;

    [Header("UI References")]
    public Transform handTransform; // Vị trí hiển thị bài trên tay
    public Transform playedCardsTransform; // Vị trí bài đã đánh
    public Transform itemSlotsTransform; // Vị trí items

    private bool canSelectCards = false;
    private bool hasUsedItemThisRound = false;

    // Components (2D setup với tiềm năng 3D)
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private BoxCollider boxCollider;

    void Awake()
    {
        // Setup components cho 2D với constraints cho 3D tương lai
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        // Constrain Y axis cho 2D gameplay trên X-Z plane
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        }
    }

    public void Initialize(int id, int hp, int startingCards)
    {
        playerId = id;
        currentHP = hp;
        maxHP = hp;

        // Rút bài ban đầu
        for (int i = 0; i < startingCards; i++)
        {
            DrawCard();
        }

        // Mua 1 item giá 1-2 shell ban đầu
        // (Implementation đơn giản - random item tier 1-2)
        BuyRandomStartingItem();
    }

    public void DrawCard()
    {
        if (handCards.Count >= maxHandSize) return;

        // Lấy random card từ database
        CardData cardData = GameManager.Instance.allCards[Random.Range(0, GameManager.Instance.allCards.Count)];

        // Tạo card object
        GameObject cardObj = Instantiate(GameManager.Instance.cardPrefab, handTransform);
        Card card = cardObj.GetComponent<Card>();
        card.Initialize(cardData);

        handCards.Add(card);
        UpdateHandVisual();
    }

    public void EnableCardSelection(bool enable)
    {
        canSelectCards = enable;

        // Enable/disable click cho cards
        foreach (var card in handCards)
        {
            card.SetSelectable(enable);
        }
    }

    public bool TryPlayCard(Card card)
    {
        if (handCards.Contains(card))
        {
            handCards.Remove(card);
            playedCards.Add(card);

            // Set parent + reset local transform
            card.transform.SetParent(playedCardsTransform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.Euler(0f, 60f, 90f); // <-- reset rotation (0,0,0)
            card.transform.localScale = Vector3.one;

            card.SetFaceUp(false); // Úp bài

            UpdateHandVisual();
            UpdatePlayedCardSlot();
            return true;
        }

        return false;
    }

    public IEnumerator ItemUsePhase()
    {
        if (hasUsedItemThisRound || items.Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        Debug.Log($"Player {playerId} - Item use phase (3 seconds)");

        // Enable item usage
        EnableItemUsage(true);

        float timer = 3f;
        while (timer > 0 && !hasUsedItemThisRound)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        EnableItemUsage(false);
    }

    public IEnumerator ExecutePlayedCards()
    {
        if (playedCards.Count == 0)
        {
            Debug.Log($"Player {playerId} has no cards - self shoot!");
            yield return StartCoroutine(GameManager.Instance.revolver.Shoot());
            yield break;
        }

        // Lật và thực thi từng lá
        for (int i = playedCards.Count - 1; i >= 0; i--)
        {
            Card card = playedCards[i];
            if (card == null) continue;

            card.SetFaceUp(true); // Lật bài
            Debug.Log($"Player {playerId} executes: {card.cardData.cardName}");

            yield return StartCoroutine(card.ExecuteCard());
            yield return new WaitForSeconds(0.5f);

            // Xoá card sau khi xài xong
            Destroy(card.gameObject);
            playedCards.RemoveAt(i);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"Player {playerId} takes {damage} damage. HP: {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        Debug.Log($"Player {playerId} heals {amount}. HP: {currentHP}");
    }

    public void AddShells(int amount)
    {
        shellCount = Mathf.Min(5, shellCount + amount); // Max 5 shells
        Debug.Log($"Player {playerId} gains {amount} shells. Total: {shellCount}");
    }

    public bool CanAffordItem(ItemData itemData)
    {
        return shellCount >= itemData.shellCost;
    }

    public void BuyItem(ItemData itemData)
    {

        Debug.Log("itemData: " + itemData);
        Debug.Log("itemData.prefab: " + (itemData != null ? itemData.itemPrefab : "NULL"));

        if (!CanAffordItem(itemData) || items.Count >= maxItems) return;

        shellCount -= itemData.shellCost;

        if (itemData.itemPrefab == null)
        {
            Debug.LogError($"ItemData {itemData.itemName} chưa gán prefab!");
            return;
        }

        GameObject itemObj = Instantiate(itemData.itemPrefab, itemSlotsTransform);
        Item item = itemObj.GetComponent<Item>();
        item.Initialize(itemData);

        items.Add(item);
        UpdateItemSlots();
        Debug.Log($"Player {playerId} bought {itemData.itemName} for {itemData.shellCost} shells");
    }

    public void BuyRandomStartingItem()
    {
        // Simplified - tạo random item tier 1-2
        // Thực tế sẽ dùng ShopManager
        List<ItemData> startingItems = new List<ItemData>(); // Load từ resources
        if (startingItems.Count > 0)
        {
            ItemData randomItem = startingItems[Random.Range(0, startingItems.Count)];
            if (CanAffordItem(randomItem))
            {
                BuyItem(randomItem);
            }
        }
    }

    public void EnableItemUsage(bool enable)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
            {
                items.RemoveAt(i); // cleanup phần tử null
            }
        }
    }

    public bool IsAlive()
    {
        return currentHP > 0;
    }

    void Die()
    {
        Debug.Log($"Player {playerId} died!");
        // Disable visuals, etc.
    }

    public void RoundCleanup()
    {
        // Clear played cards
        foreach (var card in playedCards)
        {
            Destroy(card.gameObject);
        }
        playedCards.Clear();

        hasUsedItemThisRound = false;

        UpdateHandVisual();
    }

    void UpdateHandVisual()
    {
        int cards = handCards.Count;
        if (cards == 0) return;

        float range = 60f;       // tổng góc fan (độ)
        float initialAngle = 90f; // góc bắt đầu
        float radius = 0.3f;     // bán kính vòng cung

        for (int i = 0; i < cards; i++)
        {
            float angle;
            if (cards == 1)
            {
                // Chỉ 1 lá → đặt chính giữa
                angle = initialAngle + range / 2f;
            }
            else
            {
                // Tính toán bình thường
                angle = initialAngle + (range / (cards - 1)) * i;
            }

            float rad = angle * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0) * radius;

            handCards[i].transform.localPosition = pos;
            handCards[i].transform.localRotation = Quaternion.Euler(0, 0, angle - 90f);
            // trừ 90f để lá bài "úp" theo cung
        }
    }

    void UpdateItemSlots()
    {
        int count = items.Count;
        if (count == 0) return;

        float spacing = 2.0f; // khoảng cách giữa các item theo trục Y
        float startY = -(count - 1) * spacing * 0.5f; // căn giữa

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(0, startY + i * spacing, 0);
            items[i].transform.localPosition = pos;

            // Xoay item thẳng, không nghiêng
            items[i].transform.localRotation = Quaternion.identity;
        }
    }

    public void UpdatePlayedCardSlot()
    {
        int count = playedCards.Count;
        if (count == 0) return;

        float spacing = 1.0f; // khoảng cách giữa các item theo trục Y
        float startY = -(count - 1) * spacing * 0.5f; // căn giữa

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(0, startY + i * spacing, 0);
            playedCards[i].transform.localPosition = pos;

            // Xoay item thẳng, không nghiêng
            playedCards[i].transform.localRotation = Quaternion.Euler(0f, 60f, 90f);
        }
    }
}