using UnityEngine;
using System.Collections;

public class Card : MonoBehaviour
{
    [Header("Card Data")]
    public CardData cardData;

    [Header("Visual Components")]
    public SpriteRenderer cardRenderer;
    public SpriteRenderer backRenderer;
    public Sprite cardBack;

    private bool isFaceUp = false;
    private bool isSelectable = false;
    private bool isSelected = false;

    // Components (2D setup với tiềm năng 3D)
    private BoxCollider boxCollider;
    private Rigidbody rb;
    private Animator animator;

    void Awake()
    {
        // Setup components
        cardRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Constrain cho 2D trên X-Z plane
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        }

        // Setup collider for clicking
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }

    public void Initialize(CardData data)
    {
        cardData = data;

        if (cardRenderer != null && cardData != null)
        {
            cardRenderer.sprite = cardData.cardSprite;
        }

        // Start face down
        SetFaceUp(false);
    }

    public void SetFaceUp(bool faceUp)
    {
        isFaceUp = faceUp;

        if (cardRenderer != null)
        {
            if (isFaceUp && cardData != null)
            {
                cardRenderer.sprite = cardData.cardSprite;
            }
            else
            {
                cardRenderer.sprite = cardBack;
            }
        }

        // Animation nếu có
        if (animator != null)
        {
            animator.SetBool("FaceUp", isFaceUp);
        }
    }

    public void SetSelectable(bool selectable)
    {
        isSelectable = selectable;

        // Visual feedback
        if (cardRenderer != null)
        {
            cardRenderer.color = selectable ? Color.white : Color.gray;
        }
    }

    public void PlayCard()
    {
        if (cardData == null) return;
        Debug.Log($"Playing card: {cardData.cardName}");

        PlayerController owner = GetOwnerPlayer();
        Debug.Log($"owner: {owner?.playerId}");
        if (owner != null)
        {
            bool success = owner.TryPlayCard(this);
            if (success)
            {
                Debug.Log($"Card {cardData.cardName} played!");
                // SetSelected(true);
            }
        }
    }

    // void OnMouseDown()
    // {
    //     // if (!isSelectable) return;

    //     // Find owner player
    //     PlayerController owner = GetOwnerPlayer();
    //     if (owner != null)
    //     {
    //         bool success = owner.TryPlayCard(this);
    //         if (success)
    //         {
    //             Debug.Log($"Card {cardData.cardName} played!");
    //             SetSelected(true);
    //         }
    //     }
    // }

    void SetSelected(bool selected)
    {
        isSelected = selected;

        // Visual feedback
        if (cardRenderer != null)
        {
            cardRenderer.color = selected ? Color.green : Color.white;
        }
    }

    // ====================== CARD EXECUTION ======================

    public IEnumerator ExecuteCard()
    {
        if (cardData == null) yield break;

        Debug.Log($"Executing card: {cardData.cardName}");

        RevolverManager revolver = GameManager.Instance.revolver;

        switch (cardData.cardType)
        {
            case CardType.RotateBarrelLeft:
                revolver.RotateBarrelLeft();
                break;

            case CardType.RotateBarrelRight:
                revolver.RotateBarrelRight();
                break;

            case CardType.RotateCylinderLeft:
                revolver.RotateCylinderLeft();
                break;

            case CardType.RotateCylinderRight:
                revolver.RotateCylinderRight();
                break;

            case CardType.SelfShoot:
                yield return StartCoroutine(revolver.Shoot());
                break;

            case CardType.PeekBullet:
                BulletType currentBullet = revolver.PeekCurrentBullet();
                Debug.Log($"Peeked bullet: {currentBullet}");
                // Show UI feedback to player
                break;

            case CardType.SkipNext:
                // Implementation will be in TurnManager
                Debug.Log("Next player turn skipped!");
                break;

            case CardType.AddGoldBullet:
                // For prototype, add to random empty slot
                revolver.AddBullet(BulletType.Gold);
                break;

            case CardType.AddBullet:
                revolver.AddBullet(BulletType.Normal);
                break;

            case CardType.ShuffleCylinder:
                revolver.ShuffleCylinder();
                break;

            case CardType.DrawCards:
                // Find card owner and draw 2 cards
                PlayerController owner = GetOwnerPlayer();
                if (owner != null)
                {
                    owner.DrawCard();
                    owner.DrawCard();
                }
                break;

            case CardType.Counter:
                Debug.Log("Counter card activated - blocks next harmful effect");
                // Implementation tương lai
                break;

            default:
                Debug.Log($"Card effect {cardData.cardType} not implemented yet");
                break;
        }

        // Card execution animation
        if (animator != null)
        {
            animator.SetTrigger("Execute");
        }

        yield return new WaitForSeconds(0.5f);
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
}