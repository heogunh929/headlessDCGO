// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_104.cs (an Option, single branch)
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_104 (BT1/Yellow).
//   [Main] All of your Digimon gain the following effect for the turn: "[When Attacking] 1 of your
//   opponent's Digimon gets -2000 DP for the turn." (OptionSkill only — no [Security] branch in AS-IS.)
// AS-IS structure kept verbatim: the CreateBuffEffect VFX loop is stripped (pure UI, no state); the
// AddSkillClass ("DP -2000") splice is registered directly via the bridged `AddSkillClass.SetUpAddSkillClass`
// + `CardEffectCommons.AddEffectToPlayer(effectDuration, card, cardEffect, timing)` (the exact AS-IS
// `GiveEffectToPermanentOrPlayer.cs:57` bridge, per its doc comment) — this is the literal AS-IS call, not a
// declarative old-model factory, so the previous pass's "no composed primitive" STOP does not apply to this
// inline-verbatim structure.
// Substrate translations: `Permanent` (AS-IS type) vs the mirror `PermanentView` returned by
// `CardSource.PermanentOfThisCard()` — `PermanentCondition(Permanent)` is fed `ICardEffect.
// ResolvePermanentOfThisCard(cardSource)` (the real mirror `Permanent`); `cardSource == ....TopCard` (AS-IS
// CardSource value-equality) -> `cardSource.InstanceId == ....PermanentOfThisCard().TopInstanceId` (same
// identity, id form — PermanentView exposes only `.TopInstanceId`, not `.TopCard`). `GManager.instance.
// attackProcess.AttackingPermanent == cardSource.PermanentOfThisCard()` -> id-equality on the same two
// members. `cardSource.Owner.Enemy.GetBattleAreaDigimons()` (AS-IS `Player.Enemy`, a live object) ->
// `new Player(cardSource.Context, cardSource.Owner).Enemy` (mirror `CardSource.Owner` is a bare
// `HeadlessPlayerId`, so the Player handle is reconstructed, same idiom as this batch's BT1_110/BT1_111
// `card.Owner.Enemy` translations). `activateClass1.SetEffectSourcePermanent(cardSource.PermanentOfThisCard())`
// -> the mirror setter takes the real `Permanent`, so `ResolvePermanentOfThisCard` feeds it too; the AS-IS
// null-check becomes an empty-id check (PermanentView is never a null reference on the mirror).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_104 : CEntity_Effect
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
                return "[Main] All of your Digimon gain the following effect for the turn: \"[When Attacking] 1 of your opponent's Digimon gets -2000 DP for the turn.\"";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
                }

                foreach (Permanent permanent in new Player(card.Context, card.Owner).GetBattleAreaPermanents())
                {
                    if (PermanentCondition(permanent))
                    {
                        // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(permanent)` — pure
                        // VFX/SE (Effects.cs:1433), stripped (see file header).
                    }
                }

                AddSkillClass addSkillClass = new AddSkillClass();
                addSkillClass.SetUpICardEffect("DP -2000", CanUseCondition1, card);
                addSkillClass.SetUpAddSkillClass(cardSourceCondition: CardSourceCondition, getEffects: GetEffects);
                CardEffectCommons.AddEffectToPlayer(effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: addSkillClass, timing: EffectTiming.None);

                await Task.CompletedTask;

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return true;
                }

                bool CardSourceCondition(CardSource cardSource)
                {
                    Permanent permanentOfCardSource = ICardEffect.ResolvePermanentOfThisCard(cardSource);

                    if (PermanentCondition(permanentOfCardSource))
                    {
                        HeadlessEntityId topOfCardSource = cardSource.PermanentOfThisCard().TopInstanceId;

                        if (!topOfCardSource.IsEmpty && cardSource.InstanceId == topOfCardSource)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                List<ICardEffect> GetEffects(CardSource cardSource, List<ICardEffect> cardEffects, EffectTiming _timing)
                {
                    if (_timing == EffectTiming.OnAllyAttack)
                    {
                        ActivateClass activateClass1 = new ActivateClass();
                        activateClass1.SetUpICardEffect("DP -2000", CanUseCondition2, cardSource);
                        activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                        cardEffects.Add(activateClass1);

                        Permanent permanentOfCardSource1 = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                        if (!permanentOfCardSource1.InstanceId.IsEmpty)
                        {
                            activateClass1.SetEffectSourcePermanent(permanentOfCardSource1);
                        }

                        bool CanSelectPermanentCondition(HeadlessEntityId id)
                        {
                            return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
                        }

                        string EffectDiscription1()
                        {
                            return "[When Attacking] 1 of your opponent's Digimon gets -2000 DP for the turn.";
                        }

                        bool CanUseCondition2(Hashtable hashtable)
                        {
                            if (CardSourceCondition(cardSource))
                            {
                                if (GManager.instance.attackProcess.AttackingPermanent is { } attackingPermanent
                                    && attackingPermanent.InstanceId == cardSource.PermanentOfThisCard().TopInstanceId)
                                {
                                    return true;
                                }
                            }

                            return false;
                        }

                        bool CanActivateCondition1(Hashtable hashtable)
                        {
                            if (CardEffectCommons.IsExistOnBattleArea(cardSource))
                            {
                                if (new Player(cardSource.Context, cardSource.Owner).Enemy?.GetBattleAreaDigimons()
                                    .Some(p => CanSelectPermanentCondition(p.InstanceId)) ?? false)
                                {
                                    return true;
                                }
                            }

                            return false;
                        }

                        async Task ActivateCoroutine1(Hashtable _hashtable)
                        {
                            if (CardSourceCondition(cardSource))
                            {
                                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                selectPermanentEffect.SetUp(
                                    selectPlayer: cardSource.Owner,
                                    canTargetCondition: CanSelectPermanentCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: false,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: SelectPermanentCoroutine,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Custom,
                                    cardEffect: activateClass1);

                                // UNRESOLVED (logged): AS-IS `CardEffectCommons.customPermanentMessageArray_ChangeDP`.
                                selectPermanentEffect.SetUpCustomMessage(customMessageArray: CardEffectCommons.customPermanentMessageArray_ChangeDP(changeValue: -2000, maxCount: maxCount));

                                await selectPermanentEffect.Activate();

                                async Task SelectPermanentCoroutine(Permanent permanent)
                                {
                                    await CardEffectCommons.ChangeDigimonDP(
                                        targetPermanent: permanent,
                                        changeValue: -2000,
                                        effectDuration: EffectDuration.UntilEachTurnEnd,
                                        activateClass: activateClass1);
                                }
                            }
                        }
                    }

                    return cardEffects;
                }
            }
        }

        return cardEffects;
    }
}
