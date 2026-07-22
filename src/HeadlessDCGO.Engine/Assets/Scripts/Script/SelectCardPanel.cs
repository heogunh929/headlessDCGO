// Source: Assets/Scripts/Script/SelectCardPanel.cs
// Decision: PORT
// Category: UnityMixedLogic
// Priority: MEDIUM
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
// (SKEL-Exhaust) RECLASSIFIED: NOT a card-effect primitive. The AS-IS body is a pure Unity UI panel
// (MonoBehaviour : TextMeshProUGUI/ScrollRect/Button/DG.Tweening, GameObject.SetActive, Instantiate). Card
// selection in headless is driven by the ChoiceProvider substrate (ChoiceRequest/ChoiceResult), so this UI
// panel has no engine-primitive body to port — out of scope for the engine freeze contract (same class as
// the §3 UI-panel UNPORTED items). Left intentionally bodiless.
