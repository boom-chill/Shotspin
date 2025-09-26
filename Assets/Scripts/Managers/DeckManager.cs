using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DeckManager : MonoBehaviour
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

    private List<CardData> currentDeck = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();

    public static DeckManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        BuildDeck();
    }

    void BuildDeck()
    {
        currentDeck.Clear();

        int playerCount = GameManager.Instance.players.Count;

        // Adjust ratios based on player count
        float aggressiveMultiplier = GetAggressiveMultiplier(playerCount);
        float utilityMultiplier = GetUtilityMultiplier(playerCount);

        // Calculate actual card counts
        int rotateCards = Mathf.RoundToInt(totalDeckSize * rotateCardsRatio / 100f);
        int aggressiveCards = Mathf.RoundToInt(totalDeckSize * aggressiveCardsRatio * aggressiveMultiplier / 100f);
        int utilityCards = Mathf.RoundToInt(totalDeckSize * utilityCardsRatio * utilityMultiplier / 100f);
        int bluffCards = totalDeckSize - rotateCards - aggressiveCards - utilityCards;

        Debug.Log($"Building deck for {playerCount} players:");
        Debug.Log($"Rotate: {rotateCards}, Aggressive: {aggressiveCards}, Utility: {utilityCards}, Bluff: {bluffCards}");

        // Add cards to deck based on ratios
        AddCardsByType(GetRotateCards(), rotateCards);
        AddCardsByType(GetAggressiveCards(), aggressiveCards);
        AddCardsByType(GetUtilityCards(), utilityCards);
        AddCardsByType(GetBluffCards(), bluffCards);

        // Shuffle deck
        ShuffleDeck();

        Debug.Log($"Deck built with {currentDeck.Count} cards");
    }

    void AddCardsByType(List<CardData> cardTypes, int count)
    {
        if (cardTypes.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            CardData randomCard = cardTypes[Random.Range(0, cardTypes.Count)];
            currentDeck.Add(randomCard);
        }
    }

    List<CardData> GetRotateCards()
    {
        return GameManager.Instance.allCards.Where(card =>
            card.cardType == CardType.RotateBarrelLeft ||
            card.cardType == CardType.RotateBarrelRight ||
            card.cardType == CardType.RotateCylinderLeft ||
            card.cardType == CardType.RotateCylinderRight
        ).ToList();
    }

    List<CardData> GetAggressiveCards()
    {
        return GameManager.Instance.allCards.Where(card =>
            card.cardType == CardType.SelfShoot ||
            card.cardType == CardType.AddGoldBullet ||
            card.cardType == CardType.AddBullet ||
            card.cardType == CardType.SkipNext
        ).ToList();
    }

    List<CardData> GetUtilityCards()
    {
        return GameManager.Instance.allCards.Where(card =>
            card.cardType == CardType.PeekBullet ||
            card.cardType == CardType.DrawCards ||
            card.cardType == CardType.ShuffleCylinder
        ).ToList();
    }

    List<CardData> GetBluffCards()
    {
        return GameManager.Instance.allCards.Where(card =>
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
        // Giảm utility cards khi ít người chơi
        switch (playerCount)
        {
            case 2: return 0.7f;
            case 3: return 0.8f;
            default: return 1f;
        }
    }

    public CardData DrawCard()
    {
        if (currentDeck.Count == 0)
        {
            // Reshuffle discard pile back to deck
            if (discardPile.Count > 0)
            {
                currentDeck.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeck();
                Debug.Log("Deck reshuffled from discard pile");
            }
            else
            {
                Debug.LogWarning("No cards available to draw!");
                return null;
            }
        }

        CardData drawnCard = currentDeck[0];
        currentDeck.RemoveAt(0);

        return drawnCard;
    }

    public void DiscardCard(CardData card)
    {
        discardPile.Add(card);
    }

    void ShuffleDeck()
    {
        for (int i = currentDeck.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            CardData temp = currentDeck[i];
            currentDeck[i] = currentDeck[randomIndex];
            currentDeck[randomIndex] = temp;
        }
    }

    public int GetRemainingCards()
    {
        return currentDeck.Count;
    }

    public int GetDiscardedCards()
    {
        return discardPile.Count;
    }
}