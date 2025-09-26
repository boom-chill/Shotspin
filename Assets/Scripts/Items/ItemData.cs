using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Game/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public GameObject itemPrefab;
    public ItemType itemType;
    public int shellCost; // 1-4 shells
    public int tier; // 1-4
}
