// ====================== REVOLVER MANAGER ======================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RevolverManager : MonoBehaviour
{
    [Header("Revolver Settings")]
    public int cylinderSize = 6;
    public BulletType[] bulletSlots;
    public int currentSlot = 0; // Vị trí đạn sẽ bắn tiếp theo
    public int targetPlayerIndex = 0; // Người chơi bị súng chỉ vào

    [Header("Visual Components")]
    public Transform cylinder; // Child object cho ổ đạn
    public Transform barrel; // Child object cho nòng súng  
    public Transform[] bulletSlotVisuals; // 6 visual slots
    public LineRenderer aimLine; // Đường ngắm chỉ player

    [Header("Effects")]
    public ParticleSystem shootEffect;
    public AudioSource audioSource;
    public AudioClip shootSound;
    public AudioClip emptySound;

    // Components (2D setup với tiềm năng 3D)
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private Animator animator;

    void Awake()
    {
        // Setup components
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Constrain cho 2D trên X-Z plane
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        }

        // Initialize bullet slots
        bulletSlots = new BulletType[cylinderSize];
        for (int i = 0; i < cylinderSize; i++)
        {
            bulletSlots[i] = BulletType.Empty;
        }

        InitializeRevolver();
    }

    void InitializeRevolver()
    {
        InitializeGoldenRevolver();
        return;

        // Khởi tạo với 1 viên đạn ngẫu nhiên
        int randomSlot = Random.Range(0, cylinderSize);
        bulletSlots[randomSlot] = BulletType.Normal;

        Debug.Log($"Initial bullet at slot {randomSlot}");

        // Show initial cylinder state once
        StartCoroutine(ShowInitialCylinder());

        UpdateVisuals();
        UpdateAimLine();
    }

    void InitializeGoldenRevolver()
    {
        // Khởi tạo toàn bộ là đạn vàng
        for (int i = 0; i < cylinderSize; i++)
        {
            bulletSlots[i] = BulletType.Gold;
        }

        Debug.Log("Initial cylinder filled with GOLD bullets");

        // Show initial cylinder state once
        StartCoroutine(ShowInitialCylinder());

        UpdateVisuals();
        UpdateAimLine();
    }

    IEnumerator ShowInitialCylinder()
    {
        Debug.Log("=== Initial Cylinder State ===");
        for (int i = 0; i < cylinderSize; i++)
        {
            Debug.Log($"Slot {i}: {bulletSlots[i]}");
        }

        // Visual effect để hiển thị cho players xem 1 lần
        // Có thể làm animation xoay cylinder, highlight bullets

        yield return new WaitForSeconds(2f);

        Debug.Log("Initial reveal complete!");
    }

    // ====================== BARREL ROTATION ======================

    public void RotateBarrelLeft()
    {
        RotateBarrel(-1);
    }

    public void RotateBarrelRight()
    {
        RotateBarrel(1);
    }

    void RotateBarrel(int direction)
    {
        int playerCount = GameManager.Instance.players.Count;
        targetPlayerIndex = (targetPlayerIndex + direction + playerCount) % playerCount;

        Debug.Log($"Barrel rotated. Now targeting Player {targetPlayerIndex}");

        // Visual rotation animation
        StartCoroutine(RotateBarrelAnimation(direction));
        UpdateAimLine();
    }

    IEnumerator RotateBarrelAnimation(int direction)
    {
        if (barrel != null)
        {
            float duration = 0.5f;
            float timer = 0f;

            // Lưu trạng thái xoay ban đầu
            Quaternion startRot = barrel.rotation;

            // Tính trạng thái xoay đích: xoay thêm 90 độ quanh trục Y (local)
            Quaternion endRot = startRot * Quaternion.Euler(direction * 90f, 0f, 0f);

            // Xoay mượt
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                barrel.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            // Đảm bảo kết thúc đúng góc
            barrel.rotation = endRot;
        }
    }

    // ====================== CYLINDER ROTATION ======================

    public void RotateCylinderLeft()
    {
        RotateCylinder(-1);
    }

    public void RotateCylinderRight()
    {
        RotateCylinder(1);
    }

    void RotateCylinder(int direction)
    {
        currentSlot = (currentSlot + direction + cylinderSize) % cylinderSize;

        Debug.Log($"Cylinder rotated. Current slot: {currentSlot}");

        // Visual rotation
        StartCoroutine(RotateCylinderAnimation(direction));
        UpdateVisuals();
    }

    IEnumerator RotateCylinderAnimation(int direction)
    {
        if (cylinder != null)
        {
            float targetAngle = cylinder.eulerAngles.z + (direction * 60f); // 60 degrees per slot
            float currentAngle = cylinder.eulerAngles.z;

            float timer = 0f;
            float duration = 0.5f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float angle = Mathf.LerpAngle(currentAngle, targetAngle, timer / duration);
                cylinder.rotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }
        }
    }

    // ====================== BULLET MANAGEMENT ======================

    public BulletType PeekCurrentBullet()
    {
        return bulletSlots[currentSlot];
    }

    public void AddBullet(BulletType bulletType, int slotIndex = -1)
    {
        if (slotIndex == -1)
        {
            // Find first empty slot
            for (int i = 0; i < cylinderSize; i++)
            {
                if (bulletSlots[i] == BulletType.Empty)
                {
                    bulletSlots[i] = bulletType;
                    Debug.Log($"Added {bulletType} bullet to slot {i}");
                    break;
                }
            }
        }
        else if (slotIndex >= 0 && slotIndex < cylinderSize)
        {
            bulletSlots[slotIndex] = bulletType;
            Debug.Log($"Added {bulletType} bullet to slot {slotIndex}");
        }

        UpdateVisuals();
    }

    public void RemoveBullet(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < cylinderSize)
        {
            BulletType removedBullet = bulletSlots[slotIndex];
            bulletSlots[slotIndex] = BulletType.Empty;

            Debug.Log($"Removed {removedBullet} bullet from slot {slotIndex}");

            // Give shell reward
            PlayerController targetPlayer = GameManager.Instance.players[targetPlayerIndex];
            targetPlayer.AddShells(1);
        }

        UpdateVisuals();
    }

    public void ShuffleCylinder()
    {
        Debug.Log("Shuffling cylinder...");

        // Collect all bullets
        List<BulletType> bullets = new List<BulletType>();
        for (int i = 0; i < cylinderSize; i++)
        {
            if (bulletSlots[i] != BulletType.Empty)
            {
                bullets.Add(bulletSlots[i]);
                bulletSlots[i] = BulletType.Empty;
            }
        }

        // Redistribute randomly
        for (int i = 0; i < bullets.Count; i++)
        {
            int randomSlot;
            do
            {
                randomSlot = Random.Range(0, cylinderSize);
            }
            while (bulletSlots[randomSlot] != BulletType.Empty);

            bulletSlots[randomSlot] = bullets[i];
        }

        // Random current slot
        currentSlot = Random.Range(0, cylinderSize);

        StartCoroutine(ShuffleAnimation());
        UpdateVisuals();
    }

    void ShiftCylinderRight()
    {
        if (cylinderSize <= 1) return;

        BulletType last = bulletSlots[cylinderSize - 1];

        for (int i = cylinderSize - 1; i > 0; i--)
        {
            bulletSlots[i] = bulletSlots[i - 1];
        }

        bulletSlots[0] = last;

        // Sau khi shift, slot 0 sẽ là viên kế tiếp
        currentSlot = 0;

        Debug.Log("Cylinder shifted right by 1 step");
    }

    IEnumerator ShuffleAnimation()
    {
        if (cylinder != null)
        {
            // Spin effect
            float spinTime = 1f;
            float timer = 0f;

            while (timer < spinTime)
            {
                timer += Time.deltaTime;
                cylinder.Rotate(0, 0, 720f * Time.deltaTime); // 2 rotations per second
                yield return null;
            }
        }
    }

    // ====================== SHOOTING ======================

    public IEnumerator Shoot()
    {
        PlayerController targetPlayer = GameManager.Instance.players[targetPlayerIndex];
        BulletType currentBullet = bulletSlots[currentSlot];

        Debug.Log($"Player {targetPlayerIndex} shoots! Bullet: {currentBullet}");

        // Shoot animation
        if (animator != null)
        {
            animator.SetTrigger("Shooting1");
        }

        // Sound effect
        if (audioSource != null)
        {
            audioSource.clip = (currentBullet != BulletType.Empty) ? shootSound : emptySound;
            audioSource.Play();
        }

        // Particle effect
        if (shootEffect != null)
        {
            shootEffect.Play();
        }

        yield return new WaitForSeconds(0.5f);

        // Handle hit/miss
        if (currentBullet != BulletType.Empty)
        {
            // HIT!
            int damage = (currentBullet == BulletType.Gold) ? 2 : 1;
            targetPlayer.TakeDamage(damage);

            Debug.Log($"HIT! Player {targetPlayerIndex} takes {damage} damage!");
        }
        else
        {
            // MISS!
            targetPlayer.AddShells(1);
            Debug.Log($"MISS! Player {targetPlayerIndex} gets 1 shell reward!");
        }

        // Remove bullet after shooting
        bulletSlots[currentSlot] = BulletType.Empty;

        // Rotate cylinder one step to the right
        ShiftCylinderRight();

        // Player chooses new direction and position
        yield return StartCoroutine(HandlePostShoot(targetPlayer));

        UpdateVisuals();
    }

    IEnumerator HandlePostShoot(PlayerController shooter)
    {
        // In a real game, this would be player input
        // For prototype, random or simple logic

        Debug.Log($"Player {shooter.playerId} chooses new gun direction...");

        // Simple AI: random direction
        int direction = Random.Range(0, 2) == 0 ? -1 : 1;
        RotateBarrel(direction);

        yield return new WaitForSeconds(1f);
    }

    // ====================== UTILITY METHODS ======================

    public int GetTargetPlayerIndex()
    {
        return targetPlayerIndex;
    }

    public void SetTargetPlayer(int playerIndex)
    {
        targetPlayerIndex = playerIndex;
        UpdateAimLine();
    }

    void UpdateVisuals()
    {
        // Update bullet slot visuals
        if (bulletSlotVisuals != null)
        {
            for (int i = 0; i < bulletSlotVisuals.Length && i < cylinderSize; i++)
            {
                if (bulletSlotVisuals[i] != null)
                {
                    SpriteRenderer slotRenderer = bulletSlotVisuals[i].GetComponent<SpriteRenderer>();
                    if (slotRenderer != null)
                    {
                        switch (bulletSlots[i])
                        {
                            case BulletType.Empty:
                                slotRenderer.color = Color.gray;
                                break;
                            case BulletType.Normal:
                                slotRenderer.color = Color.white;
                                break;
                            case BulletType.Gold:
                                slotRenderer.color = Color.yellow;
                                break;
                        }

                        // Highlight current slot
                        if (i == currentSlot)
                        {
                            slotRenderer.color = Color.Lerp(slotRenderer.color, Color.red, 0.3f);
                        }
                    }
                }
            }
        }
    }

    void UpdateAimLine()
    {
        if (aimLine != null && GameManager.Instance.players.Count > 0)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = GameManager.Instance.players[targetPlayerIndex].transform.position;

            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, targetPos);
            aimLine.enabled = true;
        }
    }

    public void RevealAllSlots()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Cylinder Full Reveal ===");
        sb.AppendLine($"Current: {currentSlot}");

        for (int i = 0; i < cylinderSize; i++)
        {
            sb.Append($"[{i}:{bulletSlots[i]}] ");
        }

        Debug.Log(sb.ToString());
    }
}