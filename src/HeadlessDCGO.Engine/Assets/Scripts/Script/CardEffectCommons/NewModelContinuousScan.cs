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

using System;
using System.Collections;
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

    /// <summary>AS-IS <c>gameContext.Players</c> (the FULL roster — CanSuspend/CanUnsuspend/CanNotEvolve/
    /// CanBlock/CanAttackTargetDigimon all scan this, not the turn-first Players_ForTurnPlayer). Same isolated-
    /// unit-context fallback as <see cref="ScanPlayers"/> (SUBSTRATE ADAPTATION 2).</summary>
    private static List<Player> ScanAllPlayers(EngineContext context)
    {
        var gameContext = new GameContext(context);
        List<Player> players = gameContext.Players;
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

    // (RD-P6B-10/11/12/P7 cause-conditional pass) Minimal stand-in ICardEffect used ONLY to carry a CAUSING
    // (deleting / reducing / bouncing) source's CardSource into an interface predicate that expects a live
    // ICardEffect — AS-IS ICanNotBeDestroyedBySkillEffect.CanNotBeDestroyedBySkill(Permanent, ICardEffect),
    // ICannotReturnToHandEffect.CannotReturnToHand / ICannotReturnToLibraryEffect.CannotReturnToLibrary,
    // IImmuneFromDPMinusEffect.ImmuneFromDPMinus — all take the REAL causing/reducing ICardEffect, which in
    // AS-IS is always a live scanned object (AS-IS has no legacy/new-model split; every effect is one kind of
    // object). The mirror mid-migration only has the causing/reducing SOURCE ENTITY ID at these call sites
    // (MatchStateMutationSink's mutation.SourceEntityId / a legacy NumericModifier's SourceEntityId) — not the
    // actual live ICardEffect instance that performed the mutation. Design item RD-P6B-13 (documented
    // compromise, same tier as HasPierce's no-hashtable presence check / CanNotDigivolve's source fallback
    // above): a cardEffectCondition that reads ONLY EffectSourceCard (the overwhelmingly common shape for these
    // interfaces — every real card's condition checks src.EffectSourceCard.Owner / IsOpponentEffect(src, card))
    // evaluates correctly; one that inspects other ICardEffect state (EffectName, EffectDiscription,
    // IsInheritedEffect, …) sees defaults, not the real causing effect's.
    private sealed class CausingEffectStandIn : ICardEffect
    {
        public CausingEffectStandIn(CardSource? causingSource) => SetEffectSourceCard(causingSource!);
    }

    private static ICardEffect BuildCausingEffectStandIn(EngineContext context, HeadlessEntityId causingSourceId)
    {
        CardSource? source = causingSourceId.IsEmpty ? null : Headless.Runtime.RestrictionScan.MakeSource(context, causingSourceId);
        return new CausingEffectStandIn(source);
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

            // AS-IS Permanent.DP (Permanent.cs:542-576, "Effects of face up security"): FACE-UP (!IsFlipped)
            // security cards are ALSO scanned as an IChangeDPEffect source, between the field-permanents and
            // player-effect regions — SEC-FaceUpSecuritySource (RD-P6B-3): the mirror previously omitted this
            // scope entirely. SUBSTRATE: the mirror's security-specific face state is tracked by
            // Headless.Runtime.SecurityFaceState (a dedicated `securityFaceUp` instance flag, gated on the card
            // still being in the Security zone), NOT the generic CardSource.IsFlipped field flag — mirrors the
            // AS-IS SetFace()/SetReverse() security-placement state.
            foreach (CardSource source in player.SecurityCards)
            {
                if (!Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(context, source.InstanceId))
                {
                    continue;
                }

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
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
    // Base DP — AS-IS Permanent.BaseDP (Permanent.cs:193-322): the SAME shape as DP's IChangeDPEffect scan
    // (field permanents + players' EffectList(None), PermanentCondition(this) && CanUse(null) &&
    // !CanNotBeAffected, IsUpDown() group first then the rest) but over IChangeBaseDPEffect, NO face-up
    // security scope (the BaseDP getter has no such region) and NO LinkedDP/DPBoost tail (those are DP-only).
    // Parity note: mirrors FoldDp's simplification tier — the AS-IS IsMinusDP()+ImmuneFromDPMinus per-effect
    // skip is not modelled here either (same as FoldDp above; no card exercises new-model BaseDP-minus +
    // immunity together today).
    // ==================================================================================================
    public static int FoldBaseDp(EngineContext context, HeadlessEntityId cardId, int baseValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        var collected = new List<IChangeBaseDPEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeBaseDPEffect baseDp
                && baseDp.PermanentCondition(subject)
                && cardEffect.CanUse(null)
                && NotImmune(subject, cardEffect))
            {
                collected.Add(baseDp);
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

        // AS-IS folds the IsUpDown() group first, then the rest (Permanent.cs:290-310).
        int baseDpValue = baseValue;
        foreach (IChangeBaseDPEffect effect in collected.Where(e => e.IsUpDown()))
        {
            baseDpValue = effect.GetDP(baseDpValue, subject);
        }

        foreach (IChangeBaseDPEffect effect in collected.Where(e => !e.IsUpDown()))
        {
            baseDpValue = effect.GetDP(baseDpValue, subject);
        }

        return baseDpValue;
    }

    // ==================================================================================================
    // Link Max — AS-IS Permanent.LinkedMax (Permanent.cs:896-1039). Collect IChangeLinkMaxEffect over
    // Players_ForTurnPlayer field permanents' EffectList(None) + FACE-UP (!IsFlipped) security cards'
    // EffectList(None) + players' EffectList(None), gated by PermanentCondition(this) && CanUse(null) &&
    // !TopCard.CanNotBeAffected, then split by isUpDown() and fold in the order UpToConstant -> UpDownValue ->
    // DownToConstant, calling GetLinkMax(Max, this, InvertSecutiryValue). AS-IS base is a constant 1; here the
    // legacy-resolved value (base metadata + legacy binding modifiers, LinkHelpers.ResolveLinkedMax) is the
    // baseValue we fold onto — the two representations are interface-disjoint so the union double-counts
    // nothing (RD-P6B-16).
    // ==================================================================================================
    public static int FoldLinkedMax(EngineContext context, HeadlessEntityId cardId, int baseValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        int invert = InvertSecurityValue(context, subject);

        var collected = new List<IChangeLinkMaxEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeLinkMaxEffect linkMax
                && linkMax.PermanentCondition(subject)
                && cardEffect.CanUse(null)
                && NotImmune(subject, cardEffect))
            {
                collected.Add(linkMax);
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

            // AS-IS "Effects of face up security" (Permanent.cs:930-953): FACE-UP security cards are scanned
            // as an IChangeLinkMaxEffect source too (same SecurityFaceState face-state gate as FoldDp).
            foreach (CardSource source in player.SecurityCards)
            {
                if (!Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(context, source.InstanceId))
                {
                    continue;
                }

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
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
        int max = baseValue;
        foreach (CalculateOrder order in new[] { CalculateOrder.UpToConstant, CalculateOrder.UpDownValue, CalculateOrder.DownToConstant })
        {
            foreach (IChangeLinkMaxEffect effect in collected)
            {
                if (effect.isUpDown() == order)
                {
                    max = effect.GetLinkMax(max, subject, invert);
                }
            }
        }

        return max;
    }

    // ==================================================================================================
    // Link Cost — AS-IS CardSource.GetChangedLinkCost (CardSource.cs:3267-3331). Collect IChangeLinkCostEffect
    // over Players_ForTurnPlayer field permanents (EXCLUDING this card's own permanent) + players' + THIS
    // card's own EffectList(None), gated by CanUse(null) && CardCondition(this) && PermanentCondition(target),
    // then fold the NotIsUpDown() group first, then the IsUpDown() group, calling GetCost(cost, this, target,
    // root); clamp >= 0. AS-IS seeds Cost from linkCondition.cost — here the caller (LinkHelpers.ResolveLinkCost)
    // supplies the base cost it is folding, and the new-model interface scan is UNIONed onto the legacy binding
    // fold (interface-disjoint, no double-count; RD-P6B-16). SUBSTRATE: the mirror's real cost call sites supply
    // no target permanent / SelectCardEffect.Root (the link-play orchestration RD-P6C2-7 is unported), so
    // targetPermanentId defaults empty (an unconditional PermanentCondition — the common shape — still applies).
    // ==================================================================================================
    public static int FoldLinkCost(EngineContext context, HeadlessEntityId cardId, int baseValue, HeadlessEntityId targetPermanentId = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        CardSource cardSource = new(context, cardId, owner);
        Permanent targetPermanent = new(context, targetPermanentId.IsEmpty ? cardId : targetPermanentId, owner);
        PermanentView ownPermanent = cardSource.PermanentOfThisCard();

        var collected = new List<IChangeLinkCostEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeLinkCostEffect linkCost
                && cardEffect.CanUse(null)
                && linkCost.CardCondition(cardSource)
                && linkCost.PermanentCondition(targetPermanent))
            {
                collected.Add(linkCost);
            }
        }

        // (AS-IS region 1) the effects of permanents — EXCLUDING this card's own permanent.
        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (!ownPermanent.IsEmpty && permanent.InstanceId == ownPermanent.TopInstanceId)
                {
                    continue;
                }

                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }
        }

        // (AS-IS region 2) the effects of players.
        foreach (Player player in ScanPlayers(context))
        {
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                Collect(cardEffect);
            }
        }

        // (AS-IS region 3) the effects of itself.
        foreach (ICardEffect cardEffect in cardSource.EffectList(EffectTiming.None))
        {
            Collect(cardEffect);
        }

        int cost = baseValue;
        foreach (IChangeLinkCostEffect effect in collected.Where(e => !e.IsUpDown()))
        {
            cost = effect.GetCost(cost, cardSource, targetPermanent, SelectCardEffect.Root.None);
        }

        foreach (IChangeLinkCostEffect effect in collected.Where(e => e.IsUpDown()))
        {
            cost = effect.GetCost(cost, cardSource, targetPermanent, SelectCardEffect.Root.None);
        }

        return Math.Max(0, cost);
    }

    // ==================================================================================================
    // Card DP (security-Digimon battle DP) — AS-IS CardSource.CardDP (CardSource.cs:2383-2443). Collect
    // IChangeCardDPEffect over Players_ForTurnPlayer field permanents' EffectList(None) + players'
    // EffectList(None), gated by CanUse(null) && CardCondition(this), then fold the IsUpDown() group FIRST,
    // then the NotIsUpDown() group (CardSource.cs:2432-2436 — note the order is the REVERSE of DP), calling
    // GetDP(cardDP, this). AS-IS seeds cardDP = BaseCardDP + BaseDP; here the caller (SecurityResolver) supplies
    // the security Digimon's already-resolved battle DP as baseValue and this new-model interface scan is
    // UNIONed onto the legacy securityCardDpDelta fold (interface-disjoint; RD-P6B-18). AS-IS ChangeCardDPClass.
    // CardCondition additionally gates on GManager.instance.attackProcess.SecurityDigimon == this — the caller
    // MUST have set attackProcess.SecurityDigimon to `cardId` (the card IS the revealed security Digimon), which
    // SecurityResolver does before invoking this fold. Clamp >= 0 (AS-IS Math.Max(0, cardDP)).
    // ==================================================================================================
    public static int FoldCardDp(EngineContext context, HeadlessEntityId cardId, int baseValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        CardSource subject = new(context, cardId, owner);

        var collected = new List<IChangeCardDPEffect>();
        void Collect(ICardEffect cardEffect)
        {
            if (cardEffect is IChangeCardDPEffect cardDp
                && cardEffect.CanUse(null)
                && cardDp.CardCondition(subject))
            {
                collected.Add(cardDp);
            }
        }

        // (AS-IS region 1) the effects of permanents.
        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }
        }

        // (AS-IS region 2) the effects of players.
        foreach (Player player in ScanPlayers(context))
        {
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                Collect(cardEffect);
            }
        }

        // AS-IS folds the IsUpDown() group first, then the rest (CardSource.cs:2432-2436).
        int cardDpValue = baseValue;
        foreach (IChangeCardDPEffect effect in collected.Where(e => e.IsUpDown()))
        {
            cardDpValue = effect.GetDP(cardDpValue, subject);
        }

        foreach (IChangeCardDPEffect effect in collected.Where(e => !e.IsUpDown()))
        {
            cardDpValue = effect.GetDP(cardDpValue, subject);
        }

        return Math.Max(0, cardDpValue);
    }

    // ==================================================================================================
    // Play cost — AS-IS CardSource.GetChangedCostItselef (CardSource.cs:775-858, the "not-paying" group,
    // IChangeCostEffect.IsChangePayingCost()==false) THEN CardSource.GetChangedPayingCost (:864-930, the
    // "paying" group, IsChangePayingCost()==true) — CardSource.cs:741/743 runs BOTH sequentially over the
    // SAME running Cost. Each group collects IChangeCostEffect over Players_ForTurnPlayer field permanents'
    // EffectList(None) + players' EffectList(None) + (when the card is NOT itself a live permanent —
    // PermanentOfThisCard() empty) its OWN EffectList(None), gated by CanUse(null) && CardCondition(this) &&
    // !(IsCheckAvailability() && !checkAvailability) && the matching IsChangePayingCost(); within a group,
    // fold the NotIsUpDown() effects first, then the IsUpDown() effects (CardSource.cs:843-853 / 918-928),
    // calling GetCost(cost, this, root, targetPermanents) — AS-IS ChangeCostClass.GetCost ALREADY re-derives
    // the "can this reduction apply" veto live via Player.CanReduceCost(targetPermanents, cardSource) (a real
    // ICannotReduceCostEffect scan, ported 1:1), so no separate immunity plumbing is needed here for that
    // part. SUBSTRATE: the mirror's real cost call sites (PlayCardAction/DigivolveAction) supply no
    // `targetPermanents` at all (no evolution-target modelling at those choke points yet) — an optional
    // targetPermanentIds lets a caller thread real battle-area permanents through for the (real-caller-less
    // today) target-permanent-conditioned shape (CardEffectFactory.ChangePlayCostStaticEffect's
    // PermanentsCondition), defaulting to the empty list a plain play genuinely supplies. `canReduceCost`
    // mirrors the SAME external "can this metric be reduced at all" knob the legacy numeric-modifier fold
    // already exposes (ContinuousModifierGate's `effectiveCanReduce`) — applied here on top of (not instead
    // of) the live GetCost veto, so both representations honour one uniform caller-supplied signal.
    // ==================================================================================================
    public static int FoldPlayCost(
        EngineContext context,
        HeadlessEntityId cardId,
        int baseValue,
        bool checkAvailability = false,
        bool canReduceCost = true,
        IReadOnlyList<HeadlessEntityId>? targetPermanentIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        CardSource cardSource = new(context, cardId, owner);

        List<Permanent> targetPermanents = (targetPermanentIds ?? Array.Empty<HeadlessEntityId>())
            .Select(id => new Permanent(context, id, owner))
            .ToList();

        bool cardIsNotAPermanent = cardSource.PermanentOfThisCard().IsEmpty;

        List<IChangeCostEffect> CollectGroup(bool isChangePayingCost)
        {
            var collected = new List<IChangeCostEffect>();
            void Collect(ICardEffect cardEffect)
            {
                if (cardEffect is IChangeCostEffect changeCost
                    && cardEffect.CanUse(null)
                    && changeCost.CardCondition(cardSource)
                    && !(changeCost.IsCheckAvailability() && !checkAvailability)
                    && changeCost.IsChangePayingCost() == isChangePayingCost)
                {
                    collected.Add(changeCost);
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

            if (cardIsNotAPermanent)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(EffectTiming.None))
                {
                    Collect(cardEffect);
                }
            }

            return collected;
        }

        int Fold(int cost, bool isChangePayingCost)
        {
            List<IChangeCostEffect> group = CollectGroup(isChangePayingCost);
            foreach (IChangeCostEffect changeCost in group.Where(e => !e.IsUpDown()).Concat(group.Where(e => e.IsUpDown())))
            {
                int newCost = changeCost.GetCost(cost, cardSource, SelectCardEffect.Root.None, targetPermanents);
                if (changeCost.IsUpDown() && newCost < cost && !canReduceCost)
                {
                    newCost = cost;
                }

                cost = newCost;
            }

            return cost;
        }

        int result = Fold(baseValue, isChangePayingCost: false);
        result = Fold(result, isChangePayingCost: true);
        return Math.Max(0, result);
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

    // AS-IS Permanent.HasIceclad (Permanent.cs:2540-2581): IIcecladEffect && CanTrigger(null) &&
    // HasIceclad(this), scanned over Players_ForTurnPlayer — self EffectList(None) (redundant per player in
    // AS-IS, folded once here) + each player's EffectList(None).
    public static bool HasIceclad(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.None))
        {
            if (cardEffect is IIcecladEffect iceclad && cardEffect.CanTrigger(null) && iceclad.HasIceclad(subject))
            {
                return true;
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IIcecladEffect iceclad && cardEffect.CanTrigger(null) && iceclad.HasIceclad(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasRaid (Permanent.cs:2687-2704): SELF EffectList(OnAllyAttack), ActivateICardEffect &&
    // EffectName=="Raid" — no CanTrigger call (presence-only, matching AS-IS exactly).
    public static bool HasRaid(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnAllyAttack))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "Raid")
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.RetaliationCount (Permanent.cs:2793-2815, backing HasRetaliation:2778-2789): SELF
    // EffectList(OnDestroyedAnyone), ActivateICardEffect && EffectName=="Retaliation" &&
    // CanTrigger(OnDeletionCheckHashtableOfPermanent(this)).
    public static bool HasRetaliation(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = CardEffectCommons.OnDeletionCheckHashtableOfPermanent(subject);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnDestroyedAnyone))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "Retaliation" && cardEffect.CanTrigger(hashtable))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasAscension (Permanent.cs:2819-2839): SELF EffectList(OnDestroyedAnyone),
    // ActivateICardEffect && EffectName=="Ascension" && CanTrigger(OnDeletionCheckHashtableOfPermanent(this)).
    public static bool HasAscension(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = CardEffectCommons.OnDeletionCheckHashtableOfPermanent(subject);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnDestroyedAnyone))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "Ascension" && cardEffect.CanTrigger(hashtable))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasFortitude (Permanent.cs:2843-2864): SELF EffectList(OnDestroyedAnyone),
    // ActivateICardEffect && EffectName=="Fortitude" && CanTrigger(OnDeletionCheckHashtableOfPermanent(this)).
    public static bool HasFortitude(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = CardEffectCommons.OnDeletionCheckHashtableOfPermanent(subject);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnDestroyedAnyone))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "Fortitude" && cardEffect.CanTrigger(hashtable))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasBlitz (Permanent.cs:2867-2890): SELF EffectList(OnEnterFieldAnyone),
    // ActivateICardEffect && (CanTrigger(WhenDigivolutionCheckHashtableOfPermanent(this)) ||
    // CanTrigger(OnPlayCheckHashtableOfPermanent(this))) && EffectDiscription.Contains("Blitz").
    public static bool HasBlitz(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnEnterFieldAnyone))
        {
            if (cardEffect is ActivateICardEffect
                && (cardEffect.CanTrigger(CardEffectCommons.WhenDigivolutionCheckHashtableOfPermanent(subject))
                    || cardEffect.CanTrigger(CardEffectCommons.OnPlayCheckHashtableOfPermanent(subject)))
                && !string.IsNullOrEmpty(cardEffect.EffectDiscription)
                && cardEffect.EffectDiscription.Contains("Blitz"))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasEvade (Permanent.cs:2894-2920): SELF EffectList(WhenPermanentWouldBeDeleted),
    // ActivateICardEffect && CanTrigger({"Permanents":[this]}) && EffectName=="Evade".
    public static bool HasEvade(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable { { "Permanents", new List<Permanent> { subject } } };

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable) && cardEffect.EffectName == "Evade")
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasMindLink (Permanent.cs:2923-2946): SELF EffectList(OnDeclaration),
    // ActivateICardEffect && CanTrigger(null) && EffectDiscription.Contains("Mind Link").
    public static bool HasMindLink(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnDeclaration))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(null)
                && !string.IsNullOrEmpty(cardEffect.EffectDiscription) && cardEffect.EffectDiscription.Contains("Mind Link"))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasBarrier (Permanent.cs:2950-2975): SELF EffectList(WhenPermanentWouldBeDeleted),
    // ActivateICardEffect && CanTrigger({"Permanents":[this], "battle": new IBattle(null,null,null)}) &&
    // EffectName=="Barrier".
    public static bool HasBarrier(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable
        {
            { "Permanents", new List<Permanent> { subject } },
            { "battle", new IBattle(null, null, null) },
        };

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable) && cardEffect.EffectName == "Barrier")
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasAlliance (Permanent.cs:2978-3040): hashtable {"AttackingPermanent": this}; scan
    // Players_ForTurnPlayer field permanents' + FACE-UP security's + players' EffectList(OnAllyAttack) for
    // EffectName=="Alliance" && CanTrigger(hashtable) (no interface-type filter — matches AS-IS verbatim).
    public static bool HasAlliance(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable { { "AttackingPermanent", subject } };

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.OnAllyAttack))
                {
                    if (cardEffect.EffectName == "Alliance" && cardEffect.CanTrigger(hashtable))
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

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.OnAllyAttack))
                {
                    if (cardEffect.EffectName == "Alliance" && cardEffect.CanTrigger(hashtable))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.OnAllyAttack))
            {
                if (cardEffect.EffectName == "Alliance" && cardEffect.CanTrigger(hashtable))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasCollision (Permanent.cs:3043-3110): ICollisionEffect && CanTrigger(null) &&
    // HasCollision(this), over Players_ForTurnPlayer field permanents' + FACE-UP security's + players'
    // EffectList(OnCounterTiming).
    public static bool HasCollision(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.OnCounterTiming))
                {
                    if (cardEffect is ICollisionEffect collision && cardEffect.CanTrigger(null) && collision.HasCollision(subject))
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

                foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.OnCounterTiming))
                {
                    if (cardEffect is ICollisionEffect collision && cardEffect.CanTrigger(null) && collision.HasCollision(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.OnCounterTiming))
            {
                if (cardEffect is ICollisionEffect collision && cardEffect.CanTrigger(null) && collision.HasCollision(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.HasPartition (Permanent.cs:3113-3130): SELF EffectList(WhenPermanentWouldBeDeleted),
    // ActivateICardEffect && EffectName=="Partition" — no CanTrigger call (presence-only, matching AS-IS).
    public static bool HasPartition(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "Partition")
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.HasScapegoat (Permanent.cs:3134-3151): SELF EffectList(WhenPermanentWouldBeDeleted),
    // ActivateICardEffect && EffectName=="<Scapegoat>" (literal, angle brackets — every real ScapegoatSelfEffect
    // call site passes effectName:"<Scapegoat>", e.g. EX8_061/EX8_071/BT20_080/LM_043) — no CanTrigger call.
    public static bool HasScapegoat(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.EffectName == "<Scapegoat>")
            {
                return true;
            }
        }

        return false;
    }

    // Vortex/Overclock/Execute (CardEffectFactory/KeyWordEffects/Vortex.cs, Overclock.cs, Execute.cs): AS-IS has
    // no dedicated Permanent.Has<X> getter for these (unlike Blocker/Jamming/Reboot/Rush) — presence is only
    // ever consulted structurally (CanActivateVortex, EndOfTurnEffectAttack.TryOpen). Each factory builds a
    // fixed-EffectName ActivateClass registered at EffectTiming.OnEndTurn (AS-IS EX8_074 "Vortex"/"Overclock"/
    // "Execute" regions; EndOfTurnEffectAttack.cs:62 "AS-IS cards register ExecuteSelfEffect at
    // EffectTiming.OnEndTurn"). Their CanUseCondition(hashtable) (IsExistOnBattleArea(card) &&
    // IsOwnerTurn(card)) reads no hashtable content, so CanTrigger(null) evaluates it exactly.
    private static bool HasSelfEndTurnKeyword(EngineContext context, HeadlessEntityId cardId, string effectName)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.OnEndTurn))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(null) && cardEffect.EffectName == effectName)
            {
                return true;
            }
        }

        return false;
    }

    // Save/ArmorPurge (CardEffectFactory/KeyWordEffects/Save.cs, ArmorPurge.cs): deletion-replacement family,
    // no dedicated Permanent.Has<X> getter. Fixed EffectName ("Save" / "Armor Purge"), registered at
    // EffectTiming.WhenPermanentWouldBeDeleted (same timing as Evade/Barrier/Partition/Scapegoat above).
    // Save's CanUseCondition is CanTriggerOnDeletion(hashtable, card) -> real gate via
    // OnDeletionCheckHashtableOfPermanent (same builder Retaliation/Ascension/Fortitude use). ArmorPurge's is
    // CanTriggerWhenRemoveField(hashtable, card) -> real gate via the {"Permanents":[this]} shape (Evade/Barrier).
    public static bool HasSave(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = CardEffectCommons.OnDeletionCheckHashtableOfPermanent(subject);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable) && cardEffect.EffectName == "Save")
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasArmorPurge(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable { { "Permanents", new List<Permanent> { subject } } };

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable) && cardEffect.EffectName == "Armor Purge")
            {
                return true;
            }
        }

        return false;
    }

    // Decode (CardEffectFactory/KeyWordEffects/Decode.cs): EffectName is computed as $"Decode {color}" per
    // call site (e.g. "Decode Red/Black") — no fixed literal, so match the "Decode " prefix. CanUseCondition
    // = IsExistOnBattleAreaDigimon && CanTriggerWhenRemoveField(hashtable, card) && !IsByBattle(hashtable); the
    // {"Permanents":[this]} shape satisfies the first two, and the absent "battle" key makes IsByBattle false.
    public static bool HasDecode(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable { { "Permanents", new List<Permanent> { subject } } };

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable)
                && cardEffect.EffectName is not null && cardEffect.EffectName.StartsWith("Decode", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Fragment (CardEffectFactory/KeyWordEffects/Fragment.cs): EffectName is caller-supplied per call site but
    // every real card passes a "Fragment <N>" literal (e.g. EX8_051/EX8_055/BT22_061 "Fragment <3>") — match
    // the "Fragment" prefix (case-insensitive: a test fixture may lower-case it). CanUseCondition =
    // IsPermanentExistsOnBattleArea && CanTriggerWhenRemoveField(hashtable, TopCard) — the {"Permanents":[this]}
    // shape satisfies it.
    public static bool HasFragment(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Hashtable hashtable = new Hashtable { { "Permanents", new List<Permanent> { subject } } };

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect && cardEffect.CanTrigger(hashtable)
                && cardEffect.EffectName is not null
                && cardEffect.EffectName.StartsWith("Fragment", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Decoy (CardEffectFactory/KeyWordEffects/Decoy.cs): EffectName is caller-supplied but every real card
    // passes a "Decoy (<color>)" literal (e.g. BT6_064 "Decoy (Black)") — match the "Decoy" prefix. AS-IS
    // CanUseCondition additionally requires CardEffectCommons.IsByEffect(hashtable, ...) — an OPPONENT-sourced
    // causing effect must be present in the hashtable, which a generic presence query has no causing effect
    // for; design item RD-P6B-5 (Decoy's presence check is therefore gate-less, like Pierce's documented
    // compromise above — the real CanTrigger evaluation still runs at actual deletion-replacement time via the
    // existing DeletionReplacementGate/CandidateConditions consumers).
    public static bool HasDecoy(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect
                && cardEffect.EffectName is not null && cardEffect.EffectName.StartsWith("Decoy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // (RD-P6B-14 partial resolution — Decoy joint scan) AS-IS Decoy.cs CanUseCondition is a JOINT (holder,
    // candidate) predicate embedded in the trigger's own hashtable-driven check — NOT a simple presence-on-
    // the-holder scan like HasDecoy above: `IsPermanentExistsOnBattleArea(targetPermanent=holder) &&
    // CanTriggerWhenPermanentRemoveField(hashtable, CanSelectPermanentCondition) && IsByEffect(hashtable,
    // CardEffectCondition)`, where CanSelectPermanentCondition(permanent) = `permanent != holder &&
    // IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) && (permanentCondition == null ||
    // permanentCondition(permanent))` and CardEffectCondition(cardEffect) = `cardEffect.EffectSourceCard.Owner
    // == holder-owner's Enemy`. Both `CanSelectPermanentCondition` and the causing-effect condition are
    // closures with no retrievable property on the built ActivateClass (same closure-encapsulation tier as
    // HasFragment's trashValue, RD-P6B-4) — but AS-IS itself only ever evaluates them THROUGH
    // `cardEffect.CanTrigger(hashtable)` (which invokes the stored CanUseCondition delegate), so building the
    // EXACT hashtable shape the real WhenPermanentWouldBeDeleted window populates (`{"Permanents": [protected
    // target]}` — GetPermanentsFromHashtable's key — plus `{"CardEffect": causingEffect}` —
    // GetCardEffectFromHashtable's key, CanUseEffects/OnDeletion.cs / GetFromHashtable.cs) and calling
    // CanTrigger on it exercises the closures faithfully without needing to extract them. This is a joint scan
    // (holder AND candidate), not a per-holder presence flag.
    // <paramref name="causingSourceId"/> empty means "the caller has not yet resolved the deleting effect"
    // (e.g. FindDecoyRedirectCandidates enumerates BEFORE the deferred deletion names its deleter — AS-IS
    // decides that separately, at defer time, per DeletionReplacementGate's own doc comment) — the by-enemy-
    // effect condition is (and always was) validated OUTSIDE this scan by every caller (FindDecoyRedirect's own
    // `deleter.OwnerId == target.OwnerId` guard; FindDecoyRedirectCandidates' caller gates on the separately-
    // computed DecoyEligibleKey marker), so a synthetic enemy-owned stand-in here — trivially satisfying
    // IsByEffect — is a safe stand-in for "some enemy effect, already independently confirmed", not a
    // weakening: it lets THIS scan isolate the part it uniquely owns (the permanentCondition joint predicate).
    public static bool DecoyAcceptsSubject(
        EngineContext context, HeadlessEntityId decoyId, HeadlessEntityId targetId, HeadlessPlayerId targetOwner, HeadlessEntityId causingSourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent decoySubject = BuildSubject(context, decoyId);
        Permanent targetPermanent = new(context, targetId, targetOwner);
        ICardEffect causingEffect;
        if (!causingSourceId.IsEmpty)
        {
            causingEffect = BuildCausingEffectStandIn(context, causingSourceId);
        }
        else
        {
            HeadlessPlayerId enemyOwner = new Player(context, targetOwner).Enemy?.PlayerId ?? default;
            CardSource syntheticEnemySource = new(context, new HeadlessEntityId("$decoy-synthetic-cause"), enemyOwner);
            causingEffect = new CausingEffectStandIn(syntheticEnemySource);
        }

        Hashtable hashtable = new Hashtable
        {
            { "Permanents", new List<Permanent> { targetPermanent } },
            { "CardEffect", causingEffect },
        };

        foreach (ICardEffect cardEffect in decoySubject.EffectList(EffectTiming.WhenPermanentWouldBeDeleted))
        {
            if (cardEffect is ActivateICardEffect
                && cardEffect.EffectName is not null && cardEffect.EffectName.StartsWith("Decoy", StringComparison.OrdinalIgnoreCase)
                && cardEffect.CanTrigger(hashtable))
            {
                return true;
            }
        }

        return false;
    }

    // Progress (CardEffectFactory/KeyWordEffects/Progress.cs): a CanNotAffectedClass (immunity kind-class, NOT
    // ActivateICardEffect) with fixed EffectName "Progress", self-registered continuously (EffectTiming.None,
    // same registration timing as the other SelfStaticEffect continuous grants in this file). Presence-only
    // (no CanUse gate), matching the Raid/Partition/Scapegoat treatment above: AS-IS CanActivateProgress
    // additionally requires GManager.instance.attackProcess.IsAttacking && AttackingPermanent==this — that
    // "currently attacking" gate belongs to the LIVE immunity consumer (ProgressImmunity), not this
    // grant-presence query (does the card HAVE Progress, vs is it live at this instant).
    public static bool HasProgress(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.EffectList(EffectTiming.None))
        {
            if (cardEffect.EffectName == "Progress")
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS CardEffectCommons.PermanentHasVortexCanAttackPlayers (CardEffectCommons/KeyWordEffects/Vortex.cs):
    // scans EVERY player (gameContext.Players — NOT Players_ForTurnPlayer, unlike the other Has* members
    // above) field permanents' + players' EffectList(None) for IVortexCanAttackPlayersEffect && CanUse(null)
    // && VortexCanAttackPlayersPermanent(SUBJECT) (the AS-IS lambda parameter shadowing means the interface
    // call always tests the OUTER method's subject permanent, not the per-candidate scan permanent).
    public static bool HasVortexCanAttackPlayers(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        var gameContext = new GameContext(context);

        foreach (Player player in gameContext.Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IVortexCanAttackPlayersEffect marker && cardEffect.CanUse(null)
                        && marker.VortexCanAttackPlayersPermanent(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IVortexCanAttackPlayersEffect marker && cardEffect.CanUse(null)
                    && marker.VortexCanAttackPlayersPermanent(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.IsDigimon (Permanent.cs:3438-3510), the ITreatAsDigimonEffect region only — the native
    // CardType/DigiEgg/flip fast path is handled by ContinuousKeywordGate.IsDigimon BEFORE it falls through to
    // HasKeyword(TreatAsDigimon). Scans (a) the subject's OWN TopCard EffectList(None) first (a self grant —
    // e.g. an Option that treats itself as a Digimon), THEN (b) Players_ForTurnPlayer's field permanents'
    // EffectList_Added(None) (AS-IS uses EffectList_Added here specifically — "EffectList_forCard checks
    // IsDigimon and this causes a stack overflow") + players' EffectList(None). Gate: ITreatAsDigimonEffect &&
    // CanTrigger(null) && IsDigimon(subject).
    public static bool HasTreatAsDigimon(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (ICardEffect cardEffect in subject.TopCard.EffectList(EffectTiming.None))
        {
            if (cardEffect is ITreatAsDigimonEffect treat && cardEffect.CanTrigger(null) && treat.IsDigimon(subject))
            {
                return true;
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList_Added(EffectTiming.None))
                {
                    if (cardEffect is ITreatAsDigimonEffect treat && cardEffect.CanTrigger(null) && treat.IsDigimon(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ITreatAsDigimonEffect treat && cardEffect.CanTrigger(null) && treat.IsDigimon(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ==================================================================================================
    // RESTRICTION / IMMUNITY joint predicates — AS-IS Permanent.CanSuspend/CanUnsuspend/CanBlock/
    // CanAttackTargetDigimon (Permanent.cs) + CardSource.CanNotEvolve (CardSource.cs:1291). Each scans
    // Players (the FULL roster, not turn-first) field permanents' + players' EffectList(None) for the
    // matching marker interface, gated by CanUse(null), joint predicate over (subject, counterpart).
    // Mirrors RestrictionScan.IsRestricted's registry scan (Headless/Runtime/RestrictionScan.cs) — UNIONED
    // by ContinuousRestrictionGate.JointResult, not called directly by callers.
    // ==================================================================================================

    // AS-IS Permanent.CanUnsuspend (Permanent.cs:1962-2006): ICanNotUnsuspendEffect && CanUse(null) &&
    // CanNotUnsuspend(this) over ALL players' field permanents + players.
    public static bool CanNotUnsuspend(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotUnsuspendEffect e && cardEffect.CanUse(null) && e.CanNotUnsuspend(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotUnsuspendEffect e && cardEffect.CanUse(null) && e.CanNotUnsuspend(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CanSuspend (Permanent.cs:3698-3739): ICanNotSuspendEffect && CanUse(null) &&
    // CanNotSuspend(this) over ALL players' field permanents + players.
    public static bool CanNotSuspend(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotSuspendEffect e && cardEffect.CanUse(null) && e.CanNotSuspend(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotSuspendEffect e && cardEffect.CanUse(null) && e.CanNotSuspend(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS CardSource.CanNotEvolve(Permanent) (CardSource.cs:1291-1349): ICanNotDigivolveEffect &&
    // CanUse(null) && CanNotEvolve(targetPermanent, this) over ALL players' field permanents + players (+
    // self, when this card is not itself a permanent — not modelled here, subject is always a live permanent
    // at the EvaluateDigivolve call site). `targetPermanent` = the under-card being digivolved (subject);
    // `this` = the digivolving CardSource (counterpart). Most call sites (DigivolveAction) evaluate the
    // TARGET-side restriction without yet knowing which specific material card is digivolving — when
    // <paramref name="sourceEntityId"/> is empty/unresolvable, fall back to the target's OWN card as a
    // structurally-valid (non-null) stand-in CardSource: a null-cardCondition grant (the common case — "cannot
    // be digivolved", no source filter) evaluates correctly either way; a source-conditional grant is
    // evaluated against the wrong source in this fallback (documented compromise, same tier as HasPierce's
    // no-hashtable presence check above).
    public static bool CanNotDigivolve(EngineContext context, HeadlessEntityId targetCardId, HeadlessEntityId sourceEntityId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent targetPermanent = BuildSubject(context, targetCardId);
        HeadlessEntityId effectiveSourceId = sourceEntityId.IsEmpty ? targetCardId : sourceEntityId;
        HeadlessPlayerId sourceOwner = context.CardInstanceRepository.TryGetInstance(effectiveSourceId, out var src) && src is not null
            ? src.OwnerId
            : default;
        var sourceCard = new CardSource(context, effectiveSourceId, sourceOwner, sourceOwner);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotDigivolveEffect e && cardEffect.CanUse(null) && e.CanNotEvolve(targetPermanent, sourceCard))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotDigivolveEffect e && cardEffect.CanUse(null) && e.CanNotEvolve(targetPermanent, sourceCard))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CanBlock (Permanent.cs:2123-2210): ICannotBlockEffect && CanUse(null) &&
    // CannotBlock(AttackingPermanent, this) over ALL players' field permanents (no immunity check — AS-IS
    // omits it in this region) + players (immunity-gated: `!TopCard.CanNotBeAffected(cardEffect)`).
    // Direction-agnostic: pass (attackerPermanent, blockerPermanent) — callers supply either order
    // (EvaluateBlock: subject=blocker, counterpart=attacker; EvaluateBeBlocked: subject=attacker,
    // counterpart=blocker).
    private static bool CannotBlockJoint(EngineContext context, Permanent attacker, Permanent blocker)
    {
        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotBlockEffect e && cardEffect.CanUse(null) && e.CannotBlock(attacker, blocker))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotBlockEffect e && cardEffect.CanUse(null) && e.CannotBlock(attacker, blocker)
                    && NotImmune(blocker, cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A missing counterpart (the common EvaluateBlock/EvaluateBeBlocked calling convention — the caller often
    // asks "is subject restricted at all", not against one specific pairing) falls back to the SUBJECT itself
    // as a structurally-valid stand-in Permanent for the missing role: an attackerCondition/defenderCondition
    // of null (the common case) accepts any counterpart including this placeholder; a condition genuinely
    // keyed on the counterpart's identity may misevaluate against it — documented compromise, same tier as
    // CanNotDigivolve's source fallback above.
    public static bool CanNotBlock(EngineContext context, HeadlessEntityId blockerId, HeadlessEntityId? attackerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        HeadlessEntityId effectiveAttackerId = attackerId is { IsEmpty: false } id ? id : blockerId;
        return CannotBlockJoint(context, BuildSubject(context, effectiveAttackerId), BuildSubject(context, blockerId));
    }

    public static bool CanNotBeBlocked(EngineContext context, HeadlessEntityId attackerId, HeadlessEntityId? blockerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        HeadlessEntityId effectiveBlockerId = blockerId is { IsEmpty: false } id ? id : attackerId;
        return CannotBlockJoint(context, BuildSubject(context, attackerId), BuildSubject(context, effectiveBlockerId));
    }

    // AS-IS Permanent.CanAttackTargetDigimon (Permanent.cs:2214-2373, the "cannot attack THIS defender"
    // region:2252-2301): ICanNotAttackTargetDefendingPermanentEffect && CanUse(null) &&
    // CanNotAttackTargetDefendingPermanent(this=attacker, Defender) over ALL players' field permanents +
    // players, immunity-gated (`!TopCard.CanNotBeAffected(cardEffect1)`, TopCard = the ATTACKER's). A null
    // Defender (blanket "cannot attack anything") is passed through verbatim — the interface's own predicate
    // decides whether a null defender matches.
    private static bool CannotAttackJoint(EngineContext context, Permanent attacker, Permanent? defender)
    {
        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotAttackTargetDefendingPermanentEffect e && cardEffect.CanUse(null)
                        && e.CanNotAttackTargetDefendingPermanent(attacker, defender) && NotImmune(attacker, cardEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotAttackTargetDefendingPermanentEffect e && cardEffect.CanUse(null)
                    && e.CanNotAttackTargetDefendingPermanent(attacker, defender) && NotImmune(attacker, cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool CanNotAttack(EngineContext context, HeadlessEntityId attackerId, HeadlessEntityId? defenderId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent? defender = defenderId is { IsEmpty: false } id ? BuildSubject(context, id) : null;
        return CannotAttackJoint(context, BuildSubject(context, attackerId), defender);
    }

    public static bool CanNotBeAttacked(EngineContext context, HeadlessEntityId defenderId, HeadlessEntityId? attackerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        if (attackerId is not { IsEmpty: false } id)
        {
            return false;
        }

        return CannotAttackJoint(context, BuildSubject(context, id), BuildSubject(context, defenderId));
    }

    // AS-IS Permanent.ImmuneFromDPMinus(ICardEffect) (Permanent.cs:703-740): IImmuneFromDPMinusEffect &&
    // CanUse(null) && ImmuneFromDPMinus(this, cardEffect) over ALL players' field permanents + players.
    // <paramref name="causingSourceId"/> (RD-P6B-13, P7 FAILa-02 fix): the REAL causing/reducing effect's source
    // — a legacy NumericModifier's SourceEntityId — resolved via <see cref="BuildCausingEffectStandIn"/>, so a
    // conditional grant (cardEffectCondition: e.g. "immune to your OPPONENT's DP-minus") is evaluated against
    // the ACTUAL reducer's owner, not a stand-in. Absent/empty (the default) falls back to the PRE-EXISTING
    // presence-only compromise (the immunity effect itself stands in for the causing effect — correct only for
    // an unconditional/null cardEffectCondition grant; kept for the one remaining 2-arg call site,
    // ContinuousDpGate's "does any new-model immunity exist" fast gate, which has no per-modifier source to hand).
    public static bool HasImmuneFromDpMinus(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId causingSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        ICardEffect? explicitCause = causingSourceId.IsEmpty ? null : BuildCausingEffectStandIn(context, causingSourceId);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IImmuneFromDPMinusEffect e && cardEffect.CanUse(null) && e.ImmuneFromDPMinus(subject, explicitCause ?? cardEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IImmuneFromDPMinusEffect e && cardEffect.CanUse(null) && e.ImmuneFromDPMinus(subject, explicitCause ?? cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ==================================================================================================
    // Deletion / removal immunity — AS-IS Permanent.CanBeDestroyed:3186 / CanBeDestroyedByBattle:3233 /
    // CanBeDestroyedBySkill:3309, Permanent.CannotReturnToHand:744 / CannotReturnToLibrary:785 (RD-P6B-10/12,
    // P7 FAILa-01/04 + G9-053 fix). Each is expressed here as a "HasCanNot…" presence scan (true = the AS-IS
    // getter would return the PROTECTED/blocked outcome), so callers can OR it into their existing result.
    // ==================================================================================================

    // AS-IS Permanent.CanBeDestroyed (Permanent.cs:3186-3229): ICanNotBeDestroyedEffect && CanUse(null) &&
    // CanNotBeDestroyed(this) over Players_ForTurnPlayer field permanents + players. (The factory's own
    // PermanentCondition already embeds the `!TopCard.CanNotBeAffected` immunity check — see
    // CardEffectFactory/CanNotBeDeleted.cs — so, matching AS-IS, no separate NotImmune call here.)
    public static bool HasCanNotBeDestroyed(EngineContext context, HeadlessEntityId cardId)
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
                    if (cardEffect is ICanNotBeDestroyedEffect e && cardEffect.CanUse(null) && e.CanNotBeDestroyed(subject))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeDestroyedEffect e && cardEffect.CanUse(null) && e.CanNotBeDestroyed(subject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CanBeDestroyedByBattle (Permanent.cs:3233-3305), the ICanNotBeDestroyedByBattleEffect scan
    // half only (the leading `!CanBeDestroyed()` check is the caller's separate HasCanNotBeDestroyed union —
    // BattleDeletionGate ORs both, battle-only per this method's caller, matching the AS-IS
    // preventBattleDeletion-vs-effect-delete split documented on BattleDeletionGate.PreventBattleDeletionKey).
    // Scope: Players_ForTurnPlayer field permanents + FACE-UP security + players. A candidate's own predicate
    // may dereference a null attacker/defender when called outside an active battle (AS-IS card predicates are
    // unguarded) — treated as "condition not met", mirroring BattleDeletionGate.EvaluateBattleCondition.
    public static bool HasCanNotBeDestroyedByBattle(
        EngineContext context, HeadlessEntityId cardId, HeadlessEntityId attackerId, HeadlessEntityId defenderId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        Permanent? attacker = attackerId.IsEmpty ? null : BuildSubject(context, attackerId);
        Permanent? defender = defenderId.IsEmpty ? null : BuildSubject(context, defenderId);
        CardSource? defendingCard = defender?.TopCard;

        bool Matches(ICardEffect cardEffect)
        {
            try
            {
                return cardEffect is ICanNotBeDestroyedByBattleEffect e && cardEffect.CanUse(null)
                    && e.CanNotBeDestroyedByBattle(subject, attacker!, defender!, defendingCard!);
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (Matches(cardEffect))
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
                    if (Matches(cardEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (Matches(cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CanBeDestroyedBySkill (Permanent.cs:3309-3365): TopCard.CanNotBeAffected(cardEffect) first
    // (immune to the DELETING effect itself), then the ICanNotBeDestroyedBySkillEffect scan (Players_ForTurnPlayer
    // field permanents + players) against the REAL causing effect (RD-P6B-13 stand-in built from
    // <paramref name="causingSourceId"/>). The separate general CanBeDestroyed() check is the caller's own
    // HasCanNotBeDestroyed union (MatchStateMutationSink ORs both).
    public static bool HasCanNotBeDestroyedBySkill(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId causingSourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        if (subject.TopCard is null)
        {
            return false;
        }

        ICardEffect causingEffect = BuildCausingEffectStandIn(context, causingSourceId);
        if (subject.TopCard.CanNotBeAffected(causingEffect.EffectSourceCard?.InstanceId))
        {
            return true;
        }

        foreach (Player player in ScanPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICanNotBeDestroyedBySkillEffect e && cardEffect.CanUse(null) && e.CanNotBeDestroyedBySkill(subject, causingEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICanNotBeDestroyedBySkillEffect e && cardEffect.CanUse(null) && e.CanNotBeDestroyedBySkill(subject, causingEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CannotReturnToHand(ICardEffect) (Permanent.cs:744-781): ICannotReturnToHandEffect &&
    // CanUse(null) && CannotReturnToHand(this, cardEffect) over ALL players' field permanents + players, against
    // the REAL causing effect (RD-P6B-13 stand-in).
    public static bool HasCannotReturnToHand(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId causingSourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        ICardEffect causingEffect = BuildCausingEffectStandIn(context, causingSourceId);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotReturnToHandEffect e && cardEffect.CanUse(null) && e.CannotReturnToHand(subject, causingEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotReturnToHandEffect e && cardEffect.CanUse(null) && e.CannotReturnToHand(subject, causingEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS Permanent.CannotReturnToLibrary(ICardEffect) (Permanent.cs:785-822): ICannotReturnToLibraryEffect &&
    // CanUse(null) && CannotReturnToLibrary(this, cardEffect) over ALL players' field permanents + players,
    // against the REAL causing effect (RD-P6B-13 stand-in).
    public static bool HasCannotReturnToLibrary(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId causingSourceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Permanent subject = BuildSubject(context, cardId);
        ICardEffect causingEffect = BuildCausingEffectStandIn(context, causingSourceId);

        foreach (Player player in ScanAllPlayers(context))
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is ICannotReturnToLibraryEffect e && cardEffect.CanUse(null) && e.CannotReturnToLibrary(subject, causingEffect))
                    {
                        return true;
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is ICannotReturnToLibraryEffect e && cardEffect.CanUse(null) && e.CannotReturnToLibrary(subject, causingEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Dispatch bridge used by <see cref="Headless.Effects.MatchStateMutationSink.IsRestrictedFromCause"/>
    /// to UNION the new-model cause-conditional interface scans (<see cref="HasCanNotBeDestroyedBySkill"/>,
    /// <see cref="HasCannotReturnToHand"/>, <see cref="HasCannotReturnToLibrary"/>) alongside the existing
    /// registry-based <c>RestrictionScan.IsRestricted</c> — mirrors <see cref="IsRestrictedNewModel"/> but for the
    /// ICardEffect-typed (not CardSource-joint) interfaces. A <paramref name="kind"/> with no ported cause-scan
    /// returns false (the registry path still serves it).</summary>
    public static bool IsRestrictedByCauseNewModel(EngineContext context, string kind, HeadlessEntityId subjectId, HeadlessEntityId causingSourceId) => kind switch
    {
        RestrictionHelpers.CannotBeDeletedBySkillKey => HasCanNotBeDestroyedBySkill(context, subjectId, causingSourceId),
        RestrictionHelpers.CannotReturnToHandKey => HasCannotReturnToHand(context, subjectId, causingSourceId),
        RestrictionHelpers.CannotReturnToDeckKey => HasCannotReturnToLibrary(context, subjectId, causingSourceId),
        _ => false,
    };

    /// <summary>Dispatch bridge used by <see cref="Headless.Runtime.ContinuousRestrictionGate.JointResult"/> to
    /// UNION the new-model interface scan alongside the existing registry-based <c>RestrictionScan.IsRestricted</c>
    /// (mirrors the keyword/DP/SAttack union pattern above). A <paramref name="kind"/> with no ported
    /// interface scan returns false (the registry path still serves it).</summary>
    public static bool IsRestrictedNewModel(EngineContext context, string kind, HeadlessEntityId subjectId, HeadlessEntityId? counterpartId) => kind switch
    {
        RestrictionHelpers.CannotUnsuspendKey => CanNotUnsuspend(context, subjectId),
        RestrictionHelpers.CannotSuspendKey => CanNotSuspend(context, subjectId),
        RestrictionHelpers.CannotDigivolveKey => CanNotDigivolve(context, subjectId, counterpartId ?? default),
        RestrictionHelpers.CannotBlockKey => CanNotBlock(context, subjectId, counterpartId),
        RestrictionHelpers.CannotBeBlockedKey => CanNotBeBlocked(context, subjectId, counterpartId),
        RestrictionHelpers.CannotAttackKey => CanNotAttack(context, subjectId, counterpartId),
        RestrictionHelpers.CannotBeAttackedKey => CanNotBeAttacked(context, subjectId, counterpartId),
        _ => false,
    };

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
        "Iceclad" => HasIceclad(context, cardId),
        "Raid" => HasRaid(context, cardId),
        "Retaliation" => HasRetaliation(context, cardId),
        "Ascension" => HasAscension(context, cardId),
        "Fortitude" => HasFortitude(context, cardId),
        "Blitz" => HasBlitz(context, cardId),
        "Evade" => HasEvade(context, cardId),
        "MindLink" => HasMindLink(context, cardId),
        "Barrier" => HasBarrier(context, cardId),
        "Alliance" => HasAlliance(context, cardId),
        "Collision" => HasCollision(context, cardId),
        "Partition" => HasPartition(context, cardId),
        "Scapegoat" => HasScapegoat(context, cardId),
        "Vortex" => HasSelfEndTurnKeyword(context, cardId, "Vortex"),
        "VortexCanAttackPlayers" => HasVortexCanAttackPlayers(context, cardId),
        "Overclock" => HasSelfEndTurnKeyword(context, cardId, "Overclock"),
        "Execute" => HasSelfEndTurnKeyword(context, cardId, "Execute"),
        "Save" => HasSave(context, cardId),
        "Armor Purge" => HasArmorPurge(context, cardId),
        "Decode" => HasDecode(context, cardId),
        "Fragment" => HasFragment(context, cardId),
        "Decoy" => HasDecoy(context, cardId),
        "Progress" => HasProgress(context, cardId),
        "TreatAsDigimon" => HasTreatAsDigimon(context, cardId),
        _ => false,
    };
}
