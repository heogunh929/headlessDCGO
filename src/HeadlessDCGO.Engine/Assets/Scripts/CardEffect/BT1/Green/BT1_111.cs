// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_111.cs — an Option.
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_111 (BT1/Green).
//   [Main]     Suspend 1 of your opponent's Digimon or 2 of your opponent's Digimon with 5000 DP or less.
//   [Security] (reuse [Main] verbatim, no afterMainEffect) -> AddActivateMainOptionSecurityEffect (bridged).
// AS-IS structure kept verbatim: inline ActivateClass. The 2-vs-1 mode pick uses
// `GManager.instance.userSelectionManager.SetBoolSelection(...)`/`WaitForEndSelect()`/`SelectedBoolValue` —
// UNRESOLVED (kept verbatim, logged): the mirror `GManager` has no `userSelectionManager` field at all, and
// no mirror `UserSelectionManager`/`SelectionElement<T>` type exists (this is a genuine gameplay CHOICE, not
// cosmetic UI, so it is kept AS-IS-shaped rather than silently dropped/simplified).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_111 : CEntity_Effect
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

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            bool CanSelectPermanentCondition1(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= 5000)
                    {
                        return true;
                    }
                }

                return false;
            }

            string EffectDiscription()
            {
                return "[Main] Suspend 1 of your opponent's Digimon or 2 of your opponent's Digimon with 5000 DP or less.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool canSuspend1 = CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
                bool canSuspend2 = CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1);

                if (canSuspend1 || canSuspend2)
                {
                    // UNRESOLVED (see file header): AS-IS `GManager.instance.userSelectionManager.
                    // SetBoolSelection/SetBool/WaitForEndSelect/SelectedBoolValue` — no mirror bridge exists
                    // for this genuine gameplay choice. Kept verbatim.
                    if (canSuspend1 && canSuspend2)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: "Suspend 1", value: true, spriteIndex: 0),
                            new SelectionElement<bool>(message: "Suspend 2", value: false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "Which effect do you choose?";
                        string notSelectPlayerMessage = "The opponent is choosing the effect.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSuspend1);
                    }

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool _isSuspendOne = GManager.instance.userSelectionManager.SelectedBoolValue;

                    int maxCount = _isSuspendOne
                        ? Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition))
                        : Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: _isSuspendOne ? CanSelectPermanentCondition : CanSelectPermanentCondition1,
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
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "Suspend Digimon");
        }

        return cardEffects;
    }
}
