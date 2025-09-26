// using Unity.Netcode;
// using UnityEngine;
// using System.Collections;

// public class NetworkRevolverManager : NetworkBehaviour
// {
//     [Header("Revolver Network State")]
//     private NetworkVariable<int> currentSlot = new NetworkVariable<int>(0);
//     private NetworkVariable<int> targetPlayerIndex = new NetworkVariable<int>(0);

//     // NetworkList for bullet slots synchronization
//     private NetworkList<BulletType> bulletSlots;

//     [Header("Visual Components")]
//     public Transform cylinder;
//     public Transform barrel;
//     public LineRenderer aimLine;
//     public ParticleSystem shootEffect;
//     public AudioSource audioSource;

//     public override void OnNetworkSpawn()
//     {
//         // Initialize NetworkList
//         bulletSlots = new NetworkList<BulletType>();

//         if (IsServer)
//         {
//             // Server initializes the revolver
//             InitializeRevolverServerRpc();
//         }

//         // Subscribe to network variable changes
//         currentSlot.OnValueChanged += OnCurrentSlotChanged;
//         targetPlayerIndex.OnValueChanged += OnTargetPlayerChanged;
//         bulletSlots.OnListChanged += OnBulletSlotsChanged;

//         Debug.Log($"NetworkRevolver spawned. IsServer: {IsServer}");
//     }

//     public override void OnNetworkDespawn()
//     {
//         currentSlot.OnValueChanged -= OnCurrentSlotChanged;
//         targetPlayerIndex.OnValueChanged -= OnTargetPlayerChanged;
//         if (bulletSlots != null)
//             bulletSlots.OnListChanged -= OnBulletSlotsChanged;
//     }

//     [ServerRpc(RequireOwnership = false)]
//     void InitializeRevolverServerRpc()
//     {
//         if (!IsServer) return;

//         Debug.Log("Initializing network revolver...");

//         // Initialize 6 empty slots
//         bulletSlots.Clear();
//         for (int i = 0; i < 6; i++)
//         {
//             bulletSlots.Add(BulletType.Empty);
//         }

//         // Add initial random bullet
//         int randomSlot = UnityEngine.Random.Range(0, 6);
//         bulletSlots[randomSlot] = BulletType.Normal;

//         currentSlot.Value = 0;
//         targetPlayerIndex.Value = 0;

//         // Show initial state to all clients
//         ShowInitialStateClientRpc(randomSlot);
//     }

//     [ClientRpc]
//     void ShowInitialStateClientRpc(int bulletSlot)
//     {
//         Debug.Log($"=== Initial Revolver State (Network) ===");
//         Debug.Log($"Initial bullet at slot {bulletSlot}");

//         // Visual feedback for initial reveal
//         StartCoroutine(InitialRevealEffect());
//     }

//     System.Collections.IEnumerator InitialRevealEffect()
//     {
//         // Visual effect to show cylinder state briefly
//         if (cylinder != null)
//         {
//             // Highlight effect
//             var renderer = cylinder.GetComponent<SpriteRenderer>();
//             if (renderer != null)
//             {
//                 Color original = renderer.color;
//                 renderer.color = Color.yellow;
//                 yield return new WaitForSeconds(2f);
//                 renderer.color = original;
//             }
//         }
//     }

//     // ====================== NETWORK VARIABLE CALLBACKS ======================

//     void OnCurrentSlotChanged(int oldSlot, int newSlot)
//     {
//         Debug.Log($"Current slot changed: {oldSlot} → {newSlot}");
//         UpdateCylinderVisual();
//     }

//     void OnTargetPlayerChanged(int oldTarget, int newTarget)
//     {
//         Debug.Log($"Target player changed: {oldTarget} → {newTarget}");
//         UpdateAimLine();

//         // Update UI
//         UIManager.Instance?.UpdateTargetPlayerIndicator(newTarget);
//     }

//     void OnBulletSlotsChanged(NetworkListEvent<BulletType> changeEvent)
//     {
//         Debug.Log($"Bullet slots changed: {changeEvent.Type}");
//         UpdateCylinderVisual();

