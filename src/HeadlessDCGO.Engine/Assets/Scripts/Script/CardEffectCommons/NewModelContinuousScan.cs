// (P6 dispatch-flip STAGE B) New-model continuous interface scan.
//
// AS-IS truth: Permanent.DP / Permanent.Strike (SAttack) / Permanent.HasBlocker / HasJamming / HasPierce /
// … each RE-SCAN, at read time, every field permanent's + player's + face-up security's live
// `EffectList(EffectTiming.None)` and apply the matching 74-marker interface (IChangeDPEffect /
// IChangeSAttackEffect / IBlockerEffect / …). There is NO registry in AS-IS.
//
// The mirror mid-migration carries TWO effect representations that are DISJOINT by interface:
//   * LEGACY effects (ContinuousAndRestrictionEffects.cs classes) are `: ICardEffect` ONLY and lower to a
//     substrate `EffectBinding` (ToBinding) that the OLD gates (ContinuousDpGate / ContinuousModifierGate /
//     ContinuousKeywordGate / …) read. They do NOT implement the marker interfaces.
//   * NEW-model kind-classes (CardEffects/*.cs — ChangeSAttackClass:IChangeSAttackEffect,
//     BlockerClass:IBlockerEffect, …) implement the marker interfaces directly and register NO binding.
//
// So the OLD gates cannot see a new-model kind-class (the P6 symptom: ST7_10 SA+1 / a ported <Blocker> is
// inert). This helper performs the AS-IS interface scan over the LIVE `EffectList(None)` objects, and the
// gates UNION it with their existing binding path — legacy effects keep flowing through bindings, new-model
// effects now flow through the interface scan, and (because the two are interface-disjoint) nothing is
// double-counted. The scan order / aggregation is the verbatim AS-IS property body (anchors per method).
//
// SUBSTRATE ADAPTATIONS (logic verbatim):
//   (1) AS-IS `TopCard.CanNotBeAffected(<ICardEffect>)` -> mirror `CanNotBeAffected(<effect>.EffectSourceCard?.InstanceId)`
//       (mirror CanNotBeAffected takes a cause instance id, per the kind-class factory headers).
//   (2) AS-IS `gameContext.Players_ForTurnPlayer` needs a live turn context. In an isolated unit context the
//       TurnController may be un-initialised (PlayerOrder empty); the player set then falls back to the
//       distinct owners of all live card instances (ordering-insensitive for the single-effect cases that
//       hit that path). In real play PlayerOrder is populated, so this is the AS-IS turn-first order verbatim.
//   (3) The scan calls CanUse/CanTrigger, which (AS-IS) read game state through the process-global
//       GManager.instance; the mirror resolves that from AmbientMatchContext, so each public entry point
//       scopes the match (a caller already in the same scope re-enters harmlessly).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public static class NewModelContinuousScan
{
    /// <summary>AS-IS <c>gameContext.Players_ForTurnPlayer</c> (SUBSTRATE ADAPTATION 2): turn-player-first
    /// when the turn is initialised, else every distinct owner of a live instance.</summary>
    private static List<Player> ScanPlayers(EngineContext context)
    {
        var gameContext = new GameContext(context);
        List<Player> players = gameContext.Players_ForTurnPlayer;
        if (players.Count > 0)
        {
            return players;
        }

        return context.CardInstanceRepository.Snapshot()
            .Select(record => record.OwnerId)
            .Where(id => !id.IsEmpty)
            .Distinct()
            .Select(id => new Player(context, id))
            .ToList();
    }

    private static Permanent BuildSubject(EngineContext context, HeadlessEntityId cardId)
    {
        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        return new Permanent(context, cardId, owner);
    }

    // AS-IS gate: `!TopCard.CanNotBeAffected(cardEffect)` (SUBSTRATE ADAPTATION 1). A null TopCard (no live
    // top card) cannot be immune; treat as affectable, matching the AS-IS null-guarded property bodies.
    private static bool NotImmune(Permanent subject, ICardEffect cardEffect) =>
        subject.TopCard is null
        || !subject.TopCard.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId);

    // ==================================================================================================
    // Security Attack — AS-IS Permanent.Strike_AllowMinus (Permanent.cs:1817-1930). Collect
    // IChangeSAttackEffect over Players_ForTurnPlayer's field permanents + players, gated by
    // PermanentCondition(this) && CanUse(null) && !CanNotBeAffected, then fold split by isUpDown() in the
    // order UpToConstant -> UpDownValue -> DownToConstant, GetSAttack(Strike, this, InvertSecutiryValue).
    // ==================================================================================================
    public static int FoldSAttack(EngineContext context, HeadlessEntityId cardId, int baseValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        int invert = InvertSecurityValue(context, subject);

        var collected = new List<IChangeSAttackEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeSAttackEffect sattack
                && sattack.PermanentCondition(subject)
                && cardEffect.CanUse(null)
                && NotImmune(subject, cardEffect))
            {
                collected.Add(sattack);
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                Collect(cardEffect);
            }
        }

        // AS-IS split into UpToConstant / UpDownValue / DownToConstant and fold in that order.
        int strike = baseValue;
        foreach (CalculateOrder order in new[] { CalculateOrder.UpToConstant, CalculateOrder.UpDownValue, CalculateOrder.DownToConstant })
        {
            foreach (IChangeSAttackEffect effect in collected)
            {
                if (effect.isUpDown() == order)
                {
                    strike = effect.GetSAttack(strike, subject, invert);
                }
            }
        }

        return strike;
    }

    // AS-IS Permanent.InvertSecutiryValue (Permanent.cs:1670-1729): fold IInvertSAttackEffect over
    // Players_ForTurnPlayer field permanents + players (CanUse(null) && !CanNotBeAffected), clamp [-1,1].
    private static int InvertSecurityValue(EngineContext context, Permanent subject)
    {
        var collected = new List<IInvertSAttackEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IInvertSAttackEffect invert
                && cardEffect.CanUse(null)
                && NotImmune(subject, cardEffect))
            {
                collected.Add(invert);
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                Collect(cardEffect);
            }
        }

        int value = 0;
        foreach (IInvertSAttackEffect effect in collected)
        {
            value = effect.InversionValue(subject, value);
        }

        return Math.Clamp(value, -1, 1);
    }

    // ==================================================================================================
    // DP — AS-IS Permanent.DP (Permanent.cs:499-692): the full ordered scan (IsMinusDP immunity via
    // ImmuneFromDPMinus, IsUpDown grouping, LinkedDP, DPBoost). The mirror keeps the intricate AS-IS
    // aggregation in ContinuousDpGate over the binding representation; here we fold ONLY the new-model
    // IChangeDPEffect contribution over the value the binding gate already resolved, preserving AS-IS
    // grouping (IsUpDown()==true first, then the rest) WITHIN the new-model set.
    // design item RD-P6B-1: a permanent mixing LEGACY and NEW DP effects folds legacy-then-new (two ordered
    // passes) rather than AS-IS's single interleaved pass — result-identical unless a legacy up/down and a
    // new-model up/down interact on the same permanent (no such card today; both models homogeneous per card).
    // ==================================================================================================
    public static int FoldDp(EngineContext context, HeadlessEntityId cardId, int baseValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        // AS-IS gates EVERY ChangeDP candidate with !TopCard.CanNotBeAffected(cardEffect) (an opponent effect
        // the subject is immune to is dropped, minus or buff alike). A self-sourced effect is not an opponent
        // effect, so NotImmune returns true and it is kept.
        var collected = new List<IChangeDPEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeDPEffect dp
                && dp.PermanentCondition(subject)
                && cardEffect.CanUse(null)
                && NotImmune(subject, cardEffect))
            {
                collected.Add(dp);
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                Collect(cardEffect);
            }
        }

        // AS-IS folds the IsUpDown() group first, then the rest (Permanent.cs:626-651).
        int dpValue = baseValue;
        foreach (IChangeDPEffect effect in collected.Where(e => e.IsUpDown()))
        {
            dpValue = effect.GetDP(dpValue, subject);
        }

        foreach (IChangeDPEffect effect in collected.Where(e => !e.IsUpDown()))
        {
            dpValue = effect.GetDP(dpValue, subject);
        }

        return dpValue;
    }

    // ==================================================================================================
    // Keyword derivations — each mirrors its AS-IS Permanent.Has* property EXACTLY (scope, timing,
    // interface, gate predicate). Returns true iff SOME live new-model effect grants the keyword.
    // ==================================================================================================

    // AS-IS Permanent.HasBlocker (Permanent.cs:2397-2483): scan Players_ForTurnPlayer field permanents +
    // FACE-UP security cards + players' EffectList(None) for IBlockerEffect && CanTrigger(null) && IsBlocker(this).
    // (The attacker-Collision short-circuit at the top of the AS-IS property is a battle-time concern handled
    // by the existing gate/BlockTiming path, not this static keyword derivation.)
    public static bool HasBlocker(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IBlockerEffect blocker && cardEffect.CanTrigger(null) && blocker.IsBlocker(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (CardSource source in player.SecurityCards)
            {
                if (source.IsFlipped)
                {
                    continue;
                }

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IBlockerEffect blocker && cardEffect.CanTrigger(null) && blocker.IsBlocker(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IBlockerEffect blocker && cardEffect.CanTrigger(null) && blocker.IsBlocker(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasJamming (Permanent.cs:2486-2540): ICanNotBeDestroyedByBattleEffect && CanTrigger(null)
    // && EffectName=="Jamming" && PermanentCondition(this) over Players_ForTurnPlayer field permanents + players.
    public static bool HasJamming(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeDestroyedByBattleEffect jamming && cardEffect.CanTrigger(null)
                        && cardEffect.EffectName == "Jamming" && jamming.PermanentCondition(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeDestroyedByBattleEffect jamming && cardEffect.CanTrigger(null)
                    && cardEffect.EffectName == "Jamming" && jamming.PermanentCondition(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasPierce (Permanent.cs:2585-2611): SELF-scope only — IsDigimon-gated scan of THIS
    // permanent's EffectList(OnDetermineDoSecurityCheck) for ActivateICardEffect && (EffectName=="Pierce" ||
    // "Piercing"). (SUBSTRATE: AS-IS gates each with CanTrigger(PierceCheckHashtable); the static
    // keyword-presence query passes no hashtable, matching the mirror keyword gate's presence semantics — the
    // battle-time hashtable gating stays on the live Pierce activation path.)
    public static bool HasPierce(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        if (!subject.IsDigimon)
        {
            return false;
        }

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnDetermineDoSecurityCheck))
        {
            if (cardEffect is ActivateICardEffect
                && (cardEffect.EffectName == "Pierce" || cardEffect.EffectName == "Piercing"))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasReboot (Permanent.cs:2614-…): IRebootEffect && CanTrigger(null) && HasReboot(this)
    // over field permanents + face-up security + players.
    public static bool HasReboot(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IRebootEffect reboot && cardEffect.CanTrigger(null) && reboot.HasReboot(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (CardSource source in player.SecurityCards)
            {
                if (source.IsFlipped)
                {
                    continue;
                }

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IRebootEffect reboot && cardEffect.CanTrigger(null) && reboot.HasReboot(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IRebootEffect reboot && cardEffect.CanTrigger(null) && reboot.HasReboot(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasRush: IRushEffect && CanTrigger(null) && HasRush(this) over field permanents + players.
    public static bool HasRush(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IRushEffect rush && cardEffect.CanTrigger(null) && rush.HasRush(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IRushEffect rush && cardEffect.CanTrigger(null) && rush.HasRush(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The generic keyword-name -> new-model interface bridge used by
    /// <see cref="Headless.Runtime.ContinuousKeywordGate.HasKeyword(EngineContext, HeadlessEntityId, string)"/>.
    /// Returns true iff the new-model interface scan for <paramref name="keyword"/> grants it to
    /// <paramref name="cardId"/>. A keyword with no ported continuous interface returns false (the binding
    /// path still serves it) — those are latent (design item RD-P6B-2).</summary>
    public static bool HasKeyword(EngineContext context, HeadlessEntityId cardId, string keyword) => keyword switch
    {
        "Blocker" => HasBlocker(context, cardId),
        "Jamming" => HasJamming(context, cardId),
        "Piercing" or "Pierce" => HasPierce(context, cardId),
        "Reboot" => HasReboot(context, cardId),
        "Rush" => HasRush(context, cardId),
        _ => false,
    };
}
