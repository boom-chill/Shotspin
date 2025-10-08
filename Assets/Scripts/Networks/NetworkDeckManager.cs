using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class NetworkDeckManager : NetworkBehaviour
{
    [Header("Card Distribution")]
    public int totalDeckSize = 60;

    [Header("Base Card Ratios (%)")]
    [Range(0, 100)] public int rotateCardsRatio = 20;
    [Range(0, 100)] public int aggressiveCardsRatio = 25;
    [Range(0, 100)] public int utilityCardsRatio = 30;
    [Range(0, 100)] public int bluffCardsRatio = 25;

    [Header("Player Count Adjustments")]
    public float twoPlayerAggressiveMultiplier = 1.5f;
    public float threePlayerAggressiveMultiplier = 1.3f;

    [Header("Card Database")]
    public List<CardData> allCards = new List<CardData>();

    private NetworkList<NetworkCardData> currentDeck = new NetworkList<NetworkCardData>();
    private NetworkList<NetworkCardData> discardPile = new NetworkList<NetworkCardData>();
    
    public NetworkVariable<int> remainingCards = new NetworkVariable<int>(0);
    public NetworkVariable<int> discardedCards = new NetworkVariable<int>(0);

    public static NetworkDeckManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // currentDeck = new NetworkList<NetworkCardData>();
            // discardPile = new NetworkList<NetworkCardData>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            BuildDeck();
        }

        remainingCards.OnValueChanged += OnRemainingCardsChanged;
        discardedCards.OnValueChanged += OnDiscardedCardsChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        remainingCards.OnValueChanged -= OnRemainingCardsChanged;
        discardedCards.OnValueChanged -= OnDiscardedCardsChanged;
    }

    // ====================== DECK BUILDING ======================

    void BuildDeck()
    {
        if (!IsServer) return;

        currentDeck.Clear();
        discardPile.Clear();

        int playerCount = NetworkGameManager.Instance.networkPlayers.Count;

        float aggressiveMultiplier = GetAggressiveMultiplier(playerCount);
        float utilityMultiplier = GetUtilityMultiplier(playerCount);

        int rotateCards = Mathf.RoundToInt(totalDeckSize * rotateCardsRatio / 100f);
        int aggressiveCards = Mathf.RoundToInt(totalDeckSize * aggressiveCardsRatio * aggressiveMultiplier / 100f);
        int utilityCards = Mathf.RoundToInt(totalDeckSize * utilityCardsRatio * utilityMultiplier / 100f);
        int bluffCards = totalDeckSize - rotateCards - aggressiveCards - utilityCards;

        Debug.Log($"[DeckManager] Building deck for {playerCount} players:");
        Debug.Log($"Rotate: {rotateCards}, Aggressive: {aggressiveCards}, Utility: {utilityCards}, Bluff: {bluffCards}");

        AddCardsByType(GetRotateCards(), rotateCards);
        AddCardsByType(GetAggressiveCards(), aggressiveCards);
        AddCardsByType(GetUtilityCards(), utilityCards);
        AddCardsByType(GetBluffCards(), bluffCards);

        ShuffleDeck();

        remainingCards.Value = currentDeck.Count;
        discardedCards.Value = 0;

        Debug.Log($"[DeckManager] ✓ Deck built with {currentDeck.Count} cards");
    }

    void AddCardsByType(List<CardData> cardTypes, int count)
    {
        if (!IsServer || cardTypes.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            CardData randomCard = cardTypes[Random.Range(0, cardTypes.Count)];
            
            NetworkCardData networkCard = new NetworkCardData
            {
                cardType = randomCard.cardType,
                cardId = i
            };
            
            currentDeck.Add(networkCard);
        }
    }

    // ====================== CARD TYPE FILTERS ======================

    List<CardData> GetRotateCards()
    {
        return allCards.Where(card =>
            card.cardType == CardType.RotateBarrelLeft ||
            card.cardType == CardType.RotateBarrelRight ||
            card.cardType == CardType.RotateCylinderLeft ||
            card.cardType == CardType.RotateCylinderRight
        ).ToList();
    }

    List<CardData> GetAggressiveCards()
    {
        return allCards.Where(card =>
            card.cardType == CardType.SelfShoot ||
            card.cardType == CardType.AddGoldBullet ||
            card.cardType == CardType.AddBullet ||
            card.cardType == CardType.SkipNext
        ).ToList();
    }

    List<CardData> GetUtilityCards()
    {
        return allCards.Where(card =>
            card.cardType == CardType.PeekBullet ||
            card.cardType == CardType.DrawCards ||
            card.cardType == CardType.ShuffleCylinder
        ).ToList();
    }

    List<CardData> GetBluffCards()
    {
        return allCards.Where(card =>
            card.cardType == CardType.Counter
        ).ToList();
    }

    float GetAggressiveMultiplier(int playerCount)
    {
        switch (playerCount)
        {
            case 2: return twoPlayerAggressiveMultiplier;
            case 3: return threePlayerAggressiveMultiplier;
            default: return 1f;
        }
    }

    float GetUtilityMultiplier(int playerCount)
    {
        switch (playerCount)
        {
            case 2: return 0.7f;
            case 3: return 0.8f;
            default: return 1f;
        }
    }

    // ====================== DRAW CARD ======================

    [ServerRpc(RequireOwnership = false)]
    public void DrawCardServerRpc(ulong requestingClientId)
    {
        if (!IsServer) return;

        CardData drawnCard = DrawCardInternal();
        
        if (drawnCard != null)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { requestingClientId }
                }
            };

            SendCardToClientClientRpc(drawnCard.cardType, clientRpcParams);
        }
    }

    CardData DrawCardInternal()
    {
        if (!IsServer) return null;

        if (currentDeck.Count == 0)
        {
            if (discardPile.Count > 0)
            {
                foreach (var card in discardPile)
                {
                    currentDeck.Add(card);
                }
                discardPile.Clear();
                
                ShuffleDeck();
                
                Debug.Log("[DeckManager] Deck reshuffled from discard pile");
            }
            else
            {
                Debug.LogWarning("[DeckManager] No cards available to draw!");
                return null;
            }
        }

        NetworkCardData drawnNetworkCard = currentDeck[0];
        currentDeck.RemoveAt(0);

        remainingCards.Value = currentDeck.Count;

        CardData cardData = allCards.Find(c => c.cardType == drawnNetworkCard.cardType);
        
        Debug.Log($"[DeckManager] Drew card: {drawnNetworkCard.cardType}");
        
        return cardData;
    }

    [ClientRpc]
    void SendCardToClientClientRpc(CardType cardType, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[DeckManager] Received card: {cardType}");
        
        NetworkPlayerController localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            CardData cardData = NetworkGameManager.Instance.allCards.Find(c => c.cardType == cardType);
            if (cardData != null)
            {
                GameObject cardObj = Instantiate(NetworkGameManager.Instance.cardPrefab, localPlayer.handTransform);
                Card card = cardObj.GetComponent<Card>();
                card.Initialize(cardData);
                
                localPlayer.handCards.Add(card);
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

    // ====================== DISCARD CARD ======================

    [ServerRpc(RequireOwnership = false)]
    public void DiscardCardServerRpc(CardType cardType)
    {
        if (!IsServer) return;

        NetworkCardData networkCard = new NetworkCardData
        {
            cardType = cardType,
            cardId = discardPile.Count
        };
        
        discardPile.Add(networkCard);
        discardedCards.Value = discardPile.Count;

        Debug.Log($"[DeckManager] Card discarded: {cardType}");
    }

    // ====================== SHUFFLE DECK ======================

    void ShuffleDeck()
    {
        if (!IsServer) return;

        List<NetworkCardData> tempList = new List<NetworkCardData>();
        foreach (var card in currentDeck)
        {
            tempList.Add(card);
        }

        for (int i = tempList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            var temp = tempList[i];
            tempList[i] = tempList[randomIndex];
            tempList[randomIndex] = temp;
        }

        currentDeck.Clear();
        foreach (var card in tempList)
        {
            currentDeck.Add(card);
        }

        Debug.Log("[DeckManager] Deck shuffled");
    }

    // ====================== CALLBACKS ======================

    void OnRemainingCardsChanged(int oldValue, int newValue)
    {
        Debug.Log($"[DeckManager] Remaining cards: {oldValue} → {newValue}");
    }

    void OnDiscardedCardsChanged(int oldValue, int newValue)
    {
        Debug.Log($"[DeckManager] Discarded cards: {oldValue} → {newValue}");
    }

    // ====================== GETTERS ======================

    public int GetRemainingCards()
    {
        return remainingCards.Value;
    }

    public int GetDiscardedCards()
    {
        return discardedCards.Value;
    }
}