//         // Update UI
//         if (bulletSlots.Count == 6)
//         {
//             BulletType[] slots = new BulletType[6];
//             for (int i = 0; i < 6; i++)
//             {
//                 slots[i] = bulletSlots[i];
//             }
//             UIManager.Instance?.UpdateCylinderUI(slots, currentSlot.Value);
//         }
//     }

//     // ====================== CARD EFFECT EXECUTION ======================

//     public IEnumerator ExecuteCardEffect(CardType cardType, int executingPlayerId)
//     {
//         if (!IsServer) yield break;

//         Debug.Log($"Executing card effect: {cardType} by Player {executingPlayerId}");

//         switch (cardType)
//         {
//             case CardType.RotateBarrelLeft:
//                 RotateBarrelServerRpc(-1);
//                 break;
//             case CardType.RotateBarrelRight:
//                 RotateBarrelServerRpc(1);
//                 break;
//             case CardType.RotateCylinderLeft:
//                 RotateCylinderServerRpc(-1);
//                 break;
//             case CardType.RotateCylinderRight:
//                 RotateCylinderServerRpc(1);
//                 break;
//             case CardType.SelfShoot:
//                 yield return StartCoroutine(NetworkShoot(targetPlayerIndex.Value));
//                 break;
//             case CardType.PeekBullet:
//                 PeekBulletServerRpc(executingPlayerId);
//                 break;
//             case CardType.AddBullet:
//                 AddBulletServerRpc(BulletType.Normal, -1);
//                 break;
//             case CardType.AddGoldBullet:
//                 AddBulletServerRpc(BulletType.Gold, -1);
//                 break;
//             case CardType.ShuffleCylinder:
//                 ShuffleCylinderServerRpc();
//                 break;
//         }

//         yield return new WaitForSeconds(0.5f);
//     }

//     // ====================== SERVER RPC METHODS ======================

//     [ServerRpc(RequireOwnership = false)]
//     public void RotateBarrelServerRpc(int direction)
//     {
//         if (!IsServer) return;

//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         int playerCount = networkPlayers.Length;

//         targetPlayerIndex.Value = (targetPlayerIndex.Value + direction + playerCount) % playerCount;

//         // Trigger barrel rotation animation
//         RotateBarrelClientRpc(direction);
//     }

//     [ServerRpc(RequireOwnership = false)]
//     public void RotateCylinderServerRpc(int direction)
//     {
//         if (!IsServer) return;

//         currentSlot.Value = (currentSlot.Value + direction + bulletSlots.Count) % bulletSlots.Count;

//         // Trigger cylinder rotation animation
//         RotateCylinderClientRpc(direction);
//     }

//     [ServerRpc(RequireOwnership = false)]
//     public void AddBulletServerRpc(BulletType bulletType, int slotIndex)
//     {
//         if (!IsServer) return;

//         if (slotIndex == -1)
//         {
//             // Find first empty slot
//             for (int i = 0; i < bulletSlots.Count; i++)
//             {
//                 if (bulletSlots[i] == BulletType.Empty)
//                 {
//                     bulletSlots[i] = bulletType;
//                     Debug.Log($"Added {bulletType} bullet to slot {i}");
//                     return;
//                 }
//             }
//             Debug.LogWarning("No empty slots available for bullet!");
//         }
//         else if (slotIndex >= 0 && slotIndex < bulletSlots.Count)
//         {
//             bulletSlots[slotIndex] = bulletType;
//             Debug.Log($"Added {bulletType} bullet to slot {slotIndex}");
//         }
//     }

//     [ServerRpc(RequireOwnership = false)]
//     public void ShuffleCylinderServerRpc()
//     {
//         if (!IsServer) return;

//         Debug.Log("Shuffling network cylinder...");

//         // Collect all bullets
//         var bullets = new System.Collections.Generic.List<BulletType>();
//         for (int i = 0; i < bulletSlots.Count; i++)
//         {
//             if (bulletSlots[i] != BulletType.Empty)
//             {
//                 bullets.Add(bulletSlots[i]);
//             }
//             bulletSlots[i] = BulletType.Empty;
//         }

