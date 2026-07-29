// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/TurnStateMachine.cs::TurnStateMachine.StartGame (마리건 절차: _isRedraw 결정 → 핸드 산 밑으로 → Shuffle → Draw 5 → 전원 결정 후 시큐리티 딜)@StartGame:341; _isRedraw:340; redraw 블록(핸드파괴/AddLibrary
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (N-5) Drives the opening-hand mulligan as a sequence of per-player agent decisions, mirroring the
/// original TurnStateMachine flow: each player (first player first) chooses keep or redraw BEFORE
/// security is dealt; a redraw returns the hand to the bottom of the deck, shuffles, and draws a fresh
/// hand. Once every player has decided, security is dealt from the (post-mulligan) deck top.
///
/// The decision is surfaced through the <see cref="IHeadlessChoiceController"/> as a
/// <see cref="ChoiceType.Mulligan"/> choice: selecting the redraw candidate = mulligan, skipping = keep.
/// Because a pending choice is dispatched to its owner (not the turn player), the per-player ordering is
/// handled simply by opening the next player's choice after the previous one resolves.
/// </summary>
public sealed class MulliganCoordinator
{
    public static readonly HeadlessEntityId RedrawCandidateId = new("mulligan:redraw");

    private readonly Queue<HeadlessPlayerId> _pending = new();
    private IReadOnlyList<HeadlessPlayerId> _order = Array.Empty<HeadlessPlayerId>();
    private int _handSize;
    private int _securitySize;
    private bool _securityFaceUp;
    private bool _active;

    /// <summary>True while one or more players still owe a mulligan decision.</summary>
    public bool IsActive => _active;

    /// <summary>Begins the mulligan sequence and opens the first player's decision.</summary>
    public void Begin(
        IHeadlessChoiceController choiceController,
        IReadOnlyList<HeadlessPlayerId> order,
        int handSize,
        int securitySize,
        bool securityFaceUp = false)
    {
        ArgumentNullException.ThrowIfNull(choiceController);
        ArgumentNullException.ThrowIfNull(order);

        _order = order.ToArray();
        _pending.Clear();
        foreach (HeadlessPlayerId playerId in _order)
        {
            _pending.Enqueue(playerId);
        }

        _handSize = handSize;
        _securitySize = securitySize;
        _securityFaceUp = securityFaceUp;
        _active = _pending.Count > 0;

        if (_active)
        {
            OpenChoiceFor(choiceController, _pending.Peek());
        }
    }

    /// <summary>
    /// Resolves the current player's mulligan decision: applies the redraw (or keep), then either opens
    /// the next player's decision or, when all have decided, deals security from the post-mulligan deck.
    /// </summary>
    public async Task<MulliganResolveResult> ResolveAsync(
        IZoneMover zoneMover,
        IHeadlessChoiceController choiceController,
        ChoiceResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(choiceController);
        ArgumentNullException.ThrowIfNull(result);

        if (!_active || _pending.Count == 0)
        {
            return MulliganResolveResult.Failure("No pending mulligan decision.");
        }

        HeadlessPlayerId decider = _pending.Dequeue();
        bool redraw = !result.IsSkipped;

        // Consume this player's choice (validates the selection / skip against the open request).
        choiceController.ResolveChoice(result);

        if (redraw)
        {
            await RedrawAsync(zoneMover, decider, cancellationToken).ConfigureAwait(false);
        }

        if (_pending.Count > 0)
        {
            OpenChoiceFor(choiceController, _pending.Peek());
        }
        else
        {
            await DealSecurityAsync(zoneMover, cancellationToken).ConfigureAwait(false);
            _active = false;
        }

        return MulliganResolveResult.Success(decider, redraw);
    }

    public void Clear()
    {
        _pending.Clear();
        _order = Array.Empty<HeadlessPlayerId>();
        _handSize = 0;
        _securitySize = 0;
        _securityFaceUp = false;
        _active = false;
    }

    private static void OpenChoiceFor(IHeadlessChoiceController choiceController, HeadlessPlayerId playerId)
    {
        var redrawCandidate = new ChoiceCandidate(
            RedrawCandidateId,
            "Mulligan (redraw opening hand)",
            ChoiceZone.Hand,
            IsSelectable: true,
            ownerId: playerId);

        var request = new ChoiceRequest(
            ChoiceType.Mulligan,
            playerId,
            "Mulligan your opening hand?",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.Hand,
            new[] { redrawCandidate });

        choiceController.RequestChoice(request);
    }

    private async Task RedrawAsync(IZoneMover zoneMover, HeadlessPlayerId playerId, CancellationToken cancellationToken)
    {
        if (zoneMover is not IZoneStateReader reader)
        {
            return;
        }

        foreach (HeadlessEntityId cardId in reader.GetCards(playerId, ChoiceZone.Hand).ToArray())
        {
            await zoneMover.MoveToDeckBottomAsync(playerId, cardId, cancellationToken).ConfigureAwait(false);
        }

        await zoneMover.ShuffleAsync(playerId, cancellationToken).ConfigureAwait(false);
        await zoneMover.DrawAsync(playerId, _handSize, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task DealSecurityAsync(IZoneMover zoneMover, CancellationToken cancellationToken)
    {
        foreach (HeadlessPlayerId playerId in _order)
        {
            await zoneMover
                .AddSecurityFromLibraryAsync(playerId, _securitySize, _securityFaceUp, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

public sealed record MulliganResolveResult(
    bool IsSuccess,
    string FailureReason,
    HeadlessPlayerId Player,
    bool Redrew)
{
    public static MulliganResolveResult Success(HeadlessPlayerId player, bool redrew) =>
        new(true, string.Empty, player, redrew);

    public static MulliganResolveResult Failure(string reason) =>
        new(false, reason ?? string.Empty, default, false);
}
