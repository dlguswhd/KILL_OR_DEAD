---
name: branch-create
description: 사용자가 "브랜치 만들어줘", "N번 이슈로 브랜치 파줘", "작업 브랜치 생성" 등을 요청할 때 사용합니다. 이슈 번호와 이슈의 type을 확인해 <type>/#<N> 형식의 브랜치를 생성합니다. GitHub CLI가 없어도 type을 사용자에게 받아 동작합니다.
license: MIT
compatibility: opencode, claude-code, codex, cursor, gemini-cli, github-copilot
metadata:
  author: Chillbok
  version: "1.0.0"
allowed-tools:
  - Bash
---

# 브랜치 생성 (branch-create)

작업할 이슈 번호를 기준으로 표준 형식의 브랜치를 생성한다. `git`만으로 동작하며, `gh`는 이슈 type 조회에 선택적으로만 사용한다.

## 수행 절차

### 1. 입력
- 사용자에게서 **이슈 번호**를 받는다. 없으면 질문한다(규칙상 브랜치는 이슈 기반).
- 예: "6번 이슈로 브랜치 파줘"

### 2. 이슈 정보 조회 (gh가 있으면)
```
gh issue view <N> --json title,labels
```
- 이슈 제목의 `type:` prefix 또는 라벨에서 type을 추출한다.
- gh가 없거나 조회 실패 시 → 사용자에게 type을 묻거나 설명에서 추론한다. **실패로 끝내지 않는다.**

### 3. type 결정 (우선순위)
1. 이슈 제목의 `type:` prefix
2. 이슈 라벨 매핑 (`enhancement`→`feat`, `bug`→`fix`, `documentation`→`docs`, 그 외)
3. 사용자 지정/추론

type 어휘: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `perf`, `style`

브랜치명 = `<type>/#<N>` (예: `feat/#6`)

### 4. 저장소 관례 탐지
```
git branch -a
git log --oneline --merges -10
```
- 기존 브랜치 패턴을 학습해 형식을 확인한다.

### 5. 기준 브랜치·작업 상태 확인
```
git fetch origin
git status --porcelain
```
- 기본 브랜치(`main` 등)가 뒤처져 있으면 pull을 제안한다.
- 작업 트리에 미커밋 변경이 있으면 경고한다(브랜치 전환 오염 방지).
- **같은 이름 브랜치가 이미 있으면 중단**하고 사용자에게 확인한다.

### 6. 브랜치 생성
```
git switch -c "<type>/#<N>"
```
- `#` 때문에 **따옴표 필수**.
- 기본 브랜치에서 분기한다.

### 7. 보고
- `feat/#6 브랜치 생성됨` + 현재 상태 요약.
- 원격 push는 기본적으로 하지 않는다(필요 시 사용자가 수행하거나 pr-create 단계에서 처리).

## 주의사항
- 이슈 번호 없이 임의로 브랜치를 만들지 않는다.
- 기존 동명 브랜치·미커밋 변경·base 뒤처짐은 경고 후 사용자 확인을 거친다.
- 브랜치 생성만 담당하고 이슈·커밋·PR은 건드리지 않는다.
