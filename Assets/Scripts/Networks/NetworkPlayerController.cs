// // ====================== NETWORK PLAYER CONTROLLER ======================

// using Unity.Netcode;
// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;

// public class NetworkPlayerController : NetworkBehaviour
// {
//     [Header("Network Player Stats")]
//     public NetworkVariable<int> playerId = new NetworkVariable<int>(-1);
//     public NetworkVariable<int> currentHP = new NetworkVariable<int>(4);
//     public NetworkVariable<int> maxHP = new NetworkVariable<int>(4);
//     public NetworkVariable<int> shellCount = new NetworkVariable<int>(1);

//     [Header("Network Game State")]
//     public NetworkVariable<bool> hasPlayedCards = new NetworkVariable<bool>(false);
//     public NetworkVariable<bool> hasUsedItem = new NetworkVariable<bool>(false);

//     // Local data (not networked - managed by owner)
//     private List<Card> handCards = new List<Card>();
//     private List<Card> playedCards = new List<Card>();
//     private List<Item> items = new List<Item>();

//     [Header("UI References")]
//     public Transform handTransform;
//     public Transform playedCardsTransform;
//     public Transform itemSlotsTransform;

//     private bool canSelectCards = false;

//     public override void OnNetworkSpawn()
//     {
//         // Subscribe to network variable changes
//         currentHP.OnValueChanged += OnHPChanged;
//         shellCount.OnValueChanged += OnShellCountChanged;
//         hasPlayedCards.OnValueChanged += OnPlayedCardsChanged;

//         Debug.Log($"NetworkPlayer spawned. PlayerId: {playerId.Value}, IsOwner: {IsOwner}");

//         // Setup UI for this player
//         if (IsOwner)
//         {
//             SetupLocalPlayerUI();
//         }
//     }

//     public override void OnNetworkDespawn()
//     {
//         currentHP.OnValueChanged -= OnHPChanged;
//         shellCount.OnValueChanged -= OnShellCountChanged;
//         hasPlayedCards.OnValueChanged -= OnPlayedCardsChanged;
//     }

//     void OnHPChanged(int oldHP, int newHP)
//     {
//         Debug.Log($"Player {playerId.Value} HP changed: {oldHP} → {newHP}");

//         // Update local UI
//         UpdateHPDisplay();

//         // Death check
//         if (newHP <= 0 && oldHP > 0)
//         {
//             HandlePlayerDeath();
//         }
//     }

//     void OnShellCountChanged(int oldShells, int newShells)
//     {
//         Debug.Log($"Player {playerId.Value} shells: {oldShells} → {newShells}");
//         UpdateShellDisplay();
//     }

//     void OnPlayedCardsChanged(bool oldValue, bool newValue)
//     {
//         if (newValue)
//         {
//             Debug.Log($"Player {playerId.Value} has played their cards");
//         }
//     }

//     // ====================== SERVER RPC METHODS ======================

//     [ServerRpc]
//     public void InitializeNetworkPlayerServerRpc(int id, int hp, int startingCards)
//     {
//         playerId.Value = id;
//         currentHP.Value = hp;
//         maxHP.Value = hp;
//         shellCount.Value = 1;

//         // Draw starting cards
//         DrawCardsClientRpc(startingCards);
//     }

//     [ServerRpc(RequireOwnership = true)]
//     public void PlayCardNetworkServerRpc(int handCardIndex)
//     {
//         if (hasPlayedCards.Value || playedCards.Count >= 3) return;

//         // Validate card index
//         if (handCardIndex < 0 || handCardIndex >= handCards.Count) return;

//         // Move card to played area (on all clients)
//         PlayCardClientRpc(handCardIndex);
//     }

//     [ServerRpc]
//     public void ProcessCardPlayServerRpc(int cardIndex)
//     {
//         // Server-side card play validation and processing
//         if (cardIndex >= 0 && cardIndex < handCards.Count)
//         {
//             PlayCardClientRpc(cardIndex);
//         }
//     }

//     [ServerRpc(RequireOwnership = true)]
//     public void UseItemNetworkServerRpc(int itemIndex)
//     {
//         if (hasUsedItem.Value || itemIndex < 0 || itemIndex >= items.Count) return;

//         hasUsedItem.Value = true;
//         UseItemClientRpc(itemIndex);
//     }

//     [ServerRpc]
//     public void ProcessItemUseServerRpc(int itemIndex)
//     {
//         if (itemIndex >= 0 && itemIndex < items.Count)
//         {
//             UseItemClientRpc(itemIndex);
//         }
//     }

//     [ServerRpc(RequireOwnership = true)]
//     public void TakeDamageServerRpc(int damage)
//     {
//         currentHP.Value = Mathf.Max(0, currentHP.Value - damage);
//     }

//     [ServerRpc(RequireOwnership = true)]
//     public void AddShellsServerRpc(int amount)
//     {
//         shellCount.Value = Mathf.Min(5, shellCount.Value + amount);
//     }

//     // ====================== CLIENT RPC METHODS ======================

//     [ClientRpc]
//     void DrawCardsClientRpc(int amount)
//     {
//         for (int i = 0; i < amount; i++)
//         {
//             DrawCard();
//         }
//     }

//     [ClientRpc]
//     void PlayCardClientRpc(int cardIndex)
//     {
//         if (cardIndex >= 0 && cardIndex < handCards.Count)
//         {
//             Card card = handCards[cardIndex];
//             handCards.RemoveAt(cardIndex);
//             playedCards.Add(card);

//             // Move visually to played area
//             card.transform.SetParent(playedCardsTransform);
//             card.SetFaceUp(false); // Face down until execution

