// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/Permanent.cs::Permanent.AddLinkCard / Permanent.RemoveLinkedCard / Permanent.LinkedCards / Permanent.LinkedMax / Permanent.LinkedDP@AddLinkCard:1237; RemoveLinkedCard:1306; 
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-1 Link) Link-card attach/detach for a Digimon permanent. Mirrors the AS-IS <c>Permanent</c> link
/// model (<c>LinkedCards</c> list, <c>LinkedDP</c>, <c>LinkedMax</c>, <c>AddLinkCard</c>/
/// <c>RemoveLinkedCard</c>) using the same off-field-storage pattern as
/// <see cref="DigivolutionStackHelpers"/>: linked cards move to <see cref="ChoiceZone.None"/> and are
/// tracked on the host permanent's metadata (<c>linkedCardIds</c>, ordered newest-first like the original
/// which inserts at index 0). The accumulated link DP is kept in <c>linkedDp</c> for the DP calculator.
///
/// Timing windows (F-6.9): the caller opens <see cref="TriggerTimings.WhenLinked"/> after an attach and
/// <see cref="TriggerTimings.OnLinkCardDiscarded"/> when linked cards are trashed — both emitted here via
/// the game-event queue when one is supplied.
/// </summary>
public static class LinkHelpers
{
    public const string LinkedCardIdsKey = "linkedCardIds";
    public const string LinkedDpKey = "linkedDp";
    public const string LinkedMaxKey = "linkedMax";
    public const string LinkDpKey = "linkDp";

    /// <summary>Default maximum number of link cards a Digimon can hold (AS-IS <c>LinkedMax</c> default 1).</summary>
    public const int DefaultLinkedMax = 1;

    /// <summary>The link cards currently attached to <paramref name="metadata"/>'s host (newest first).</summary>
    public static IReadOnlyList<HeadlessEntityId> ReadLinkedCardIds(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.TryGetValue(LinkedCardIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>(),
        };
    }

    /// <summary>The accumulated link DP on the host (sum of attached cards' LinkDP).</summary>
    public static int ReadLinkedDp(IReadOnlyDictionary<string, object?> metadata) =>
        ReadInt(metadata, LinkedDpKey) ?? 0;

    /// <summary>The host's link maximum (its <c>linkedMax</c> override, else <see cref="DefaultLinkedMax"/>).</summary>
    public static int ReadLinkedMax(IReadOnlyDictionary<string, object?> metadata) =>
        ReadInt(metadata, LinkedMaxKey) ?? DefaultLinkedMax;

    /// <summary>(M-4) The host's EFFECTIVE link maximum: its base <see cref="ReadLinkedMax"/> folded with
    /// continuous <c>linkedMaxDelta</c> modifiers (AS-IS ChangeLinkMax / ChangeSelfLinkMax) — previously
    /// registered but consumed by nothing.</summary>
    public static int ResolveLinkedMax(Bridge.EngineContext context, HeadlessEntityId hostId)
    {
        ArgumentNullException.ThrowIfNull(context);
        int baseMax = context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
            ? ReadLinkedMax(host.Metadata)
            : DefaultLinkedMax;

        // (RD-P6B-16 RETIRED — C5-1, 2026-07-24) The legacy linkedMaxDelta pre-fold that used to run here
        // (ContinuousScopeEvaluation.EvaluateForCard + ModifierHelpers.Evaluate over NumericModifierMetric.LinkedMax)
        // was an AS-IS-ABSENT union scaffold with ZERO producers: the "linkedMaxDelta" key
        // (ModifierHelpers.LinkedMaxDeltaKey) is READ by ReadSimpleModifiers but WRITTEN by nothing
        // (ChangeLinkMaxClass registers no binding — it is new-model IChangeLinkMaxEffect only), and the
        // EvaluateForCard result set is itself permanently empty (ApplicableEffects producer 0). With no producer
        // the pre-fold returned baseMax unchanged, so this retirement is BIT-IDENTICAL (legacyResolved == baseMax).
        // Restored to AS-IS 1:1: the sole path is the new-model FoldLinkedMax (AS-IS Permanent.LinkedMax). Same
        // precedent as ResolveLinkCost below (RD-P6B-16 / G-Link P2-③).
        return Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldLinkedMax(context, hostId, baseMax);
    }

