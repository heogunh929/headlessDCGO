// (EFFECT-MODEL REBUILD / bridge W3) AS-IS-signature `Task` overloads for the PLAY-family mutation helpers
// whose AS-IS home is the monolith `DCGO/Assets/Scripts/Script/CardEffectCommons.cs` (same sibling-partial
// rationale as ProcessAccordingToResultBridge.cs — the mirror's own `Script/CardEffectCommons.cs` holds the
// substrate translations and is out of bounds for this batch):
//   - PlayPermanentCards        (AS-IS :23, 977 card calls — highest raw count of the whole 91-row set)
//   - PlayOptionCards           (AS-IS :59, 43 calls — NO-MIRROR row, implemented imperatively here)
//   - PlaceDelayOptionCards     (AS-IS :113, 182 calls — AS-IS `SelectCardEffect.Root` overload)
//   - PlayToken named family    (AS-IS :140-420, 14 helpers)
// plus the wrapper-side reproduction of the AS-IS `cardEffect`-gated play filtering the substrate drops
// (`CanPlayAsNewPermanent(cardEffect: …)` → `CanPlayCardTargetFrame` → `CanEnterField(cardEffect)`, the
// ICanNotPutFieldEffect scan — see CanEnterFieldByEffect below).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

public static partial class CardEffectCommons
{
    #region Play cards as new permanents (AS-IS CardEffectCommons.cs:23-53)

    /// <summary>(BRIDGE W3) AS-IS <c>PlayPermanentCards</c> — AS-IS-signature overload. The AS-IS body filters
    /// the given list by <c>CanPlayAsNewPermanent(cardSource, payCost, cardEffect: activateClass,
    /// isBreedingArea, fixedCost)</c> BEFORE building the PlayCardClass; the substrate overload re-runs that
    /// filter but with <c>cardEffect: null</c> (its <c>CanPlayAsNewPermanent</c> documents the discard), which
    /// silently skips the AS-IS <c>CanEnterField(cardEffect)</c> "can't be played (by effects)" scan. Per
    /// §11.11 rule 4 this wrapper reproduces that filtering WRAPPER-SIDE (<see cref="CanEnterFieldByEffect"/>)
    /// and only then delegates to the verified substrate play path (whose own null-effect re-filter passes a
    /// superset, so the wrapper-side filter is the effective one).</summary>
    public static async Task PlayPermanentCards(
        List<CardSource> cardSources, ICardEffect activateClass, bool payCost, bool isTapped,
        SelectCardEffect.Root root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)
    {
        // AS-IS guard (:25).
        if (cardSources == null)
        {
            return;
        }

        // AS-IS filter chain (:27-34): null filter + CanPlayAsNewPermanent(cardEffect: activateClass, ...).
        // The substrate CanPlayAsNewPermanent models the cost/option/frame halves (isBreedingArea has no
        // frame model — documented there); the cardEffect half is reproduced by CanEnterFieldByEffect.
        List<CardSource> playable = cardSources
            .Where(cardSource => cardSource != null)
            .Where(cardSource => CanPlayAsNewPermanent(cardSource, payCost, activateClass, isPlayOption: false, fixedCost: fixedCost)
                              && CanEnterFieldByEffect(cardSource, activateClass))
            .ToList();

        if (playable.Count == 0)
        {
            return;
        }

        await PlayPermanentCards(
            playable, activateClass?.EffectSourceCard!, payCost, isTapped,
            MapRootToChoiceZone(root), activateETB, isBreedingArea, fixedCost).ConfigureAwait(false);
    }

    #endregion

    #region Play option cards (AS-IS CardEffectCommons.cs:59-109 — NO-MIRROR row, imperative implementation)

