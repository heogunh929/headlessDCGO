// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — AD1_012 "CresGarurumon" (Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/AD1/Blue/AD1_012.cs (8 regions)
//    * None            : AddSelfDigivolutionRequirementStaticEffect(level 5, [Garurumon]/[ADVENTURE] 위 cost 3).
//    * OnAllyAttack    : AllianceSelfEffect / WhenPermanentWouldBeDeleted: EvadeSelfEffect.
//    * Shared OP/WD/WA : 최저레벨 상대 1체 손패로 반환 후 이 디지몬+[Greymon] 1체 언서스펜드 (3개 등재).
//    * Opponent's Turn : 상대 공격 시 DNA 진화 후 공격 대상 전환.
//    * Assembly        : AddAssemblyConditionClass (PRIMARY covered element: AddAssemblyCondition/Assembly).
//    * Your Turn - ESS : CanNotSwitchAttackTargetClass (PRIMARY covered element: CanNotSwitchAttackTarget).
// ③ 배선(trigger-wiring): [When Digivolving]은 미러 방언 EffectTiming.WhenDigivolving 전용 키
//    (AS-IS OnEnterFieldAnyone+CanTriggerWhenDigivolving; DigivolveAction이 WhenDigivolving만 해소, 이중-키 금지 —
//     AD1_006/BT17_026 idiom). [On Play]=OnEnterFieldAnyone+CanTriggerOnPlay, [When Attacking]/[Opponent's Turn]
//     =OnAllyAttack (각각 CanTriggerOnAttack / CanTriggerOnPermanentAttack) 그대로.
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * SelectPermanentEffect.canTargetCondition은 정본 Func<Permanent,bool> 오버로드 — id 어댑터 없이 Permanent-술어 직결.
//    * `HasMatchConditionOpponentsPermanent(card, cond)`도 Func<Permanent,bool> — 동일 술어 그대로 재사용.
//    * `card.Owner.Enemy`(IsMinLevel 인자) → `CardEffectCommons.OpponentOf(card)` (HeadlessPlayerId).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()` → 미러 1:1(ICardEffect cardEffect).
//    * `GManager.instance.attackProcess.SwitchDefender(activateClass, false, perm)` → `(activateClass
//      .EffectSourceCard?.InstanceId, false, perm.InstanceId)` (미러 시그니처).
//    * UserSelectionManager.SetBoolSelection/WaitForEndSelect/SelectedBoolValue — 미러 1:1(bridge W5).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class AD1_012 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.ContainsCardName("Garurumon")
                    || targetPermanent.TopCard.EqualsTraits("ADVENTURE");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                level: 5,
                permanentCondition: PermanentCondition,
                digivolutionCost: 3,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null)
            );
        }
        #endregion

        #region Alliance and Evade
        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AllianceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.EvadeSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Shared OP / WD / WA

        string SharedHashString = "AD1_012_OP_WD_WA";

        string SharedEffectName = "You may return 1 lowest level opponent to hand. This and 1 [Greymon] may unsuspend";

        string SharedEffectDescription(string tag)
            => $"[{tag}] [Once Per Turn] You may return 1 of your opponent's lowest level Digimon to the hand. Then, this Digimon and 1 of your Digimon with [Greymon] in its name may unsuspend.";

        bool SharedCanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

        bool IsOpponentsLowestLevel(Permanent permanent) => CardEffectCommons.IsMinLevel(permanent, CardEffectCommons.OpponentOf(card));

        bool IsOwnGreymon(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.TopCard.ContainsCardName("Greymon")
                && permanent.IsSuspended;
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, permanent => IsOpponentsLowestLevel(permanent)))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: IsOpponentsLowestLevel,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Bounce,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 opponent's Digimon to return to hand", "The opponent is selecting 1 Digimon to return to hand");
                await selectPermanentEffect.Activate();
            }

            string selectPlayerMessage = "Will you unsuspend your digimon?";
            string notSelectPlayerMessage = "The opponent is choosing if they will unsuspend.";

            List<SelectionElement<bool>> command_SelectCommands = new List<SelectionElement<bool>>()
            {
                new SelectionElement<bool>(message: $"Yes", value: true, spriteIndex: 0),
                new SelectionElement<bool>(message: $"No", value: false, spriteIndex: 1),
            };

            GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: command_SelectCommands, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

            await GManager.instance.userSelectionManager.WaitForEndSelect();

            bool unsuspend = GManager.instance.userSelectionManager.SelectedBoolValue;

            if (unsuspend)
            {
                List<Permanent> selectedPermanents = new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) };

                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, IsOwnGreymon))
                {
                    SelectPermanentEffect selectPermanentEffect =
                            GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOwnGreymon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Greymon to unsuspend with this card.", "The opponent is selecting 1 Digimon to unsuspend.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanents.Add(permanent);
                        return Task.CompletedTask;
                    }
                }

                await new IUnsuspendPermanents(selectedPermanents, activateClass).Unsuspend();
            }
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("On Play"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("When Digivolving"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("When Attacking"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }
        }
        #endregion

        #region Opponent's Turn
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may DNA digivolve. Then you may change the attack target to 1 Digimon.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("Redirect_AD1_012");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Opponent's Turn] [Once Per Turn] When one of your opponent's Digimon attacks, 2 of your Digimon may DNA digivolve into [Omnimon Alter-S] in the hand. Then, you may change the attack target to 1 of your Digimon.";

            bool IsOpponentDigimon(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOpponentTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, IsOpponentDigimon);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            bool CanSelectDNACardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.CanPlayJogress(true)
                    && cardSource.EqualsCardName("Omnimon Alter-S");
            }

            bool IsOwnDigimon(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                #region DNA digivolve
                await CardEffectCommons.DNADigivolvePermanentsIntoHandOrTrashCard(
                        CanSelectDNACardCondition,
                        payCost: true,
                        isHand: true,
                        activateClass
                    );
                #endregion
                #region redirect attack
                Permanent? selectedPermanent = null;

                SelectPermanentEffect selectPermanentEffect =
                            GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: IsOwnDigimon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to change the attack target to.", "The opponent is selecting 1 Digimon to change the attack target to.");

                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;
                    return Task.CompletedTask;
                }

                if (selectedPermanent != null)
                {
                    await GManager.instance.attackProcess.SwitchDefender(
                            activateClass.EffectSourceCard?.InstanceId,
                            false,
                            selectedPermanent.InstanceId);
                }
                #endregion
            }
        }
        #endregion

        #region Assembly
        if (timing == EffectTiming.None)
        {
            string cardName1 = "WereGarurumon: Sagittarius Mode";
            string cardName2 = "Garurumon";
            AddAssemblyConditionClass addAssemblyConditionClass = new AddAssemblyConditionClass();
            addAssemblyConditionClass.SetUpICardEffect($"Assembly", CanUseCondition, card);
            addAssemblyConditionClass.SetUpAddAssemblyConditionClass(getAssemblyCondition: GetAssembly);
            addAssemblyConditionClass.SetNotShowUI(true);
            cardEffects.Add(addAssemblyConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            AssemblyCondition? GetAssembly(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    AssemblyConditionElement element1 = new AssemblyConditionElement(CanSelectCardCondition1, selectMessage: $"[{cardName1}]", elementCount: 1);
                    AssemblyConditionElement element2 = new AssemblyConditionElement(CanSelectCardCondition2, selectMessage: $"[{cardName2}]", elementCount: 1);

                    bool CanSelectCardCondition1(CardSource cardSource)
                    {
                        return cardSource != null &&
                            cardSource.Owner == card.Owner &&
                            cardSource.IsDigimon &&
                            cardSource.EqualsCardName(cardName1);
                    }

                    bool CanSelectCardCondition2(CardSource cardSource)
                    {
                        return cardSource != null &&
                            cardSource.Owner == card.Owner &&
                            cardSource.IsDigimon &&
                            cardSource.EqualsCardName(cardName2);
                    }

                    AssemblyCondition assemblyCondition = new AssemblyCondition(
                        elements: new List<AssemblyConditionElement>() { element1, element2 },
                        reduceCost: 4);

                    return assemblyCondition;
                }

                return null;
            }
        }
        #endregion

        #region Your Turn - ESS
        if (timing == EffectTiming.None)
        {
            CanNotSwitchAttackTargetClass canNotSwitchAttackTargetClass = new CanNotSwitchAttackTargetClass();
            canNotSwitchAttackTargetClass.SetUpICardEffect("This Digimon's attack target can't be changed.", CanUseCondition, card);
            canNotSwitchAttackTargetClass.SetUpCanNotSwitchAttackTargetClass(PermanentCondition: PermanentCondition);
            canNotSwitchAttackTargetClass.SetIsInheritedEffect(true);
            cardEffects.Add(canNotSwitchAttackTargetClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.IsOwnerTurn(card);
            }

            bool PermanentCondition(Permanent permanent)
            {
                // (미러 idiom, BT24_062) AS-IS `permanent.TopCard`(암시적 bool) → `permanent.TopCard != null`
                // (미러 CardSource엔 implicit operator bool 없음).
                return permanent != null && permanent.TopCard != null && permanent == ICardEffect.ResolvePermanentOfThisCard(card);
            }
        }
        #endregion

        return cardEffects;
    }
}
