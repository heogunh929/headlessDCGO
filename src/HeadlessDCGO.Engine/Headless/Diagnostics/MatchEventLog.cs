namespace HeadlessDCGO.Engine.Headless.Diagnostics;

using System.Text.Json;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (L4, docs/audit/rl_l4_match_log_design.md) 매치 사건 JSONL 로거 — 기존 이벤트 패브릭의
/// 소비자. 새 계측 없음: 존무버 append-only 스트림(<see cref="IZoneStateReader"/> 구현의
/// <c>Events</c>)과 DcgoMatch 이벤트 깔때기를 스텝 경계에서 drain해, 타입→레벨 분류로 거르고
/// 육하원칙 필드 + turn/phase 스탬프로 1행씩 쓴다.
///
/// FR-7.3 가드: <see cref="LogStep"/> 첫 줄의 레벨 분기 1회가 OFF 비용의 전부다(이벤트 객체는
/// 트리거 수집용으로 어차피 생산됨 — 로깅은 소비만 추가).
/// </summary>
public sealed class MatchEventLog
{
    private readonly TextWriter _sink;
    private readonly string _matchId;
    private EngineContext? _context;
    private int _zoneCursor;

    public MatchEventLog(MatchLogLevel level, TextWriter sink, string matchId)
    {
        Level = level;
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _matchId = matchId ?? string.Empty;
    }

    public MatchLogLevel Level { get; }

    /// <summary>매치 시작 시 배선(DcgoMatch InitializeAsync) — 존 이벤트 커서 초기화.</summary>
    public void Attach(EngineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _zoneCursor = ZoneEvents()?.Count ?? 0;
    }

    /// <summary>스텝 경계 훅 — DcgoMatch가 이벤트 배출 시 호출.</summary>
    public void LogStep(IReadOnlyList<GameEvent> matchEvents)
    {
        if (Level < MatchLogLevel.Replay)
        {
            return; // FR-7.3: OFF/RESULT의 비용 = 이 분기 1회.
        }

        HeadlessTurnState turn = _context?.TurnController.Current ?? HeadlessTurnState.Empty;

        IReadOnlyList<GameEvent>? zoneEvents = ZoneEvents();
        if (zoneEvents is not null)
        {
            for (; _zoneCursor < zoneEvents.Count; _zoneCursor++)
            {
                Write(zoneEvents[_zoneCursor], turn);
            }
        }

        foreach (GameEvent gameEvent in matchEvents)
        {
            Write(gameEvent, turn);
        }

        _sink.Flush();
    }

    /// <summary>타입 → 최소 레벨 분류 (설계 §1.3, FR-7.1 의미 그대로).</summary>
    public static MatchLogLevel LevelOf(GameEventType type)
    {
        return type switch
        {
            GameEventType.CardMoved or
            GameEventType.ActionProcessed or
            GameEventType.AttackDeclared or
            GameEventType.AttackResolved or
            GameEventType.SecurityCheck or
            GameEventType.SecuritySkill or
            GameEventType.ChoiceResolved or
            GameEventType.GameEnded => MatchLogLevel.Replay,

            GameEventType.EffectQueued or
            GameEventType.EffectResolved or
            GameEventType.ChoiceRequested or
            GameEventType.ChoiceCleared or
            GameEventType.DelayedTrigger or
            GameEventType.StateChanged or
            GameEventType.ActionQueued or
            GameEventType.AttackCleared => MatchLogLevel.Analysis,

            _ => MatchLogLevel.Trace
        };
    }

    private void Write(GameEvent gameEvent, HeadlessTurnState turn)
    {
        if (LevelOf(gameEvent.Type) > Level)
        {
            return;
        }

        var line = new Dictionary<string, object?>
        {
            ["matchId"] = _matchId,
            ["seq"] = gameEvent.Sequence,
            ["turn"] = turn.TurnNumber,
            ["phase"] = turn.Phase.ToString(),
            ["type"] = gameEvent.Type.ToString(),
            ["actor"] = gameEvent.Actor?.Value,
            ["subject"] = gameEvent.Subject?.Value,
            ["target"] = gameEvent.Target?.Value,
            ["zoneFrom"] = gameEvent.ZoneFrom?.ToString(),
            ["zoneTo"] = gameEvent.ZoneTo?.ToString(),
            ["cause"] = gameEvent.Cause,
            ["tags"] = Array.Empty<string>() // FR-7.5 semantic 태깅 예약 — 자리만.
        };

        if (Level >= MatchLogLevel.Trace)
        {
            line["message"] = gameEvent.Message;
        }

        _sink.WriteLine(JsonSerializer.Serialize(line));
    }

    private IReadOnlyList<GameEvent>? ZoneEvents()
    {
        return (_context?.ZoneMover as InMemoryZoneMover)?.Events;
    }
}
