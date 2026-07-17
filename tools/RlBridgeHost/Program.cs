using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Tools.RlBridgeHost;

// (M4) seat 매치 프로토콜 v1의 stdio 전송 래퍼 — docs/audit/rl_seat_protocol_v1.md.
// stdin 1행 = 요청 1건, stdout 1행 = 응답 1건(순서 보존). 진단은 stderr로만.
// 사용:  dotnet run --project tools/RlBridgeHost
//        [--result-log <path>] [--log-level OFF|RESULT|REPLAY|ANALYSIS|TRACE] [--event-log <path>]
//        [--strict]  (B4/D6 퍼징 프로파일: unbound 효과 = 하드 실패 → 예외 채널 수확)

string? resultLogPath = null;
string? eventLogPath = null;
MatchLogLevel logLevel = MatchLogLevel.Off;
bool strictUnbound = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--strict":
            strictUnbound = true;
            break;
        case "--result-log" when i + 1 < args.Length:
            resultLogPath = args[++i];
            break;
        case "--event-log" when i + 1 < args.Length:
            eventLogPath = args[++i];
            break;
        case "--log-level" when i + 1 < args.Length:
            logLevel = Enum.Parse<MatchLogLevel>(args[++i], ignoreCase: true);
            break;
    }
}

var host = new SeatMatchHost(
    resultLogPath: resultLogPath,
    logLevel: logLevel,
    eventLogPath: eventLogPath,
    strictUnbound: strictUnbound);
Console.Error.WriteLine("rl-bridge-host: ready (protocol v1, stdio)");

string? line;
while ((line = Console.ReadLine()) is not null)
{
    if (line.Length == 0)
    {
        continue;
    }

    foreach (string response in await host.HandleLineAsync(line))
    {
        Console.WriteLine(response);
    }

    Console.Out.Flush();
}
