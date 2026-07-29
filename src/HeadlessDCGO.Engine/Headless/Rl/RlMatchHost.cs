// ============================================================================================================
// ONE MATCH SERVED TO A POLICY — the engine side of seat protocol v1 (docs/audit/rl_seat_protocol_v1.md).
//
// The shape is MatchSmoke.RunToCompletion generalised: build the scene, supply both decks, run the
// lifecycle, pin the seed in the AS-IS handshake window, then tick the driver — but where MatchSmoke's
// random seats answer inline, the two PolicyVirtualPlayers CAPTURE questions, and this host stops ticking
// to hand each one out as a `turn`. `Step` applies the lane and resumes. Ends: endGame (winner read from
// the AS-IS result surface — WinImage/LoseImage active flags), step cap, stall, or engine exception
// (protocol reason `aborted`). Teardown between matches is the scene's own (leak-proof, match-independent —
// both gated 2026-07-29).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

using System.Reflection;
using System.Text.Json;
using HeadlessDCGO.Engine.Headless.Bootstrap;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.DataLoading;

public sealed record TurnMessage(int Seat, long StepIndex, double[] Observation, int[] Mask, int LegalCount);

public sealed record ResultMessage(
    double RewardSeat1, double RewardSeat2, int? WinnerSeat, bool IsDraw, string Reason, int Steps, int Turns);

public sealed class RlMatchHost
{
    private readonly CEntity_Base[] _cards;
    private readonly Dictionary<string, CEntity_Base> _byCanonical;

    private HeadlessScene? _scene;
    private CoroutineDriver? _driver;
    private IDisposable? _hook;
    private PolicyVirtualPlayer? _seat1;
    private PolicyVirtualPlayer? _seat2;
    private int _seed;
    private int _maxSteps;
    private long _stepIndex;
    private int _tick;
    private int _stableFrom;
    private string _lastState = "";
    private string _lastServedNote = "";
    private bool _pinned;

    public RlMatchHost(CEntity_Base[] cards, CardVocabulary vocab)
    {
        _cards = cards;
        Vocab = vocab;
        _byCanonical = new Dictionary<string, CEntity_Base>(StringComparer.Ordinal);

        foreach (CEntity_Base card in cards)
        {
            _byCanonical[CardVocabulary.Canonical(card.CardID)] = card;   // 뒤 레코드 우선(파이썬 CardIndex와 동일)
        }
    }

    public CardVocabulary Vocab { get; }

    /// <summary>Schema/capacity overflow notes (never silently truncated).</summary>
    public List<string> Overflows { get; } = new();

    /// <summary>Selector asks auto-answered by the minimal fallback this match.</summary>
    public IEnumerable<string> AutoAnswered =>
        (_seat1?.AutoAnswered ?? Enumerable.Empty<string>()).Concat(_seat2?.AutoAnswered ?? Enumerable.Empty<string>());

    public object Reset(int seed, JsonElement decks, int maxSteps)
    {
        TearDown();

        _seed = seed;
        _maxSteps = maxSteps;
        _stepIndex = 0;
        _tick = 0;
        _stableFrom = 0;
        _lastState = "";
        _pinned = false;

        _scene = new HeadlessScene();
        _scene.Build();

        (DeckData deck1, DeckData deck2) = (BuildDeck(decks.GetProperty("1")), BuildDeck(decks.GetProperty("2")));
        SupplyDecks(deck1, deck2);
        _scene.RunLifecycle();

        _driver = new CoroutineDriver();
        _hook = _driver.AttachToStartCoroutine();

        MethodInfo? awake = typeof(GManager).GetMethod(
            "AwakeCoroutine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (awake?.Invoke(GManager.instance, null) is System.Collections.IEnumerator routine)
        {
            _driver.Start(routine);
        }

        _seat1 = new PolicyVirtualPlayer { Seat = GManager.instance!.You, Host = this };
        _seat2 = new PolicyVirtualPlayer { Seat = GManager.instance.Opponent, Host = this };

        return Advance();
    }

    public object Step(int seat, int lane)
    {
        PolicyVirtualPlayer player = SeatPlayer(seat);

