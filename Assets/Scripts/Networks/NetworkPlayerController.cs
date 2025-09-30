using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

// ====================== NETWORK PLAYER CONTROLLER ======================
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Network Stats")]
    public NetworkVariable<int> playerId = new NetworkVariable<int>();
    public NetworkVariable<int> currentHP = new NetworkVariable<int>();
    public NetworkVariable<int> maxHP = new NetworkVariable<int>(5);
    public NetworkVariable<int> shellCount = new NetworkVariable<int>();
    
    [Header("Local State")]
    public List<Card> playedCards = new List<Card>();
    public List<Card> handCards = new List<Card>();
    public List<Item> items = new List<Item>();
    public int maxHandSize = 7;
    public int maxItems = 3;
    public int maxPlayedCards = 3;
    
    [Header("UI References")]
    public Transform handTransform;
    public Transform playedCardsTransform;
    public Transform itemSlotsTransform;
    
    private bool canSelectCards = false;
    private bool canUseItems = false;
    
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Setup camera for local player
            GameObject cameraObj = GameObject.Find("PlayerCamera");
            if (cameraObj != null)
            {
                cameraObj.transform.SetParent(transform);
                cameraObj.transform.localPosition = new Vector3(0, 1.5f, -2f);
                cameraObj.transform.localRotation = Quaternion.Euler(15f, 0, 0);
            }
        }
        
        // Subscribe to HP changes
        currentHP.OnValueChanged += OnHPChanged;
        shellCount.OnValueChanged += OnShellsChanged;
    }
    
    public void Initialize(int id, int hp, int startingCards)
    {
        if (!IsServer) return;
        
        playerId.Value = id;
        currentHP.Value = hp;
        maxHP.Value = hp;
        
        // Draw starting cards
        DrawCardsClientRpc(startingCards);
    }
    
    [ClientRpc]
    void DrawCardsClientRpc(int count)
    {
        if (!IsOwner) return;
        
        for (int i = 0; i < count; i++)
        {
            DrawCard();
        }
    }
    
    public void DrawCard()
    {
        if (!IsOwner) return;
        if (handCards.Count >= maxHandSize) return;
        
        // Get random card from database
        CardData cardData = GameManager.Instance.allCards[Random.Range(0, GameManager.Instance.allCards.Count)];
        
        // Create card object locally
        GameObject cardObj = Instantiate(GameManager.Instance.cardPrefab, handTransform);
        Card card = cardObj.GetComponent<Card>();
        card.Initialize(cardData);
        
        handCards.Add(card);
        UpdateHandVisual();
    }
    
    public void OnCardClicked(Card card)
    {
        if (!IsOwner || !canSelectCards) return;
        
        // Play card locally first for responsiveness
        if (TryPlayCard(card))
        {
            // Notify server about played card
            PlayCardServerRpc(card.cardData.cardType);
        }
    }
    
    [ServerRpc]
    void PlayCardServerRpc(CardType cardType)
    {
        Debug.Log($"Player {playerId.Value} played card: {cardType}");
        
        // Store card data on server for execution phase
        // This would be tracked in a server-side list
    }
    
    public bool TryPlayCard(Card card)
    {
        if (!IsOwner) return false;
        
        if (playedCards.Count >= maxPlayedCards) return false;
        
        if (handCards.Contains(card))
        {
            handCards.Remove(card);
            playedCards.Add(card);
            
            card.transform.SetParent(playedCardsTransform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.Euler(0f, 60f, 90f);
            card.transform.localScale = Vector3.one;
            
            card.SetFaceUp(false);
            
            UpdateHandVisual();
            UpdatePlayedCardSlot();
            return true;
        }
        
        return false;
    }
    
    public IEnumerator ExecutePlayedCards()
    {
        if (!IsServer) yield break;
        
        // Server executes cards and broadcasts results
        ExecuteCardsClientRpc();
        
        yield return new WaitForSeconds(2f);
    }
    
    [ClientRpc]
    void ExecuteCardsClientRpc()
    {
        if (!IsOwner) return;
        
        StartCoroutine(LocalExecuteCards());
    }
    
    IEnumerator LocalExecuteCards()
    {
        if (playedCards.Count == 0)
        {
            Debug.Log($"Player {playerId.Value} has no cards - self shoot!");
            RequestShootServerRpc();
            yield break;
        }
        
        for (int i = playedCards.Count - 1; i >= 0; i--)
        {
            Card card = playedCards[i];
            if (card == null) continue;
            
            card.SetFaceUp(true);
            Debug.Log($"Player {playerId.Value} executes: {card.cardData.cardName}");
            
            // Send card execution to server
            ExecuteCardServerRpc(card.cardData.cardType);
            
            yield return new WaitForSeconds(0.5f);
            
            Destroy(card.gameObject);
            playedCards.RemoveAt(i);
        }
    }
    
    [ServerRpc]
    void ExecuteCardServerRpc(CardType cardType)
    {
        // Server handles card logic
        NetworkRevolverManager revolver = FindObjectOfType<NetworkRevolverManager>();
        
        switch (cardType)
        {
            case CardType.RotateBarrelLeft:
                revolver.RotateBarrelServerRpc(-1);
                break;
            case CardType.RotateBarrelRight:
                revolver.RotateBarrelServerRpc(1);
                break;
            case CardType.SelfShoot:
                // revolver.ShootServerRpc();
                break;
            // Add other card types...
        }
    }
    
    [ServerRpc]
    void RequestShootServerRpc()
    {
        NetworkRevolverManager revolver = FindObjectOfType<NetworkRevolverManager>();
        // revolver.ShootServerRpc();
    }
    
    public void TakeDamage(int damage)
    {
        if (!IsServer) return;
        
        currentHP.Value = Mathf.Max(0, currentHP.Value - damage);
        Debug.Log($"Player {playerId.Value} takes {damage} damage. HP: {currentHP.Value}");
    }
    
    public void Heal(int amount)
    {
        if (!IsServer) return;
        
        currentHP.Value = Mathf.Min(maxHP.Value, currentHP.Value + amount);
        Debug.Log($"Player {playerId.Value} heals {amount}. HP: {currentHP.Value}");
    }
    
    public void AddShells(int amount)
    {
        if (!IsServer) return;
        
        shellCount.Value = Mathf.Min(5, shellCount.Value + amount);
        Debug.Log($"Player {playerId.Value} gains {amount} shells. Total: {shellCount.Value}");
    }
    
    void OnHPChanged(int oldValue, int newValue)
    {
        // Update UI
        Debug.Log($"Player {playerId.Value} HP: {oldValue} -> {newValue}");
        
        if (newValue <= 0 && IsOwner)
        {
            // Show death UI
            Debug.Log("You died!");
        }
    }
    
    void OnShellsChanged(int oldValue, int newValue)
    {
        // Update UI
        Debug.Log($"Player {playerId.Value} Shells: {oldValue} -> {newValue}");
    }
    
    public void EnableCardSelection(bool enable)
    {
        if (!IsOwner) return;
        
        canSelectCards = enable;
        
        // Visual feedback
        foreach(var card in handCards)
        {
            card.SetSelectable(enable);
        }
    }
    
    public void EnableItemUsage(bool enable)
    {
        if (!IsOwner) return;
        
        canUseItems = enable;
    }
    
    public bool IsAlive()
    {
        return currentHP.Value > 0;
    }
    
    public void RoundCleanup()
    {
        if (!IsOwner) return;
        
        foreach (var card in playedCards)
        {
            Destroy(card.gameObject);
        }
        playedCards.Clear();
        
        UpdateHandVisual();
    }
    
    void UpdateHandVisual()
    {
        int cards = handCards.Count;
        if (cards == 0) return;
        
        float range = 60f;
        float initialAngle = 90f;
        float radius = 0.3f;
        
        for (int i = 0; i < cards; i++)
        {
            float angle;
            if (cards == 1)
            {
                angle = initialAngle + range / 2f;
            }
            else
            {
                angle = initialAngle + (range / (cards - 1)) * i;
            }
            
            float rad = angle * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0) * radius;
            
            handCards[i].transform.localPosition = pos;
            handCards[i].transform.localRotation = Quaternion.Euler(0, 0, angle - 90f);
        }
    }
    
    void UpdatePlayedCardSlot()
    {
        int count = playedCards.Count;
        if (count == 0) return;
        
        float spacing = 1.0f;
        float startY = -(count - 1) * spacing * 0.5f;
        
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(0, startY + i * spacing, 0);
            playedCards[i].transform.localPosition = pos;
            playedCards[i].transform.localRotation = Quaternion.Euler(0f, 60f, 90f);
        }
    }
}
