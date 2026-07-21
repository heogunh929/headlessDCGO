// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_15.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_15 (ST4/Green) — an Option.
//   [Main]     Suspend 1 of your opponent's Digimon.
//   [Security] Suspend 1 of your opponent's Digimon. Then, add this card to your hand.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndSuspendEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + `SetUp(...)` (Mode.Tap) structure (see
// BT1_043.cs / ST4_13.cs for the identically-shaped select+Mode precedent within this batch).
// [Security] block (uniform-사멸 flip): `CardEffectCommons.AddActivateMainOptionSecurityEffect(...,
// afterMainEffect: new AddThisCardToHandEffect(card, ...))` — the AS-IS "reuse [Main] then afterMainEffect"
// SecuritySkill shape; the follow-up is the same AddThisCardToHandEffect kind the EX1_072/BT9_109 [Security]
// add-to-hand path resolves (mirrors AS-IS AddThisCardToHand exactly). Replaces the retired uniform
// `ActivatedEffect` + `SelfToHandBody` carrier.
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `Func<Permanent,bool> CanSelectPermanentCondition` -> the established
// `Func<HeadlessEntityId,bool>` id-shape idiom (IsOpponentBattleAreaDigimon(card, id)).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_15 : CEntity_Effect
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
                return "[Main] Suspend 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
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
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            // [Security] reuse [Main] (suspend 1 opponent Digimon), THEN add this card to hand — the AS-IS
            // afterMainEffect callback, VERBATIM: `IEnumerator AfterMainEffect(ICardEffect activateClass) =>
            // AddThisCardToHand(card, activateClass)` (substrate IEnumerator->Task; the mirror
            // `AddThisCardToHand(card1, sourceCard)` takes sourceCard = the cause card = `card`, the ST3_13
            // convention since SetUpICardEffect sets EffectSourceCard = card).
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card, cardEffects: ref cardEffects,
                effectName: "Suspend 1 Digimon and add this card to hand",
                afterMainEffect: AfterMainEffect);

            async Task AfterMainEffect(ICardEffect activateClass)
            {
                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }

        return cardEffects;
    }
}
