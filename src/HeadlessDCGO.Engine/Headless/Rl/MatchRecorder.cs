// ============================================================================================================
// MATCH RECORDER — 판 로그 파일(<매치ID>.jsonl.gz) 생성기. 설계 정본: docs/audit/rl_ops_tool_design_v1.md §2.
//
// 원칙(요구 v1.2): 기록형(재실행 아님)·스텝 앵커 전지적 스냅샷·PlayLog 원문 이벤트·전정보(숨김은
// 뷰어 토글)·판 진행 중 메모리 버퍼→종료 시 모드 판정 후 일괄 gz 기록(핫패스 압축 비용 0)·
// 원본(.jsonl) 무잔존. 사고판(abort/swallowed/step_cap)은 모드와 무관하게 항상 기록.
// 카드는 id만 기록 — 이름 해석은 뷰어가 cards.json 사전으로(로그 크기 절감, 설계 §2 갱신 2026-07-30).
// 상태 판독은 AS-IS 공개 표면만(RlSchema.Encode와 같은 접근자) — 두 번째 오라클을 만들지 않는다.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

using System.IO.Compression;
using System.Text;
using System.Text.Json;

public sealed class MatchRecorder
{
    private readonly List<string> _lines = new();
    private readonly string _dir;
    private readonly string _mode;      // "all" | "sample:N" | "accident" | "off"
    private readonly string _matchId;
    private int _steps;

    /// <summary>sample:N 모드의 판 카운터 — 호스트 프로세스 수명 기준(N판당 1판 기록).</summary>
    private static int _sampleCounter;

    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public MatchRecorder(string dir, string mode, string matchId, int seed, string engineSha, object players)
    {
        _dir = dir;
        _mode = mode;
        _matchId = matchId;

        Add(new
        {
            v = 1,
            type = "header",
            matchId,
            ts = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            engineSha,
            seed,
            players,
            recordPolicy = mode,
        });
    }

    private void Add(object line) => _lines.Add(JsonSerializer.Serialize(line, Json));

    /// <summary>결정 시점 기록: 스냅샷 앵커. Apply 직전(pending 결정 + 선택 레인이 확정된 순간)에 호출.</summary>
    public void RecordStep(int tick, int seat, string decisionKind, int[] legal, int chosen)
    {
        Add(new
        {
            type = "step",
            i = _steps++,
            tick,
            seat,
            decision = new { kind = decisionKind, legal, chosen },
            state = Snapshot(),
        });
    }

    /// <summary>PlayLog.OnAddLog 원문. cat은 텍스트 선두 키워드의 얕은 분류 — 뷰어 칩 필터용.</summary>
    public void RecordEvent(string text)
    {
        string cat = text.Contains("Attack") || text.Contains("Battle") ? "battle"
            : text.Contains("Phase") || text.Contains("Turn") ? "phase"
            : text.Contains("Select") ? "select"
            : "effect";

        // 이벤트에도 발생 순간의 스냅샷 동봉(로그 v2) — "등장 따로, 드로우 따로" 화면 변화를
        // 뷰어가 이벤트 단위로 재생할 수 있게(사용자 요구 2026-07-30). gz가 반복 구조를 흡수한다.
        // v2.2(2026-08-01): 동명 카드 하이라이트 중복 수리 — 어택 주체/대상·선택 대상의 퍼머넌트
        // uid를 동봉한다. 어택은 AS-IS 공개 표면(attackProcess), 선택은 발행 주체의 사적 리스트
        // (_targetPermanents)를 리플렉션 직독(무수정 원문 — 카드 효과들도 attackProcess를 직접 읽는다).
        // AS-IS는 어택 종료 후에도 AttackingPermanent를 비우지 않는다(실측 2026-08-01: 턴 종료·
        // 드로우 이벤트까지 잔존) — 어택/배틀 이벤트에만 동봉해 창 밖 오표시를 막는다.
        bool attackEvent = text.StartsWith("\nAttack:", StringComparison.Ordinal)
            || text.StartsWith("\nBattle:", StringComparison.Ordinal);

        Add(new
        {
            type = "event", afterStep = _steps - 1, cat, text,
            atkUid = attackEvent ? Uid(GManager.instance?.attackProcess?.AttackingPermanent) : null,
            tgtUid = attackEvent ? Uid(GManager.instance?.attackProcess?.DefendingPermanent) : null,
            selUids = SelectedUids(text),
            state = Snapshot(),
        });
    }

