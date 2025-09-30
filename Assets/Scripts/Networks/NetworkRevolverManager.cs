// NetworkBullet.cs (put in same folder)
using Unity.Netcode;
using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


[Serializable]
public struct NetworkBullet : INetworkSerializable, IEquatable<NetworkBullet>
{
    public BulletType bulletType;

    public NetworkBullet(BulletType t)
    {
        bulletType = t;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref bulletType);
    }

    public bool Equals(NetworkBullet other)
    {
        return bulletType == other.bulletType;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkBullet other && Equals(other);
    }

    public override int GetHashCode()
    {
        return bulletType.GetHashCode();
    }

    // convenient casts
    public static implicit operator BulletType(NetworkBullet nb) => nb.bulletType;
    public static implicit operator NetworkBullet(BulletType bt) => new NetworkBullet(bt);

    public override string ToString()
    {
        return bulletType.ToString();
    }
}
// NetworkRevolverManager.cs

public class NetworkRevolverManager : NetworkBehaviour
{
    [Header("Revolver Network State")]
    private NetworkVariable<int> currentSlot = new NetworkVariable<int>(0);
    public NetworkVariable<int> targetPlayerIndex = new NetworkVariable<int>(0);

    // NetworkList for bullet slots synchronization
    private NetworkList<NetworkBullet> bulletSlots;

    [Header("Visual Components")]
    public Transform cylinder;
    public Transform barrel;
    public LineRenderer aimLine;
    public ParticleSystem shootEffect;
    public AudioSource audioSource;

    public override void OnNetworkSpawn()
    {
        // Initialize NetworkList
        bulletSlots = new NetworkList<NetworkBullet>();

        // If we are server, initialize actual revolver state directly (do NOT call a ServerRpc from server)
        if (IsServer)
        {
            InitializeRevolverOnServer();
        }

        // Subscribe to network variable changes
        currentSlot.OnValueChanged += OnCurrentSlotChanged;
        targetPlayerIndex.OnValueChanged += OnTargetPlayerChanged;
        bulletSlots.OnListChanged += OnBulletSlotsChanged;

        Debug.Log($"NetworkRevolver spawned. IsServer: {IsServer}");
    }

    public override void OnNetworkDespawn()
    {
        currentSlot.OnValueChanged -= OnCurrentSlotChanged;
        targetPlayerIndex.OnValueChanged -= OnTargetPlayerChanged;
        if (bulletSlots != null)
            bulletSlots.OnListChanged -= OnBulletSlotsChanged;
    }

    // --- server-only initializer (call directly from server)
    void InitializeRevolverOnServer()
    {
        if (!IsServer) return;

        Debug.Log("Initializing network revolver (server) ...");

        // Initialize 6 empty slots
        bulletSlots.Clear();
        for (int i = 0; i < 6; i++)
        {
            bulletSlots.Add(new NetworkBullet(BulletType.Empty));
        }

        // Add initial random bullet
        int randomSlot = UnityEngine.Random.Range(0, 6);
        bulletSlots[randomSlot] = new NetworkBullet(BulletType.Normal);

        currentSlot.Value = 0;
        targetPlayerIndex.Value = 0;

        // Show initial state to all clients
        ShowInitialStateClientRpc(randomSlot);
    }

    [ClientRpc]
    void ShowInitialStateClientRpc(int bulletSlot)
    {
        Debug.Log($"=== Initial Revolver State (Network) ===");
        Debug.Log($"Initial bullet at slot {bulletSlot}");

        // Visual feedback for initial reveal
        StartCoroutine(InitialRevealEffect());
    }

