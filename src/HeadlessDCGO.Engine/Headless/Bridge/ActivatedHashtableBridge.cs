// (EFFECT-MODEL REBUILD / P6 stage A, substrate translation layer — NOT an AS-IS file)
//
// AS-IS threads ONE Hashtable object from the emit site (`StackSkillInfos(hashtable, timing)`) through the
// collect gate (`CanTrigger(hashtable)`, AutoProcessing.cs:770-887) → the per-pass gate
// (`CanActivate(hashtable)`, MultipleSkills.cs:122/366) → the body (`Activate(hashtable)`,
// AutoProcessing.cs:1063-1088). The mirror's driving artifact is a queued `GameEvent`; this bridge rebuilds
// the AS-IS per-timing payload from the event (+ live state) so the NEW-model gates/bodies read the exact
// AS-IS key contract (HashtableSetting.cs write side ↔ GetFromHashtable/CanUseEffects read side).
//
// EVERY shape below cites its AS-IS emit anchor. A timing NOT yet mapped returns null — the AS-IS null-payload
// timings are listed explicitly; the rest are logged design items (rebuild_p6_stageA_notes.md §5):
//   * P6A-HT-ENTERFIELD — OnEnterFieldAnyone/WhenDigivolving use the AS-IS CHECK builders
//     (OnPlayCheckHashtableOfCard / WhenDigivolvingCheckHashtableOfCard, HashtableSetting.cs:224/244 — the
//     same shapes AS-IS itself gates IsOnPlay/IsWhenDigivolving with); the FULL play payload
//     (OnEnterFieldHashtable: evoRoots/evoRootTops/Root/oldLevels, HashtableSetting.cs:146) needs
//     OnEnterFieldHashtableParams threaded from PlayCardAction/DigivolveAction.
//   * P6A-HT-USEOPTION — {Card} only; the AS-IS payload also carries Root + Cost (CardController.cs:1754-1759).
//   * P6A-HT-DIGISOURCE — OnDigivolutionCardDiscarded ({CardEffect, Permanent, DiscardedCards},
//     CardController.cs:5206-5211) / OnDigivolutionCardReturnToDeckBottom ({Permanent, DeckBottomCards,
//     CardEffect}, CardController.cs:5388-5393): the discarded/returned SOURCE list is not yet threaded on the
//     mirror event; mapped minimally (Permanent = subject's permanent, list = [subject]).
//   * P6A-HT-ENDBATTLE — the battle-result payload (winner/loser permanents, CardController.cs:4700-4718).
//   * P6A-HT-SECURITY — the [Security] skill activation payload (security-check flow).
//   * P6A-HT-CAUSE — AS-IS payloads carry the causing ICardEffect object; the mirror event carries only the
//     cause EFFECT-SOURCE id. A minimal "cause stub" ICardEffect with EffectSourceCard = that card preserves
//     the two data points AS-IS gates read (CardEffect != null; CardEffect.EffectSourceCard.*).

