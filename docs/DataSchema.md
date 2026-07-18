# OUROBOROS: SWARM — 데이터 스키마 기준선

> Step 01에서 필드 계약과 입력 형식을 고정했고, Step 02에서 실제 ScriptableObject 타입·검증·런타임 복사 구조를 구현했다. 런타임은 원본 SO 목록을 직접 변경하거나 공유하지 않는다.

## ID 규칙

- 소문자 `snake_case`를 사용한다.
- 카탈로그 안에서 고유해야 하며 빈 문자열과 공백만 있는 값을 거부한다.
- 런타임 ID와 공격 이벤트 ID는 세션 단조 증가 정수이며 데이터 ID와 구분한다.
- 적: `enemy_*`, 보스: `boss_*`, 업그레이드: 기능 중심 명사, 역할: `shield/attack/laser/control`.

## ScriptableObject 필드 목록

| SO | 필수 필드 |
| --- | --- |
| `OSPlayerBalanceData` | dataVersion, maxHealth, moveSpeed, hitInvulnerability, headDamage, headFireInterval, headRange, magnetRadius |
| `OSBodyBalanceData` | dataVersion, fragmentRequirement, technicalGuard, segmentSpacing, pathSampleInterval, pathReserveDistance, bodyDamageRate, cutGuardDuration, 역할 4종 설정, 폭발 설정 |
| `OSEncounterBalanceData` | dataVersion, enemyDefinitions, eliteDefinition, bossDefinition, activeEnemyLimit, projectileLimit, pickupLimit, vfxLimit |
| `OSWaveScheduleData` | dataVersion, entries(start/end, weights, spawnRate, specialEvent, targetActiveEnemies) |
| `OSUpgradeCatalog` | dataVersion, entries(id, category, maxLevel, operation, perLevelValue, clamp, candidateWeight) |
| `OSFeedbackCatalog` | dataVersion, roleVisuals, attackVfxKeys, telegraphKeys, audioKeys |
| `OSCustomCharacterSettings` | dataVersion, endpoint, limits, timeout, polling, mockAssets, outputSpec |

## 기본 데이터 에셋

| 데이터 | 경로 |
| --- | --- |
| 플레이어 밸런스 | `Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset` |
| 몸통·역할·폭발 밸런스 | `Assets/Ouroboros/Data/Balance/OSBodyBalance.asset` |
| 피드백 카탈로그 | `Assets/Ouroboros/Data/Balance/OSFeedbackCatalog.asset` |
| 적·풀 상한 | `Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset` |
| 웨이브 스케줄 | `Assets/Ouroboros/Data/Waves/OSWaveSchedule.asset` |
| 업그레이드 카탈로그 | `Assets/Ouroboros/Data/Upgrades/OSUpgradeCatalog.asset` |

- 누락된 기본 데이터와 `00_Boot` 참조를 생성·보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 02 Data Foundation`을 사용한다.
- 각 SO의 `OnValidate`는 개별 값을 검사하고, `OSDataValidator`는 ID 중복·참조·웨이브 적 ID·업그레이드 후보 수 같은 교차 계약을 검사한다.
- `OSSessionRuntimeState.InitializeFrom`은 검증을 통과한 데이터에서 새 상태와 목록 컨테이너를 만들며, 설정 오류는 `OSResultCode.ConfigurationError`로 반환한다.

## 웨이브 입력

- 원본 표: `Assets/Ouroboros/Data/Waves/OSWaveSchedule.csv`
- 시간은 초 단위 `[start_seconds, end_seconds)` 구간이다.
- 일반 적 가중치 합은 특별 이벤트 전용 행을 제외하고 1이어야 한다.
- `special_event` 값은 `none`, `elite_accelerator`, `boss_warning`, `boss_swarm_core` 중 하나다.
- CSV를 직접 런타임 로드하지 않고 검수된 `OSWaveScheduleData` 에셋을 사용한다.