//         // Redistribute randomly
//         for (int i = 0; i < bullets.Count; i++)
//         {
//             int randomSlot;
//             do
//             {
//                 randomSlot = UnityEngine.Random.Range(0, bulletSlots.Count);
//             }
//             while (bulletSlots[randomSlot] != BulletType.Empty);

//             bulletSlots[randomSlot] = bullets[i];
//         }

//         // Random current slot
//         currentSlot.Value = UnityEngine.Random.Range(0, bulletSlots.Count);

//         // Trigger shuffle animation
//         ShuffleCylinderClientRpc();
//     }

//     [ServerRpc(RequireOwnership = false)]
//     public void PeekBulletServerRpc(int peekingPlayerId)
//     {
//         if (!IsServer || currentSlot.Value >= bulletSlots.Count) return;

//         BulletType peekedBullet = bulletSlots[currentSlot.Value];

//         // Send peek result only to the peeking player
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         foreach (var player in networkPlayers)
//         {
//             if (player.playerId.Value == peekingPlayerId)
//             {
//                 PeekResultClientRpc(peekedBullet, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
//                 break;
//             }
//         }
//     }

//     // ====================== SHOOTING MECHANICS ======================

//     public IEnumerator NetworkShoot(int shootingPlayerId)
//     {
//         if (!IsServer) yield break;

//         if (currentSlot.Value >= bulletSlots.Count) yield break;

//         BulletType currentBullet = bulletSlots[currentSlot.Value];
//         Debug.Log($"Player {shootingPlayerId} shoots! Bullet: {currentBullet}");

//         // Trigger shoot animation on all clients
//         ShootAnimationClientRpc();

//         yield return new WaitForSeconds(0.5f);

//         // Find target player
//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         NetworkPlayerController targetPlayer = null;

//         foreach (var player in networkPlayers)
//         {
//             if (player.playerId.Value == shootingPlayerId)
//             {
//                 targetPlayer = player;
//                 break;
//             }
//         }

//         if (targetPlayer == null) yield break;

//         // Process hit/miss
//         if (currentBullet != BulletType.Empty)
//         {
//             // HIT!
//             int damage = (currentBullet == BulletType.Gold) ? 2 : 1;
//             targetPlayer.TakeDamageServerRpc(damage);

//             // Visual effects
//             HitEffectClientRpc(targetPlayer.transform.position);

//             Debug.Log($"HIT! Player {shootingPlayerId} takes {damage} damage!");
//         }
//         else
//         {
//             // MISS!
//             targetPlayer.AddShellsServerRpc(1);

//             // Visual effects
//             MissEffectClientRpc(targetPlayer.transform.position);

//             Debug.Log($"MISS! Player {shootingPlayerId} gets 1 shell reward!");
//         }

//         // Remove bullet after shooting
//         bulletSlots[currentSlot.Value] = BulletType.Empty;

//         // Player chooses new direction (simplified - random for now)
//         yield return new WaitForSeconds(1f);
//         int newDirection = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
//         RotateBarrelServerRpc(newDirection);
//     }

//     // ====================== CLIENT RPC METHODS ======================

//     [ClientRpc]
//     void RotateBarrelClientRpc(int direction)
//     {
//         if (barrel != null)
//         {
//             StartCoroutine(RotateBarrelAnimation(direction));
//         }

//         // Play sound effect
//         AudioManager.Instance?.PlayGunRotate();
//     }

//     [ClientRpc]
//     void RotateCylinderClientRpc(int direction)
//     {
//         if (cylinder != null)
//         {
//             StartCoroutine(RotateCylinderAnimation(direction));
//         }

//         AudioManager.Instance?.PlayCylinderRotate();
//     }

//     [ClientRpc]
//     void ShuffleCylinderClientRpc()
//     {
//         if (cylinder != null)
//         {
//             StartCoroutine(ShuffleAnimation());
//         }
//     }

//     [ClientRpc]
//     void ShootAnimationClientRpc()
//     {
//         // Shoot animation
//         if (shootEffect != null)
//         {
//             shootEffect.Play();
//         }