// (C3 SUBSTRATE→HEADLESS) Relocated from Assets/Scripts/Script/CardEffectCommons/ to Headless/Bridge/ — this is a
// substrate translation layer (GameEvent → AS-IS Hashtable payload), NOT an AS-IS game-rule file; it belongs in
// the Headless substrate beside the other bridges (peer of BareCauseEffect, EngineContext, GManagerBridge).
namespace HeadlessDCGO.Engine.Headless.Bridge;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public static class ActivatedHashtableBridge
{
    /// <summary>Rebuild the AS-IS <c>StackSkillInfos</c> payload for <paramref name="timing"/> from the mirror
    /// driving event. <paramref name="card"/> is the REACTING card being scanned (the AS-IS fallback subject
    /// for the direct — non-broadcast — paths, e.g. an [On Play] resolution of the entering card itself).
    /// Null = the AS-IS null-payload timings (and the not-yet-mapped ones, see file header).</summary>
    public static Hashtable? Build(EngineContext context, EffectTiming timing, GameEvent? drivingEvent, CardSource card)
    {
        switch (timing)
        {
            // AS-IS passes NULL verbatim: OnEndTurn (AutoProcessing.cs:699), OnStartTurn
            // (TurnStateMachine.cs:564), OnStartMainPhase (TurnStateMachine.cs:905), AfterEffectsActivate
            // (ICardEffect.cs:1283), RulesTiming (AutoProcessing.cs:134). Declared [Main] skills also run with
            // null (TurnStateMachine.cs:1178-1192: CanUse(null) → ActivateEffectProcess(effect, null)).
            case EffectTiming.OnEndTurn:
            case EffectTiming.OnStartTurn:
            case EffectTiming.OnStartMainPhase:
            case EffectTiming.AfterEffectsActivate:
            case EffectTiming.RulesTiming:
            case EffectTiming.OnDeclaration:
                return null;

            // {AttackingPermanent, CardEffect} — the attack-declaration EffectHashtable
            // (AttackProcess.cs:98 = CardEffectCommons.OnAttackCheckHashtableOfPermanent, HashtableSetting.cs:325),
            // re-broadcast at OnAllyAttack (AttackProcess.cs:197) and OnEndAttack (AttackProcess.cs:480).
            // OnCounterTiming uses the same shape (CounterEffectHashtable, AttackProcess.cs:99/266/285).
            case EffectTiming.OnUseAttack:
            case EffectTiming.OnAllyAttack:
            case EffectTiming.OnEndAttack:
            case EffectTiming.OnCounterTiming:
                return CardEffectCommons.OnAttackCheckHashtableOfPermanent(SubjectPermanent(context, drivingEvent, card), null);

            // {AttackingPermanent, DefendingPermanent, CardEffect} (+IsBlock on a block) — AS-IS
            // AttackProcess.SwitchDefender (AttackProcess.cs:536-545). Mirror event subject = the ATTACKER
            // (BlockTiming.cs / RaidAttackSwitch.cs emits); the defender is the attack pipeline's current target.
            case EffectTiming.OnAttackTargetChanged:
            case EffectTiming.OnBlockAnyone:
            {
                Permanent attacker = SubjectPermanent(context, drivingEvent, card);
                Permanent? defender = GManager.instance?.attackProcess.DefendingPermanent;
                Hashtable switchHashtable = new Hashtable()
                {
                    { "AttackingPermanent", attacker },
                    { "DefendingPermanent", defender },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                };
                if (timing == EffectTiming.OnBlockAnyone)
                {
                    switchHashtable.Add("IsBlock", true);
                }

                return switchHashtable;
            }

            // OnDeletionHashtable — "[On Deletion] / permanent leaves / returned to hand" share the one builder
            // (HashtableSetting.cs:96; emit sites CardController.cs:2692/3737 …). Subject = the leaving card.
            case EffectTiming.OnDestroyedAnyone:
            case EffectTiming.OnLeaveFieldAnyone:
            case EffectTiming.WhenRemoveField:
            case EffectTiming.OnRemovedField:
            case EffectTiming.WhenReturntoHandAnyone:
            case EffectTiming.WhenReturntoLibraryAnyone:
                return CardEffectCommons.OnDeletionHashtable(
                    new List<Permanent>() { SubjectPermanent(context, drivingEvent, card) },
                    CauseStub(context, drivingEvent), null, false);

            // {isEvolution:false/true, hashtables:[{Permanent}]} — the AS-IS CHECK shapes (HashtableSetting.cs:224/244);
            // full play payload = design item P6A-HT-ENTERFIELD (file header).
            case EffectTiming.OnEnterFieldAnyone:
                return CardEffectCommons.OnPlayCheckHashtableOfCard(SubjectCard(context, drivingEvent, card));
            case EffectTiming.WhenDigivolving:
                return CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(SubjectCard(context, drivingEvent, card));

            // {Card} — option [Main] (HashtableSetting.cs:264-271; the OnUseOption emit payload
            // {Card, Root, Cost} at CardController.cs:1754-1759 — Root/Cost = design item P6A-HT-USEOPTION).
            case EffectTiming.OptionSkill:
            case EffectTiming.OnUseOption:
                return CardEffectCommons.OptionMainCheckHashtable(SubjectCard(context, drivingEvent, card));

            // (EXEMPLAR-T1) the AS-IS pay-flow BeforePayCost window hashtable — WouldEnterFieldHashtable
            // (CardController.cs:591-599 → HashtableSetting.cs:178-194: {PayCost, Card, Root, isEvolution,
            // PlayCardClass, CardEffect, isJogress, Permanents}). The pump play path (PlayCardAction) resolves
            // a top-level HAND play: payCost=true, Root.Hand, isEvolution=false, no jogress/targets; the
            // PlayCardClass/cause seats have no pump analog on this path (null — AS-IS gates null-check them).
            case EffectTiming.BeforePayCost:
                return CardEffectCommons.WouldEnterFieldHashtable(
                    payCost: true,
                    card: SubjectCard(context, drivingEvent, card),
                    root: HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand,
                    isEvolution: false,
                    playCardClass: null!,
                    cardEffect: CauseStub(context, drivingEvent)!,
                    isJogress: false,
                    targetPermanents: null!);

            // (EXEMPLAR-T1) {Card, isFaceDown} — security skill (AS-IS CardController.cs:3991-3999:
            // `hashtable1 = { Card: brokenSecurityCard, isFaceDown }` gated by CanUse before stacking the
            // SkillInfo). AS-IS computes `isFaceDown = brokenSecurityCard.IsFlipped` (CardController.cs:3946):
            // IsFlipped is set true by SetReverse() (face-down, the security default) and false by SetFace()
            // (face-up). The mirror analog is the persistent SecurityFaceState flag — face-up == IsFaceUpInSecurity,
            // so isFaceDown == !IsFaceUpInSecurity. A face-down security card (the default) reports true, matching
            // the AS-IS default; a card placed face-up in security now correctly reports false.
            case EffectTiming.SecuritySkill:
            {
                CardSource securityCard = SubjectCard(context, drivingEvent, card);
                return new Hashtable()
                {
                    { "Card", securityCard },
                    { "isFaceDown", !SecurityFaceState.IsFaceUpInSecurity(context, securityCard.InstanceId) },
                };
            }

            // {Permanent} — a Digimon MOVED from breeding to battle area (CardObjectController.cs:1105-1111).
            case EffectTiming.OnMove:
                return new Hashtable() { { "Permanent", SubjectPermanent(context, drivingEvent, card) } };

            // {Permanents, IsBlock} (+CardEffect when effect-driven) — suspend (CardController.cs:5636-5648).
            case EffectTiming.OnTappedAnyone:
            {
                Hashtable tappedHashtable = new Hashtable()
                {
                    { "Permanents", new List<Permanent>() { SubjectPermanent(context, drivingEvent, card) } },
                    { "IsBlock", false },
                };
                ICardEffect tapCause = CauseStub(context, drivingEvent);
                if (tapCause != null)
                {
                    tappedHashtable.Add("CardEffect", tapCause);
                }

                return tappedHashtable;
            }

            // {CardEffect, Permanents} — unsuspend (CardController.cs:5746-5754).
            case EffectTiming.OnUnTappedAnyone:
                return new Hashtable()
                {
                    { "CardEffect", CauseStub(context, drivingEvent) },
                    { "Permanents", new List<Permanent>() { SubjectPermanent(context, drivingEvent, card) } },
                };

            // {Player, SkillInfo, CardEffect} — security loss (IReduceSecurity, CardController.cs:5433-5444).
            // Subject = the removed security card; its owner = the losing player.
            case EffectTiming.OnLoseSecurity:
                return new Hashtable()
                {
                    { "Player", SubjectOwnerPlayer(context, drivingEvent, card) },
                    { "SkillInfo", null },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                };

            // {Player, CardSources:[card]} — security add, PER CARD (IAddSecurity, CardController.cs:5481-5489).
            case EffectTiming.OnAddSecurity:
                return new Hashtable()
                {
                    { "Player", SubjectOwnerPlayer(context, drivingEvent, card) },
                    { "CardSources", new List<CardSource>() { SubjectCard(context, drivingEvent, card) } },
                };

            // {Players, CardEffect, CardSources} — hand add (CardObjectController.cs:612-620). The mirror emits
            // one event per added card (batch-collapsed at collect), so the lists carry the event subject.
            case EffectTiming.OnAddHand:
                return new Hashtable()
                {
                    { "Players", new List<Player>() { SubjectOwnerPlayer(context, drivingEvent, card) } },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                    { "CardSources", new List<CardSource>() { SubjectCard(context, drivingEvent, card) } },
                };

            // {DiscardedCards, CardEffect} — the three discard timings share one shape
            // (OnDiscardHand CardController.cs:48-53 / OnDiscardSecurity :4369-4374 / OnDiscardLibrary :5807-5812).
            case EffectTiming.OnDiscardHand:
            case EffectTiming.OnDiscardSecurity:
            case EffectTiming.OnDiscardLibrary:
                return new Hashtable()
                {
                    { "DiscardedCards", new List<CardSource>() { SubjectCard(context, drivingEvent, card) } },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                };

            // {Permanent(host), CardEffect, Card(link), isFromDigimon} — Permanent.AddLinkCard
            // (Permanent.cs:1281-1287). Mirror event: subject = HOST, metadata linkCardId = the attached card.
            // isFromDigimon is not threaded on the mirror emit (documented latent, ActivatedBridgeTimings) — false.
            case EffectTiming.WhenLinked:
            {
                CardSource? linkCard = MetadataCard(context, drivingEvent, "linkCardId");
                return new Hashtable()
                {
                    { "Permanent", SubjectPermanent(context, drivingEvent, card) },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                    { "Card", linkCard },
                    { "isFromDigimon", false },
                };
            }

            // {Permanent(host), CardEffect, CardSources(added), isFromSameDigimon, isFromDigimon} —
            // Permanent.AddDigivolutionCardsTop/Bottom (Permanent.cs:1108-1115). Mirror event: subject = host,
            // metadata addedCardIds (CSV) + causeSourceId. isFrom* flags = F1-ADDDIGI-FROMFLAGS (latent) — false.
            case EffectTiming.OnAddDigivolutionCards:
            {
                return new Hashtable()
                {
                    { "Permanent", SubjectPermanent(context, drivingEvent, card) },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                    { "CardSources", MetadataCards(context, drivingEvent, "addedCardIds") },
                    { "isFromSameDigimon", false },
                    { "isFromDigimon", false },
                };
            }

            // Minimal mappings (design item P6A-HT-DIGISOURCE, file header): AS-IS payloads carry the whole
            // trashed/returned source LIST; the mirror event carries one subject.
            case EffectTiming.OnDigivolutionCardDiscarded:
                return new Hashtable()
                {
                    { "CardEffect", CauseStub(context, drivingEvent) },
                    { "Permanent", SubjectPermanent(context, drivingEvent, card) },
                    { "DiscardedCards", new List<CardSource>() { SubjectCard(context, drivingEvent, card) } },
                };
            case EffectTiming.OnDigivolutionCardReturnToDeckBottom:
                return new Hashtable()
                {
                    { "Permanent", SubjectPermanent(context, drivingEvent, card) },
                    { "DeckBottomCards", new List<CardSource>() { SubjectCard(context, drivingEvent, card) } },
                    { "CardEffect", CauseStub(context, drivingEvent) },
                };

            // Not yet mapped (design items, file header): OnEndBattle (P6A-HT-ENDBATTLE) —
            // (EXEMPLAR-T1) SecuritySkill now mapped above ({Card, isFaceDown} with the real SecurityFaceState
            // face flag) — WhenTopCardTrashed, OnSecurityCheck, the would-be windows, and any timing
            // without a live new-model reactor. Null keeps the AS-IS null-tolerant gates working; a gate that
            // READS a key from null hashtable surfaces loudly (NRE) rather than silently mis-gating.
            default:
                return null;
        }
    }

