// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT15_078 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT15/Purple/BT15_078.cs (3 regions)
//    * OnEnterFieldAnyone: [All Turns] 효과로 상대 디지몬 플레이 시, 상대 턴 종료까지 모든 상대 디지몬에
//      "[On Deletion] Lose 1 memory." 부여 (PRIMARY covered elements: AddDetail/AddSkill).
//    * OnAllyAttack     : [When Attacking] 상대가 트래시에서 level≤4 디지몬 1장 플레이 후 공격 대상 전환 가능.
//    * OnDestroyedAnyone: PierceSelfEffect(inherited).
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.Owner.Enemy.GetBattleAreaPermanents()` → `CardEffectCommons.OpponentOf(card).GetBattleAreaPermanents()`
//      (HeadlessPlayerId W4 확장). `card.Owner.Enemy.TrashCards` → `new Player(card.Context, OpponentOf(card)).TrashCards`.
//    * `card.Owner.UntilOpponentTurnEndEffects.Add(...)` → `new Player(card.Context, card.Owner)
//      .UntilOpponentTurnEndEffects.Add(...)` (BT9_103 idiom).
//    * `GManager.instance.GetComponent<Effects>().CreateDebuffEffect(permanent)` = UI 연출(스트립, BT17_026 판례).
//    * `cardSource.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(cardSource)`.
//    * `cardSource.Owner.AddMemory(-1, activateClass1)` → HeadlessPlayerId W4 확장.
//    * SelectCardEffect.selectPlayer: `card.Owner.Enemy` → `CardEffectCommons.OpponentOf(card)`.
//    * `GManager.instance.attackProcess.SwitchDefender(activateClass, false, perm)` → id-시그니처.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Purple;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT15_078 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region All Turns - Effect plays opponents digimon

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("All Opponents digimon, gain Memory -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("LoseMemory_BT15_078");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] [Once Per Turn] When an effect plays an opponent's Digimon, until the end of their turn, all of your opponent's Digimon gain \"[On Deletion] Lose 1 memory.\"";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
                    {
                        if (CardEffectCommons.IsByEffect(hashtable, null))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                foreach (Permanent permanent in CardEffectCommons.OpponentOf(card).GetBattleAreaPermanents())
                {
                    if (PermanentCondition(permanent))
                    {
                        // AS-IS Effects.CreateDebuffEffect(permanent) = UI 연출(스트립, BT17_026 판례).
                    }
                }

                AddSkillClass addSkillClass = new AddSkillClass();
                addSkillClass.SetUpICardEffect("Memory -1", CanUseCondition1, card);
                addSkillClass.SetUpAddSkillClass(cardSourceCondition: CardSourceCondition, getEffects: GetEffects);

                new Player(card.Context, card.Owner).UntilOpponentTurnEndEffects.Add((_timing) => addSkillClass);
                new Player(card.Context, card.Owner).UntilOpponentTurnEndEffects.Add(GetDetailEffect);

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return true;
                }

                bool CardSourceCondition(CardSource cardSource)
                {
                    if (PermanentCondition(ICardEffect.ResolvePermanentOfThisCard(cardSource)))
                    {
                        if (cardSource == ICardEffect.ResolvePermanentOfThisCard(cardSource).TopCard)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                ICardEffect? GetDetailEffect(EffectTiming timing)
                {
                    if (timing == EffectTiming.None)
                    {
                        return CardEffectFactory.AddDetailClass(CanUseCondition1, PermanentCondition, "[On Deletion] Lose 1 memory.", true, card);
                    }
                    return null;
                }

                List<ICardEffect> GetEffects(CardSource cardSource, List<ICardEffect> cardEffects, EffectTiming _timing)
                {
                    if (_timing == EffectTiming.OnDestroyedAnyone)
                    {
                        ActivateClass activateClass1 = new ActivateClass();
                        activateClass1.SetUpICardEffect("Memory -1", CanUseCondition2, cardSource);
                        activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                        cardEffects.Add(activateClass1);

                        if (ICardEffect.ResolvePermanentOfThisCard(cardSource) != null)
                        {
                            activateClass1.SetEffectSourcePermanent(ICardEffect.ResolvePermanentOfThisCard(cardSource));
                        }

                        string EffectDiscription1()
                        {
                            return "[On Deletion] Lose 1 memory.";
                        }

                        bool CanUseCondition2(Hashtable hashtable)
                        {
                            if (CardSourceCondition(cardSource))
                            {
                                if (CardEffectCommons.CanTriggerOnDeletion(hashtable, cardSource))
                                {
                                    return true;
                                }
                            }

                            return false;
                        }

                        bool CanActivateCondition1(Hashtable hashtable)
                        {
                            if (CardEffectCommons.CanActivateOnDeletion(hashtable, cardSource))
                            {
                                return true;
                            }

                            return false;
                        }

                        async Task ActivateCoroutine1(Hashtable _hashtable)
                        {
                            await cardSource.Owner.AddMemory(-1, activateClass1);
                        }
                    }

                    return cardEffects;
                }
            }
        }

        #endregion

        #region When Attacking - opponent plays a digimon

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Opponent plays 1  Digimon from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Your opponent plays 1 level 4 or lower Digimon card from their trash suspended without paying the cost. [On Play] effects on Digimon played by this effect don't activate. Then, you may switch the target of attack to that Digimon.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level <= 4)
                    {
                        if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                        {
                            if (cardSource.HasLevel)
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
                    if (CardEffectCommons.HasMatchConditionOpponentsCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsCardInTrash(card, CanSelectCardCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = Math.Min(1, new Player(card.Context, CardEffectCommons.OpponentOf(card)).TrashCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => false,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select 1 card to play.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Trash,
                                customRootCardList: null,
                                canLookReverseCard: true,
                                selectPlayer: CardEffectCommons.OpponentOf(card),
                                cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    if (selectedCards.Count > 0)
                    {
                        await CardEffectCommons.PlayPermanentCards(
                                                cardSources: selectedCards,
                                                activateClass: activateClass,
                                                payCost: false,
                                                isTapped: true,
                                                root: SelectCardEffect.Root.Trash,
                                                activateETB: false);

                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: $"Redirect Attack", value : true, spriteIndex: 0),
                            new SelectionElement<bool>(message: $"Continue Attack", value : false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "Would you like to redirect the attack?";
                        string notSelectPlayerMessage = "The opponent is selecting whether to redirect the attack.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                        await GManager.instance.userSelectionManager.WaitForEndSelect();

                        if (GManager.instance.userSelectionManager.SelectedBoolValue)
                        {
                            await GManager.instance.attackProcess.SwitchDefender(
                                activateClass.EffectSourceCard?.InstanceId,
                                false,
                                ICardEffect.ResolvePermanentOfThisCard(selectedCards[0]).InstanceId);
                        }
                    }
                }
            }
        }

        #endregion

        //Inherited Effect
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }

        return cardEffects;
    }
}
