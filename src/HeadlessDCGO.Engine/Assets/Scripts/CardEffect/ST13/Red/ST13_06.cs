// Source: Assets/Scripts/CardEffect/ST13/Red/ST13_06.cs — Imperialdramon Paladin Mode (ST13, Red)
// (RD-W3-7 witness) 1:1 port of the AS-IS ST13_06 — the SOLE AS-IS caller of BlitzProcess with a
// beforeOnAttackCoroutine. Three regions:
//   * [None] DNA Digivolution requirement (AddJogressConditionClass: a lvl-6 Red + a lvl-6 Black material).
//   * [When Digivolving] <Blitz> + (on DNA digivolve) per-4-digi-cards delete a cost<=20 opponent Digimon and
//     trash the top opponent security — the destroy runs as the Blitz attack's PRE-OnAttack hook (RD-W3-7).
//   * [All Turns] on losing security, unsuspend this Digimon.
//
// ADAPTATION (substrate translations only, established idioms):
//   * IEnumerator ActivateCoroutine → async Task; `yield return StartCoroutine(X)` → `await X`;
//     lone `yield return null` → Task.CompletedTask (BT8_092).
//   * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (PermanentView→Permanent).
//   * `permanent.Levels_ForJogress(card)` → `permanent.TopCard.JogressLevelsAgainst(card)` (AD1_011 idiom).
//   * `permanent.TopCard.CardColors.Contains(CardColor.Red)` → `permanent.TopCard.HasCardColor("Red")`
//     (mirror CardColors is string-typed).
//   * SelectPermanentEffect.canTargetCondition is id-typed — the Permanent predicate gets a PermanentOf(id)
//     adapter (BT17_026 / EX11_074 idiom; predicate NOT tunnelled).
//   * `new IDestroySecurity(player: card.Owner.Enemy, ...)` → `new IDestroySecurity(card.Context,
//     OpponentOf(card), count, activateClass, fromTop: true)` (AD1_025 idiom).
//   * [When Digivolving] mirror dialect key = EffectTiming.WhenDigivolving (AS-IS OnEnterFieldAnyone +
//     CanTriggerWhenDigivolving — EX11_074 ③ precedent).
//   * The AS-IS destroy+security block appears verbatim THREE times (pre-OnAttack hook, the post-BlitzProcess
//     "still attacking" arm, and the no-Blitz else arm); factored here into one local `DnaDeleteAndTrash()`
//     called at the SAME three AS-IS points (behaviour-identical — no logic change).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST13.Red;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST13_06 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region DNA Requirements
        if (timing == EffectTiming.None)
        {
            AddJogressConditionClass addJogressConditionClass = new AddJogressConditionClass();
            addJogressConditionClass.SetUpICardEffect($"DNA Digivolution", CanUseCondition, card);
            addJogressConditionClass.SetUpAddJogressConditionClass(getJogressCondition: GetJogress);
            addJogressConditionClass.SetNotShowUI(true);
            cardEffects.Add(addJogressConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            JogressCondition GetJogress(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    bool PermanentCondition1(Permanent permanent)
                    {
                        if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                        {
                            if (permanent.TopCard.HasCardColor("Red"))
                            {
                                if (permanent.TopCard.JogressLevelsAgainst(card).Contains(6))
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    bool PermanentCondition2(Permanent permanent)
                    {
                        if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                        {
                            if (permanent.TopCard.HasCardColor("Black"))
                            {
                                if (permanent.TopCard.JogressLevelsAgainst(card).Contains(6))
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    JogressConditionElement[] elements = new JogressConditionElement[]
                    {
                        new JogressConditionElement(PermanentCondition1, "a level 6 red Digimon"),

                        new JogressConditionElement(PermanentCondition2, "a level 6 black Digimon"),
                    };

                    JogressCondition jogressCondition = new JogressCondition(elements, 0);

                    return jogressCondition;
                }

                return null;
            }
        }
        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:85) + CanTriggerWhenDigivolving → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Blitz, delete Digimon and trash opponent's Security", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] <Blitz> (This Digimon can attack when your opponent has 1 or more memory.) When DNA digivolving, for every 4 cards in this Digimon's digivolution cards, delete 1 of your opponent's Digimon with a play cost of 20 or less and trash the top card of your opponent's security stack.";
            }

            int count()
            {
                int count = 0;

                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    count = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count / 4;
                }

                return count;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.GetCostItself <= 20)
                    {
                        if (permanent.TopCard.HasPlayCost)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            // (BT17_026 / EX11_074 관례) SelectPermanentEffect canTargetCondition는 id-형 — Permanent-술어에 id 어댑터.
            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                {
                    return true;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateBlitz(card, activateClass))
                    {
                        return true;
                    }

                    if (CardEffectCommons.IsJogress(hashtable))
                    {
                        if (count() >= 1)
                        {
                            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                            {
                                return true;
                            }

                            if (new Player(card.Context, CardEffectCommons.OpponentOf(card)).SecurityCards.Count >= 1)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            // AS-IS :164-272의 destroy+security 블록(3회 반복) — 동일 로직을 하나로 접어 동일 3지점에서 호출.
            async Task DnaDeleteAndTrash(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsJogress(_hashtable))
                {
                    if (count() >= 1)
                    {
                        int maxCount = Math.Min(count(), CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentById,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: null,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Destroy,
                            cardEffect: activateClass);

                        await selectPermanentEffect.Activate();

                        await new IDestroySecurity(
                            card.Context,
                            CardEffectCommons.OpponentOf(card),
                            count(),
                            activateClass,
                            fromTop: true).DestroySecurity();
                    }
                }
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.CanActivateBlitz(card, activateClass))
                {
                    // AS-IS :168-170 — BlitzProcess with the PRE-OnAttack hook (RD-W3-7).
                    await CardEffectCommons.BlitzProcess(card, activateClass, BeforeOnAttackCoroutine);

                    async Task BeforeOnAttackCoroutine()
                    {
                        await DnaDeleteAndTrash(_hashtable);
                    }

                    // AS-IS :204-236 — the "still attacking this permanent" arm.
                    if (GManager.instance.attackProcess.IsAttacking
                        && GManager.instance.attackProcess.AttackingPermanent == ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        await DnaDeleteAndTrash(_hashtable);
                    }
                }

                else
                {
                    // AS-IS :239-271 — the no-Blitz arm (still delete+trash on DNA digivolve).
                    await DnaDeleteAndTrash(_hashtable);
                }
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Unsuspend_ ST13_06");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once Per Turn] When a card is removed from a player's security stack, unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, player => true))
                    {
                        return true;
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
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }
        }
        #endregion

        return cardEffects;
    }
}
