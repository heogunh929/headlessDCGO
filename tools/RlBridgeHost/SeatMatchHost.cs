namespace HeadlessDCGO.Tools.RlBridgeHost;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// seat 매치 프로토콜 v1 상태기계 (docs/audit/rl_seat_protocol_v1.md). 전송 무관:
/// 입력 = JSON 메시지 1행, 출력 = 응답 JSON 행 목록. stdio/TCP 래퍼가 이걸 감싼다.
/// 학습 브리지와 아레나 매치 서버가 공유하는 계약의 참조 구현.
/// </summary>
public sealed class SeatMatchHost
{
    public const int ProtocolVersion = 1;
    private const int DefaultMaxSteps = 2000;

    private readonly FactoredActionSchema _schema;
    private readonly string? _resultLogPath;
    private readonly MatchLogLevel _logLevel;
    private readonly string? _eventLogPath;
    private readonly bool _strictUnbound;
    private StreamWriter? _eventWriter;

    // (B4/D6 수확 계층) 직전 내부 오류의 포렌식 — aborted result에 1회 동봉 후 소거.
    private JsonObject? _lastException;

    // 세션 상태
    private bool _helloDone;
    private readonly HashSet<int> _claimedSeats = new();

    // 핸드셰이크 산출물(hello 시 1회 계산)
    private CardVocabulary? _vocabulary;
    private ObservationEncodingOptions? _encodingOptions;
    private int _obsSize = -1;
    private string _obsSchemaHash = string.Empty;
    private string _vocabHash = string.Empty;
    private IReadOnlyList<string> _featureNames = Array.Empty<string>();

    // 매치 상태
    private DcgoMatch? _match;
    private string _matchId = string.Empty;
    private int _matchCounter;
    private int _maxSteps = DefaultMaxSteps;
    private int _steps;
    private int? _pendingSeat; // 마지막으로 turn을 발행한 좌석 — action은 이 좌석에서만 유효
    private string _lastTurnLine = string.Empty;
    private IReadOnlyDictionary<string, string> _deckNames = new Dictionary<string, string>();

    public SeatMatchHost(
        FactoredActionSchema? schema = null,
        string? resultLogPath = null,
        MatchLogLevel logLevel = MatchLogLevel.Off,
        string? eventLogPath = null,
        bool strictUnbound = false)
    {
        _schema = schema ?? FactoredActionSchema.Default;
        _resultLogPath = resultLogPath;
        _logLevel = logLevel;
        _eventLogPath = eventLogPath;
        _strictUnbound = strictUnbound;
    }

    public async Task<IReadOnlyList<string>> HandleLineAsync(string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException ex)
        {
            return new[] { Error("bad_json", ex.Message) };
        }