    /// <summary>(BRIDGE W3) AS-IS <c>PlayOptionCards</c> — plays every playable card of a PRE-GIVEN list as an
    /// Option (unlike the mirror's <c>PlayOptionCardEffect</c>, which runs its own zone select). AS-IS body:
    /// filter by <c>!CanNotPlayThisOption</c> (mirrored by the same two substrate scans the effect-driven
    /// option-play path uses — CanNotPlayOptionScan regions ①②③ + the colour requirement), optionally register
    /// the until-turn-end "place the used Option on top of security" hook (<paramref name="setAddSecurityEndOption"/>),
    /// then play each card cost-optionally. Play flow per card mirrors the VERIFIED effect-driven option play
    /// (ActivatedEffectResolver's PlayOptionCardEffect branch): pay (when <paramref name="payCost"/>), move the
    /// option to the trash (headless OptionActivate order: trash-before-resolve), emit OnUseOption, resolve its
    /// [Main] (OptionSkill) effects, then — when the security-end hook is armed — move it from the trash to the
    /// TOP of security face down through the sink's AddToSecurity route (which applies the central AS-IS
    /// <c>CanAddSecurity</c> gate, mirroring PlaceToSecurityEffect's <c>CanResolveCondition</c>).
    /// <c>playCard.SetShowEffect()</c> is UI-only (elided).</summary>
    public static async Task PlayOptionCards(
        List<CardSource> cardSources, ICardEffect activateClass, bool payCost, SelectCardEffect.Root root,
        bool setAddSecurityEndOption = false)
    {
        // AS-IS guard (:62).
        if (cardSources == null)
        {
            return;
        }

        CardSource effectSourceCard = activateClass?.EffectSourceCard!;

        // AS-IS filter (:66-68): null + !CanNotPlayThisOption. The mirror models CanNotPlayThisOption as
        // CanNotPlayOptionScan (regions ①②③) AND !MatchColorRequirement (OptionColorRequirement) — the exact
        // pair the effect-driven option-play path applies (PlayOptionCardEffect.BuildRequest, E3-P1-1).
        List<CardSource> playable = cardSources
            .Where(cardSource => cardSource != null)
            .Where(cardSource => !CanNotPlayOptionScan.CanNotPlay(cardSource.Context, cardSource.Owner, cardSource.InstanceId)
                              && OptionColorRequirement.Matches(cardSource.Context, cardSource.Owner, cardSource.InstanceId))
            .ToList();

        if (playable.Count == 0)
        {
            return;
        }

        EngineContext context = playable[0].Context;
        ChoiceZone sourceZone = MapRootToChoiceZone(root);

        // AS-IS :81-89: the setAddSecurityEndOption hook (UntilEachTurnEndEffects + PlaceToSecurityEffect,
        // toTop: true, face down) is armed only while this play runs and only when activateClass != null; it
        // redirects each used Option's post-use placement from the trash to the top of security.
        bool armSecurityEnd = setAddSecurityEndOption && activateClass != null;

        foreach (CardSource card in playable)
        {
            // Cost (AS-IS: PlayCardClass payCost — resolved play cost through the modifier pipeline; a card
            // whose cost cannot be paid fails its play and is skipped, AS-IS endPlayCard).
            if (payCost)
            {
                int baseCost = context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? inst) && inst is not null
                    && context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                    ? def.PlayCost ?? 0
                    : 0;
                int cost = Math.Max(0, card.GetPayingCostWithBaseCost(baseCost, root, targetPermanents: null));
                if (!context.MemoryController.CanPay(cost))
                {
                    continue;
                }

                if (cost > 0)
                {
                    var paySink = NewSink(context);
                    paySink.Apply(new EffectMutation(
                        MatchStateMutationSink.AddMemoryKind,
                        effectSourceCard?.InstanceId ?? card.InstanceId,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [MatchStateMutationSink.PlayerIdKey] = card.Owner.Value,
                            [MatchStateMutationSink.AmountKey] = -cost,
                        }));
                    await paySink.FlushAsync().ConfigureAwait(false);
                }
            }

