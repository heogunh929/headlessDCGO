// Source: Assets/Scripts/Script/SelectDeck.cs
// Decision: PORT
// Category: UnityMixedLogic
// Priority: MEDIUM
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
// (SKEL-Exhaust) RECLASSIFIED: NOT a card-effect primitive. The AS-IS body is a pure Unity deck-selection
// screen (MonoBehaviour : OffAnimation with Animator/ScrollRect/DeckInfoPanel/DeckInfoPrefab, GameObject
// .SetActive). Deck choice is a game-setup UI, not a card-effect primitive — out of scope for the engine
// freeze contract (same class as the §3 UI-panel UNPORTED items). Left intentionally bodiless.
