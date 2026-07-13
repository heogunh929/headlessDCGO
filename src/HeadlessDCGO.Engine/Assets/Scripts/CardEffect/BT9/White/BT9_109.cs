// Source: Assets/Scripts/CardEffect/BT9/White/BT9_109.cs
// Decision: PORT (partial — C-3 witness)
// Category: CardEffect
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.White
//
// (C-3) C-3 witness: the [X Antibody]-under-this-Digimon trash protection (AS-IS timing==None,
// CanNotTrashFromDigivolutionCardsClass, SetIsInheritedEffect(true)) is ported 1:1 as a continuous
// ContinuousTrashProtectionEffect, consumed by the EFFECT-trash filter (TrashProtectionScan) but bypassed by the
// DELETION path (AS-IS DiscardEvoRoots trashes evo roots unconditionally). Being INHERITED, the AS-IS effect-list
// membership (Permanent.EffectList_ForCard) exposes it to the scan only while THIS card is a NON-TOP (tucked)
// digivolution source of a Digimon permanent — a top-card BT9_109 grants nothing (the scan enforces this).
// Remaining branch state (C-3 재상환 P2-3):
//   - [None] IgnoreColorConditionClass — PORTED (was design item C3-01): AS-IS CanUse = "owner controls >=1
//     battle-area Digimon", CardCondition = self. UseRequirements with cardCondition:null is the exact self
//     shape, with the CanUse folded into the plain condition gate.
//   - [Security] Memory +1 + add this card to hand — PORTED (was design item C3-02): the ST3_13 composite
//     pattern (ActivatedMemoryEffect then AddThisCardToHandEffect at SecuritySkill, AS-IS coroutine order).
//   - [Main/OptionSkill] place this card under a Digimon WITHOUT [X Antibody] as its bottom source
//     (design item C3-03, STOP stands): the flow is a SelectPermanent whose per-selected follow-up must run
//     `selectedPermanent.AddDigivolutionCardsBottom([THIS option card], activateClass)` — a tuck of the RESOLVING
//     option card itself (self-pin) out of the execution area, which no current body/mutation expresses
//     (AddSourcesBottomAsync exists, but the activation-flow "tuck self after select" body does not).
//   - [When Attacking] digivolve this Digimon into an [X Antibody] Digimon from hand for its digivolution
//     cost (design item C3-04, STOP stands): SelectAndDigivolveEffect EXISTS (the select+digivolve flow is
//     built), but this grant is an INHERITED activated skill (SetIsInheritedEffect(true) on the ActivateClass)
//     and the uniform ActivatedEffect carries no inherited flag — the host-permanent membership (fire while
//     tucked, target = the HOST permanent, not this card) plus the self-pinned digivolve-target selection stage
//     (CanPlayCardTargetFrame against the host's frame) have no headless surface yet.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.White;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT9_109 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            // (C-3 재상환 P2-3 / C3-01) AS-IS BT9_109.cs:15-31 — IgnoreColorConditionClass: CanUseCondition =
            // card.Owner.GetBattleAreaDigimons().Count >= 1; CardCondition = (cardSource == card), i.e. the
            // ignore-color applies to THIS card only. UseRequirements(cardCondition: null) registers exactly the
            // self-scoped ignore-color flag; the CanUse gate rides the plain condition.
            cardEffects.Add(CardEffectFactory.UseRequirements(
                card: card,
                cardCondition: null,
                isInheritedEffect: false,
                condition: () => CardEffectCommons.HasMatchConditionOwnersPermanent(card, p => p.IsDigimon)));

            // AS-IS BT9_109.cs:132-167 — CanNotTrashFromDigivolutionCardsClass, inherited continuous.
            // CanUseCondition = host IsExistOnBattleArea; CardCondition = source name contains "X Antibody"/
            // "XAntibody" while host on field; CardEffectCondition = effect != null (any effect).
            cardEffects.Add(CardEffectFactory.CanNotTrashFromDigivolutionCardsStaticEffect(
                cardCondition: CardCondition,
                cardEffectCondition: CardEffectCondition,
                isInheritedEffect: true,
                card: card,
                condition: CanUseCondition));

            bool CanUseCondition() => CardEffectCommons.IsExistOnBattleArea(card);

            bool CardCondition(CardSource cardSource) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && (cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody"));

            // AS-IS CardEffectCondition(ICardEffect) = `cardEffect != null`. The headless scan always supplies a
            // non-null causing effect source (the effect-trash mutation's SourceEntityId), so this is always true.
            bool CardEffectCondition(CardSource causingEffectSource) => true;
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            // (C-3 재상환 P2-3 / C3-02) AS-IS BT9_109.cs:33-57 — [Security] ActivateCoroutine:
            // card.Owner.AddMemory(1) THEN CardEffectCommons.AddThisCardToHand(card). The ST3_13 composite
            // pattern: sibling activated effects at SecuritySkill, resolved in list order (memory first).
            cardEffects.Add(new ActivatedMemoryEffect(
                card, amount: 1, "[Security] Gain 1 memory."));
            cardEffects.Add(new AddThisCardToHandEffect(
                card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
