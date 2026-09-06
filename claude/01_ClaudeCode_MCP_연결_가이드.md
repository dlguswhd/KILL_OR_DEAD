# Claude Code + Unity MCP 연결 가이드

> 최종 수정: 2026-09-01
> 프로젝트: `C:\Unity\KILL_OR_DEAD` (Unity 6000.5.1f1 / URP 17.5)

---

## 현재 상태

| 항목 | 상태 |
|---|---|
| Unity CLI 1.0.0-beta.6 설치·로그인 | ✅ 완료 (전역 설치라 프로젝트 재생성과 무관) |
| Claude Code 2.1.252 설치 | ✅ 완료 (`C:\Users\hi128\.local\bin\claude.exe`) |
| Claude Code PATH 등록 | ❌ **할 일 1** |
| `com.unity.pipeline` 패키지 | ⚠️ manifest.json에 기록됨 → **에디터에서 임포트 필요 (할 일 2)** |
| Claude Code용 MCP 등록 | ❌ **할 일 3** (기존 등록은 Claude Desktop 대상이라 재사용 불가) |
| 에셋 (TSP / WarFX / Low Poly) | ❌ **할 일 4** — 프로젝트 재생성으로 전부 사라짐 |

> **왜 Cowork가 아니라 터미널인가**
> Cowork 세션은 Anthropic 클라우드에서 실행되고, 로컬 stdio MCP 서버를 전달받지 못합니다.
> Unity CLI의 MCP는 stdio 전용이라 원격 전송을 지원하지 않아 우회도 불가능합니다.
> Claude Code CLI는 PC에서 직접 실행되므로 이 제약이 없습니다.

---

## 할 일 1 — Claude Code PATH 등록

설치는 됐지만 PATH에 안 잡혀서 `claude` 명령이 인식되지 않는 상태입니다.

```powershell
$userPath = [Environment]::GetEnvironmentVariable('Path','User')
[Environment]::SetEnvironmentVariable('Path', "$userPath;$env:USERPROFILE\.local\bin", 'User')
```

그다음 **PowerShell 창을 닫고 새로 엽니다.**

> ⚠️ `$env:Path + ...` 형태로 저장하면 안 됩니다. 시스템 PATH까지 사용자 PATH에 복사되어
> 나중에 중복·충돌 문제를 일으킵니다. 반드시 위처럼 `'User'` 스코프를 따로 읽어서 붙이세요.

확인:

```powershell
claude --version
```

`2.1.252 (Claude Code)`가 나오면 성공입니다.

---

## 할 일 2 — com.unity.pipeline 임포트

MCP가 에디터를 조작하는 통로가 되는 패키지입니다. 새 프로젝트에는 없어서 `Packages\manifest.json`에
다음 줄을 추가해뒀습니다:

```json
"com.unity.pipeline": "0.5.0-exp.1",
```

Unity 에디터를 켜거나, 이미 켜져 있다면 **에디터 창을 클릭해 포커스를 주면** 자동으로 패키지를
내려받아 임포트합니다.

### 확인 방법

`Logs\Editor.log`에 다음 줄이 찍히면 파이프라인 서버가 뜬 것입니다:

```
Start HTTP server: port:7800
```

PowerShell에서:

```powershell
Select-String -Path "C:\Unity\KILL_OR_DEAD\Logs\Editor.log" -Pattern "Start HTTP server"
```

Package Manager(`Window` → `Package Manager`)의 `In Project` 목록에 **Unity Pipeline**이
보여도 됩니다.

---

## 할 일 3 — Claude Code용 MCP 등록

**에디터가 켜져 있어야 합니다.** 파이프라인 서버가 에디터 안에서 돌기 때문입니다.

프로젝트 폴더로 이동 (이제 대괄호가 없어서 `cd`가 정상 동작합니다):

```powershell
cd C:\Unity\KILL_OR_DEAD
```

지원 클라이언트 이름 확인:

```powershell
unity mcp configure --list
```

목록에 `claude-code`가 있으면:

```powershell
unity mcp configure claude-code
```

없으면 Claude Code 쪽 명령으로 직접 등록합니다 (이 방법이 더 확실합니다):

```powershell
claude mcp add unity -- unity mcp
```

> 이전에 실행한 `unity mcp configure claude`는 **Claude Desktop 채팅용** 설정 파일에 기록된 것이라
> Claude Code에는 적용되지 않습니다. 위 명령을 새로 실행해야 합니다.

---

## 할 일 4 — 에셋 재임포트

프로젝트를 새로 만들면서 사라진 것들입니다. **라이선스는 계정에 남아 있으므로 재구매 불필요합니다.**

Unity 에디터 → `Window` → `Package Manager` → 좌측 상단 드롭다운을 **`My Assets`**로 변경 →
각 항목을 `Download` → `Import`:

- **Tactical Shooter Pack (TSP)** — KINEMATION. 주무기 AK105, 보조무기 WK-11 Viper가 여기 들어 있음
- **War FX** — JMO Assets. 총알 임팩트, 총구 구멍, 벽 튐 효과
- **Low Poly AR Weapon Pack 3** — 부착물용
- **Low Poly SMG Weapon Pack 3** — 부착물용

