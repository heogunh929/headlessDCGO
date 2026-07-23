// Source: Assets/Scripts/CardEffect/EX6/Red/EX6_011.cs — Durandamon (EX6, Red).
// 1:1 port of the AS-IS EX6_011 (P2-8 상환 — the FIRST live caller of the ported BlastDNADigivolveEffect
// keyword body). Six regions, mirrored verbatim (substrate translations only, all established idioms):
//   * [Ace] <Blast DNA Digivolve> — timing OnCounterTiming -> CardEffectFactory.BlastDNADigivolveEffect with the
//     two BlastDNACondition names ("Durandamon" / "BryweLudramon").
//   * DNA Digivolution — timing None -> raw AddJogressConditionClass + GetJogress (Lv.6 Red + Lv.6 Black,
//     each slot owner-battle-area scoped) — ST13_06 idiom.
//   * <Raid> — timing OnAllyAttack -> RaidSelfEffect.
//   * <Reboot> — timing None -> RebootSelfStaticEffect.
//   * [On Play] shared body — timing OnEnterFieldAnyone -> ActivateClass: trash top opponent security +
//     UntilOpponentTurnEnd immunity; then if DNA-digivolving de-digivolve all enemy Digimon + delete 1.
//   * [When Digivolving] shared body — AS-IS OnEnterFieldAnyone + CanTriggerWhenDigivolving -> mirror dialect
//     key EffectTiming.WhenDigivolving (AD1_025 / ST13_06 precedent), same shared body.
//
// ADAPTATION (substrate translations only, established idioms):
//   * `permanent.Levels_ForJogress(card)` -> `permanent.TopCard.JogressLevelsAgainst(card)` (ST13_06 idiom).
//   * `permanent.TopCard.CardColors.Contains(CardColor.Red/Black)` -> `permanent.TopCard.HasCardColor("Red"/"Black")`.
//   * `card.Owner.Enemy.GetBattleAreaDigimons()` -> `new Player(card.Context, CardEffectCommons.OpponentOf(card))
//     .GetBattleAreaDigimons()` (AD1_025 idiom).
//   * `new IDestroySecurity(player: card.Owner.Enemy, 1, activateClass, fromTop: true)` -> `new IDestroySecurity(
//     card.Context, CardEffectCommons.OpponentOf(card), 1, activateClass, fromTop: true)` (AD1_025 idiom).
//   * `new IDegeneration(permanent, 1, activateClass).Degeneration()` -> `new IDegeneration(permanent, 1,
//     activateClass.EffectSourceCard?.InstanceId).Degeneration()` (EX8_026 ctor).
//   * `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`.
//   * IEnumerator ActivateCoroutine -> async Task; `yield return StartCoroutine(X)` -> `await X`.
//   * `IsOpponentEffect(cardEffect, card)` -> `IsOpponentEffect(cardEffect.EffectSourceCard, card)` (mirror
//     signature takes the source card — EX10_010 idiom).
//   * `HasMatchConditionPermanent(pred)` / `MatchConditionPermanentCount(pred)` -> the (card, pred) overloads.
//   * SelectPermanentEffect canTargetCondition is the canonical Permanent-typed overload -> the Permanent
//     predicate IsEnemyPermanentShared is passed directly (no id adapter).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.Red;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX6_011 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ace - Blast DNA Digivolve

        if (timing == EffectTiming.OnCounterTiming)
        {
            List<BlastDNACondition> blastDNAConditions = new List<BlastDNACondition>
            {
                new("Durandamon"),
                new("BryweLudramon")
            };
            cardEffects.Add(CardEffectFactory.BlastDNADigivolveEffect(card: card,
                blastDNAConditions: blastDNAConditions, condition: null));
        }

        #endregion

        #region DNA Digivolution

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
                        if (permanent != null)
                        {
                            if (permanent.TopCard != null)
                            {
                                if (permanent.TopCard.Owner == card.Owner)
                                {
                                    if (permanent.IsDigimon)
                                    {
                                        if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent,
                                                card))
                                        {
                                            if (permanent.TopCard.HasCardColor("Red"))
                                            {
                                                if (permanent.TopCard.JogressLevelsAgainst(card).Contains(6))
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    bool PermanentCondition2(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            if (permanent.TopCard != null)
                            {
                                if (permanent.TopCard.Owner == card.Owner)
                                {
                                    if (permanent.IsDigimon)
                                    {
                                        if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent,
                                                card))
                                        {
                                            if (permanent.TopCard.HasCardColor("Black"))
                                            {
                                                if (permanent.TopCard.JogressLevelsAgainst(card).Contains(6))
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    JogressConditionElement[] elements =
                    {
                        new(PermanentCondition1, "a level 6 Red Digimon"),
                        new(PermanentCondition2, "a level 6 Black Digimon")
                    };

                    JogressCondition jogressCondition = new JogressCondition(elements, 0);

                    return jogressCondition;
                }

                return null;
            }
        }

        #endregion

        #region Raid

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card,
                condition: null));
        }

        #endregion

        #region Reboot

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        #endregion

        #region Shared On Play / When Digivolving

        bool CanActivateSharedCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        bool CanUseImmunitySharedCondition(Hashtable hashtableI)
        {
            return CardEffectCommons.IsPermanentExistsOnBattleArea(ICardEffect.ResolvePermanentOfThisCard(card));
        }

        bool CardImmunitySharedCondition(CardSource cardSource)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                if (cardSource == ICardEffect.ResolvePermanentOfThisCard(card).TopCard)
                {
                    return true;
                }
            }

            return false;
        }

        bool SkillImmunitySharedCondition(ICardEffect cardEffect)
        {
            if (cardEffect != null)
            {
                if (cardEffect.EffectSourceCard != null)
                {
                    if (CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, card))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool IsEnemyPermanentShared(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(
                "Trash the top card of your opponent's security stack and this Digimon isn't affected by your opponent's effects until the end of their turn.",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateSharedCondition, ActivateCoroutine, -1, false,
                EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Play] Trash the top card of your opponent's security stack and this Digimon isn't affected by your opponent's effects until the end of their turn. Then, if DNA digivolving,  all of your opponent's Digimon (Trash the top card. You can't trash past level 3 cards) and delete 1 of their Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // Trash Top of Opponent's Security Stack
                await new IDestroySecurity(
                    card.Context,
                    CardEffectCommons.OpponentOf(card),
                    1,
                    activateClass,
                    fromTop: true).DestroySecurity();

                // This Digimon isn't affected by your opponent's effects until the end of their turn.
                CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
                canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's effects",
                    CanUseImmunitySharedCondition, card);
                canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardImmunitySharedCondition,
                    SkillCondition: SkillImmunitySharedCondition);
                ICardEffect.ResolvePermanentOfThisCard(card).UntilOpponentTurnEndEffects.Add(_ => canNotAffectedClass);

                // if DNA Digivolving
                if (CardEffectCommons.IsJogress(hashtable))
                {
                    // De-Digivolve all Opponent's Digimon
                    List<Permanent> enemyPermanents =
                        new Player(card.Context, CardEffectCommons.OpponentOf(card)).GetBattleAreaDigimons();

                    foreach (Permanent permanent in enemyPermanents)
                    {
                        await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId).Degeneration();
                    }

                    // Delete 1 Opponent's Digimon
                    if (CardEffectCommons.HasMatchConditionPermanent(card, IsEnemyPermanentShared))
                    {
                        int enemyCount = Math.Min(1,
                            CardEffectCommons.MatchConditionPermanentCount(card, IsEnemyPermanentShared));

                        SelectPermanentEffect selectEnemyEffect =
                            GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectEnemyEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: IsEnemyPermanentShared,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: enemyCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: null,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Destroy,
                            cardEffect: activateClass);

                        await selectEnemyEffect.Activate();
                    }
                }
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(
                "Trash the top card of your opponent's security stack and this Digimon isn't affected by your opponent's effects until the end of their turn.",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateSharedCondition, ActivateCoroutine, -1, false,
                EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] Trash the top card of your opponent's security stack and this Digimon isn't affected by your opponent's effects until the end of their turn. Then, if DNA digivolving,  all of your opponent's Digimon (Trash the top card. You can't trash past level 3 cards) and delete 1 of their Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // Trash Top of Opponent's Security Stack
                await new IDestroySecurity(
                    card.Context,
                    CardEffectCommons.OpponentOf(card),
                    1,
                    activateClass,
                    fromTop: true).DestroySecurity();

                // This Digimon isn't affected by your opponent's effects until the end of their turn.
                CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
                canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's effects",
                    CanUseImmunitySharedCondition, card);
                canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardImmunitySharedCondition,
                    SkillCondition: SkillImmunitySharedCondition);
                ICardEffect.ResolvePermanentOfThisCard(card).UntilOpponentTurnEndEffects.Add(_ => canNotAffectedClass);

                // if DNA Digivolving
                if (CardEffectCommons.IsJogress(hashtable))
                {
                    // De-Digivolve all Opponent's Digimon
                    List<Permanent> enemyPermanents =
                        new Player(card.Context, CardEffectCommons.OpponentOf(card)).GetBattleAreaDigimons().ToList();

                    foreach (Permanent permanent in enemyPermanents)
                    {
                        await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId).Degeneration();
                    }

                    // Delete 1 Opponent's Digimon
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsEnemyPermanentShared))
                    {
                        SelectPermanentEffect selectEnemyEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectEnemyEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: IsEnemyPermanentShared,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: null,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Destroy,
                            cardEffect: activateClass);

                        await selectEnemyEffect.Activate();
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
