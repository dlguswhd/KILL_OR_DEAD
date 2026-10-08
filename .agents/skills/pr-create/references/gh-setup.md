# GitHub CLI(gh) 전제 조건 · 설치

이슈/PR 생성에는 GitHub CLI(`gh`)가 필요하다. **없으면 먼저 사용자에게 묻고, 동의를 얻은 뒤 AI가 설치**한다. 동의 없는 설치나 `curl | bash`는 금지한다.

## 1. 감지
```
gh --version
gh auth status
```
- `gh`가 없음 → 2번 설치 절차
- `gh`는 있는데 인증 안 됨 → 4번 인증

## 2. OS · 패키지 매니저 감지
- macOS: `uname -s` = `Darwin`
- Linux: `uname -s` = `Linux`, `/etc/os-release`로 배포판 확인
- Windows: PowerShell / Git Bash, `winget` 사용 가능 여부 확인

## 3. 동의 후 설치 (AI가 실행)

**먼저 실행할 명령을 사용자에게 그대로 보여주고 동의를 구한다.** 동의하면 AI가 실행한다.

| OS | 우선 명령 | 대안 |
|---|---|---|
| macOS | `brew install gh` | brew 없으면 GitHub 릴리스 `.pkg`/바이너리 |
| Windows | `winget install --id GitHub.cli` | `scoop install gh` / `choco install gh` |
| Debian/Ubuntu | 공식 apt repo 등록 후 `sudo apt install gh` | `sudo snap install gh` |
| Fedora/RHEL | `sudo dnf install gh` | 릴리스 바이너리 |
| Arch | `sudo pacman -S github-cli` | 릴리스 바이너리 |
| 공통 폴백 | https://github.com/cli/cli/releases 에서 바이너리 | — |

설치 후 검증:
```
gh --version
```

## 4. 인증
```
gh auth login
```
- 브라우저 인증은 **사용자가 완료**한다.
- 완료 후 확인:
```
gh auth status
```

## 5. 실제 제약 (반드시 고려)
- **sudo / 관리자 권한**: AI가 명령을 실행해도 `sudo` 비밀번호나 Windows UAC는 사용자가 직접 입력해야 한다. AI는 "터미널에서 비밀번호를 입력해 주세요"라고 안내하고 기다린다.
- **macOS에 Homebrew가 없을 때**: Homebrew 자체 설치(오래 걸림)는 별도 동의가 필요하므로 **바이너리/`.pkg` 다운로드 폴백**을 기본으로 한다.
- **거부/실패 시**: 억지로 진행하지 않고, 만들려던 이슈/PR의 제목·본문 초안만 출력하고 종료한다(graceful exit).