//         // Sound effect
//         AudioManager.Instance?.PlayGunShot();

//         // Camera shake
//         VFXManager.Instance?.ShakeCamera(0.5f, 0.3f);
//     }

//     [ClientRpc]
//     void PeekResultClientRpc(BulletType peekedBullet, RpcParams rpcParams = default)
//     {
//         Debug.Log($"Peeked bullet: {peekedBullet}");
//         UIManager.Instance?.ShowNotification($"Peeked: {peekedBullet}", Color.cyan);
//     }

//     [ClientRpc]
//     void HitEffectClientRpc(Vector3 position)
//     {
//         VFXManager.Instance?.PlayBloodEffect(position);
//         AudioManager.Instance?.PlaySFX(null); // Hit sound
//     }

//     [ClientRpc]
//     void MissEffectClientRpc(Vector3 position)
//     {
//         VFXManager.Instance?.PlayShellSparkleEffect(position);
//         AudioManager.Instance?.PlayShellReward();
//     }

//     // ====================== VISUAL UPDATES ======================

//     void UpdateCylinderVisual()
//     {
//         // Update cylinder visual representation
//         // This runs on all clients when network variables change
//     }

//     void UpdateAimLine()
//     {
//         if (aimLine == null) return;

//         var networkPlayers = FindObjectsOfType<NetworkPlayerController>();
//         if (targetPlayerIndex.Value < networkPlayers.Length)
//         {
//             Vector3 startPos = transform.position;
//             Vector3 targetPos = Vector3.zero;

//             // Find target player position
//             foreach (var player in networkPlayers)
//             {
//                 if (player.playerId.Value == targetPlayerIndex.Value)
//                 {
//                     targetPos = player.transform.position;
//                     break;
//                 }
//             }

//             aimLine.SetPosition(0, startPos);
//             aimLine.SetPosition(1, targetPos);
//             aimLine.enabled = true;
//         }
//     }

//     // ====================== ANIMATION COROUTINES ======================

//     System.Collections.IEnumerator RotateBarrelAnimation(int direction)
//     {
//         if (barrel != null)
//         {
//             float targetAngle = barrel.eulerAngles.z + (direction * 60f);
//             float currentAngle = barrel.eulerAngles.z;

//             float timer = 0f;
//             float duration = 0.5f;

//             while (timer < duration)
//             {
//                 timer += Time.deltaTime;
//                 float angle = Mathf.LerpAngle(currentAngle, targetAngle, timer / duration);
//                 barrel.rotation = Quaternion.Euler(0, 0, angle);
//                 yield return null;
//             }

//             barrel.rotation = Quaternion.Euler(0, 0, targetAngle);
//         }
//     }

//     System.Collections.IEnumerator RotateCylinderAnimation(int direction)
//     {
//         if (cylinder != null)
//         {
//             float targetAngle = cylinder.eulerAngles.z + (direction * 60f);
//             float currentAngle = cylinder.eulerAngles.z;

//             float timer = 0f;
//             float duration = 0.5f;

//             while (timer < duration)
//             {
//                 timer += Time.deltaTime;
//                 float angle = Mathf.LerpAngle(currentAngle, targetAngle, timer / duration);
//                 cylinder.rotation = Quaternion.Euler(0, 0, angle);
//                 yield return null;
//             }
//         }
//     }

//     System.Collections.IEnumerator ShuffleAnimation()
//     {
//         if (cylinder != null)
//         {
//             float spinTime = 1f;
//             float timer = 0f;

//             while (timer < spinTime)
//             {
//                 timer += Time.deltaTime;
//                 cylinder.Rotate(0, 0, 720f * Time.deltaTime);
//                 yield return null;
//             }
//         }
//     }

//     // ====================== PUBLIC GETTERS ======================

//     public int GetCurrentSlot() => currentSlot.Value;
//     public int GetTargetPlayerIndex() => targetPlayerIndex.Value;

//     public BulletType GetBulletAtSlot(int slot)
//     {
//         if (slot >= 0 && slot < bulletSlots.Count)
//             return bulletSlots[slot];
//         return BulletType.Empty;
//     }
// }