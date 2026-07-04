namespace HeadlessDCGO.Engine.Headless.Diagnostics;

/// <summary>
/// (L4, FR-7.1) 매치 로그 레벨 — 누적적. OFF=대량 학습(오버헤드 0), RESULT=승패 요약 1줄
/// (브리지 호스트 result-log 소관), REPLAY=사람이 알아볼 굵은 사건, ANALYSIS=효과 발동 등
/// 잔 사건(밸런스 집계), TRACE=엔진 내부 진단 전부.
/// </summary>
public enum MatchLogLevel
{
    Off = 0,
    Result = 1,
    Replay = 2,
    Analysis = 3,
    Trace = 4
}