//             UpdateHandVisual();
//         }

//         // Mark as played if this is the owner
//         if (IsOwner)
//         {
//             hasPlayedCards.Value = playedCards.Count > 0;
//         }
//     }

//     [ClientRpc]
//     void UseItemClientRpc(int itemIndex)
//     {
//         if (itemIndex >= 0 && itemIndex < items.Count)
//         {
//             Item item = items[itemIndex];
//             StartCoroutine(item.ExecuteNetworkEffect());

//             // Remove consumable items
//             if (item.IsConsumable())
//             {
//                 items.RemoveAt(itemIndex);
//                 Destroy(item.gameObject);
//             }
//         }
//     }

//     // ====================== NETWORK GAME EXECUTION ======================

//     public IEnumerator ExecuteNetworkCards()
//     {
//         if (!IsServer) yield break;

//         Debug.Log($"Executing cards for Player {playerId.Value}");

//         if (playedCards.Count == 0)
//         {
//             // No cards - self shoot
//             Debug.Log($"Player {playerId.Value} has no cards - self shoot!");

//             // Execute self shoot
//             var revolver = FindObjectOfType<NetworkRevolverManager>();
//             if (revolver != null)
//             {
//                 yield return StartCoroutine(revolver.NetworkShoot(playerId.Value));
//             }
//         }
//         else
//         {
//             // Execute each played card
//             ExecuteCardsClientRpc();

//             foreach (var card in playedCards)
//             {
//                 yield return StartCoroutine(ExecuteNetworkCard(card));
//                 yield return new WaitForSeconds(0.5f);
//             }
//         }

//         // Cleanup
//         CleanupRoundClientRpc();
//     }

//     [ClientRpc]
//     void ExecuteCardsClientRpc()
//     {
//         // Flip cards face up for execution
//         foreach (var card in playedCards)
//         {
//             card.SetFaceUp(true);
//         }
//     }

//     IEnumerator ExecuteNetworkCard(Card card)
//     {
//         if (card == null || card.cardData == null) yield break;

//         Debug.Log($"Player {playerId.Value} executes: {card.cardData.cardName}");

//         // Execute card effect through network revolver
//         var networkRevolver = FindObjectOfType<NetworkRevolverManager>();
//         if (networkRevolver != null)
//         {
//             yield return StartCoroutine(networkRevolver.ExecuteCardEffect(card.cardData.cardType, playerId.Value));
//         }
//     }

//     public IEnumerator NetworkShopTurn()
//     {
//         if (!IsServer) yield break;

//         Debug.Log($"Player {playerId.Value} shop turn");

//         // Open shop for this player
//         OpenShopForPlayerClientRpc();

//         // Shop timer (simplified)
//         float shopTime = 10f;
//         while (shopTime > 0)
//         {
//             shopTime -= Time.deltaTime;
//             yield return null;
//         }

//         // Close shop
//         CloseShopClientRpc();
//     }

//     [ClientRpc]
//     void OpenShopForPlayerClientRpc()
//     {
//         if (IsOwner)
//         {
//             // Show shop UI for this player only
//             Debug.Log($"Opening shop for local player {playerId.Value}");
//             // Implementation would show shop UI
//         }
//     }

//     [ClientRpc]
//     void CloseShopClientRpc()
//     {
//         if (IsOwner)
//         {
//             Debug.Log($"Closing shop for local player {playerId.Value}");
//             // Implementation would hide shop UI
//         }
//     }

//     [ClientRpc]
//     void CleanupRoundClientRpc()
//     {
//         // Clear played cards
//         foreach (var card in playedCards)
//         {
//             if (card != null) Destroy(card.gameObject);
//         }
//         playedCards.Clear();

//         hasPlayedCards.Value = false;
//         hasUsedItem.Value = false;

//         // Draw new card
//         DrawCard();

//         UpdateHandVisual();
//     }

//     // ====================== LOCAL METHODS ======================

//     void SetupLocalPlayerUI()
//     {
//         // Setup UI elements for local player
//         Debug.Log($"Setting up UI for local player {playerId.Value}");
//     }

//     public void EnableCardSelection(bool enable)
//     {
//         if (!IsOwner) return;

//         canSelectCards = enable;

//         foreach (var card in handCards)
//         {
//             card.SetSelectable(enable);
//         }
//     }

//     void DrawCard()
//     {
//         // Draw card from deck (local)
//         CardData cardData = DeckManager.Instance?.DrawCard();
//         if (cardData == null) return;

//         GameObject cardObj = Instantiate(NetworkGameManager.Instance.networkCardPrefab, handTransform);
//         Card card = cardObj.GetComponent<Card>();
//         card.Initialize(cardData);

//         handCards.Add(card);
//         UpdateHandVisual();
//     }

//     void UpdateHandVisual()
//     {
//         for (int i = 0; i < handCards.Count; i++)
//         {
//             Vector3 pos = new Vector3(i * 0.5f, 0, 0);
//             handCards[i].transform.localPosition = pos;
//         }
//     }

//     void UpdateHPDisplay()
//     {
//         // Update HP UI display
//     }

//     void UpdateShellDisplay()
//     {
//         // Update shell count UI display
//     }

//     void HandlePlayerDeath()
//     {
//         Debug.Log($"Player {playerId.Value} died!");

//         // Disable player interactions
//         EnableCardSelection(false);

//         // Visual death effects
//         GetComponent<SpriteRenderer>().color = Color.gray;
//     }

//     public bool IsAlive()
//     {
//         return currentHP.Value > 0;
//     }
// }

