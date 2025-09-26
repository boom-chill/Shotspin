using UnityEngine;
using System.Collections;

public class Item : MonoBehaviour
{
    [Header("Item Data")]
    public ItemData itemData;

    [Header("Visual Components")]

    // Components (3D-ready từ đầu)
    private BoxCollider boxCollider;
    private Rigidbody rb;
    private Animator animator;

    void Awake()
    {
        // Setup components
        boxCollider = GetComponent<BoxCollider>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Setup collider for clicking
        // if (boxCollider != null)
        // {
        //     boxCollider.isTrigger = true;
        // }
    }

    public void Initialize(ItemData data)
    {
        Debug.Log($"ItemData {data}");
        itemData = data;
    }

    void OnMouseDown()
    {
        Debug.Log("Click item");
        UseItem();
    }

    public void UseItem()
    {
        if (itemData == null) return;
        Debug.Log($"Using item: {itemData.itemName}");

        PlayerController owner = GetOwnerPlayer();
        Debug.Log($"owner: {owner?.playerId}");
        if (owner == null) return;

        StartCoroutine(ExecuteItemEffect(owner));
    }

    IEnumerator ExecuteItemEffect(PlayerController owner)
    {
        RevolverManager revolver = GameManager.Instance.revolver;

        switch (itemData.itemType)
        {
            case ItemType.Camera:
                // Xem bài của 1 người - for prototype, random player
                int randomPlayerIndex = Random.Range(0, GameManager.Instance.players.Count);
                PlayerController targetPlayer = GameManager.Instance.players[randomPlayerIndex];
                Debug.Log($"Camera reveals Player {randomPlayerIndex}'s hand:");
                foreach (var card in targetPlayer.handCards)
                {
                    Debug.Log($"- {card.cardData.cardName}");
                }
                break;

            case ItemType.Magnifier:
                // Xem toàn bộ ổ đạn
                revolver.RevealAllSlots();
                break;

            case ItemType.LightShield:
                Debug.Log("Light Shield activated - blocks 1 damage this round");
                // Implementation: add shield component to player
                break;

            case ItemType.HeavyShield:
                Debug.Log("Heavy Shield activated - blocks 2 damage this round");
                break;

            case ItemType.Cigarette:
                owner.Heal(1);
                break;

            case ItemType.HotGift:
                Debug.Log("Hot Gift activated - turning bullets to gold!");
                // For prototype: turn all bullets to gold
                for (int i = 0; i < revolver.bulletSlots.Length; i++)
                {
                    if (revolver.bulletSlots[i] == BulletType.Normal)
                    {
                        revolver.bulletSlots[i] = BulletType.Gold;
                    }
                }
                break;

            default:
                Debug.Log($"Item effect {itemData.itemType} not implemented yet");
                break;
        }

        // Item use animation
        if (animator != null)
        {
            animator.SetTrigger("Use");
        }

        yield return new WaitForSeconds(1f);

        // Some items are consumed, others last the round
        bool isConsumable = (itemData.itemType == ItemType.Cigarette ||
                           itemData.itemType == ItemType.Camera ||
                           itemData.itemType == ItemType.Magnifier);

        if (owner.items.Contains(this))
        {
            owner.items.Remove(this);
        }

        // Destroy with effect
        Destroy(gameObject, 0.5f);

    }

    PlayerController GetOwnerPlayer()
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            PlayerController player = parent.GetComponent<PlayerController>();
            if (player != null) return player;
            parent = parent.parent;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;

            // Vẽ theo loại collider
            if (col is BoxCollider)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.bounds.center, sphere.radius * transform.lossyScale.x);
            }
            else if (col is CapsuleCollider capsule)
            {
                Gizmos.DrawWireSphere(capsule.bounds.center, capsule.radius * transform.lossyScale.x);
            }
        }
    }
}