> ⚠️ **WarFX URP 주의사항**
> WarFX는 URP 셰이더를 제공하지 않습니다. Lit 버전 프리팹·머티리얼은 URP에서 분홍색으로 보입니다.
> 반드시 **Unlit 버전**을 사용하세요.

---

## 검증 — 전부 됐는지 한 번에 확인

새 PowerShell 창에서:

```powershell
cd C:\Unity\KILL_OR_DEAD
claude
```

세션 안에서:

```
/mcp
```

`unity`가 **connected**로 뜨면 성공입니다.

### 실전 확인

Claude Code에게 이렇게 시켜보세요:

> 유니티 프로젝트에 Texture2D 에셋이 몇 개 있는지 확인해줘

내부적으로 이런 호출이 나갑니다:

```
unity command eval --code 'return AssetDatabase.FindAssets("t:Texture2D").Length;'
```

숫자가 돌아오면 **에디터와 완전히 연결된 것**입니다.
`eval`은 도메인 리로드 없이 실행 중인 에디터에서 C# 스니펫을 바로 돌립니다.
씬 조작, 에셋 조회, 프리팹 생성, 콘솔 로그 확인까지 전부 여기서 나옵니다.

---

## Unity 공식 스킬 붙이기

claude.ai 계정에는 Unity 공식 플러그인이 이미 활성화되어 있습니다.
Claude Code 세션에서도 쓰려면:

```
/plugin
```

`unity` 플러그인을 설치하면 스킬 21종이 붙습니다:

| 분야 | 스킬 |
|---|---|
| UI | `ui`, `ui-uitk`, `ui-ugui`, `ui-imgui` |
| 2D | `2d-pixel-perfect`, `manage-sprite-atlas`, `tilemap-*` |
| 텍스트 | `optimize-text-mesh-pro`, `localization` |
| 백엔드·수익화 | `build-live-game`, `setup-multiplayer-services`, `implement-in-app-purchases`, `levelplay-unity-integration` |
| 렌더링 | `shader-graph-create-custom-node`, `validate-urp-render-graph-renderer-feature` |

---

## 트러블슈팅

| 증상 | 원인 / 해결 |
|---|---|
| `claude` 용어가 인식되지 않습니다 | PATH 미등록. 할 일 1 수행 후 **새 창**에서 재시도 |
| `/mcp`에 unity가 안 뜸 | `claude mcp list`로 등록 확인 → 세션 재시작 |
| unity가 뜨지만 `failed` | 에디터가 꺼져 있음. 켜고 재시도 |
| `eval` 타임아웃 | 에디터가 컴파일 중이거나 재생 모드. 대기 |
| `Start HTTP server` 로그 없음 | pipeline 패키지 임포트 안 됨. 할 일 2 재확인 |
| 포트 7800 충돌 | `Logs\Editor.log`에서 실제 포트 번호 확인 |
| `unity` 명령 인식 안 됨 | Unity CLI가 PATH에 없음. 터미널 재시작 |
| 설치 스크립트가 멈춘 것처럼 보임 | PS 5.1 진행률 표시 문제. `$ProgressPreference = 'SilentlyContinue'` 먼저 실행 |

로그 확인:

```powershell
Get-Content -Path "C:\Unity\KILL_OR_DEAD\Logs\Editor.log" -Tail 50
```

---

## 연결되면 바로 할 것

프로젝트가 백지 상태라 순서대로 쌓아 올리면 됩니다.

1. `Assets/_Game` 폴더 구조 생성 (Scripts / Prefabs / Scenes / Art / Settings)
2. TSP 프리팹 목록 조회 → AK105, WK-11 Viper 위치 파악
3. 부위별 HP 전투 코어 작성
   - 플레이어 7부위: 머리 30, 흉부 100, 복부 100, 팔 150, 다리 150
   - 머리·흉부·복부 중 하나라도 소진 시 사망
   - HP 0인 부위 추가 피격 시 데미지 일부를 나머지 부위로 분산
   - 적: 공유 HP 500 + 부위 배수 (머리 x5.0, 흉부 x2.0, 복부 x1.5, 팔·다리 x0.5)
4. 반동 시스템 — 배틀그라운드식 수동 제어 (마우스로 직접 눌러 잡는 방식)
5. 거점(집) → 임무 → 보상 → 총기 커스터마이징 루프
   - 작업 테이블에서 총을 띄워놓고 구매한 부착물을 눌러 장착

---

## 참고 자료

- [Claude Code 설치 문서](https://code.claude.com/docs/en/setup)
- [Claude Code 설치 트러블슈팅](https://code.claude.com/docs/en/troubleshoot-install)
- [Unity CLI 공식 문서](https://docs.unity.com/en-us/unity-cli)
- [Unity CLI eval + MCP 실전 가이드](https://perflint.dev/blog/unity-cli-eval-mcp-guide/)
- Cowork 로컬 MCP 미지원 이슈: [#20377](https://github.com/anthropics/claude-code/issues/20377), [#23424](https://github.com/anthropics/claude-code/issues/23424), [#42453](https://github.com/anthropics/claude-code/issues/42453)
