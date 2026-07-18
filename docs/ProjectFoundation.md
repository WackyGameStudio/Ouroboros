# OUROBOROS: SWARM — 프로젝트 기반

> 최근 갱신: 2026-07-18 KST
>
> 대상 Step: `OUROBOROS_SWARM_구현순서.md` Step 01

## 버전과 패키지

| 항목 | 기준 |
| --- | --- |
| Unity Editor | `6000.5.1f1` 고정 |
| 릴리스 계열 | Supported Update. 프로젝트 승인 기준이며 버전 변경 시 기반 회귀 테스트와 Windows/WebGL 빌드를 다시 검증 |
| 렌더링 | URP 2D `17.5.0`, Linear Color Space |
| 입력 | New Input System `1.19.0`만 사용 |
| UI | uGUI `2.5.0`, TextMeshPro 포함 |
| 테스트 | Unity Test Framework `1.7.0` |

## 해상도 정책

- 기준 Canvas: 1920×1080, `Scale With Screen Size`, Match 0.5
- Windows 기본: 1920×1080, 창 크기 조절 허용
- WebGL 기본: 960×540
- 플레이어가 포커스를 잃으면 전투가 계속 진행되지 않도록 `runInBackground=false`를 유지한다.

## 씬과 빌드

| 순서 | 씬 | 책임 |
| ---: | --- | --- |
| 0 | `Assets/Ouroboros/Scenes/00_Boot.unity` | 버전·씬·입력 계약 검증 후 MainMenu 이동 |
| 1 | `Assets/Ouroboros/Scenes/10_MainMenu.unity` | 최소 메뉴와 Game 진입 |
| 2 | `Assets/Ouroboros/Scenes/20_Game.unity` | 설계서 기준 계층과 HUD 와이어프레임 |

Build Profile:

- `Assets/Ouroboros/BuildProfiles/Windows Development.asset`
- `Assets/Ouroboros/BuildProfiles/WebGL Development.asset`

로컬 검증 출력은 Git에서 제외되는 `Builds/Step01/` 아래에 둔다.

## 저장소 정책

- Asset Serialization: Force Text
- Version Control Mode: Visible Meta Files
- 프로젝트 코드 Root Namespace: `Ouroboros`
- 전용 자산은 `Assets/Ouroboros/` 아래에 둔다.
- 프로젝트 설정은 `Ouroboros/Setup/Apply Step 01 Foundation` 메뉴로 재검증·초기 생성할 수 있다.