    /// <summary>(M-4) The EFFECTIVE link cost: <paramref name="baseCost"/> folded with continuous
    /// <c>linkCostDelta</c> modifiers (AS-IS GrantedReduceLinkCost) — previously registered but consumed by
    /// nothing. Clamped to &gt;= 0.</summary>
    public static int ResolveLinkCost(
        Bridge.EngineContext context,
        HeadlessEntityId cardId,
        int baseCost,
        HeadlessEntityId targetPermanentId = default,
        Assets.Scripts.Script.SelectCardEffect.Root root = Assets.Scripts.Script.SelectCardEffect.Root.None)
    {
        ArgumentNullException.ThrowIfNull(context);

        // (RD-P6B-16 RETIRED — G-Link P2-③, 2026-07-23) The legacy linkCostDelta pre-fold that used to run here
        // (ContinuousScopeEvaluation.EvaluateForCard + ModifierHelpers.Evaluate over NumericModifierMetric.LinkCost)
        // was an AS-IS-ABSENT union scaffold: AS-IS CardSource.GetChangedLinkCost (CardSource.cs:3267-3331) is PURELY
        // the three-region IChangeLinkCostEffect scan — it has no legacy modifier fold. The scaffold had ZERO
        // producers: a whole-tree census finds the "linkCostDelta" key (ModifierHelpers.LinkCostDeltaKey) is READ at
        // ModifierHelpers.cs:503-505 but WRITTEN by nothing (GrantedReduceLinkCostClass / ChangeLinkCostClass
        // register no binding — they are new-model IChangeLinkCostEffect only). With no producer the pre-fold
        // returned `baseCost` unchanged, so this retirement is BIT-IDENTICAL (resolved == baseCost). Restored to
        // AS-IS 1:1: the sole cost path is the new-model FoldLinkCost. (EXEMPLAR-GLINK W3's RISK-3 negative
        // assertion — legacy fold leaves base intact — still holds; it exercises the primitives directly.)
        // targetPermanent + root are threaded so GetChangedLinkCost's AS-IS PermanentCondition / GetCost(root)
        // evaluate faithfully (defaults preserve the metadata-only callers).
        return Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldLinkCost(context, cardId, baseCost, targetPermanentId, root);
    }