    /// <summary>The driving event's subject as a mirror <see cref="Permanent"/> (transient single-card view
    /// when off-field — the AS-IS <c>new Permanent(new List&lt;CardSource&gt;{card})</c> analog); falls back to
    /// the reacting card itself on a subject-less direct path.</summary>
    private static Permanent SubjectPermanent(EngineContext context, GameEvent? drivingEvent, CardSource card)
    {
        CardSource subject = SubjectCard(context, drivingEvent, card);
        return ICardEffect.ResolvePermanentOfThisCard(subject)
            ?? new Permanent(subject.Context, subject.InstanceId, subject.Owner);
    }

    /// <summary>The driving event's subject as a <see cref="CardSource"/>; the reacting card itself when the
    /// event carries no subject (direct paths — the card being resolved IS the event subject).</summary>
    private static CardSource SubjectCard(EngineContext context, GameEvent? drivingEvent, CardSource card)
    {
        if (drivingEvent?.Subject is HeadlessEntityId subject && !subject.IsEmpty && subject != card.InstanceId)
        {
            HeadlessPlayerId owner = OwnerOf(context, subject, card.Owner);
            return new CardSource(context, subject, owner, owner);
        }

        return card;
    }

    private static Player SubjectOwnerPlayer(EngineContext context, GameEvent? drivingEvent, CardSource card) =>
        new Player(context, SubjectCard(context, drivingEvent, card).Owner);