    private static int? Uid(Permanent? p) =>
        p is null ? null : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p);

    /// <summary>"Selected Cards:" 이벤트의 선택 퍼머넌트 uid 목록 — SelectPermanentEffect의
    /// _targetPermanents(발행 시점에 채워져 있는 선택 실체)를 리플렉션으로 직독.</summary>
    private static List<int>? SelectedUids(string text)
    {
        if (!text.StartsWith("\nSelected Cards:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            SelectPermanentEffect? selector = GManager.instance?.GetComponent<SelectPermanentEffect>();
            System.Reflection.FieldInfo? field = typeof(SelectPermanentEffect).GetField(
                "_targetPermanents",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (selector is not null && field?.GetValue(selector) is List<Permanent> targets && targets.Count > 0)
            {
                return targets.Select(p => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p)).ToList();
            }
        }
        catch (Exception)
        {
            // 기록이 판을 죽이면 안 된다 — uid 없으면 뷰어가 ID 매칭 폴백
        }

        return null;
    }

    /// <summary>판 종료: 모드 판정 후 기록 또는 폐기. accident는 모드 무관 항상 기록.</summary>
    /// <returns>기록된 파일 경로, 폐기면 null.</returns>
    public string? Finish(string reason, int? winnerSeat, int turns, IReadOnlyList<string> swallowed)
    {
        bool accident = reason.StartsWith("aborted", StringComparison.Ordinal)
            || reason == "step_cap" || swallowed.Count > 0;

        Add(new { type = "result", reason, winnerSeat, steps = _steps, turns, census = new { swallowed } });

        bool write = _mode switch
        {
            "all" => true,
            "accident" => accident,
            _ when _mode.StartsWith("sample:", StringComparison.Ordinal) =>
                accident || (_sampleCounter++ % Math.Max(1, int.Parse(_mode["sample:".Length..])) == 0),   // 워커당 첫 판 포함(짧은 런 0기록 방지)
            _ => false,
        };

        if (!write)
        {
            return null;
        }

        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, $"{_matchId}.jsonl.gz");

        using FileStream file = File.Create(path);
        using GZipStream gz = new(file, CompressionLevel.Fastest);
        using StreamWriter writer = new(gz, new UTF8Encoding(false));

        foreach (string line in _lines)
        {
            writer.WriteLine(line);
        }

        return path;
    }

    // --- 전지적 스냅샷: AS-IS 공개 표면 직독 -----------------------------------------------------------

    internal static object Snapshot()
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;

        return new
        {
            turn = GManager.instance.turnStateMachine.TurnCount,
            phase = context.TurnPhase.ToString(),
            activeSeat = context.TurnPlayer == context.You ? 1 : 2,
            memory = context.Memory,   // You 관점 부호(AS-IS 그대로) — 뷰어가 좌석 관점으로 변환
            p1 = Side(context.You),
            p2 = Side(context.Opponent),
        };
    }

    private static object Side(Player side) => new
    {
        deckCount = side.LibraryCards.Count,
        hand = side.HandCards.Select(c => c.CardID),
        security = side.SecurityCards.Select(c => c.CardID),
        trash = side.TrashCards.Select(c => c.CardID),
        breeding = side.GetBreedingAreaPermanents().Select(PermanentView),
        // 배틀에어리어 전용 접근자를 쓴다 — GetFieldPermanents()는 AS-IS에서 육성 포함 전체라
        // 육성 중 디지타마가 배틀 칸에 이중 표시됐다(사용자 발견 2026-07-30, Player.cs:617 vs :665).
        field = side.GetBattleAreaPermanents().Select(PermanentView),
    };

    private static object PermanentView(Permanent p) => new
    {
        // AS-IS 의미론적 접근자 그대로 — StackCards는 top이 첫 요소라 순서 추측이 왜곡을 낳았다
        // (사용자 발견 2026-07-30). roots = DigivolutionCards(Permanent.cs:888) = 정확히 "진화원".
        // uid = 퍼머넌트 객체 identity(판 내 안정) — 동명 카드 하이라이트 중복 수리(사용자 보고 2026-08-01)
        uid = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p),
        roots = p.DigivolutionCards.Select(c => c.CardID),
        top = p.TopCard?.CardID,
        level = p.Level,
        dp = p.DP,                                   // AS-IS 계산값(버프 반영)
        baseDp = p.TopCard?.BaseCardDP,              // 카드 원본 DP(CardSource:2376) — 뷰어가 버프 델타 표시
        suspended = p.IsSuspended,
        links = p.LinkedCards.Select(c => c.CardID),
        // 부여된 지속 효과(사용자 요구 2026-08-01): Until* 부여 리스트에서 효과명 원문 추출.
        // 팩토리 호출은 AS-IS EffectList(:1373)가 룰 판정마다 하는 것과 동일한 순수 구성 — 실행 아님.
        effects = GrantedEffects(p),
    };

    /// <summary>퍼머넌트에 부여된 지속 효과의 (효과명, 지속) 목록 — AS-IS 부여 리스트 원문.</summary>
    private static List<object> GrantedEffects(Permanent p)
    {
        List<object> output = new();

        void Collect(IEnumerable<Func<EffectTiming, ICardEffect>>? factories, string until)
        {
            if (factories is null)
            {
                return;
            }

            foreach (Func<EffectTiming, ICardEffect> factory in factories)
            {
                ICardEffect? effect;

                try
                {
                    effect = factory(EffectTiming.None);
                }
                catch (Exception)
                {
                    continue;   // 구성 단계 예외는 표시 생략 — 기록이 판을 죽이면 안 된다
                }

                string? name = effect?.EffectName;
                string? description = effect?.EffectDiscription;
                string text = !string.IsNullOrEmpty(name) ? name!
                    : !string.IsNullOrEmpty(description) ? description! : "";

                if (text.Length > 0)
                {
                    output.Add(new { text, until });
                }
            }
        }

        Collect(p.UntilEachTurnEndEffects, "이 턴");
        Collect(p.UntilOwnerTurnEndEffects, "자기 턴 끝");
        Collect(p.UntilOpponentTurnEndEffects, "상대 턴 끝");
        Collect(p.UntilEndBattleEffects, "배틀 동안");
        Collect(p.UntilEndAttackEffects, "어택 동안");
        Collect(p.UntilNextUntapEffects, "다음 언탭");
        Collect(p.PermanentEffects, "영구");

        return output;
    }
}
