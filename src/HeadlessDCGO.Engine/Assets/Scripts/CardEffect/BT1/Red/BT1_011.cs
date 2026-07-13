// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_011.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_011 (BT1/Red).
//   [On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions. CanUseCondition/CanActivateCondition (CanTriggerOnPlay / IsExistOnBattleArea /
// HasMatchConditionOwnersCardInTrash) all resolve via the existing bridge. maxCount's `card.Owner.TrashCards.
// Count(CanSelectCardCondition)` (AS-IS Player field) is translated via the mirror zone-state read
// (IZoneStateReader.GetCards + per-id CardSource reconstruction), the same idiom already established elsewhere
// (e.g. BT1_010's LibraryCards.Count translation).
//
// UNRESOLVED MEMBERS (kept verbatim, not simplified/faked; see docs/audit/rebuild_p5_cards_missing.md): AS-IS
// `GManager.instance.GetComponent<SelectCardEffect>()` — the mirror `GManager` (Assets/Scripts/Script/GManager.cs)
// exposes no `GetComponent<T>()` (UI/Photon/component members were deliberately not ported); the mirror
// `SelectCardEffect` (Assets/Scripts/Script/SelectCardEffect.cs) exposes a DIFFERENT, simplified `SetUp` (fewer
// params, no `canTargetCondition_ByPreSelecetedList`/`canEndSelectCondition`/`afterSelectCardCoroutine`/
// `isShowOpponent`/`customRootCardList`/`canLookReverseCard`/`cardEffect`) and no `Activate()` Task method at all
// — the actual choice-resolution loop for this AS-IS `SetUp(...).Activate()` pattern lives entirely in the
// newer `ActivatedEffect`/`IEffectBody`/`ChoiceRequest` resolver machinery, which this task's no-fallback rule
// forbids reaching for. None of the W1-W3 bridge batches touched this selection machinery (they covered only
// `CardEffectCommons.*` mutation coroutines). Kept in AS-IS shape (only the standard IEnumerator->Task /
// StartCoroutine->await substrate translation applied) rather than substituted with the old-model
// ActivatedSelectFromZoneEffect route.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_011 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 card from trash to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.ContainsCardName("Agumon"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    // AS-IS `card.Owner.TrashCards.Count(CanSelectCardCondition)` (Player field) -> mirror
                    // zone-state read + per-id CardSource reconstruction (same idiom as BT1_010).
                    int maxCount = Math.Min(1, ((IZoneStateReader)card.Context.ZoneMover)
                        .GetCards(card.Owner, ChoiceZone.Trash)
                        .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner))));

                    // UNRESOLVED (see file header): AS-IS `GManager.instance.GetComponent<SelectCardEffect>()` /
                    // full AS-IS `SetUp(...)` / `.Activate()` — no mirror bridge exists. Kept verbatim.
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to add to your hand.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();
                }
            }
        }

        return cardEffects;
    }
}
