using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Game/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public string description;
    public Sprite cardSprite;
    public CardType cardType;
    public int cost = 0; // Nếu có chi phí đặc biệt
}