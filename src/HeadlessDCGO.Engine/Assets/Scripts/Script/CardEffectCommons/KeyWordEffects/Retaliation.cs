// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Retaliation.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Retaliation). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Retaliation's resolution branch (1:1 with the original).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.Services;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveRetaliation(
            CardEffectResolveContext context,
            MatchState state,
            CardInstanceState target)
        {
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedByBattle, out bool deletedByBattle)
                || !deletedByBattle)
            {
                return Failure("Retaliation requires battle deletion.", "deletedByBattle", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedCardId, out HeadlessEntityId deletedCardId)
                || deletedCardId != target.InstanceId)
            {
                return Failure("Retaliation requires the keyword target to be deleted.", "deletedCardId", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.OpponentBattleCardId, out HeadlessEntityId opponentId)
                || !state.CardInstances.TryGetValue(opponentId, out CardInstanceState? opponent)
                || opponent.OwnerId == target.OwnerId)
            {
                return Failure("Retaliation requires an opponent battle target.", "opponentBattleCardId", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Retaliation can delete the opposing Digimon.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainRetaliation` (CardEffectCommons.cs:3417). Kept in the flat `...Script.CardEffectCommons` namespace
// (not the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(C-Btl grant rehousing) AS-IS <c>CardEffectCommons.GainRetaliation</c>
        /// (KeyWordEffects/Retaliation.cs:136-166), 1:1: grant a target Digimon the [Retaliation] trigger for the
        /// duration by building the <see cref="CardEffectFactory.RetaliationEffect"/> ActivateClass and storing it
        /// in the target permanent's <c>OnDestroyedAnyone</c> duration bucket via <see cref="AddEffectToPermanent"/>
        /// (W3 live) — so the AS-IS collect-before-removal deletion window (BattleResolver.FinalizeAsync) picks it up
        /// and the post-battle AutoProcessCheck resolves <see cref="RetaliationProcess"/>. Replaces the
        /// invented <c>GainKeywordToPermanent</c> funnel (ContinuousKeywordGate.Retaliation continuous marker, which
        /// Permanent.HasRetaliation/the window never read). ADAPTATION: AS-IS's terminal visual
        /// <c>CreateBuffEffect</c> (a Unity presentation coroutine) has no headless substrate — dropped.</summary>
        public static async Task GainRetaliation(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool CanUseCondition()
            {
                if (IsPermanentExistsOnBattleArea(targetPermanent))
                {
                    if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            ActivateClass retaliation = CardEffectFactory.RetaliationEffect(
                targetPermanent: targetPermanent, isInheritedEffect: false, condition: CanUseCondition,
                rootCardEffect: activateClass, targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: retaliation, timing: EffectTiming.OnDestroyedAnyone);

            await Task.CompletedTask;
        }

        /// <summary>(P6 cluster2) AS-IS <c>CanActivateRetaliation</c> (KeyWordEffects/Retaliation.cs:10, verbatim):
        /// this Digimon (now in the trash) was on the losing side of the battle that just deleted it, and the
        /// opponent's Digimon is identifiable among either side of that battle (accounting for ties).</summary>
        public static bool CanActivateRetaliation(Hashtable hashtable)
        {
            List<Hashtable>? hashtables = GetHashtablesFromHashtable(hashtable);
            if (hashtables is null)
            {
                return false;
            }

            foreach (Hashtable hashtable1 in hashtables)
            {
                CardSource? topCard = GetTopCardFromOneHashtable(hashtable1);
                if (topCard is null || !IsExistOnTrash(topCard))
                {
                    continue;
                }

                IBattle? battle = GetBattleFromHashtable(hashtable);
                Hashtable? battleHashtable = battle?.hashtable;
                if (battleHashtable is null)
                {
                    continue;
                }

                List<Permanent>? loserPermanents = GetLoserPermanentsFromHashtable(battleHashtable);
                if (loserPermanents is null || !loserPermanents.Exists(permanent => permanent.cardSources.Contains(topCard)))
                {
                    continue;
                }

                if (loserPermanents.Exists(permanent => IsOpponentPermanent(permanent, topCard)))
                {
                    return true;
                }

                List<Permanent>? winnerPermanents = GetWinnerPermanentsRealFromHashtable(battleHashtable);
                if (winnerPermanents is not null && winnerPermanents.Exists(permanent => IsOpponentPermanent(permanent, topCard)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>(R2-B) AS-IS <c>RetaliationProcess</c> (KeyWordEffects/Retaliation.cs:72): delete the
        /// opposing Digimon this card was battling (winner side; on a tie, the other loser side). RD-P6C2-2
        /// resolved. ADAPTATION: AS-IS's terminal <c>new DestroyPermanentsClass(destroyTargetPermanents,
        /// CardEffectHashtable(activateClass)).Destroy()</c> has no standalone mirror class — the mirror exposes
        /// that exact deletion pipeline through the <c>…AndProcessAccordingToResult</c> family (AS-IS's own
        /// <c>DeletePeremanentAndProcessAccordingToResult</c>, CardEffectCommons.cs:463, is literally
        /// <c>new DestroyPermanentsClass(targets, hashtable).Destroy()</c> + IsDestroyed dispatch), so the
        /// terminal batch-delete is issued via that verified substrate with no success/failure continuation —
        /// behaviourally identical to bare <c>Destroy()</c>. Structure/order otherwise verbatim with AS-IS.</summary>
        public static async Task RetaliationProcess(Hashtable hashtable, ICardEffect activateClass)
        {
            if (hashtable != null)
            {
                List<Hashtable>? hashtables = GetHashtablesFromHashtable(hashtable);

                if (hashtables != null)
                {
                    foreach (Hashtable hashtable1 in hashtables)
                    {
                        if (hashtable1 != null)
                        {
                            CardSource? topCard = GetTopCardFromOneHashtable(hashtable1);

                            if (topCard != null)
                            {
                                if (IsByBattle(hashtable))
                                {
                                    IBattle? battle = GetBattleFromHashtable(hashtable);

                                    if (battle != null)
                                    {
                                        Hashtable? battleHashtable = battle.hashtable;

                                        if (battleHashtable != null)
                                        {
                                            List<Permanent>? winnerPermanents = GetWinnerPermanentsRealFromHashtable(battleHashtable);

                                            if (winnerPermanents != null)
                                            {
                                                List<Permanent> destroyTargetPermanents = winnerPermanents.Filter(permanent => IsOpponentPermanent(permanent, topCard));

                                                if (destroyTargetPermanents.Count >= 1)
                                                {
                                                    await DeletePeremanentAndProcessAccordingToResult(destroyTargetPermanents, activateClass, successProcess: null, failureProcess: null).ConfigureAwait(false);
                                                }
                                                else // In case of tie there is no winner permanents but the other loser permanent is the target
                                                {
                                                    List<Permanent>? loserPermanents = GetLoserPermanentsFromHashtable(battleHashtable);

                                                    if (loserPermanents != null)
                                                    {
                                                        destroyTargetPermanents = loserPermanents.Filter(permanent => IsOpponentPermanent(permanent, topCard));

                                                        if (destroyTargetPermanents.Count >= 1)
                                                        {
                                                            await DeletePeremanentAndProcessAccordingToResult(destroyTargetPermanents, activateClass, successProcess: null, failureProcess: null).ConfigureAwait(false);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