        string type = node?["type"]?.GetValue<string>() ?? string.Empty;
        try
        {
            return type switch
            {
                "hello" => HandleHello(node!),
                "describe" => HandleDescribe(),
                "claim" => HandleClaim(node!),
                "reset" => await HandleResetAsync(node!),
                "action" => await HandleActionAsync(node!),
                _ => new[] { Error("protocol_violation", $"unknown message type '{type}'") }
            };
        }
        catch (Exception ex)
        {
            // 내부 오류: 진행 중 매치는 보상 0으로 종결(프로토콜 §5).
            // (B4/D6 수확 계층) 예외 포렌식을 aborted result에 동봉 — 스택/타입/스텝이 result JSONL로
            // 남아 캠페인 드라이버가 seed·덱쌍·직전 액션열과 함께 결함 레코드로 수확한다(도구층 필드
            // 추가만 — 엔진 무수정). 프로토콜 소비자에겐 additive 필드라 기존 계약 무변.
            Exception root = ex.GetBaseException();
            _lastException = new JsonObject
            {
                ["type"] = ex.GetType().FullName,
                ["message"] = ex.Message,
                ["rootType"] = root.GetType().FullName,
                ["rootMessage"] = root.Message,
                ["stack"] = TruncateForForensics(root.StackTrace ?? ex.StackTrace),
                ["atStep"] = _steps
            };
            var responses = new List<string> { Error("internal", $"{ex.GetType().Name}: {ex.Message}") };
            if (_match is not null)
            {
                responses.Add(EmitResult(winnerSeat: null, isDraw: false, reason: "aborted", zeroRewards: true));
            }

            return responses;
        }
    }

    // --- 세션 수립 -----------------------------------------------------------

    private IReadOnlyList<string> HandleHello(JsonNode node)
    {
        int protocol = node["protocol"]?.GetValue<int>() ?? -1;
        if (protocol != ProtocolVersion)
        {
            return new[] { Error("protocol_violation", $"unsupported protocol {protocol}; host speaks {ProtocolVersion}") };
        }

        EnsureHandshakeArtifacts();
        _helloDone = true;

        var welcome = new JsonObject
        {
            ["type"] = "welcome",
            ["protocol"] = ProtocolVersion,
            ["obsSchemaVersion"] = "infoset-v1",
            ["obsSize"] = _obsSize,
            ["obsSchemaHash"] = _obsSchemaHash,
            ["actionSchemaVersion"] = $"factored-v{FactoredActionSchema.Version}",
            ["actionSize"] = _schema.TotalSize,
            ["schema"] = new JsonObject
            {
                ["maxHand"] = _schema.MaxHand,
                ["maxField"] = _schema.MaxField,
                ["maxChoice"] = _schema.MaxChoice
            },
            ["vocabVersion"] = _vocabulary!.Version,
            ["vocabSize"] = _vocabulary.Count,
            ["vocabHash"] = _vocabHash
        };
        return new[] { welcome.ToJsonString() };
    }

    // 관측 피처명 전체(진단 + 트레이너의 cardId 채널 식별용). obsSchemaHash = sha256(이 목록의 \n 결합).
    private IReadOnlyList<string> HandleDescribe()
    {
        if (!_helloDone)
        {
            return new[] { Error("protocol_violation", "describe before hello") };
        }

        var schema = new JsonObject
        {
            ["type"] = "schema",
            ["obsSchemaHash"] = _obsSchemaHash,
            ["features"] = new JsonArray(_featureNames.Select(name => (JsonNode)JsonValue.Create(name)!).ToArray())
        };
        return new[] { schema.ToJsonString() };
    }

    private IReadOnlyList<string> HandleClaim(JsonNode node)
    {
        if (!_helloDone)
        {
            return new[] { Error("protocol_violation", "claim before hello") };
        }

        if (node["seats"] is not JsonArray seats || seats.Count == 0)
        {
            return new[] { Error("protocol_violation", "claim requires non-empty seats") };
        }

        _claimedSeats.Clear();
        foreach (JsonNode? seat in seats)
        {
            int value = seat?.GetValue<int>() ?? -1;
            if (value is not (1 or 2))
            {
                return new[] { Error("protocol_violation", $"seat must be 1 or 2, got {value}") };
            }

            _claimedSeats.Add(value);
        }

        var claimed = new JsonObject
        {
            ["type"] = "claimed",
            ["seats"] = new JsonArray(_claimedSeats.OrderBy(s => s).Select(s => JsonValue.Create(s)).ToArray<JsonNode?>())
        };
        return new[] { claimed.ToJsonString() };
    }

    // --- 매치 루프 -------------------------------------------------------------

    private async Task<IReadOnlyList<string>> HandleResetAsync(JsonNode node)
    {
        if (!_helloDone || _claimedSeats.Count == 0)
        {
            return new[] { Error("protocol_violation", "reset before hello/claim") };
        }

        // stdio v1: 단일 연결이 모든 좌석을 잡아야 매치를 돌릴 수 있다(두 좌석 다 이 연결이 응답).
        if (_claimedSeats.Count != 2)
        {
            return new[] { Error("protocol_violation", "stdio host v1 requires the connection to claim both seats") };
        }

        int seed = node["seed"]?.GetValue<int>() ?? 0;
        _maxSteps = node["maxSteps"]?.GetValue<int>() ?? DefaultMaxSteps;

        // (B4/D6) 퍼징 캠페인은 strict 프로파일 고정(unbound 효과 = 하드 실패 → 예외 채널로 수확).
        EngineContext context = EngineContext.CreateDefault(randomSeed: seed, strictUnbound: _strictUnbound);
        var database = (CardDatabase)context.CardRepository;
        CardBaseEntityLoader.LoadInto(database);

        var deckSetups = new List<PlayerDeckSetup>(2);
        var deckNames = new Dictionary<string, string>();
        foreach (int seat in new[] { 1, 2 })
        {
            JsonNode? deckNode = node["decks"]?[seat.ToString()];
            if (deckNode is null)
            {
                return new[] { Error("protocol_violation", $"reset.decks missing seat {seat}") };
            }

            (PlayerDeckSetup setup, string name, string? deckError) =
                BuildDeck(new HeadlessPlayerId(seat), deckNode, database);
            if (deckError is not null)
            {
                return new[] { Error("bad_deck", deckError) };
            }

            deckSetups.Add(setup);
            deckNames[seat.ToString()] = name;
        }

        HeadlessPlayerId[] players = { new(1), new(2) };
        // (R4 S3c-d1, pump promotion) Setup seeds DECKS ONLY: the pump's StartGameAsync owns the opening
        // hand deal, the mulligan choices and the security deal (hand 0 / security 0 / setup-mulligan off;
        // deck seeding + seeded shuffle kept). CreatePumpDriven normalizes this anyway — explicit here so
        // the protocol reference implementation states the pump-era setup contract.
        MatchSetupConfig setup2 = MatchSetupConfig.Create(
            deckSetups,
            firstPlayerId: players[0],
            initialHandSize: 0,
            initialSecuritySize: 0,
            enableMulligan: false);
        MatchConfig config = MatchConfig.Create(players, randomSeed: seed, setup: setup2);

        _matchId = $"m-{seed}-{_matchCounter++}";

        // (L4) REPLAY 이상이면 매치 사건 JSONL — 파일 하나에 matchId 스탬프로 이어 쓴다.
        MatchEventLog? eventLog = null;
        if (_logLevel >= MatchLogLevel.Replay && _eventLogPath is not null)
        {
            if (_eventWriter is null)
            {
                string? directory = Path.GetDirectoryName(Path.GetFullPath(_eventLogPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _eventWriter = new StreamWriter(_eventLogPath, append: true);
            }

            eventLog = new MatchEventLog(_logLevel, _eventWriter, _matchId);
        }

        // (R4 S3c-d1, pump promotion) The agent-facing match is PUMP-DRIVEN: the AS-IS TurnFlowPump owns
        // the turn cadence; AdvancePhase/EndTurn are illegal; the first decision point is the mulligan.
        DcgoMatch match = DcgoMatch.CreatePumpDriven(
            context,
            new EngineTrace(),
            eventLog: eventLog);
        await match.InitializeAsync(config);

        _match = match;
        _steps = 0;
        _pendingSeat = null;
        _deckNames = deckNames;

        // Run the pump to its first decision stop (the StartGame mulligan choice) so the first turn
        // message carries a non-empty mask.
        await DrivePumpToDecisionAsync();

        return NextTurnOrResult();
    }

    private async Task<IReadOnlyList<string>> HandleActionAsync(JsonNode node)
    {
        if (_match is null || _pendingSeat is null)
        {
            return new[] { Error("protocol_violation", "action without a pending turn") };
        }

        int seat = node["seat"]?.GetValue<int>() ?? -1;
        if (seat != _pendingSeat)
        {
            return new[] { Error("protocol_violation", $"turn belongs to seat {_pendingSeat}, got action for seat {seat}") };
        }

        int index = node["index"]?.GetValue<int>() ?? -1;
        FactoredActionMask mask = BuildMask(new HeadlessPlayerId(seat));
        if (index < 0 || index >= mask.Size || !mask.TryGetAction(index, out LegalAction action))
        {
            // 합법성 경계(프로토콜 §5): 상태 무변경 + 같은 turn 재발행.
            return new[] { Error("illegal_action", $"index {index} is not legal for seat {seat}"), _lastTurnLine };
        }

        await _match.ApplyActionAsync(action);
        await _match.StepAsync();
        _steps++;

        // (R4 S3c-d1) Pump cadence: the game loop steps the TaskRunner BEFORE processing the queued
        // action, so the pump segment this action releases runs on the FOLLOWING step; then drive the
        // auto-flow (no-choice, no-legal-action) stretch to the next decision point.
        await DrivePumpToDecisionAsync(afterAction: true);

        return NextTurnOrResult();
    }

    // (R4 S3c-d1) Bounded pump drive — the seat protocol's "next decision point" contract on the
    // continuous pump driver. Stops at: terminal, a pending choice, or any seat holding legal actions.
    private async Task DrivePumpToDecisionAsync(bool afterAction = false)
    {
        const int PumpDriveStepBound = 128;
        DcgoMatch match = _match!;
        if (!match.IsPumpDriven || match.IsTerminal())
        {
            return;
        }

        if (afterAction)
        {
            await match.StepAsync();
        }

        for (int i = 0;
            i < PumpDriveStepBound
                && !match.IsTerminal()
                && !match.HasPendingChoice()
                && match.GetActionMask().LegalActions.Count == 0;
            i++)
        {
            await match.StepAsync();
        }
    }

    private IReadOnlyList<string> NextTurnOrResult()
    {
        DcgoMatch match = _match!;

        if (match.IsTerminal())
        {
            MatchResult result = match.GetResult();
            int? winnerSeat = result.WinnerId?.Value;
            return new[] { EmitResult(winnerSeat, result.IsDraw, string.IsNullOrEmpty(result.Reason) ? "terminal" : result.Reason) };
        }

        if (_steps >= _maxSteps)
        {
            return new[] { EmitResult(winnerSeat: null, isDraw: false, reason: "step_cap", zeroRewards: true) };
        }

        // 행동할 좌석: 턴 플레이어 우선, 없으면 합법행동을 가진 좌석.
        ObservationSnapshot probe = match.GetObservation();
        var order = new List<int>();
        if (probe.Turn.TurnPlayerId is { } turnPlayer)
        {
            order.Add(turnPlayer.Value);
        }

        foreach (int seat in new[] { 1, 2 })
        {
            if (!order.Contains(seat))
            {
                order.Add(seat);
            }
        }

        foreach (int seat in order)
        {
            var player = new HeadlessPlayerId(seat);
            if (match.GetLegalActions(player).Count == 0)
            {
                continue;
            }

            FactoredActionMask mask = BuildMask(player);
            if (mask.Actions.Count == 0)
            {
                return new[] { EmitResult(null, false, "no_mappable_action", zeroRewards: true) };
            }

            double[] observation = EncodeFor(player);
            var turn = new JsonObject
            {
                ["type"] = "turn",
                ["matchId"] = _matchId,
                ["seat"] = seat,
                ["stepIndex"] = match.GetObservation().StepIndex,
                ["observation"] = ToJsonArray(observation),
                ["actionMask"] = ToJsonArray(mask.ToMaskVector()),
                ["legalCount"] = mask.Actions.Count
            };
            _pendingSeat = seat;
            _lastTurnLine = turn.ToJsonString();
            return new[] { _lastTurnLine };
        }

        // 어느 좌석도 행동 불가인데 terminal 도 아님 — 교착: 종결 처리.
        return new[] { EmitResult(null, false, "stalled", zeroRewards: true) };
    }

    // --- 보상 귀속 (프로토콜 §4 — 승자 +1 / 패자 −1 / 무·캡 0) ---------------------

    private string EmitResult(int? winnerSeat, bool isDraw, string reason, bool zeroRewards = false)
    {
        double RewardOf(int seat)
        {
            if (zeroRewards || isDraw || winnerSeat is null)
            {
                return 0d;
            }

            return seat == winnerSeat ? 1d : -1d;
        }

        ObservationSnapshot? finalObs = _match?.GetObservation();
        var result = new JsonObject
        {
            ["type"] = "result",
            ["matchId"] = _matchId,
            ["rewards"] = new JsonObject
            {
                ["1"] = RewardOf(1),
                ["2"] = RewardOf(2)
            },
            ["winnerSeat"] = winnerSeat is { } w ? JsonValue.Create(w) : null,
            ["isDraw"] = isDraw,
            ["reason"] = reason,
            ["steps"] = _steps,
            ["turns"] = finalObs?.Turn.TurnNumber ?? 0
        };

        if (_lastException is not null)
        {
            result["exception"] = _lastException;
            _lastException = null;
        }

        string line = result.ToJsonString();
        AppendResultLog(line);
        _match = null;
        _pendingSeat = null;
        _lastTurnLine = string.Empty;
        return line;
    }

    private void AppendResultLog(string resultLine)
    {
        if (_resultLogPath is null)
        {
            return;
        }

        // RESULT 요약 1줄(JSONL) — L0 로그 최소치(프로토콜 §7). 덱 이름을 매치 메타로 함께 남긴다.
        // 로그 실패가 매치/프로세스를 죽여서는 안 된다(진단 채널이지 게임 상태가 아님) — stderr로만 알린다.
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(_resultLogPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = JsonNode.Parse(resultLine)!.AsObject();
            entry["decks"] = new JsonObject(_deckNames.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value)));
            File.AppendAllText(_resultLogPath, entry.ToJsonString() + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"rl-bridge-host: result-log write failed: {ex.Message}");
        }
    }

    // --- 관측/마스크 ------------------------------------------------------------

    private FactoredActionMask BuildMask(HeadlessPlayerId player)
    {
        return FactoredActionEncoder.Encode(
            _match!.GetLegalActions(player),
            FactoredPositionContext.FromContext(_match.Context),
            _schema);
    }

    private double[] EncodeFor(HeadlessPlayerId player)
    {
        // 좌석 시점 정보집합(프로토콜 §3): perspective 스냅샷 + InformationSet 인코딩.
        EncodedObservation encoded = new ObservationEncoder(_encodingOptions)
            .Encode(_match!.GetObservation(player));
        if (encoded.Length != _obsSize)
        {
            throw new InvalidOperationException(
                $"observation size drift: welcome advertised {_obsSize}, got {encoded.Length}.");
        }

        return encoded.ToVector();
    }

    // --- 핸드셰이크 산출물 ---------------------------------------------------------

    private void EnsureHandshakeArtifacts()
    {
        if (_vocabulary is not null)
        {
            return;
        }

        CardDatabase database = CardBaseEntityLoader.CreateDatabase();
        _vocabulary = CardVocabulary.FromRepository(database);
        _encodingOptions = ObservationEncodingOptions.InformationSet(_vocabulary, _schema);
        _vocabHash = Sha256(string.Join(",", _vocabulary.Entries.Select(e => $"{e.Key}:{e.Value}")));

        // 관측 크기/스키마 해시는 실제 2인 매치 관측 1회로 확정한다(피처 구성은 옵션·플레이어 수에만
        // 의존하고 상태에는 불변 — EncodeFor가 매 스텝 크기를 검증한다).
        (IReadOnlyList<string> names, string hash) = ProbeObservationSchema(_encodingOptions);
        _featureNames = names;
        _obsSize = names.Count;
        _obsSchemaHash = hash;
    }

    private static (IReadOnlyList<string> Names, string Hash) ProbeObservationSchema(ObservationEncodingOptions options)
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 0);
        CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);

        StarterDecks.StarterDeck deck = StarterDecks.Get("ST1");
        HeadlessPlayerId[] players = { new(1), new(2) };
        MatchSetupConfig setup = MatchSetupConfig.Create(
            new[]
            {
                new PlayerDeckSetup(players[0], deck.MainDefinitions, deck.DigitamaDefinitions),
                new PlayerDeckSetup(players[1], deck.MainDefinitions, deck.DigitamaDefinitions)
            },
            firstPlayerId: players[0]);

        // Schema probe only (no gameplay): feature composition depends on options/player count, not on the
        // turn driver, so a plain (non-pump) match is sufficient and cheapest here.
        var match = new DcgoMatch(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
        match.InitializeAsync(MatchConfig.Create(players, randomSeed: 0, setup: setup))
            .GetAwaiter()
            .GetResult();

        EncodedObservation encoded = new ObservationEncoder(options).Encode(match.GetObservation(players[0]));
        IReadOnlyList<string> names = encoded.FeatureNames;
        return (names, Sha256(string.Join("\n", names)));
    }

    // --- 덱 -----------------------------------------------------------------------

    private static (PlayerDeckSetup Setup, string Name, string? Error) BuildDeck(
        HeadlessPlayerId player,
        JsonNode deckNode,
        CardDatabase database)
    {
        if (deckNode is JsonValue value && value.TryGetValue(out string? shorthand))
        {
            if (shorthand is null || !shorthand.StartsWith("starter:", StringComparison.OrdinalIgnoreCase))
            {
                return (null!, string.Empty, $"unsupported deck shorthand '{shorthand}'");
            }

            StarterDecks.StarterDeck starter = StarterDecks.Get(shorthand["starter:".Length..]);
            return (new PlayerDeckSetup(player, starter.MainDefinitions, starter.DigitamaDefinitions), starter.Code, null);
        }

        // 내부 표준 레시피(dev design §3.1): canonical 카드번호 + 매수. 미지원 카드는 전부 모아 명시 실패.
        string name = deckNode["name"]?.GetValue<string>() ?? "recipe";
        var unknown = new List<string>();

        IReadOnlyList<HeadlessEntityId> Expand(string section)
        {
            var ids = new List<HeadlessEntityId>();
            if (deckNode[section] is not JsonArray entries)
            {
                return ids;
            }

            foreach (JsonNode? entry in entries)
            {
                string? card = entry?["card"]?.GetValue<string>();
                int count = entry?["count"]?.GetValue<int>() ?? 0;
                if (string.IsNullOrWhiteSpace(card) || count <= 0)
                {
                    unknown.Add($"<malformed entry in {section}>");
                    continue;
                }

                var id = new HeadlessEntityId(card);
                if (!database.TryGetCard(id, out _))
                {
                    unknown.Add(card);
                    continue;
                }

                ids.AddRange(Enumerable.Repeat(id, count));
            }

            return ids;
        }

        IReadOnlyList<HeadlessEntityId> main = Expand("main");
        IReadOnlyList<HeadlessEntityId> digitama = Expand("digitama");
        if (unknown.Count > 0)
        {
            return (null!, name, $"deck '{name}': unsupported cards: {string.Join(", ", unknown)}");
        }

        return (new PlayerDeckSetup(player, main, digitama), name, null);
    }

    // --- 공용 ------------------------------------------------------------------------

    private static string Error(string code, string message)
    {
        return new JsonObject
        {
            ["type"] = "error",
            ["code"] = code,
            ["message"] = message
        }.ToJsonString();
    }

    private static JsonArray ToJsonArray(double[] values)
    {
        var array = new JsonArray();
        foreach (double value in values)
        {
            array.Add(JsonValue.Create(value));
        }

        return array;
    }

    private static string Sha256(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string? TruncateForForensics(string? stack)
    {
        const int MaxStackChars = 6000;
        if (stack is null)
        {
            return null;
        }

        return stack.Length <= MaxStackChars ? stack : stack[..MaxStackChars] + "\n... (truncated)";
    }
}
