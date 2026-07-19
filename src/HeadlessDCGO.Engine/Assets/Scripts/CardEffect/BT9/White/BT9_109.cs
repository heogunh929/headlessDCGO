// Source: DCGO/Assets/Scripts/CardEffect/BT9/White/BT9_109.cs
// TRUE AS-IS-verbatim re-port (bridge-W5 pass; C-3 WITNESS card — the AS-IS fidelity of the
// CanNotTrashFromDigivolutionCards half is load-bearing). 1:1 mirror of the original BT9_109 (BT9/White,
// the "X Antibody" Option) — the WHOLE AS-IS CardEffects body, all five timing blocks:
//   [None]        IgnoreColorConditionClass — "ignore color requirements" for this card while the owner has a
//                 battle-area Digimon (the ported kind-class, verbatim).
//   [Security]    Gain 1 memory, and add this card to its owner's hand (inline ActivateClass,
//                 SetIsSecurityEffect(true)).
//   [Main]        Place this card under 1 of your Digimon without [X Antibody] in its digivolution cards as its
//                 bottom digivolution card (W4 SelectPermanentEffect Mode.Custom + per-selected coroutine +
//                 MIG4 Permanent.AddDigivolutionCardsBottom — retires the old port's C3-03 STOP).
//   [None]        CanNotTrashFromDigivolutionCardsClass — "[X Antibody] under this Digimon can't be trashed"
//                 (the ported kind-class, SetIsInheritedEffect(true), verbatim — the C-3 witnessed half, incl.
//                 the AS-IS `CardEffectCondition(ICardEffect) = cardEffect != null` at its true signature).
//   [When Attacking] (inherited) this Digimon may digivolve into an [X Antibody] Digimon card in hand for its
//                 digivolution cost (SelectHandEffect + PlayCardClass — REHABBED 수리-9, was C3-04 STOP /
//                 RD-P6C3-D1+D2; all deps now live, see below).
// Replaces the PREVIOUS pass's invented `CanNotTrashFromDigivolutionCardsStaticEffect` /
// `CardEffectFactory.UseRequirements` factory calls and old-model `ActivatedMemoryEffect`/
// `AddThisCardToHandEffect` composites with the literal AS-IS structure.
//
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`/bare
// `StartCoroutine(X)` -> `await X`; `card.Owner.GetBattleAreaDigimons()` -> the batch-3 HeadlessPlayerId
// extension; `card.Owner.AddMemory(1, activateClass)` -> the W4 extension; `AddThisCardToHand(card,
// activateClass)` -> mirror `(card, card)` (ST3_13 convention — sourceCard = the effect's source card);
// AS-IS card-less `HasMatchConditionPermanent(cond)`/`MatchConditionPermanentCount(cond)` -> the mirror
// `(card, cond)` overloads (ST2_06/ST1_08 convention); the Permanent-shaped `CanSelectPermanentCondition` is
// kept VERBATIM and adapted to the id-shaped W4 call sites via `CanSelectPermanentConditionById` (the BT2_097
// idiom); `card.Owner.HandCards` -> `new Player(card.Context, card.Owner).HandCards` (bridge-W5 mirror member,
// BT2_023 Player-handle idiom); `permanent.AddDigivolutionCardsBottom(list, activateClass)` -> the MIG4
// `(list, causeEffectSourceId)` shape; `targetPermanent: card.PermanentOfThisCard()` (a `Permanent` arg) ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`; `cardSource.HasXAntibodyTraits` -> bridge-W5 mirror of
// AS-IS CardSource.cs:1975. UI strip (anchor in-body): `card.Owner.brainStormObject.CloseBrainstrorm(card)`
// (the brainstorm hand-widget overlay, BrainStormObject.cs — pure gameObject.SetActive UI).
//
// FORMERLY-UNRESOLVED MEMBERS — ALL RESOLVED (수리-9 rehab; the three CS errors below are stale). The whole
// [When Attacking] block is now live AS-IS (design items RD-P6C3-D1 + RD-P6C3-D2 CLOSED):
//   1. `SelectHandEffect` — the mirror Script/SelectHandEffect.cs is now a full 550-line 1:1 component
//      (R5-A commit 00552dbf); `GetComponent<SelectHandEffect>()`/SetUp/Activate compile and run (RD-P6C3-D2).
//   2. `cardSource.CanPlayCardTargetFrame(...)` — now declared on the mirror CardSource (CardSource.cs:475),
//      proven live by BT25_092/ArtsDigivolve (RD-P6C3-D1).
//   3. `card.PermanentOfThisCard().PermanentFrame` — Permanent.PermanentFrame (Permanent.cs:118) now
//      materialises the frame on demand (field-list-index adaptation), consumed live by [Arts]/[App Fusion].
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.White;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_109 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return card.Owner.GetBattleAreaDigimons().Count >= 1;
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Memory +1 and add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Gain 1 memory, and add this card to its owner's hand. ";
            }
            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);

                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Place this card under 1 of your Digimon without [X Antibody] in its digivolution cards as its bottom digivolution card.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody")) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            // Id-shape adapter for the W4-bridged call sites (SetUp canTargetCondition /
            // MatchConditionPermanentCount take Func<HeadlessEntityId,bool>): resolve the mirror Permanent for
            // the candidate id (BT2_097 idiom) and evaluate the VERBATIM AS-IS predicate above.
            bool CanSelectPermanentConditionById(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                && CanSelectPermanentCondition(new Permanent(card.Context, id, rec.OwnerId));

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    Permanent? selectedPermanent = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentConditionById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentConditionById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get a digivolution card.", "The opponent is selecting 1 Digimon that will get a digivolution card.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        await Task.CompletedTask; // AS-IS `yield return null`.
                    }

                    if (selectedPermanent != null && !selectedPermanent.IsToken)
                    {
                        // AS-IS `yield return ... card.Owner.brainStormObject.CloseBrainstrorm(card)` — the
                        // brainstorm hand-overlay widget (BrainStormObject.cs:76-87, gameObject.SetActive only)
                        // = UI (stripped).

                        await selectedPermanent.AddDigivolutionCardsBottom(new List<CardSource>() { card }, activateClass.EffectSourceCard.InstanceId);
                    }
                }
            }
        }

        if (timing == EffectTiming.None)
        {
            CanNotTrashFromDigivolutionCardsClass canNotTrashFromDigivolutionCardsClass = new CanNotTrashFromDigivolutionCardsClass();
            canNotTrashFromDigivolutionCardsClass.SetUpICardEffect("[X Antibody] under this Digimon can't be trashed", CanUseCondition, card);
            canNotTrashFromDigivolutionCardsClass.SetUpCanNotTrashFromDigivolutionCardsClass(CardCondition: CardCondition, CardEffectCondition: CardEffectCondition);
            canNotTrashFromDigivolutionCardsClass.SetIsInheritedEffect(true);
            cardEffects.Add(canNotTrashFromDigivolutionCardsClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            bool CardCondition(CardSource cardSource)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CardEffectCondition(ICardEffect cardEffect)
            {
                return cardEffect != null;
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digivolve this Digimon into [X Antibody] Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] This Digimon can digivolve into a Digimon card with [X Antibody] in its traits in your hand for its digivolution cost.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                // (수리-9 REHAB, design item RD-P6C3-D1 CLOSED): AS-IS `cardSource.CanPlayCardTargetFrame(
                // card.PermanentOfThisCard().PermanentFrame, true, activateClass, root: Root.Hand)`. Both
                // members now exist on the mirror (CardSource.CanPlayCardTargetFrame CardSource.cs:475,
                // Permanent.PermanentFrame Permanent.cs:118 — materialised on demand; proven live by BT25_092),
                // so the earlier MIG5-FRAME-MODEL STOP is stale. `card.PermanentOfThisCard()` ->
                // `ICardEffect.ResolvePermanentOfThisCard(card)` (the standard mirror translation).
                if (cardSource.HasXAntibodyTraits)
                {
                    if (cardSource.IsDigimon)
                    {
                        if (CardEffectCommons.IsExistOnBattleArea(card))
                        {
                            if (cardSource.CanPlayCardTargetFrame(ICardEffect.ResolvePermanentOfThisCard(card).PermanentFrame, true, activateClass, root: SelectCardEffect.Root.Hand))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                // (수리-9 REHAB, design item RD-P6C3-D2 CLOSED): the mirror SelectHandEffect is now a full
                // 1:1 component (Script/SelectHandEffect.cs, 550 lines — R5-A commit 00552dbf), so the AS-IS
                // select-1-hand-card + PlayCardClass digivolve block is restored verbatim. Substrate only:
                // `card.Owner.HandCards` -> new Player(ctx, owner).HandCards; IEnumerator SelectCardCoroutine
                // (`yield return null`) -> `Task SelectCardCoroutine` returning Task.CompletedTask (BT22_035
                // convention); `yield return StartCoroutine(X)` -> `await X`; `card.PermanentOfThisCard()` ->
                // ICardEffect.ResolvePermanentOfThisCard(card).
                if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 card to digivolve.", "The opponent is selecting 1 card to digivolve.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectHandEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    if (selectedCards.Count >= 1)
                    {
                        CardSource selectedCard = selectedCards[0];

                        if (CardEffectCommons.IsExistOnBattleArea(card))
                        {
                            await new PlayCardClass(
                                cardSources: new List<CardSource>() { selectedCard },
                                hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                                payCost: true,
                                targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                                isTapped: false,
                                root: SelectCardEffect.Root.Hand,
                                activateETB: true).PlayCard();
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
