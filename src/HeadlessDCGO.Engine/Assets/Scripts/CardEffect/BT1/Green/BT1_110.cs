// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_110.cs — an Option.
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_110 (BT1/Green).
//   [Main]     Suspend 1 of your opponent's Digimon.
//   [Security] Suspend all of your opponent's Digimon without <Blocker>.
// AS-IS structure kept verbatim: [Main] is inline ActivateClass + SelectPermanentEffect(Mode.Tap); [Security]
// has NO SelectPermanentEffect at all — it computes `card.Owner.Enemy.GetBattleAreaDigimons().Filter(...)`
// (AS-IS `Player.Enemy`, a live object) -> `new Player(card.Context, card.Owner).Enemy` (mirror `CardSource.
// Owner` is a bare `HeadlessPlayerId`), then runs `new SuspendPermanentsClass(list, cause, isBlock:false).Tap()`
// directly (bridged 1:1, HeadlessEntityId? cause instead of Hashtable). `!permanent.HasBlocker` -> the
// (R1-c) the rehoused `permanent.HasBlocker` getter (permanent is a Permanent).
// `!permanent.TopCard.CanNotBeAffected(activateClass)` -> `CanNotBeAffected(HeadlessEntityId?)` fed the
// causing effect's source card id (the established `ICardEffect -> EffectSourceCard.InstanceId` idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_110 : CEntity_Effect
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
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend opponent's all Digimon without Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Suspend all of your opponent's Digimon without <Blocker>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.HasBlocker)
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> suspendTargetPermanents = new Player(card.Context, card.Owner).Enemy?.GetBattleAreaDigimons().Filter(PermanentCondition)
                    ?? new List<Permanent>();

                await new SuspendPermanentsClass(
                    suspendTargetPermanents,
                    activateClass,
                    isBlock: false).Tap();
            }
        }

        return cardEffects;
    }
}
