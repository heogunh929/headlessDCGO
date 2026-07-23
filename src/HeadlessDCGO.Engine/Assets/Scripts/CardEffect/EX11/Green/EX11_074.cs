// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Vortexdramon (EX11_074, Digimon / Green / Lv.7)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX11/Green/EX11_074.cs (272 lines, 8 regions)
//    * [None] alt-digivolve  :17-44  (AddSelfDigivolutionRequirementStaticEffect: [GrandGalemon] 위 코스트6,
//                                     조건=Shoto Kazama 보유)
//    * [None] Pierce         :46-51  (OnDetermineDoSecurityCheck — PierceSelfEffect)
//    * [None] Vortex         :53-58  (OnEndTurn — VortexSelfEffect)
//    * [None] Blocker        :60-66  (BlockerSelfStaticEffect)
//    * shared WD/WA          :69-150 (SelectPermanent suspend; own이면 DigimonEffectImmunity + 6000DP)
//    * [When Digivolving]     :152-166(OnEnterFieldAnyone + CanTriggerWhenDigivolving — 공유)
//    * [When Attacking]      :168-182(OnAllyAttack + CanTriggerOnAttack — 공유)
//    * [All Turns] OPT       :184-267(OnTappedAnyone — 자기 unsuspend 후 상대 1체 배틀)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #28):
//    * K:Vortex              — [None] VortexSelfEffect (AS-IS :56; CardEffectFactory KeyWord)
//    * P:DigimonEffectImmunity — shared WD/WA 몸통 (AS-IS :130; PermanentEffectFactory, 상대 디지몬효과 면역)
//    * (+K:Pierce·Blocker(factory), P:AddDigivolutionRequirementClass(조건부), SuspendPermanentsClass,
//       P:ChangeDigimonDP, IUnsuspendPermanents, IBattle, userSelectionManager bool,
//       T:OnAllyAttack/OnTappedAnyone/OnDetermineDoSecurityCheck, CanTriggerWhenPermanentSuspends)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언 rule 3).
//    * [When Attacking] self-스코프 → OnAllyAttack + CanTriggerOnAttack(AS-IS :176-180, triggerGate).
//    * [Pierce] → OnDetermineDoSecurityCheck(키워드 상시 훅); [Vortex] → OnEndTurn.
//    * [All Turns] OPT → OnTappedAnyone + CanTriggerWhenPermanentSuspends(IsPermanentExistsOnBattleAreaDigimon).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask (BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (PermanentView→Permanent; BT19_091 idiom).
//    * AS-IS :117-118 `new SuspendPermanentsClass(list, CardEffectHashtable(activateClass)).Tap()` → 미러 ctor
//      `(list, cardEffect, isBlock:false)` (Hashtable→직접 파라미터; BT8_092 idiom).
//    * AS-IS :132 `GManager.instance.GetComponent<Effects>().CreateBuffEffect(...)` = UI 연출 — 스트립.
//    * AS-IS :138-144 `ChangeDigimonDP(thisPermanent, 6000, UntilOpponentTurnEnd, activateClass)` — 미러
//      AS-IS-시그니처 async bridge(GiveEffectToPermanent/ChangeDP.cs) 동일 호출.
//    * SelectPermanentEffect.canTargetCondition는 정본 Func<Permanent,bool> — IsPermanentExistsOnBattleAreaDigimon
//      등 Permanent-술어를 직접 전달(id 어댑터 없음).
//    * userSelectionManager Set/Wait/SelectedBoolValue — 미러 UserSelectionManager 1:1 표면(P_223 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX11.Green;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX11_074 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static Effects

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.HasMatchConditionOwnersPermanent(card, HasShotoKazama);
            }

            bool HasShotoKazama(Permanent targetPermanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(targetPermanent, card) &&
                       targetPermanent.TopCard.EqualsCardName("Shoto Kazama");
            }

            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("GrandGalemon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 6,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: Condition));
        }
        #endregion

        #region Piercing
        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Vortex
        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.VortexSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #endregion

        #region Shared WD / WA

        string SharedEffectName = "Suspend 1 Digimon. If yours, this becomes immune to opponent's Digimon effects and gains +6K DP until end of their turn.";

        string SharedEffectDescription(string tag) => $"[{tag}] You may suspend 1 Digimon. If this effect suspended your Digimon, until your opponent's turn ends, their Digimon's effects don't affect this Digimon and gets +6000 DP.";

        bool SharedCanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            Permanent selectedPermanent = null;
            bool ownDigimon = false;

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: 1,
                canNoSelect: true,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to suspend.", "The opponent is selecting 1 Digimon to suspend.");

            await selectPermanentEffect.Activate();

            Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;

                return Task.CompletedTask;
            }

            if (selectedPermanent != null &&
                selectedPermanent.TopCard != null &&
                !selectedPermanent.TopCard.CanNotBeAffected(activateClass) &&
                !selectedPermanent.IsSuspended && selectedPermanent.CanSuspend)
            {
                await new SuspendPermanentsClass(new List<Permanent>() { selectedPermanent },
                    activateClass, isBlock: false).Tap();

                ownDigimon = selectedPermanent.IsSuspended &&
                                CardEffectCommons.IsOwnerPermanent(selectedPermanent, card);
            }

            if (ownDigimon)
            {
                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                #region Give Digimon Effect Immunity

                thisPermanent.UntilOpponentTurnEndEffects.Add((_timing) => PermanentEffectFactory.DigimonEffectImmunity(thisPermanent));

                // AS-IS :132 `CreateBuffEffect(thisPermanent)` = UI 연출 — 스트립.

                #endregion

                #region Gain +6000 DP

                await CardEffectCommons.ChangeDigimonDP(
                    thisPermanent,
                    6000,
                    EffectDuration.UntilOpponentTurnEnd,
                    activateClass);

                #endregion
            }
        }

        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:153) + CanTriggerWhenDigivolving → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, false, SharedEffectDescription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, false, SharedEffectDescription("When Attacking"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }
        #endregion

        #region All turns
        if (timing == EffectTiming.OnTappedAnyone)
        {
            ActivateClass activateClass = new();
            activateClass.SetUpICardEffect("This Digimon may unsuspend. Then may battle 1 opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("EX11_074_AT_Battle");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[All Turns] [Once Per Turn] When any Digimon suspend, this Digimon may unsuspend. Then, this Digimon may battle 1 of your opponent's Digimon.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenPermanentSuspends(hashtable, CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            bool CanSelectPermanentCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (ICardEffect.ResolvePermanentOfThisCard(card).IsSuspended && ICardEffect.ResolvePermanentOfThisCard(card).CanUnsuspend)
                {
                    List<SelectionElement<bool>> selectionElements1 = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"Yes", value: true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"No", value: false, spriteIndex: 1),
                    };

                    string selectPlayerMessage1 = "Will you unsuspend this Digimon?";
                    string notSelectPlayerMessage1 = "The opponent is choosing if they will unsuspend.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements1, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage1, notSelectPlayerMessage: notSelectPlayerMessage1);

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    var selectedOption = GManager.instance.userSelectionManager.SelectedBoolValue;

                    if (selectedOption)
                    {
                        await new IUnsuspendPermanents(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
                    }
                }

                Permanent selectedPermanent = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to battle.", "The opponent is selecting 1 Digimon to battle.");

                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;

                    return Task.CompletedTask;
                }

                if (selectedPermanent != null)
                {
                    // (RD-EXT2B-01 RESOLVED) AS-IS :263 `new IBattle(this, target, null, true).Battle()` — the
                    // effect-driven, participant-explicit forced battle (IsWithoutAttack=true; NO pending attack,
                    // NO attack-declaration / security machinery). The mirror IBattle now carries the 1:1
                    // `.Battle()` execution method (CardController.cs IBattle.Battle).
                    await new IBattle(ICardEffect.ResolvePermanentOfThisCard(card), selectedPermanent, null, true).Battle();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
