// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT21/Purple/BT21_077.cs (Regulusmon, Digimon / Purple, 6 regions)
//    * Digivolution condition          :15-27  (None — AddSelfDigivolutionRequirementStaticEffect, Lv4 [Gammamon] text, cost 3)
//    * On Play/When Digivolving shared  :29-46  (공유 술어 CanTargetPermanent/CanActivateConditionShared/CanTrashCard)
//    * On Play                          :48-151 (CanTriggerOnPlay — trash 1 [Gammamon]-text card → 상대 Digimon 1체에
//        Collision + [Start of Your Main Phase] 강제공격(StartOfMainAttack) UntilOwnerTurnEnd)
//    * When Digivolving                 :153-256(CanTriggerWhenDigivolving — 동일)
//    * On deletion regular              :258-335(OnDestroyedAnyone — Canoweissmon or Lv4- [Gammamon]-text play from trash)
//    * On deletion inherit              :337-415(OnDestroyedAnyone isInheritedEffect — Lv4- [Gammamon]-text play from trash)
//
// ② 프리미티브 매핑: P:AddSelfDigivolutionRequirementStaticEffect, K:Collision (GainCollision),
//    **P:StartOfMainAttack (RD-3A-01 resolved — home file 1:1 port; BT21_077 = first live caller;
//    OnStartMainPhase auto-fire 창은 TurnStateMachine.MainPhaseAsync:428 → StackSkillInfos(OnStartMainPhase) →
//    GetSkillInfos → Permanent.EffectList_Added(UntilOwnerTurnEndEffects) 로 이미 완비)**,
//    P:PlayPermanentCards (trash root).
//
// ③ 배선 관례: [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay. [When Digivolving] → WhenDigivolving 전용 키 +
//    CanTriggerWhenDigivolving (AD1_011 규약). [On Deletion] → OnDestroyedAnyone + CanTriggerOnDeletion (BT2_034 관례).
//
// 치환(substrate translations only):
//    * `card.Owner.HandCards` → `new Player(card.Context, card.Owner).HandCards`.
//    * `HasMatchConditionPermanent(cond)` → `HasMatchConditionPermanent(card, cond)` (미러 card-스레딩).
//    * SelectPermanentEffect.SetUp canTargetCondition = id-형 어댑터 CanTargetPermanentById (BT9_109 확립).
//    * IEnumerator→async Task; `yield return (Continuous/Start)Coroutine(X)`→`await X`; `yield return null`→`await Task.CompletedTask`.
//    * `PlayPermanentCards(activateClass:..., root: SelectCardEffect.Root.Trash, ...)` — AS-IS 시그니처 브릿지
//      오버로드 (PlayCardsBridge.cs:37; BT9_081 확립).
//    * `CanTrashCard(CardSource card)` 파라미터 → `cardSource` (enclosing `card` 파라미터 CS0136 쉐도잉 회피).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Purple;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT21_077 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution condition

        if (timing == EffectTiming.None)
        {
            bool Condition(Permanent permanent)
            {
                return permanent.Level == 4 && permanent.TopCard.HasText("Gammamon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(Condition, 3, false, card, null));
        }

        #endregion

        #region On Play/When Digivolving shared

        bool CanTargetPermanent(Permanent permanent)
        {
            return permanent.IsDigimon && CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
        }

        bool CanTargetPermanentById(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
            && CanTargetPermanent(new Permanent(card.Context, id, rec.OwnerId));

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        bool CanTrashCard(CardSource cardSource)
        {
            return cardSource.HasText("Gammamon");
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Force attack and give collision", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] By trashing 1 card with [Gammamon] in its text from your hand, give 1 of your opponent's Digimon <Collision> and \"[Start of Your Main Phase] This Digimon attacks.\" until their turn ends.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count(CanTrashCard) >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanTrashCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count >= 1)
                        {
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanTargetPermanent))
                        {
                            Permanent? selectedPermanent = null;

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanTargetPermanentById,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.",
                                "The opponent is selecting 1 Digimon that will get effects.");
                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                selectedPermanent = permanent;
                                await Task.CompletedTask;
                            }

                            if (selectedPermanent != null)
                            {
                                await CardEffectCommons.GainCollision(
                                    targetPermanent: selectedPermanent,
                                    effectDuration: EffectDuration.UntilOwnerTurnEnd,
                                    activateClass: activateClass);

                                await CardEffectCommons.StartOfMainAttack(
                                    targetPermanent: selectedPermanent,
                                    cardEffect: activateClass);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Force attack and give collision", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] By trashing 1 card with [Gammamon] in its text from your hand, give 1 of your opponent's Digimon <Collision> and \"[Start of Your Main Phase] This Digimon attacks.\" until their turn ends.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count(CanTrashCard) >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanTrashCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count >= 1)
                        {
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanTargetPermanent))
                        {
                            Permanent? selectedPermanent = null;

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanTargetPermanentById,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.",
                                "The opponent is selecting 1 Digimon that will get effects.");
                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                selectedPermanent = permanent;
                                await Task.CompletedTask;
                            }

                            if (selectedPermanent != null)
                            {
                                await CardEffectCommons.GainCollision(
                                    targetPermanent: selectedPermanent,
                                    effectDuration: EffectDuration.UntilOwnerTurnEnd,
                                    activateClass: activateClass);

                                await CardEffectCommons.StartOfMainAttack(
                                    targetPermanent: selectedPermanent,
                                    cardEffect: activateClass);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region On deletion regular

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Cannonweissmon or level 4 or lower gammamon in text digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Deletion] You may play 1 [Canoweissmon] or 1 level 4 or lower Digimon card with [Gammamon] in its text from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanPlay(CardSource cardSource)
            {
                return ((cardSource.HasText("Gammamon") && cardSource.HasLevel && cardSource.Level <= 4 && cardSource.IsDigimon) || cardSource.EqualsCardName("Canoweissmon")) &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanPlay);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanPlay,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 [Canoweissmon] or level 4 or lower [Gammamon] in text Digimon to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select 1 [Cannonweissmon] or level 4 or lower [Gammamon] in text Digimon to play.",
                    "The opponent is selecting 1 [Cannonweissmon] or level 4 or lower [Gammamon] in text Digimon to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    await Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(
                    cardSources: selectedCards,
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Trash,
                    activateETB: true);
            }
        }

        #endregion

        #region On deletion inherit

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Cannonweissmon or level 4 or lower gammamon in text digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Deletion] You may play 1 level 4 or lower Digimon card with [Gammamon] in its text from your trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanPlay(CardSource cardSource)
            {
                return cardSource.HasText("Gammamon") && cardSource.HasLevel && cardSource.Level <= 4 && cardSource.IsDigimon &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanPlay);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanPlay,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 level 4 or lower [Gammamon] in text Digimon to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select 1 level 4 or lower [Gammamon] in text Digimon to play.",
                    "The opponent is selecting 1 level 4 or lower [Gammamon] in text Digimon to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    await Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(
                    cardSources: selectedCards,
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Trash,
                    activateETB: true);
            }
        }

        #endregion

        return cardEffects;
    }
}
