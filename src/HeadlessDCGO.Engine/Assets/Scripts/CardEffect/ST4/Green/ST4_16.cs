// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_16.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_16 (ST4/Green) — an Option.
//   [Main] Return 1 of your opponent's suspended Digimon to its owner's hand. Trash all of the
//          digivolution cards of that Digimon.
//   [Security] (use the Main effect)
// AS-IS HandBounceClaass.Bounce() (CardController.cs:2622) unconditionally calls permanent.DiscardEvoRoots()
// (Permanent.cs:106) right before the top card leaves the field, for EVERY hand-bounce — so the "trash all
// digivolution cards" clause is the Mode.Bounce mechanic itself, not a separate effect call in this file.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBounceWithSourceDiscardEffect(...)` call
// (an invented per-card wrapper) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + `SetUp(...)` (Mode.Bounce) structure (see
// ST4_15.cs this batch for the identically-shaped Main/Security Option precedent — same shape, Mode.Bounce
// instead of Mode.Tap, plus the extra `IsSuspended` target narrowing).
// [Security] block UNCHANGED (no afterMainBody — AS-IS has none here): established genuine bridge.
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `Func<Permanent,bool> CanSelectPermanentCondition` -> the established
// `Func<HeadlessEntityId,bool>` id-shape idiom (IsOpponentBattleAreaDigimon(card, id) && IsSuspended(card, id)).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_16 : CEntity_Effect
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
                return "[Main] Return 1 of your opponent's suspended Digimon to its owner's hand. Trash all of the digivolution cards of that Digimon.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.IsSuspended(card, id))
                    {
                        return true;
                    }
                }

                return false;
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
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Return 1 Digimon to hand");
        }

        return cardEffects;
    }
}