    System.Collections.IEnumerator InitialRevealEffect()
    {
        if (cylinder != null)
        {
            var renderer = cylinder.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color original = renderer.color;
                renderer.color = Color.yellow;
                yield return new WaitForSeconds(2f);
                renderer.color = original;
            }
        }
    }

    // ====================== NETWORK VARIABLE CALLBACKS ======================

    void OnCurrentSlotChanged(int oldSlot, int newSlot)
    {
        Debug.Log($"Current slot changed: {oldSlot} → {newSlot}");
        UpdateCylinderVisual();
    }

    void OnTargetPlayerChanged(int oldTarget, int newTarget)
    {
        Debug.Log($"Target player changed: {oldTarget} → {newTarget}");
        UpdateAimLine();
    }

    private void OnBulletSlotsChanged(NetworkListEvent<NetworkBullet> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<NetworkBullet>.EventType.Add:
                Debug.Log($"Bullet added: {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Remove:
                Debug.Log($"Bullet removed: {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Value:
                Debug.Log($"Bullet changed at index {changeEvent.Index}: {changeEvent.PreviousValue} -> {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Clear:
                Debug.Log("Bullet list cleared");
                break;
        }

        // Optional: update UI from the whole slots
        if (bulletSlots.Count == 6)
        {
            var arr = new NetworkBullet[6];
            for (int i = 0; i < 6; i++) arr[i] = bulletSlots[i];
            // UIManager.Instance?.UpdateCylinderUI(arr, currentSlot.Value);
        }

        UpdateCylinderVisual();
    }

    // ====================== CARD EFFECT EXECUTION ======================

    public IEnumerator ExecuteCardEffect(CardType cardType, int executingPlayerId)
    {
        if (!IsServer) yield break;

        Debug.Log($"Executing card effect: {cardType} by Player {executingPlayerId}");

        switch (cardType)
        {
            case CardType.RotateBarrelLeft:
                RotateBarrelServerRpc(-1);
                break;
            case CardType.RotateBarrelRight:
                RotateBarrelServerRpc(1);
                break;
            case CardType.RotateCylinderLeft:
                RotateCylinderServerRpc(-1);
                break;
            case CardType.RotateCylinderRight:
                RotateCylinderServerRpc(1);
                break;
            case CardType.SelfShoot:
                yield return StartCoroutine(NetworkShoot(targetPlayerIndex.Value));
                break;
            case CardType.PeekBullet:
                PeekBulletServerRpc(executingPlayerId);
                break;
            case CardType.AddBullet:
                AddBulletServerRpc(BulletType.Normal, -1);
                break;
            case CardType.AddGoldBullet:
                AddBulletServerRpc(BulletType.Gold, -1);
                break;
            case CardType.ShuffleCylinder:
                ShuffleCylinderServerRpc();
                break;
        }

        yield return new WaitForSeconds(0.5f);
    }

    // ====================== SERVER RPC METHODS ======================

    [ServerRpc(RequireOwnership = false)]
    public void RotateBarrelServerRpc(int direction)
    {
        if (!IsServer) return;

        var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
        int playerCount = networkPlayers.Length;

        if (playerCount == 0) return;

        targetPlayerIndex.Value = (targetPlayerIndex.Value + direction + playerCount) % playerCount;

        // Trigger barrel rotation animation
        RotateBarrelClientRpc(direction);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RotateCylinderServerRpc(int direction)
    {
        if (!IsServer) return;

        if (bulletSlots.Count == 0) return;
        currentSlot.Value = (currentSlot.Value + direction + bulletSlots.Count) % bulletSlots.Count;

        // Trigger cylinder rotation animation
        RotateCylinderClientRpc(direction);
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddBulletServerRpc(BulletType bulletType, int slotIndex)
    {
        if (!IsServer) return;

        if (slotIndex == -1)
        {
            // Find first empty slot
            for (int i = 0; i < bulletSlots.Count; i++)
            {
                if (bulletSlots[i].bulletType == BulletType.Empty)
                {
                    bulletSlots[i] = new NetworkBullet(bulletType);
                    Debug.Log($"Added {bulletType} bullet to slot {i}");
                    return;
                }
            }
            Debug.LogWarning("No empty slots available for bullet!");
        }
        else if (slotIndex >= 0 && slotIndex < bulletSlots.Count)
        {
            bulletSlots[slotIndex] = new NetworkBullet(bulletType);
            Debug.Log($"Added {bulletType} bullet to slot {slotIndex}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ShuffleCylinderServerRpc()
    {
        if (!IsServer) return;

        Debug.Log("Shuffling network cylinder...");

        // Collect all bullets
        var bullets = new List<NetworkBullet>();
        for (int i = 0; i < bulletSlots.Count; i++)
        {
            if (bulletSlots[i].bulletType != BulletType.Empty)
            {
                bullets.Add(bulletSlots[i]);
            }
            bulletSlots[i] = new NetworkBullet(BulletType.Empty);
        }

        // Redistribute randomly
        for (int i = 0; i < bullets.Count; i++)
        {
            int randomSlot;
            do
            {
                randomSlot = UnityEngine.Random.Range(0, bulletSlots.Count);
            }
            while (bulletSlots[randomSlot].bulletType != BulletType.Empty);

            bulletSlots[randomSlot] = bullets[i];
        }

        // Random current slot
        currentSlot.Value = UnityEngine.Random.Range(0, bulletSlots.Count);

        // Trigger shuffle animation
        ShuffleCylinderClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PeekBulletServerRpc(int peekingPlayerId)
    {
        if (!IsServer || currentSlot.Value >= bulletSlots.Count) return;

        NetworkBullet peeked = bulletSlots[currentSlot.Value];

        // Send peek result only to the peeking player
        var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in networkPlayers)
        {
            if (player.playerId.Value == peekingPlayerId)
            {
                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { player.OwnerClientId }
                    }
                };

                PeekResultClientRpc(peeked, clientRpcParams);
                break;
            }
        }
    }

    // ====================== SHOOTING MECHANICS ======================

    public IEnumerator NetworkShoot(int shootingPlayerId)
    {
        if (!IsServer) yield break;
        if (currentSlot.Value >= bulletSlots.Count) yield break;

        NetworkBullet currentBullet = bulletSlots[currentSlot.Value];
        Debug.Log($"Player {shootingPlayerId} shoots! Bullet: {currentBullet.bulletType}");

        // Trigger shoot animation on all clients
        ShootAnimationClientRpc();

        yield return new WaitForSeconds(0.5f);

        // Find target player
        var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
        NetworkPlayerController targetPlayer = null;

        foreach (var player in networkPlayers)
        {
            if (player.playerId.Value == shootingPlayerId)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null) yield break;

        // Process hit/miss
        if (currentBullet.bulletType != BulletType.Empty)
        {
            int damage = (currentBullet.bulletType == BulletType.Gold) ? 2 : 1;
            HitEffectClientRpc(targetPlayer.transform.position);
            Debug.Log($"HIT! Player {shootingPlayerId} takes {damage} damage!");
            // call targetPlayer.TakeDamageServerRpc(damage) etc.
        }
        else
        {
            MissEffectClientRpc(targetPlayer.transform.position);
            Debug.Log($"MISS! Player {shootingPlayerId} gets 1 shell reward!");
            // call targetPlayer.AddShellsServerRpc(1) etc.
        }

        // Remove bullet after shooting
        bulletSlots[currentSlot.Value] = new NetworkBullet(BulletType.Empty);

        // Player chooses new direction (simplified - random for now)
        yield return new WaitForSeconds(1f);
        int newDirection = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
        RotateBarrelServerRpc(newDirection);
    }

    // ====================== CLIENT RPC METHODS ======================

    [ClientRpc]
    void RotateBarrelClientRpc(int direction)
    {
        if (barrel != null)
        {
            StartCoroutine(RotateBarrelAnimation(direction));
        }
        AudioManager.Instance?.PlayGunRotate();
    }

    [ClientRpc]
    void RotateCylinderClientRpc(int direction)
    {
        if (cylinder != null)
        {
            StartCoroutine(RotateCylinderAnimation(direction));
        }
        AudioManager.Instance?.PlayCylinderRotate();
    }

    [ClientRpc]
    void ShuffleCylinderClientRpc()
    {
        if (cylinder != null)
        {
            StartCoroutine(ShuffleAnimation());
        }
    }

    [ClientRpc]
    void ShootAnimationClientRpc()
    {
        if (shootEffect != null) shootEffect.Play();
        AudioManager.Instance?.PlayGunShot();
        VFXManager.Instance?.ShakeCamera(0.5f, 0.3f);
    }

    // Accept a NetworkBullet and use ClientRpcParams to target clients
    [ClientRpc]
    void PeekResultClientRpc(NetworkBullet peekedBullet, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Peeked bullet: {peekedBullet.bulletType}");
        // UIManager.Instance?.ShowNotification($"Peeked: {peekedBullet.bulletType}", Color.cyan);
    }

    [ClientRpc]
    void HitEffectClientRpc(Vector3 position)
    {
        VFXManager.Instance?.PlayBloodEffect(position);
        AudioManager.Instance?.PlaySFX(null);
    }

    [ClientRpc]
    void MissEffectClientRpc(Vector3 position)
    {
        VFXManager.Instance?.PlayShellSparkleEffect(position);
        AudioManager.Instance?.PlayShellReward();
    }

    // ====================== VISUAL UPDATES ======================

    void UpdateCylinderVisual()
    {
        // Update cylinder visual representation
        // This runs on all clients when network variables change
    }

    void UpdateAimLine()
    {
        if (aimLine == null) return;

        var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
        if (targetPlayerIndex.Value < networkPlayers.Length)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = Vector3.zero;

            foreach (var player in networkPlayers)
            {
                if (player.playerId.Value == targetPlayerIndex.Value)
                {
                    targetPos = player.transform.position;
                    break;
                }
            }

            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, targetPos);
            aimLine.enabled = true;
        }
    }

    // ====================== ANIMATION COROUTINES ====================== (unchanged)
    // ... (rotate/shuffle coroutines same as before)

    System.Collections.IEnumerator RotateBarrelAnimation(int direction)
    {
        if (barrel != null)
        {
            float targetAngle = barrel.eulerAngles.z + (direction * 60f);
            float currentAngle = barrel.eulerAngles.z;
            float timer = 0f;
            float duration = 0.5f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float angle = Mathf.LerpAngle(currentAngle, targetAngle, timer / duration);
                barrel.rotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            barrel.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    System.Collections.IEnumerator RotateCylinderAnimation(int direction)
    {
        if (cylinder != null)
        {
            float targetAngle = cylinder.eulerAngles.z + (direction * 60f);
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

    System.Collections.IEnumerator ShuffleAnimation()
    {
        if (cylinder != null)
        {
            float spinTime = 1f;
            float timer = 0f;

            while (timer < spinTime)
            {
                timer += Time.deltaTime;
                cylinder.Rotate(0, 0, 720f * Time.deltaTime);
                yield return null;
            }
        }
    }

    // ====================== PUBLIC GETTERS ======================

    public int GetCurrentSlot() => currentSlot.Value;
    public int GetTargetPlayerIndex() => targetPlayerIndex.Value;

    public BulletType GetBulletAtSlot(int slot)
    {
        if (slot >= 0 && slot < bulletSlots.Count)
            return bulletSlots[slot].bulletType;
        return BulletType.Empty;
    }
}