    /// <summary>
    /// (AS-IS <c>Permanent.AddLinkCard</c>) Attach <paramref name="linkCardId"/> to <paramref name="hostId"/>:
    /// move the link card off-field, prepend it to the host's linked list, add its LinkDP, and open the
    /// WhenLinked window. Excess over the host's max is trashed first (AS-IS force-remove). Returns true
    /// when attached.
    /// </summary>
    public static async Task<bool> AddLinkCardAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId,
        ChoiceZone fromZone,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default,
        Bridge.EngineContext? context = null,
        HeadlessEntityId causeSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null ||
            !repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) || linkCard is null)
        {
            return false;
        }

        // (C1d RDW-02, WhenLinked isFromDigimon) AS-IS Permanent.AddLinkCard (Permanent.cs:1242-1247): isFromDigimon
        // == the linked card is currently a battle-area permanent whose stack has >=1 digivolution source. Evaluate
        // PRE-move (post-move the link card is off-field, so it would read false). No zone reader => false.
        bool linkIsFromDigimon =
            zoneMover is IZoneStateReader linkReader
            && linkReader.GetCards(linkCard.OwnerId, ChoiceZone.BattleArea).Contains(linkCardId)
            && linkCard.Metadata.TryGetValue(DigivolutionStackHelpers.SourceIdsKey, out object? linkSrcRaw)
            && linkSrcRaw is IEnumerable<string> linkSrc && linkSrc.Any();

        // (BT22_035) fromZone == None ⇒ the link card is ALREADY off-field (e.g. a just-detached digivolution
        // source): there is no physical move to make, and a None → None ZoneMoveRequest is rejected (both zones
        // abstract). Link cards are stored off-field (ChoiceZone.None) anyway, so skip the move.
        if (fromZone != ChoiceZone.None)
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(linkCard.OwnerId, linkCardId, fromZone, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
        }

        // (B-3 tuck reset) AS-IS AddLinkCard resets the attached card's per-turn use counts
        // (cardSource.cEntity_EffectController.InitUseCountThisTurn(), CardController.cs:3393 — after the
        // RemoveField that already Init()-reset a field-origin permanent's stack).
        // (R6-Da'-6 D3) per-card cap reset on the CEntity_EffectController store (OnceFlags twin retired).
        if (context is not null)
        {
            Assets.Scripts.Script.CardEffectCommons.CEntity_EffectControllerStore.ResetUseCountForCard(context, linkCardId);
        }

        // (MIG2 / DEF-S10, 2026-07-24) AS-IS AddLinkCard (Permanent.cs:1251-1257): overflow is resolved BEFORE
        // the attach. The AS-IS split is on LinkedMax:
        //   if (LinkedCards.Count >= LinkedMax) {
        //       if (LinkedMax > 1) RemoveLinkedCard(null, (LinkedCards.Count + 1) - LinkedMax);  // owner SELECTS
        //       else               RemoveLinkedCard(LinkedCards[0]);                              // silent oldest
        //   }
        // LinkedMax == 1 removes the current LinkedCards[0] SILENTLY (bare RemoveLinkedCard: trash, but NO
        // OnLinkCardDiscarded window and NO selection). LinkedMax > 1 runs RemoveLinkedCard(null, excess), whose
        // removeCount>0 branch opens a SelectCardEffect (Discard mode over LinkedCards, canNoSelect => false,
        // canEndNotMax => false ⇒ the owner picks EXACTLY `excess` cards to trash) and emits NO OnLinkCardDiscarded
        // (AS-IS "//TODO: Add event call if something was removed"). DEF-S10 aligns the >1 case to that owner
        // SELECTION (ChoiceProvider is the substrate translation of SelectCardEffect.Activate()); previously it
        // silently fell through to the post-attach oldest-first enforcement. There is still no max>1 witness
        // (every ported host is max-1), so this path is latent — the correction is structural. When no context /
        // choice provider is wired the >1 case falls back to oldest-first (documented divergence, not silent).
        {
            CardInstanceRecord preHost = repository.TryGetInstance(hostId, out CardInstanceRecord? refreshed) && refreshed is not null ? refreshed : host;
            IReadOnlyList<HeadlessEntityId> preLinked = ReadLinkedCardIds(preHost.Metadata);
            int preMax = context is not null ? ResolveLinkedMax(context, hostId) : ReadLinkedMax(preHost.Metadata);
            if (preLinked.Count >= preMax && preLinked.Count >= 1)
            {
                if (preMax > 1)
                {
                    int excess = (preLinked.Count + 1) - preMax;
                    await TrimLinkedOverflowByOwnerSelectionAsync(
                        repository, zoneMover, hostId, preLinked, excess, context, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await RemoveLinkCardAsync(
                        repository, zoneMover, hostId, preLinked[0], trash: true,
                        gameEventQueue: null, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Re-read host (the move may have touched state) and prepend (AS-IS insert at index 0).
        CardInstanceRecord current = repository.TryGetInstance(hostId, out CardInstanceRecord? latest) && latest is not null ? latest : host;
        List<string> linked = ReadLinkedCardIds(current.Metadata).Select(id => id.Value).ToList();
        linked.Insert(0, linkCardId.Value);
        int linkedDp = ReadLinkedDp(current.Metadata) + (ReadInt(linkCard.Metadata, LinkDpKey) ?? 0);
        repository.Upsert(current with { Metadata = WithLinked(current.Metadata, linked, linkedDp) });

        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenLinked, actor: current.OwnerId, subject: hostId,
                extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [SkillWindowSupply.WhenLinkedLinkCardIdKey] = linkCardId.Value,
                    // (C1d RDW-02) pre-computed AS-IS isFromDigimon so the DORMANT SkillWindowSupply can build the
                    // full {Permanent, CardEffect, Card, isFromDigimon} key set.
                    [SkillWindowSupply.WhenLinkedIsFromDigimonKey] = linkIsFromDigimon,
                    // (G-Link P2 risk-1) AS-IS Permanent.AddLinkCard stacks {"CardEffect", cardEffect} into the
                    // WhenLinked hashtable (Permanent.cs:1281-1290); thread the causing effect's SOURCE id so
                    // SkillWindowSupply.TryBuildWhenLinked rebuilds a SOURCE-ful BareCauseEffect (not the empty one)
                    // — every link is effect-driven, and the real cause reaches the WhenLinked gate.
                    ["causeSourceId"] = causeSourceId.Value,
                });
        }

        // AS-IS: if over max, force-trash the oldest excess link cards.
        await EnforceLinkedMaxAsync(repository, zoneMover, hostId, gameEventQueue, cancellationToken, context).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// (AS-IS <c>Permanent.RemoveLinkedCard</c>) Detach <paramref name="linkCardId"/> from
    /// <paramref name="hostId"/>: remove it from the linked list, subtract its LinkDP, optionally trash it
    /// (default), and open the OnLinkCardDiscarded window. Returns true when removed.
    /// </summary>
    public static async Task<bool> RemoveLinkCardAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId,
        bool trash = true,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> linked = ReadLinkedCardIds(host.Metadata).Select(id => id.Value).ToList();
        if (!linked.Remove(linkCardId.Value))
        {
            return false;
        }

        int linkDp = repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) && linkCard is not null
            ? ReadInt(linkCard.Metadata, LinkDpKey) ?? 0
            : 0;
        int linkedDp = Math.Max(0, ReadLinkedDp(host.Metadata) - linkDp);
        repository.Upsert(host with { Metadata = WithLinked(host.Metadata, linked, linkedDp) });

        if (trash && linkCard is not null)
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(linkCard.OwnerId, linkCardId, ChoiceZone.None, ChoiceZone.Trash),
                cancellationToken).ConfigureAwait(false);
        }

        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.OnLinkCardDiscarded, actor: host.OwnerId, subject: hostId);
        }

        return true;
    }

    /// <summary>(DEF-S10) AS-IS <c>Permanent.RemoveLinkedCard(null, removeCount)</c> (Permanent.cs:1321-1345):
    /// the host's owner SELECTS exactly <paramref name="excess"/> of its currently-linked cards to trash
    /// (SelectCardEffect Discard mode over <c>LinkedCards</c>, <c>canNoSelect => false</c>,
    /// <c>canEndNotMax => false</c>). No <c>OnLinkCardDiscarded</c> window is opened (AS-IS
    /// "//TODO: Add event call if something was removed"). The selection is the substrate translation via
    /// <see cref="IChoiceProvider"/>; each picked card is then removed with the bare (window-less)
    /// <see cref="RemoveLinkCardAsync"/>. When no context / choice provider is wired, falls back to
    /// oldest-first (documented divergence — no max&gt;1 witness exists).</summary>
    private static async Task TrimLinkedOverflowByOwnerSelectionAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        IReadOnlyList<HeadlessEntityId> currentLinked,
        int excess,
        Bridge.EngineContext? context,
        CancellationToken cancellationToken)
    {
        if (excess <= 0 || currentLinked.Count == 0)
        {
            return;
        }

        int pick = Math.Min(excess, currentLinked.Count);
        IReadOnlyList<HeadlessEntityId> toTrash;

        if (context is not null &&
            repository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null)
        {
            // AS-IS selectPlayer = TopCard.Owner (the host's owner). Link cards live off-field
            // (ChoiceZone.None), but a ChoiceCandidate must carry a concrete zone; surface them under the
            // host's field zone (a link host is always a battle-area Digimon).
            ChoiceZone displayZone = ResolveHostZone(zoneMover, host.OwnerId, hostId);
            var candidates = currentLinked
                .Select(id => new ChoiceCandidate(id, $"Link {id.Value}", displayZone, IsSelectable: true, ownerId: host.OwnerId))
                .ToArray();
            var request = new ChoiceRequest(
                ChoiceType.Card,
                host.OwnerId,
                $"Select {pick} link card(s) to trash.",
                minCount: pick,
                maxCount: pick,
                canSkip: false,
                displayZone,
                candidates);

            ChoiceResult result = await context.ChoiceProvider
                .ChooseAsync(request, cancellationToken)
                .ConfigureAwait(false);
            result.ThrowIfInvalid(request);
            toTrash = result.SelectedIds;
        }
        else
        {
            // (MIG2-ADDLINK-SELECT fallback) oldest-first: the newest-first list keeps oldest at the end.
            toTrash = currentLinked.Skip(currentLinked.Count - pick).ToArray();
        }

        foreach (HeadlessEntityId linkCardId in toTrash)
        {
            // Bare removal (no OnLinkCardDiscarded) — AS-IS RemoveLinkedCard emits no window here.
            await RemoveLinkCardAsync(
                repository, zoneMover, hostId, linkCardId, trash: true,
                gameEventQueue: null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The concrete field zone the link host currently occupies (battle area by default), so its
    /// off-field link cards can be surfaced as choice candidates under a valid zone.</summary>
    private static ChoiceZone ResolveHostZone(IZoneMover zoneMover, HeadlessPlayerId ownerId, HeadlessEntityId hostId)
    {
        if (zoneMover is IZoneStateReader reader && !ownerId.IsEmpty)
        {
            foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
            {
                if (reader.GetCards(ownerId, zone).Contains(hostId))
                {
                    return zone;
                }
            }
        }

        return ChoiceZone.BattleArea;
    }

    /// <summary>(AS-IS auto-processing <c>IsDigimonLackLinkMaxCountProcess</c>) Trash the oldest link cards
    /// beyond the host's max. Returns the number trashed.</summary>
    public static async Task<int> EnforceLinkedMaxAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default,
        Bridge.EngineContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return 0;
        }

        // (M-4) fold continuous linkedMaxDelta modifiers when a context is wired; else metadata-only base.
        int max = context is not null ? ResolveLinkedMax(context, hostId) : ReadLinkedMax(host.Metadata);
        IReadOnlyList<HeadlessEntityId> linked = ReadLinkedCardIds(host.Metadata);
        if (linked.Count <= max)
        {
            return 0;
        }

        // Oldest cards are at the end of the newest-first list.
        var excess = linked.Skip(max).ToArray();
        int trashed = 0;
        foreach (HeadlessEntityId linkCardId in excess)
        {
            if (await RemoveLinkCardAsync(repository, zoneMover, hostId, linkCardId, trash: true, gameEventQueue, cancellationToken).ConfigureAwait(false))
            {
                trashed++;
            }
        }

        return trashed;
    }

    private static Dictionary<string, object?> WithLinked(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> linked, int linkedDp)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (linked.Count > 0)
        {
            copy[LinkedCardIdsKey] = linked.ToArray();
            copy[LinkedDpKey] = linkedDp;
        }
        else
        {
            copy.Remove(LinkedCardIdsKey);
            copy.Remove(LinkedDpKey);
        }

        return copy;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            string s when int.TryParse(s, out int parsed) => parsed,
            _ => null,
        };
    }
}
