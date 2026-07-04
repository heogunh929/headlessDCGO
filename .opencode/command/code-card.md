---
description: CODE one card's C# mirror from the plan (coder agent). Reads the plan + brief, writes the mirror. Pure plan→C# translation.
agent: coder
---
카드 **$ARGUMENTS** 의 C# 미러를 계획대로 작성하세요. `coder` 에이전트 규칙을 그대로 따릅니다.

1. 계획 읽기: `porting/data/plans/<SET>.<COLOR>/$ARGUMENTS.md` (분기별 팩토리·args·condition·STOP).
2. 브리프 읽기: `porting/briefs/<SET>.<COLOR>/$ARGUMENTS.md` (미러 뼈대 + 시그니처).
3. 미러 작성(스텁 교체): `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs`.
   미러 뼈대 그대로(namespace 필수, public sealed, IReadOnlyList). 계획의 팩토리·인자만, 발명 금지.
   계획이 불명한 분기는 `// STOP: plan unclear` 로 남김. **테스트 안 씀, 커밋 안 함.**
