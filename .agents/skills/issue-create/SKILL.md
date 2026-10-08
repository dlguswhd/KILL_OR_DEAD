---
name: issue-create
description: 사용자가 "이슈 만들어줘", "계획용 이슈 생성", "버그 이슈 등록", "작업 이슈 좀" 등을 요청할 때 사용합니다. 작업 성격을 판별해 표준 제목/라벨로 이슈를 생성하고, 생성된 이슈 번호를 보고합니다. GitHub CLI가 없으면 설치를 안내합니다.
license: MIT
compatibility: opencode, claude-code, codex, cursor, gemini-cli, github-copilot
metadata:
  author: Chillbok
  version: "1.0.0"
allowed-tools:
  - Bash
---

# 이슈 생성 (issue-create)

사용자가 설명한 작업을 GitHub 이슈로 생성한다. 규칙: **이슈만 생성**하고 브랜치·커밋·PR은 건드리지 않는다.

## 수행 절차

### 1. 의도 수집
- 어떤 작업인지, 배경은 무엇인지, 완료 조건은 무엇인지 대화로 수집한다. 부족하면 되묻는다.

### 2. gh 전제 조건
`references/gh-setup.md` 절차를 적용한다.
```
gh --version && gh auth status
```
- 없으면 OS를 판별해 **설치 명령을 제시하고 동의를 구한 뒤 AI가 설치**하고, `gh auth login`을 안내한 뒤 재개한다.
- 사용자가 거부하면 이슈 초안만 출력하고 종료한다.

### 3. 저장소 관례 탐지
```
gh label list
gh issue list --state all -L 10
```
- 사용 가능한 라벨 목록을 확인한다.
- 기존 이슈 제목 패턴과 본문 구조를 파악한다.
- `.github/ISSUE_TEMPLATE`가 있으면 그 구조를 우선한다.

### 4. 성격 판별 → type·라벨·제목
- 사용자 설명에서 type을 추론한다: `feat`(기능·프로토타입), `fix`(버그), `refactor`(리팩토링), `docs`(문서), `chore`/`test`/`perf`/`style`.
- 애매하면 후보를 제시해 질문한다.
- `references/issue-template.md`의 매핑으로 라벨을 정한다. 라벨이 저장소에 없으면 생략한다.
- 제목: `<type>: <한글 명사구>`.

### 5. 본문 초안 작성
- `references/issue-template.md`의 구조(`## 배경 / ## 요구사항 / ## 완료 조건 / ## 참고`)로 작성한다.
- 완료 조건은 체크리스트로 작성한다.
- **초안을 사용자에게 먼저 보여주고 확인**을 받는다.

### 6. 이슈 생성
```
gh issue create --title "<제목>" --body-file <임시파일> --label <라벨>
```
- 본문은 반드시 **임시 파일 + `--body-file`** 로 전달한다(한국어·줄바꿈·`` # ``·백틱 이스케이프 문제 회피).
- 라벨이 없으면 `--label`을 생략한다.

### 7. 번호 보고
- 출력된 URL에서 이슈 번호를 추출해 **`이슈 #N 생성됨`** 만 보고한다.
- 브랜치명은 안내하지 않는다(branch-create가 담당하며, 이슈 라벨로 스스로 판단한다).

## 주의사항
- 라벨을 임의로 새로 만들지 않는다.
- 기존 이슈 제목을 임의로 변경하지 않는다.
- gh 미인증·미설치 상태에서 임의로 진행하지 않는다.
- 이슈만 생성하고 다른 git 작업은 하지 않는다.
