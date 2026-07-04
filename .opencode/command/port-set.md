---
description: (v2로 대체됨) SET+COLOR 배치 포팅 — 실제 실행은 porting/scripts/port-batch.sh 외부 루프. 이 커맨드는 안내만 한다.
agent: porter
---
**아무 파일도 읽거나 쓰지 말고**, 아래 안내문만 그대로 출력하고 종료하세요. 다른 행동 금지.

---

`/port-set` 은 **pipeline-v2 로 대체**되었습니다. 이 세션(로컬 모델)이 배치를 자체 관리하면 컨텍스트 초과로 수렴하지 않습니다 (2026-07-03 BT1 Blue/Yellow 실증: 카드 0장 포팅).

배치 포팅은 **터미널에서 외부 드라이버를 실행**하세요:

```bash
# SET+COLOR 전체 (스켈레톤 스텁만 대상, 라이브 카드는 건드리지 않음)
porting/scripts/port-batch.sh $ARGUMENTS

# 카드당 타임아웃(초) 지정
porting/scripts/port-batch.sh $ARGUMENTS 600

# 특정 카드만
porting/scripts/port-batch.sh $ARGUMENTS 600 BT1_031 BT1_032
```

드라이버가 카드별 브리프 생성 → 카드마다 독립된 `opencode run --command port-card-brief` 세션(타임아웃 캡) → 마지막에 바인딩 게이트 1회 실행까지 자동으로 처리합니다. 결과 요약과 STOP 목록은 `porting/stop/<SET>.<COLOR>.md` 를 확인하세요.
