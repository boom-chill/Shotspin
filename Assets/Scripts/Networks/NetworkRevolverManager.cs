using Unity.Netcode;
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetworkRevolverManager : NetworkBehaviour
{
    [Header("Revolver Network State")]
    private NetworkVariable<int> currentSlot = new NetworkVariable<int>(0);
    public NetworkVariable<int> targetPlayerIndex = new NetworkVariable<int>(0);

    // ✅ CRITICAL: Initialize NetworkList at declaration
    private NetworkList<NetworkBullet> bulletSlots = new NetworkList<NetworkBullet>();

    [Header("Visual Components")]
    public Transform cylinder;
    public Transform barrel;
    public LineRenderer aimLine;
    public ParticleSystem shootEffect;
    public AudioSource audioSource;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"[Revolver] OnNetworkSpawn called. IsServer: {IsServer}");

        // If we are server, initialize actual revolver state
        if (IsServer)
        {
            InitializeRevolverOnServer();
        }

        // Subscribe to network variable changes
        currentSlot.OnValueChanged += OnCurrentSlotChanged;
        targetPlayerIndex.OnValueChanged += OnTargetPlayerChanged;
        bulletSlots.OnListChanged += OnBulletSlotsChanged;

        Debug.Log($"[Revolver] NetworkRevolver spawned. IsServer: {IsServer}");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        currentSlot.OnValueChanged -= OnCurrentSlotChanged;
        targetPlayerIndex.OnValueChanged -= OnTargetPlayerChanged;

        if (bulletSlots != null)
        {
            bulletSlots.OnListChanged -= OnBulletSlotsChanged;
        }
    }

    void InitializeRevolverOnServer()
    {
        if (!IsServer) return;

        Debug.Log("[Revolver] Initializing revolver on server...");

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

        Debug.Log($"[Revolver] ✓ Initialized with bullet at slot {randomSlot}");

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
        Debug.Log($"[Revolver] Current slot changed: {oldSlot} → {newSlot}");
        UpdateCylinderVisual();
    }

    void OnTargetPlayerChanged(int oldTarget, int newTarget)
    {
        Debug.Log($"[Revolver] Target player changed: {oldTarget} → {newTarget}");
        UpdateAimLine();
    }

    private void OnBulletSlotsChanged(NetworkListEvent<NetworkBullet> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<NetworkBullet>.EventType.Add:
                Debug.Log($"[Revolver] Bullet added: {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Remove:
                Debug.Log($"[Revolver] Bullet removed: {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Value:
                Debug.Log($"[Revolver] Bullet changed at index {changeEvent.Index}: {changeEvent.PreviousValue} -> {changeEvent.Value}");
                break;

            case NetworkListEvent<NetworkBullet>.EventType.Clear:
                Debug.Log("[Revolver] Bullet list cleared");
                break;
        }

        UpdateCylinderVisual();
    }

    // ====================== CARD EFFECT EXECUTION ======================

    public IEnumerator ExecuteCardEffect(CardType cardType, int executingPlayerId)
    {
        if (!IsServer) yield break;

        Debug.Log($"[Revolver] Executing card effect: {cardType} by Player {executingPlayerId}");

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

        RotateBarrelClientRpc(direction);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RotateCylinderServerRpc(int direction)
    {
        if (!IsServer) return;

        if (bulletSlots.Count == 0) return;
        currentSlot.Value = (currentSlot.Value + direction + bulletSlots.Count) % bulletSlots.Count;

        RotateCylinderClientRpc(direction);
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddBulletServerRpc(BulletType bulletType, int slotIndex)
    {
        if (!IsServer) return;

        if (slotIndex == -1)
        {
            for (int i = 0; i < bulletSlots.Count; i++)
            {
                if (bulletSlots[i].bulletType == BulletType.Empty)
                {
                    bulletSlots[i] = new NetworkBullet(bulletType);
                    Debug.Log($"[Revolver] Added {bulletType} bullet to slot {i}");
                    return;
                }
            }
            Debug.LogWarning("[Revolver] No empty slots available for bullet!");
        }
        else if (slotIndex >= 0 && slotIndex < bulletSlots.Count)
        {
            bulletSlots[slotIndex] = new NetworkBullet(bulletType);
            Debug.Log($"[Revolver] Added {bulletType} bullet to slot {slotIndex}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ShuffleCylinderServerRpc()
    {
        if (!IsServer) return;

        Debug.Log("[Revolver] Shuffling cylinder...");

        var bullets = new List<NetworkBullet>();
        for (int i = 0; i < bulletSlots.Count; i++)
        {
            if (bulletSlots[i].bulletType != BulletType.Empty)
            {
                bullets.Add(bulletSlots[i]);
            }
            bulletSlots[i] = new NetworkBullet(BulletType.Empty);
        }

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

        currentSlot.Value = UnityEngine.Random.Range(0, bulletSlots.Count);

        ShuffleCylinderClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PeekBulletServerRpc(int peekingPlayerId)
    {
        if (!IsServer || currentSlot.Value >= bulletSlots.Count) return;

        NetworkBullet peeked = bulletSlots[currentSlot.Value];

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
        Debug.Log($"[Revolver] Player {shootingPlayerId} shoots! Bullet: {currentBullet.bulletType}");

        ShootAnimationClientRpc();

        yield return new WaitForSeconds(0.5f);

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

        if (currentBullet.bulletType != BulletType.Empty)
        {
            int damage = (currentBullet.bulletType == BulletType.Gold) ? 2 : 1;
            HitEffectClientRpc(targetPlayer.transform.position);
            Debug.Log($"[Revolver] HIT! Player {shootingPlayerId} takes {damage} damage!");
        }
        else
        {
            MissEffectClientRpc(targetPlayer.transform.position);
            Debug.Log($"[Revolver] MISS! Player {shootingPlayerId} gets 1 shell reward!");
        }

        bulletSlots[currentSlot.Value] = new NetworkBullet(BulletType.Empty);

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
    }

    [ClientRpc]
    void RotateCylinderClientRpc(int direction)
    {
        if (cylinder != null)
        {
            StartCoroutine(RotateCylinderAnimation(direction));
        }
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
    }

    [ClientRpc]
    void PeekResultClientRpc(NetworkBullet peekedBullet, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[Revolver] Peeked bullet: {peekedBullet.bulletType}");
    }

    [ClientRpc]
    void HitEffectClientRpc(Vector3 position)
    {
        Debug.Log($"[Revolver] Hit effect at {position}");
    }

    [ClientRpc]
    void MissEffectClientRpc(Vector3 position)
    {
        Debug.Log($"[Revolver] Miss effect at {position}");
    }

    // ====================== VISUAL UPDATES ======================

    void UpdateCylinderVisual()
    {
        // Update cylinder visual representation
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

    // ====================== ANIMATION COROUTINES ======================

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