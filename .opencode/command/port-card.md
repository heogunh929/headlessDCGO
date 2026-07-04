---
description: (v2로 대체됨) 한 장 포팅 — 실제 포팅은 /port-card-brief (브리프 기반). 이 커맨드는 브리프를 준비해 위임한다.
agent: porter
---
카드 **$ARGUMENTS** 포팅 요청입니다. 이 커맨드는 **pipeline-v2 로 대체**되었습니다 — 90KB 카탈로그 주입은 porter 컨텍스트(32K)를 넘겨 작동하지 않습니다. 아래 안내문만 그대로 출력하고 종료하세요. 다른 행동 금지.

---

한 장 포팅은 **브리프 기반 커맨드**를 사용하세요:

```bash
# 1) 브리프가 없으면 먼저 생성 (id에서 SET/COLOR 판별해 대입)
python3 scripts/make-card-brief.py <SET> <COLOR>

# 2) 브리프 기반 포팅 (독립 세션, 카탈로그 주입 없음)
opencode run --command port-card-brief "$ARGUMENTS"
```

또는 외부 드라이버로 한 장만: `scripts/port-batch.sh <SET> <COLOR> 600 $ARGUMENTS` (브리프 생성·게이트까지 자동).
