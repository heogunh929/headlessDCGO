// ============================================================================================================
// RlBridgeHost — seat 매치 프로토콜 v1의 stdio 호스트 (docs/audit/rl_seat_protocol_v1.md).
//
// 트레이너(rl/dcgo_rl/bridge.py BridgeClient)가 이 프로세스를 자식으로 띄워 JSON-lines로 대화한다.
// stdout은 프로토콜 전용이다: AS-IS의 Debug.Log는 Console.Out으로 나오므로, 시작 즉시 실제 stdout을
// 프로토콜 스트림으로 떼어두고 Console.Out은 무음화한다(한 줄이라도 새면 클라이언트 파서가 죽는다).
//
// 부속 모드: --export-cards-json  DCGO 카드 자산 → src/.../CardBaseEntity/cards.json (vocab의 공유 원천).
// ============================================================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Rl;

const string CardsJsonRelative = "src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json";
const string CardAssetsRelative = "DCGO/Assets/CardBaseEntity";

TextWriter protocolOut = Console.Out;
Console.SetOut(TextWriter.Null);
SynchronizationContext.SetSynchronizationContext(new InlineContext());

CEntity_Base[] cards = CardEntityLoader.LoadAll(Path.GetFullPath(CardAssetsRelative));

if (args.Contains("--export-cards-json"))
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CardsJsonRelative))!);
    CardVocabulary.ExportCardsJson(cards, CardsJsonRelative);
    Console.Error.WriteLine($"cards.json exported: {cards.Length} records");

    return 0;
}

if (!File.Exists(CardsJsonRelative))
{
    Emit(new { type = "error", code = "internal", message = $"{CardsJsonRelative} 없음 — --export-cards-json 먼저" });

    return 1;
}

string? resultLog = null;
string? matchLogDir = null;
string recordMode = "off";
string engineSha = "";

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--result-log") resultLog = args[i + 1];
    if (args[i] == "--match-log-dir") matchLogDir = args[i + 1];
    if (args[i] == "--record-mode") recordMode = args[i + 1];
    if (args[i] == "--engine-sha") engineSha = args[i + 1];
}

CardVocabulary vocab = CardVocabulary.FromCardsJson(CardsJsonRelative);
RlMatchHost host = new(cards, vocab);
host.ConfigureRecording(matchLogDir, recordMode, engineSha);

string obsSchemaHash = Convert.ToHexString(
    SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", RlSchema.FeatureNames)))).ToLowerInvariant();

int resetCount = 0;
string matchId = "";
TurnMessage? lastTurn = null;
bool claimed = false;

for (string? line = Console.In.ReadLine(); line is not null; line = Console.In.ReadLine())
{
    JsonDocument request;

    try
    {
        request = JsonDocument.Parse(line);
    }
    catch (JsonException)
    {
        Emit(new { type = "error", code = "protocol_violation", message = "not a JSON object" });

        continue;
    }

    string type = request.RootElement.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";

    try
    {
        switch (type)
        {
            case "hello":
                Emit(new
                {
                    type = "welcome",
                    protocol = 1,
                    obsSchemaVersion = RlSchema.ObsSchemaVersion,
                    obsSize = RlSchema.ObsSize,
                    obsSchemaHash,
                    actionSchemaVersion = RlSchema.ActionSchemaVersion,
                    actionSize = RlSchema.ActionSize,
                    schema = new { maxHand = RlSchema.MaxHand, maxField = RlSchema.MaxField, maxChoice = RlSchema.MaxChoice },
                    vocabVersion = vocab.Version,
                    vocabSize = vocab.Count,
                    vocabHash = vocab.Hash,
                });
                break;

            case "describe":
                Emit(new { type = "schema", obsSchemaHash, features = RlSchema.FeatureNames });
                break;

            case "claim":
                claimed = true;
                Emit(new { type = "claimed", seats = request.RootElement.GetProperty("seats") });
                break;

            case "reset":
            {
                if (!claimed)
                {
                    Emit(new { type = "error", code = "protocol_violation", message = "claim 전 reset" });

                    break;
                }

                int seed = request.RootElement.GetProperty("seed").GetInt32();
                int maxSteps = request.RootElement.TryGetProperty("maxSteps", out JsonElement cap) ? cap.GetInt32() : 2000;
                resetCount++;
                matchId = $"m-{seed}-{resetCount}";
                host.MatchId = matchId;

                EmitOutcome(host.Reset(seed, request.RootElement.GetProperty("decks"), maxSteps));
                break;
            }

            case "action":
            {
                int seat = request.RootElement.GetProperty("seat").GetInt32();
                int index = request.RootElement.GetProperty("index").GetInt32();
                int[]? mask = host.PendingMask(seat);

                if (lastTurn is null || mask is null || lastTurn.Seat != seat)
                {
                    Emit(new { type = "error", code = "protocol_violation", message = $"seat {seat}의 차례가 아님" });

                    break;
                }

                if (index < 0 || index >= mask.Length || mask[index] == 0)
                {
                    // 프로토콜 §5: 상태 무변경 + error + 같은 turn 재발행.
                    Emit(new { type = "error", code = "illegal_action", message = $"index {index}" });
                    EmitTurn(lastTurn);

                    break;
                }

                EmitOutcome(host.Step(seat, index));
                break;
            }

            default:
                Emit(new { type = "error", code = "protocol_violation", message = $"unknown type '{type}'" });
                break;
        }
    }
    catch (Exception ex)
    {
        Emit(new { type = "error", code = "internal", message = ex.Message });
        Console.Error.WriteLine(ex.ToString());
        EmitResult(new ResultMessage(0, 0, null, true, "aborted", 0, 0));
    }
}

return 0;

void EmitOutcome(object outcome)
{
    switch (outcome)
    {
        case TurnMessage turn:
            EmitTurn(turn);
            break;

        case ResultMessage result:
            EmitResult(result);
            break;
    }
}

void EmitTurn(TurnMessage turn)
{
    lastTurn = turn;
    Emit(new
    {
        type = "turn",
        matchId,
        seat = turn.Seat,
        stepIndex = turn.StepIndex,
        observation = turn.Observation,
        actionMask = turn.Mask,
        legalCount = turn.LegalCount,
    });
}

void EmitResult(ResultMessage result)
{
    lastTurn = null;

    var payload = new
    {
        type = "result",
        matchId,
        rewards = new Dictionary<string, double> { ["1"] = result.RewardSeat1, ["2"] = result.RewardSeat2 },
        winnerSeat = result.WinnerSeat,
        isDraw = result.IsDraw,
        reason = result.Reason,
        steps = result.Steps,
        turns = result.Turns,
    };

    Emit(payload);

    string summary = JsonSerializer.Serialize(payload);

    if (resultLog is not null)
    {
        File.AppendAllText(resultLog, summary + "\n");
    }
    else
    {
        Console.Error.WriteLine(summary);
    }

    foreach (string note in host.Overflows.Concat(host.AutoAnswered.Distinct()))
    {
        Console.Error.WriteLine($"note: {note}");
    }

    host.Overflows.Clear();
}

void Emit(object message)
{
    protocolOut.WriteLine(JsonSerializer.Serialize(message));
    protocolOut.Flush();
}

/// <summary>AS-IS async void 콜백이 죽지 않게 하는 인라인 컨텍스트 — MatchSmoke.SilentContext와 동일.</summary>
sealed class InlineContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        try { d(state); } catch { /* AS-IS 표현층 예외 삼킴 — Unity 등가 */ }
    }

    public override void Send(SendOrPostCallback d, object? state) => Post(d, state);
}
