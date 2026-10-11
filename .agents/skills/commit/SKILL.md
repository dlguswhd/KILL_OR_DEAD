---
name: commit
description: 사용자가 "커밋해줘", "이 변경사항 커밋", "마지막 커밋 양식에 맞게 수정", "커밋 메시지 정리", "amend" 등을 요청할 때 사용합니다. 저장소 관례를 탐지해 양식에 맞는 커밋을 생성하거나 마지막 커밋을 상세 메시지로 수정합니다. Unity 저장소 사전 점검을 포함합니다.
license: MIT
compatibility: opencode, claude-code, codex, cursor, gemini-cli, github-copilot
metadata:
  author: Chillbok
  version: "1.0.0"
allowed-tools:
  - Bash
---

# 커밋 생성 및 양식 수정 (commit)

변경 사항을 분석해 저장소 관례에 맞는 커밋을 생성하거나, 마지막 커밋을 상세한 양식으로 다시 작성합니다. **push는 하지 않습니다** — 사용자가 결과를 확인한 뒤 직접 push합니다.

## 두 가지 진입점

- **모드 A — 새 커밋 생성**: 사용자가 "이 변경사항 커밋해줘"라고 요청
- **모드 B — amend**: 사용자가 "방금 커밋한 거 양식에 맞게 수정해줘"라고 요청 (짧은 커밋 → 상세 메시지로 확장)

## 수행 절차

### 1. 의도 수집
- 사용자에게서 이번 커밋의 **why**(의도/판단 근거)를 받는다.
- 없으면 `git status` / diff를 보고 되물어 확인한다. 사용자가 남긴 맥락은 어떤 경우에도 누락하지 않는다.

### 2. 사전 점검 (금지 파일 · Unity)
`references/unity-git-guard.md` 절차를 적용한다.
- `git status --porcelain`으로 변경 파일 확인
- 금지 경로(`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`, `*.zip` 등)가 staged면 경고하고 스테이징에서 제외
- 신규 에셋에 `.meta`가 누락됐으면 함께 추가 안내
- 씬/프리팹 변경 시 "Unity 저장 후 종료" 상태인지 확인
- 대용량 파일이면 LFS 여부 확인

### 3. 저장소 관례 탐지
다음 우선순위로 커밋 메시지 스타일을 결정한다.
1. 명시적 규칙: `CONTRIBUTING.md`, `AGENTS.md`, `.commitlintrc*`, `commitlint.config.*`, `.gitmessage`, `git config commit.template`
2. 기존 로그: `git log --oneline -10`, `git log --format="%B" -n 5`
3. 없으면 Conventional Commits 기본 + 프로젝트 언어(한국어 저장소면 한국어)

관찰할 것: 접두사 유무/종류, 언어, 제목 종결(`~함`/`~했음`), 본문 구조(불릿, why 포함 여부).

### 4. 타입 결정
diff 성격으로 타입을 정한다: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `perf`, `style`.
현재 브랜치명(`<type>/#<N>`)에서 이슈 번호를 추출해 커밋에 `(#N)` 참조 여부를 정한다(저장소 관례에 따름).

### 5-A. 모드 A — 새 커밋
```
git add <확인된 파일들>
git commit -m "<임시 또는 최종 메시지>"
```
- 사용자가 의도만 줬다면 최종 양식으로 바로 작성해도 되고, "간단 커밋 후 amend" 흐름을 원하면 짧게 커밋한 뒤 모드 B로 확장한다.

### 5-B. 모드 B — amend
```
git branch -vv             # push 여부 확인
git show --stat HEAD
git show HEAD
git commit --amend -m "<최종 메시지>"
```
- `git log @{u}..HEAD` / `Your branch is ahead` 등으로 **원격 push 여부**를 확인한다. 이미 push된 커밋이면 경고하고 명시적 동의 없이는 amend하지 않는다.
- 미커밋 변경(tracked/untracked)이 있으면 동의 없이 amend하지 않는다. 필요 시 `git stash`(`-u`)로 임시 보관 후 진행하고 완료 후 `git stash pop`.

### 6. 메시지 작성
- 제목: `<type>: <요약>` (저장소 관례 준수)
- 본문: `git show`로 파악한 what/how를 불릿으로, 사용자의 why를 보존·보강
- 참고/후속 작업이 있으면 별도 표기

### 7. 확인 후 실행
- 최종 메시지 초안을 **먼저 보여주고 동의**를 구한다.
- 동의하면 커밋/amend 실행, 아니면 사용자 입력을 반영한다.

### 8. 보고
- 커밋 해시와 요약을 보고한다.
- **push는 하지 않는다.** 사용자가 확인 후 직접 push하도록 안내한다.

## 주의사항
- push 이전의 커밋만 amend하는 것을 원칙으로 한다. push된 커밋은 명시적 동의 시에만.
- 작업 트리에 미커밋 변경이 있으면 동의 없이 amend하지 않는다.
- 커밋 저자/날짜 등 다른 메타데이터는 변경하지 않는다.
- 사용자가 제공한 why·판단 근거·후속 계획은 어떤 경우에도 누락하지 않는다.
- 금지 경로가 포함되거나 `.meta`가 누락되면 커밋을 중단하고 사용자에게 알린다.
