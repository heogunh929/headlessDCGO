#!/usr/bin/env python3
"""AsIsSync — AS-IS 소스를 new-TO-BE 경로로 동기화한다.

  AS-IS      DCGO/Assets/Scripts/                       (원본, 수정 금지, 미커밋)
  new-TO-BE  src/HeadlessDCGO.Engine/Assets/Scripts/    (빌드 대상, 커밋)

설계 원칙 — 변환 최소
    기본값은 **순수 바이트 복사**다. 변환은 substrate가 도저히 흡수할 수 없을 때만 쓰는 탈출구이며,
    추가할 때마다 TRANSFORMS에 선언하고 사유를 적는다.
    선언되지 않은 차이가 대장에 나타나면 그것은 누군가 손으로 고쳤다는 뜻이다.

    인코딩 변환을 하지 않는 이유: AS-IS는 UTF-8 4,285 / cp932 52 / 혼합-깨짐 17로 섞여 있으나,
    Roslyn이 이 바이트 그대로 4,354파일을 컴파일하는 것을 실측했다.
    깨진 바이트는 카드 설명 문자열·주석 안에 있다. 증명된 필요 없이 원본 데이터를 고치지 않는다.

사용법
    python3 tools/AsIsSync/sync.py            # 동기화
    python3 tools/AsIsSync/sync.py --check    # 변경 없이 대장만 출력 (게이트용)
"""

import argparse
import filecmp
import pathlib
import shutil
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
SRC = REPO / "DCGO" / "Assets" / "Scripts"
DST = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts"

# 복사 대상 확장자. `.meta` 4,777개는 유니티 에디터 전용이라 제외(2026-07-29 확정).
INCLUDE_SUFFIXES = {".cs"}

# 선언된 변환. 비어 있는 것이 정상이자 목표다.
# 항목을 추가할 때는 (이름, 사유, 적용 함수)를 넣고 아래 apply_transforms에서 태운다.
TRANSFORMS: list[tuple[str, str]] = []


def apply_transforms(data: bytes, rel: pathlib.PurePath) -> bytes:
    """선언된 변환을 순서대로 적용한다. 현재는 없다 — 순수 복사."""
    return data


def rel_paths(root: pathlib.Path) -> set[pathlib.PurePath]:
    if not root.is_dir():
        return set()
    return {
        p.relative_to(root)
        for p in root.rglob("*")
        if p.is_file() and p.suffix in INCLUDE_SUFFIXES
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="변경하지 않고 대장만 출력한다(게이트용). 차이가 있으면 종료코드 1")
    args = ap.parse_args()

    if not SRC.is_dir():
        print(f"AS-IS 소스를 찾을 수 없다: {SRC}", file=sys.stderr)
        print("DCGO/ 는 로컬 전용이며 커밋되지 않는다. 로컬에 원본을 두어야 한다.", file=sys.stderr)
        return 2

    src_files = rel_paths(SRC)
    dst_files = rel_paths(DST)

    to_add = sorted(src_files - dst_files)
    to_remove = sorted(dst_files - src_files)
    common = sorted(src_files & dst_files)

    to_update = []
    for rel in common:
        s, d = SRC / rel, DST / rel
        want = apply_transforms(s.read_bytes(), rel)
        if not d.exists() or d.read_bytes() != want:
            to_update.append(rel)

    print(f"AS-IS     {len(src_files):5} 파일   {SRC}")
    print(f"new-TO-BE {len(dst_files):5} 파일   {DST}")
    print(f"  추가 {len(to_add)} · 갱신 {len(to_update)} · 삭제 {len(to_remove)}")
    if TRANSFORMS:
        print(f"  선언된 변환 {len(TRANSFORMS)}:")
        for name, why in TRANSFORMS:
            print(f"    - {name}: {why}")
    else:
        print("  선언된 변환 없음 — 순수 바이트 복사")

    if args.check:
        drift = len(to_add) + len(to_update) + len(to_remove)
        if drift:
            print(f"\n표류 {drift}건. new-TO-BE가 AS-IS와 어긋나 있다.")
            for rel in (to_add + to_update + to_remove)[:40]:
                print(f"    {rel}")
            if drift > 40:
                print(f"    … 외 {drift - 40}")
            return 1
        print("\n표류 없음.")
        return 0

    for rel in to_remove:
        (DST / rel).unlink()
    for rel in to_add + to_update:
        s, d = SRC / rel, DST / rel
        d.parent.mkdir(parents=True, exist_ok=True)
        d.write_bytes(apply_transforms(s.read_bytes(), rel))

    # 빈 디렉터리 정리
    for p in sorted(DST.rglob("*"), key=lambda x: -len(x.parts)):
        if p.is_dir() and not any(p.iterdir()):
            p.rmdir()

    print(f"\n동기화 완료 — new-TO-BE {len(rel_paths(DST))} 파일")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