        if (player.Pending is null)
        {
            throw new InvalidOperationException($"seat {seat}에 대기 중인 결정이 없음");
        }

        player.Apply(lane);
        _stepIndex++;

        return Advance();
    }

    /// <summary>Pending mask for legality checks at the protocol layer.</summary>
    public int[]? PendingMask(int seat) =>
        SeatPlayer(seat).Pending is { } point ? RlSchema.Mask(point) : null;

    public int? PendingSeat()
    {
        if (_seat1?.Pending is not null) return 1;
        if (_seat2?.Pending is not null) return 2;

        return null;
    }

    private PolicyVirtualPlayer SeatPlayer(int seat) => seat switch
    {
        1 => _seat1 ?? throw new InvalidOperationException("reset 전 step"),
        2 => _seat2 ?? throw new InvalidOperationException("reset 전 step"),
        _ => throw new ArgumentOutOfRangeException(nameof(seat)),
    };

    /// <summary>Ticks until a seat owes a decision or the match ends.</summary>
    private object Advance()
    {
        // Apply()가 즉석 후속 결정(순차 분해)을 남겼으면 틱 없이 바로 서빙.
        if (Serve() is { } immediate)
        {
            return immediate;
        }

        while (true)
        {
            try
            {
                _tick++;
                _driver!.Tick();
            }
            catch (Exception ex)
            {
                Exception root = ex;
                while (root is TargetInvocationException { InnerException: not null } wrapped) root = wrapped.InnerException!;

                // 스택은 stderr로 — 프로토콜(stdout)엔 사유 타입만 나가므로, 여기 안 남기면 abort는
                // 재현 없이 진단 불가다(실측 2026-07-30: 본학습 NRE 2판이 타입명만 남아 원점 소실).
                Console.Error.WriteLine($"[abort] tick={_tick} {root}");

                return Finish(null, $"aborted:{root.GetType().Name}", draw: true);
            }

            if (!_pinned)
            {
                _pinned = Determinism.MatchSeed.TryPin(_seed);
            }

            foreach (PolicyVirtualPlayer seat in new[] { _seat1!, _seat2! })
            {
                seat.Waits = _driver.PendingWaits.ToArray();
                seat.Answer();
            }

            if (GManager.instance?.turnStateMachine?.endGame == true)
            {
                return FinishFromResultSurface();
            }

            if (Serve() is { } turn)
            {
                return turn;
            }

            TrackStall();

            if (_tick - _stableFrom > 1500)
            {
                return Finish(null, $"aborted:stall:{_lastServedNote}", draw: true);
            }

            if (_tick > 200_000)
            {
                return Finish(null, "aborted:tick_budget", draw: true);
            }
        }
    }

    private object? Serve()
    {
        foreach ((PolicyVirtualPlayer player, int seat) in new[] { (_seat1!, 1), (_seat2!, 2) })
        {
            if (player.Pending is { } point)
            {
                if (_stepIndex >= _maxSteps)
                {
                    return Finish(null, "step_cap", draw: true);
                }

                _lastServedNote = $"{point.Kind}@s{seat}t{_tick}";
                int[] mask = RlSchema.Mask(point);
                double[] obs = RlSchema.Encode(point, Vocab, TurnNumber(), Overflows.Add);

                return new TurnMessage(seat, _stepIndex, obs, mask, mask.Sum());
            }
        }

        return null;
    }

    private object FinishFromResultSurface()
    {
        // AS-IS의 결과 표면: ShowResult가 You 승리면 WinImage, 패배면 LoseImage를 켠다(ResultObject.cs:40-66).
        // 두 슬롯은 [SerializeField] private — 채널들과 같은 방식의 리플렉션 판독.
        ResultObject? result = GManager.instance!.resultObject;
        bool youWon = (ReadField(result, "WinImage") as UnityEngine.UI.Image)?.gameObject.activeSelf == true;
        bool youLost = (ReadField(result, "LoseImage") as UnityEngine.UI.Image)?.gameObject.activeSelf == true;

        return youWon ? Finish(1, "game_end", draw: false)
            : youLost ? Finish(2, "game_end", draw: false)
            : Finish(null, "game_end", draw: true);
    }

