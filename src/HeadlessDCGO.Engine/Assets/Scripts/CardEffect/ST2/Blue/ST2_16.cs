// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_16.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_16 (an Option).
//   [Main] Return 1 of your opponent's Digimon to its owner's hand. (Trash all of the digivolution cards of
//          that Digimon.)
//   [Security] (use the Main effect)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBounceEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow, `SelectPermanentEffect.Mode.Bounce`
// (the engine's bounce mode handles the under-card trashing — no separate coroutine/cardCondition in AS-IS).
// The [Security] block already used the REAL AS-IS `CardEffectCommons.AddActivateMainOptionSecurityEffect(...)`
// call (verified against AS-IS CardEffectCommons.cs:723) — kept unchanged.
// NOTE (fidelity debt, tracked separately, NOT addressed here per task instructions): AS-IS
// `TrashDigivolutionCardsFromTopOrBottom`/`DiscardEvoRoots` ordering for bounce under-cards is tracked as a
// pre-existing debt (ST2_16 bounce under-discard, docs/audit memory) — this file is a literal AS-IS structural
// port (Mode.Bounce, no separate discard call, exactly as AS-IS itself does at this call site) and does not
// attempt to fix that separately-tracked debt.
// AS-IS structure kept verbatim: `null` for CanActivateCondition (SetUpActivateClass's first arg).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b; IsOpponentBattleAreaDigimon converts to its Permanent-form sibling
// IsPermanentExistsOnOpponentBattleAreaDigimon); AS-IS
// `CardEffectCommons.HasMatchConditionPermanent(cond)` / `MatchConditionPermanentCount(cond)` (global scan, no
// CardSource arg in AS-IS) -> mirror's `(card, condition)` overloads.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_16 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Return 1 of your opponent's Digimon to its owner's hand. (Trash all of the digivolution cards of that Digimon.)";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Return 1 Digimon to hand");
        }

        return cardEffects;
    }
}