            // Use flow — AS-IS PlayOptionCards routes each option through `new PlayCardClass(...).PlayCard()`
            // (CardEffectCommons.cs:72-92), whose option half is `UseOptionClass.UseOption()` (CardController.cs
            // :1722-1786): trash → OnUseOption WINDOW → resolve [Main]. (EXEMPLAR-T1, first Root.Trash consumer —
            // P_223 [On Play]) an option used FROM the trash is already resident there; AS-IS hops it through the
            // execution area and back (presentation), so the zone outcome is identity — the mirror move is skipped
            // (ZoneMover rejects From==To).
            if (sourceZone != ChoiceZone.Trash)
            {
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(card.Owner, card.InstanceId, sourceZone, ChoiceZone.Trash)).ConfigureAwait(false);
            }

            // (RD-EXT1-03) AS-IS opens the "when option is used" window INLINE via StackSkillInfos(OnUseOption)
            // + ActivateBackgroundEffects (UseOption, CardController.cs:1765-1767) — the SAME seat the manual pump
            // play uses (TurnFlowDriver → PlayCardClass → UseOptionClass, mirror CardController.cs:4277-4279). The
            // former bare TriggerEventEmitter.Emit(OnUseOption) did NOT stack the battle-area OnUseOption reactor
            // onto the pump's TriggeredSkillProcess drain, so an effect-driven option play never fired the owner's
            // [All Turns] OnUseOption skill (P_223's [Pipe Fox] token). AS-IS hashtable (CardController.cs:1754):
            // {Card, Root, Cost}.
            System.Collections.Hashtable useHashtable = new System.Collections.Hashtable
            {
                { "Card", card },
                { "Root", root },
                { "Cost", card.GetCostItself },
            };
            await GManager.instance.autoProcessing.StackSkillInfos(useHashtable, EffectTiming.OnUseOption).ConfigureAwait(false);
            await AutoProcessing.ActivateBackgroundEffects(useHashtable, EffectTiming.OnUseOption).ConfigureAwait(false);

            // The substrate ActivateMainOfOptionSide route: ONLY the [Main]-tagged OptionSkill effect.
            await ActivatedEffectResolver.ResolveAsync(
                context, card.InstanceId, card.Owner, EffectTiming.OptionSkill,
                effectFilter: ActivatedEffectResolver.IsMainOptionEffect).ConfigureAwait(false);

            // AS-IS hook resolution (GetCardEffect → PlaceToSecurityEffect(toTop:true), face down): the used
            // Option leaves the trash for the TOP of security; the sink's AddToSecurity route applies the
            // central CanAddSecurity gate (= PlaceToSecurityEffect's own CanResolveCondition).
            if (armSecurityEnd && ((IZoneStateReader)context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash).Contains(card.InstanceId))
            {
                var securitySink = NewSink(context);
                securitySink.Apply(new EffectMutation(
                    MatchStateMutationSink.AddToSecurityKind, effectSourceCard!.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value,
                    }));
                await securitySink.FlushAsync().ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Play Delay Option cards as new permanents (AS-IS CardEffectCommons.cs:113-135)

    /// <summary>(BRIDGE W3) AS-IS <c>PlaceDelayOptionCards</c> — the AS-IS <c>SelectCardEffect.Root</c>
    /// overload. NOTE (deliberate): <paramref name="root"/> carries NO default value here, unlike AS-IS —
    /// the substrate overload <c>PlaceDelayOptionCards(CardSource, ICardEffect?, ChoiceZone = Execution)</c>
    /// already accepts the two-argument AS-IS call shape, so adding a defaulted third parameter would make
    /// every 2-arg call ambiguous (CS0121). Two-arg AS-IS calls therefore bind to the substrate overload
    /// directly — whose body still discards <c>cardEffect</c> before <c>CanPlayAsNewPermanent</c> (pre-existing
    /// substrate gap, design item RD-W3-2); THIS overload applies the wrapper-side
    /// <see cref="CanEnterFieldByEffect"/> gate the AS-IS <c>CanPlayAsNewPermanent(cardEffect:, isPlayOption:
    /// true)</c> chain implies.</summary>
    public static async Task PlaceDelayOptionCards(CardSource card, ICardEffect cardEffect, SelectCardEffect.Root root)
    {
        if (card == null || !CanEnterFieldByEffect(card, cardEffect))
        {
            return;
        }

        await PlaceDelayOptionCards(card, cardEffect, MapRootToChoiceZone(root)).ConfigureAwait(false);
    }

    #endregion

    #region Play tokens (AS-IS CardEffectCommons.cs:140-420)

    // AS-IS `PlayToken(CEntity_Base tokenData, ICardEffect activateClass, bool isOwnerPermanent, bool
    // isTapped, int quantity = 1)` itself is NOT bridged: the mirror's `Script/CEntity_Base.cs` carries only
    // the CardColor enum (no CEntity_Base class), so the AS-IS signature cannot even be declared without a new
    // declaration error, and ZERO card files call PlayToken directly (grep over DCGO/Assets/Scripts/CardEffect,
    // --binary-files=text) — every card call goes through the 14 named helpers below. Design item RD-W3-3.
    //
    // The named wrappers add the AS-IS gates the substrate PlayToken documents as unmodeled:
    //   (1) the AS-IS field-CAPACITY check `card.Owner.fieldCardFrames.Count(empty && battleArea) >= quantity`
    //       (:149). Evidence for the frame count: DCGO/Assets/Scenes/BattleScene.unity — YourPermanentFrame/
    //       OpponentPermanentFrame each hold exactly 16 qualifying frame children ("カード枠1..16", each with the
    //       2 sub-objects Player.Start requires), so battle-area capacity = 16 permanents. AS-IS QUIRK KEPT:
    //       the capacity is checked on the EFFECT SOURCE owner's board (card.Owner) even when the token enters
    //       the OPPONENT'S board (isOwnerPermanent:false — Fujitsumon/Petrification).
    //   (2) the AS-IS `CanPlayAsNewPermanent(playCards[0], payCost:false, cardEffect: activateClass)` gate
    //       (:158) reduces (cost-free, non-option token) to "the TOKEN owner has an empty battle frame" +
    //       CanEnterField(activateClass). The empty-frame half is applied here against the same 16-frame
    //       capacity; the CanEnterField half CANNOT run wrapper-side (the token instance does not exist until
    //       the substrate creates it, and ICanNotPutFieldEffect's predicate receives the token CardSource) —
    //       design item RD-W3-4, explicitly not silent.
    private static bool CanPlayTokens(ICardEffect activateClass, bool isOwnerPermanent, int quantity)
    {
        // AS-IS guards (:142-144).
        if (activateClass?.EffectSourceCard == null || quantity <= 0)
        {
            return false;
        }

        CardSource card = activateClass.EffectSourceCard;
        EngineContext context = card.Context;
        var zones = (IZoneStateReader)context.ZoneMover;

        // (1) capacity on the effect source OWNER's board (AS-IS :149).
        if (BattleAreaFrameCount - zones.GetCards(card.Owner, ChoiceZone.BattleArea).Count < quantity)
        {
            return false;
        }

        // (2) empty-frame half of CanPlayAsNewPermanent on the TOKEN owner's board (AS-IS :158).
        HeadlessPlayerId tokenOwner = isOwnerPermanent ? card.Owner : OpponentOf(card);
        return !tokenOwner.IsEmpty &&
            BattleAreaFrameCount - zones.GetCards(tokenOwner, ChoiceZone.BattleArea).Count >= 1;
    }

    /// <summary>The AS-IS battle-area frame count (BattleScene.unity: 16 "カード枠" children per player's
    /// PermanentFrame parent — see the region comment above for the derivation).</summary>
    private const int BattleAreaFrameCount = 16;

    /// <summary>(BRIDGE W3) AS-IS <c>PlayDiaboromonToken</c> (:182).</summary>
    public static async Task PlayDiaboromonToken(ICardEffect activateClass, int quantity = 1)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity))
        {
            await PlayToken(TokenSpecs["Diaboromon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false, quantity).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayAmonToken</c> (:197).</summary>
    public static async Task PlayAmonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Amon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayUmonToken</c> (:211).</summary>
    public static async Task PlayUmonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Umon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayFujitsumonToken</c> (:225) — enters SUSPENDED; the only named helper
    /// whose board side is caller-chosen.</summary>
    public static async Task PlayFujitsumonToken(ICardEffect activateClass, bool isOwnerPermanent)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent, quantity: 1))
        {
            await PlayToken(TokenSpecs["Fujitsumon"], activateClass.EffectSourceCard, isOwnerPermanent, isTapped: true).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayGyuukimonToken</c> (:239).</summary>
    public static async Task PlayGyuukimonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Gyuukimon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayKoHagurumonToken</c> (:253).</summary>
    public static async Task PlayKoHagurumonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["KoHagurumon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayFamiliarToken</c> (:267).</summary>
    public static async Task PlayFamiliarToken(ICardEffect activateClass, int quantity = 1)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity))
        {
            await PlayToken(TokenSpecs["Familiar"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false, quantity).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlaySelfDeleteFamiliarToken</c> (:282).</summary>
    public static async Task PlaySelfDeleteFamiliarToken(ICardEffect activateClass, int quantity = 1)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity))
        {
            await PlayToken(TokenSpecs["SelfDeleteFamiliar"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false, quantity).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayVoleeZerdrucken</c> (:297).</summary>
    public static async Task PlayVoleeZerdrucken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["VoleeZerdrucken"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayUkaNoMitama</c> (:311).</summary>
    public static async Task PlayUkaNoMitama(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["UkaNoMitama"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayWarGrowlmonToken</c> (:325).</summary>
    public static async Task PlayWarGrowlmonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["WarGrowlmon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayTaomonToken</c> (:339).</summary>
    public static async Task PlayTaomonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Taomon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayRapidmonToken</c> (:353).</summary>
    public static async Task PlayRapidmonToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Rapidmon"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayPipeFox</c> (:367).</summary>
    public static async Task PlayPipeFox(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["PipeFox"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayAthoRenePorToken</c> (:381).</summary>
    public static async Task PlayAthoRenePorToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["AthoRenePor"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayHinukamuyToken</c> (:395).</summary>
    public static async Task PlayHinukamuyToken(ICardEffect activateClass)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: true, quantity: 1))
        {
            await PlayToken(TokenSpecs["Hinukamuy"], activateClass.EffectSourceCard, isOwnerPermanent: true, isTapped: false).ConfigureAwait(false);
        }
    }

    /// <summary>(BRIDGE W3) AS-IS <c>PlayPetrificationToken</c> (:409) — always the OPPONENT'S board (the
    /// AS-IS capacity quirk applies: capacity still checked on the effect source owner's board).</summary>
    public static async Task PlayPetrificationToken(ICardEffect activateClass, int quantity = 1)
    {
        if (CanPlayTokens(activateClass, isOwnerPermanent: false, quantity))
        {
            await PlayToken(TokenSpecs["Petrification"], activateClass.EffectSourceCard, isOwnerPermanent: false, isTapped: false, quantity).ConfigureAwait(false);
        }
    }

    #endregion

    #region Shared play-bridge plumbing (private)

    /// <summary>AS-IS <c>SelectCardEffect.Root</c> → substrate <see cref="ChoiceZone"/> (the same table the
    /// mirror SelectCardEffect keeps privately).</summary>
    private static ChoiceZone MapRootToChoiceZone(SelectCardEffect.Root root) => root switch
    {
        SelectCardEffect.Root.Library => ChoiceZone.Library,
        SelectCardEffect.Root.Trash => ChoiceZone.Trash,
        SelectCardEffect.Root.Clock => ChoiceZone.Clock,
        SelectCardEffect.Root.Security => ChoiceZone.Security,
        SelectCardEffect.Root.Hand => ChoiceZone.Hand,
        SelectCardEffect.Root.Recollection => ChoiceZone.Recollection,
        SelectCardEffect.Root.Execution => ChoiceZone.Execution,
        SelectCardEffect.Root.DigivolutionCards => ChoiceZone.DigivolutionCards,
        SelectCardEffect.Root.LinkedCards => ChoiceZone.LinkedCards,
        SelectCardEffect.Root.Custom => ChoiceZone.Custom,
        _ => ChoiceZone.None,
    };

    /// <summary>(BRIDGE W3) Wrapper-side reproduction of AS-IS <c>CardSource.CanEnterField(ICardEffect)</c>
    /// (CardSource.cs, the gate <c>CanPlayCardTargetFrame</c> applies on an EMPTY frame): scan for an active
    /// <see cref="ICanNotPutFieldEffect"/> that forbids putting <paramref name="cardSource"/> into play by
    /// <paramref name="cardEffect"/>. AS-IS scans three regions; the surfaces available pre-flip cover two:
    ///   ① field permanents' EffectList(None) — mirrored via each field permanent's TOP CARD effect list (the
    ///     AS-IS Permanent.EffectList aggregate's dominant source; inherited/granted-scope producers have no
    ///     pre-flip scan surface — folded into design item RD-W3-2);
    ///   ② players' EffectList(None) — NO pre-flip mirror surface (player-bucket grants live as registry
    ///     bindings, not scannable ICardEffect lists) — design item RD-W3-2, explicitly not silent;
    ///   ③ the card's OWN EffectList(None) when it is not on a permanent — exact.
    /// No ICanNotPutFieldEffect producer is registered anywhere in the mirror today (CanNotPutFieldClass has
    /// zero factory/card producers), so ①/③ scanning nothing is currently exact; the helper exists so the gate
    /// is structurally in place the moment the first producer card is ported.</summary>
    // (R4 S3c-d, 은퇴 원장 항7) The former WRAPPER-SIDE scan copy is retired — the AS-IS-position member
    // CardSource.CanEnterField (CardSource.cs, S3b) is the single owner of the ICanNotPutFieldEffect scan
    // (all three AS-IS regions, players included — the copy's region ② gap RD-W3-2 closes with it). No
    // ICanNotPutFieldEffect producer exists yet, so the rewire is behaviourally a no-op today.
    private static bool CanEnterFieldByEffect(CardSource cardSource, ICardEffect? cardEffect)
    {
        if (cardSource == null)
        {
            return false;
        }

        return cardSource.CanEnterField(cardEffect);
    }

    #endregion
}