    private static CardSource? MetadataCard(EngineContext context, GameEvent? drivingEvent, string key)
    {
        if (drivingEvent is not null
            && drivingEvent.Metadata.TryGetValue(key, out object? raw)
            && raw is string value
            && !string.IsNullOrWhiteSpace(value))
        {
            var id = new HeadlessEntityId(value);
            HeadlessPlayerId owner = OwnerOf(context, id, default);
            return new CardSource(context, id, owner, owner);
        }

        return null;
    }

    private static List<CardSource> MetadataCards(EngineContext context, GameEvent? drivingEvent, string key)
    {
        var cards = new List<CardSource>();
        if (drivingEvent is not null
            && drivingEvent.Metadata.TryGetValue(key, out object? raw)
            && raw is string csv
            && !string.IsNullOrWhiteSpace(csv))
        {
            foreach (string value in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var id = new HeadlessEntityId(value);
                HeadlessPlayerId owner = OwnerOf(context, id, default);
                cards.Add(new CardSource(context, id, owner, owner));
            }
        }

        return cards;
    }

    /// <summary>(P6A-HT-CAUSE, file header) The causing effect as a minimal stub carrying the cause's SOURCE
    /// CARD — the two data points AS-IS gates read off the payload's CardEffect (non-null test + EffectSourceCard).
    /// Null when the event carries no cause id (a non-effect move), matching the AS-IS null-cause payloads.</summary>
    private static ICardEffect? CauseStub(EngineContext context, GameEvent? drivingEvent)
    {
        if (drivingEvent is null)
        {
            return null;
        }

        foreach (string key in CauseKeys)
        {
            if (drivingEvent.Metadata.TryGetValue(key, out object? raw) && raw is string value && !string.IsNullOrWhiteSpace(value))
            {
                var id = new HeadlessEntityId(value);
                HeadlessPlayerId owner = OwnerOf(context, id, default);
                var stub = new CauseStubEffect();
                // A CardSource requires a non-empty controller — an unresolvable cause (no live instance, hence no
                // owner) yields a SOURCE-LESS cause (matching BareCauseEffect.For), preserving the non-null
                // CardEffect the gates test without asserting on an empty owner.
                if (!owner.IsEmpty)
                {
                    var source = new CardSource(context, id, owner, owner);
                    stub.SetEffectSourceCard(source);
                    // (L1) AS-IS the causing effect is the SOURCE card's own effect, so its IsDigimonEffect /
                    // IsTamerEffect reflect the source card's type (a Digimon card's effect is a Digimon effect).
                    // The cause-type gates (e.g. BT15_083 CardEffectCondition: cause.IsDigimonEffect) read these.
                    stub.SetIsDigimonEffect(source.IsDigimon);
                    stub.SetIsTamerEffect(source.IsTamer);
                }

                return stub;
            }
        }

        return null;
    }

    private static readonly string[] CauseKeys =
    {
        MatchStateMutationSink.DiscardCauseEffectIdKey,
        MatchStateMutationSink.AddHandCauseEffectIdKey,
        "causeSourceId",
    };

    private sealed class CauseStubEffect : ICardEffect
    {
    }

    private static HeadlessPlayerId OwnerOf(EngineContext context, HeadlessEntityId id, HeadlessPlayerId fallback) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            ? instance.OwnerId
            : fallback;
}