    private static object? ReadField(object? target, string name)
    {
        for (Type? type = target?.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } field)
            {
                return field.GetValue(target);
            }
        }

        return null;
    }

    private ResultMessage Finish(int? winner, string reason, bool draw)
    {
        // Unity 등가로 삼킨 코루틴 예외는 결함 census다 — note 표면으로 올린다(조용한 삼킴 금지).
        foreach (string swallowed in _driver?.Swallowed ?? Enumerable.Empty<string>())
        {
            Overflows.Add($"swallowed:{swallowed}");
        }

        int turns = TurnNumber();
        TearDown();

        return new ResultMessage(
            RewardSeat1: winner == 1 ? 1.0 : winner == 2 ? -1.0 : 0.0,
            RewardSeat2: winner == 2 ? 1.0 : winner == 1 ? -1.0 : 0.0,
            WinnerSeat: winner,
            IsDraw: draw,
            Reason: reason,
            Steps: (int)_stepIndex,
            Turns: turns);
    }

    private void TrackStall()
    {
        // 스텝 카운트를 서명에 포함: 결정이 흐르는 한(정책이 합법-무효 행동을 반복하는 것 포함) stall이
        // 아니다 — 그 경우의 안전망은 프로토콜의 step_cap(무승부 0)이고, 이 검출기는 결정도 존도 멎은
        // 순수 엔진 무진행만 잡는다.
        Player? you = GManager.instance?.You;
        Player? foe = GManager.instance?.Opponent;
        string now = $"{_stepIndex}|{you?.LibraryCards.Count}/{you?.HandCards.Count}/{you?.SecurityCards.Count}/{you?.TrashCards.Count}" +
            $"|{foe?.LibraryCards.Count}/{foe?.HandCards.Count}/{foe?.SecurityCards.Count}/{foe?.TrashCards.Count}" +
            $"|{GManager.instance?.turnStateMachine?.gameContext?.Memory}";

        if (now != _lastState)
        {
            _lastState = now;
            _stableFrom = _tick;
        }
    }

    private int TurnNumber() => GManager.instance?.turnStateMachine?.TurnCount ?? 0;

    private void SupplyDecks(DeckData you, DeckData opponent)
    {
        ContinuousController continuous = _scene!.ContinuousObject.GetComponent<ContinuousController>()!;
        continuous.CardList = _cards;
        continuous.SortedCardList = _cards;
        continuous.BattleDeckData = you;
        continuous.DeckDatas = new List<DeckData> { opponent };
        continuous.isAI = true;
    }

    private DeckData BuildDeck(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.String)
        {
            string text = spec.GetString()!;

            return text.StartsWith("starter:", StringComparison.Ordinal)
                ? StarterDeckCatalog.Build(text["starter:".Length..], _cards)
                : StarterDeckCatalog.Build(text, _cards);
        }

        // 내부 표준 레시피(rl/dcgo_rl/decks/recipe.py to_json): main/digitama의 {card,count}.
        List<CEntity_Base> main = ResolveSection(spec, "main");
        List<CEntity_Base> digitama = ResolveSection(spec, "digitama");
        string name = spec.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "recipe" : "recipe";

        return new DeckData(DeckData.GetDeckCode(name, main, digitama, null));
    }

    private List<CEntity_Base> ResolveSection(JsonElement spec, string section)
    {
        List<CEntity_Base> cards = new();

        foreach (JsonElement entry in spec.GetProperty(section).EnumerateArray())
        {
            string canonical = CardVocabulary.Canonical(entry.GetProperty("card").GetString()!);

            if (!_byCanonical.TryGetValue(canonical, out CEntity_Base? card))
            {
                throw new InvalidOperationException($"레시피의 미지원 카드: {canonical} (명시 실패 — 조용한 생략 금지)");
            }

            for (int i = 0; i < entry.GetProperty("count").GetInt32(); i++)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private void TearDown()
    {
        _hook?.Dispose();
        _hook = null;
        _scene?.Teardown();
        _scene = null;
        _seat1 = null;
        _seat2 = null;
    }
}
