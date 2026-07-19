# OUROBOROS: SWARM 저장소 작업 규칙

이 파일은 저장소 루트 이하 전체에 적용한다. 사용자의 현재 요청이 이 파일과 충돌하면 사용자 요청을 우선한다.

## 기준 문서

- 게임 규칙, 수치, Unity 구조, 클래스 책임, 수용 기준의 기준 문서: `docs/OUROBOROS_SWARM_Unity_설계서.md`
- 구현 순서, 선행 조건, 체크리스트, 게이트, 실제 구현 현황의 기준 문서: `docs/OUROBOROS_SWARM_구현순서.md`
- Step 00 운영 문서: `docs/RuleDecisionLog.md`, `docs/BalanceHypotheses.md`, `docs/AcceptanceCriteria.md`. 이 문서들은 기준 문서에서 추출한 구현용 기록이며 기준 문서를 대체하지 않는다.
- Step 01 기반 문서: `docs/ProjectFoundation.md`, `docs/DataSchema.md`, `docs/VisualPlaceholderGuide.md`. 프로젝트 설정, 데이터 입력 계약, 임시 비주얼 규칙을 확인할 때 사용한다.
- 두 문서의 역할을 섞지 않는다. 구현됐다는 사실은 설계서가 아니라 구현순서 문서에 기록하고, 구현 과정에서 규칙이나 구조가 바뀐 경우에만 설계서의 관련 절도 함께 수정한다.
- 문서 간 내용이 충돌하면 임의로 한쪽을 택하지 않는다. 설계서의 `0. 문서 우선순위와 충돌 해소`에 따라 판단하고, 판단이 제품 범위를 바꾸면 사용자에게 확인한다.

## 구현 작업 절차

1. 작업 전에 설계서의 관련 규칙과 구현순서 문서의 대상 Step, 선행 조건, 완료 기준을 읽는다.
2. 요청 범위를 가장 작은 Step 단위로 정하고 선행 Step이 충족됐는지 코드, 에셋, 설정, 테스트 결과로 확인한다.
3. 구현은 현재 설계 계약을 따른다. 설계 변경이 필요하면 코드만 다르게 만들지 말고 아래 문서 갱신 규칙을 함께 적용한다.
4. 변경 후 Unity 컴파일 오류와 Console Error/Exception을 확인하고, 위험에 비례해 EditMode, PlayMode 또는 수동 검증을 수행한다.
5. 구현 파일이나 에셋에 지속되는 변경이 생긴 작업은 종료 전에 구현순서 문서의 해당 Step 현황을 반드시 같은 변경 묶음에서 갱신한다.
6. 저장소 전체에 지속될 작업 방식이나 전제가 달라졌다면 이 `AGENTS.md`도 같은 변경 묶음에서 갱신한다.
7. 한 Step의 완료 기준과 필요한 검증을 모두 통과해 상태를 `완료`로 기록했으면, 해당 Step의 코드·에셋·설정·문서를 하나의 Step 전용 커밋으로 만들고 현재 브랜치의 추적 원격에 즉시 푸시한다.
8. Step 커밋과 원격 푸시 성공을 확인하기 전에는 다음 Step에 착수하지 않는다. 푸시가 실패하면 원인과 로컬 커밋 상태를 보고하고, 해결 전까지 완료 사실을 원격 반영된 것으로 간주하지 않는다.

## 문서별 갱신 기준

| 발생한 변경 | 갱신할 위치 |
| --- | --- |
| 코드, 씬, 프리팹, SO, 설정의 실제 구현 진척 | `OUROBOROS_SWARM_구현순서.md`의 해당 Step `### 구현 현황`과 관련 체크박스 |
| 자동/수동 테스트, 빌드 게이트 결과 | 구현순서 문서의 해당 Step 또는 게이트 |
| 게임 규칙, 확정 수치, 데이터 계약, 클래스 책임, 이벤트 순서, 수용 기준 변경 | `OUROBOROS_SWARM_Unity_설계서.md`의 관련 절과 구현순서 문서의 영향받는 Step |
| 가설 수치 조정과 플레이테스트 결론 | 설계서의 관련 `[가설]` 및 질문, 구현순서 문서의 테스트 기록 |
| 문서 경로, Unity 기준 버전, 필수 도구, 폴더/asmdef 경계, 공통 검증 절차 변경 | `AGENTS.md`와 필요 시 두 기준 문서 |

## 구현 현황 작성 규칙

- 구현순서 문서 `0.5 구현 현황 기록 규칙`의 형식을 따른다. 해당 Step에 `### 구현 현황`이 없으면 `### 목표` 설명 바로 뒤에 만든다.
- 상태는 `미착수`, `진행 중`, `차단`, `완료` 중 하나만 사용한다.
- 일부만 구현했으면 체크박스를 완료 처리하지 말고 `진행 중`으로 두며, 완료된 범위와 남은 범위를 구분한다.
- `[x]`는 해당 항목의 구현 증거와 검증 결과가 있을 때만 표시한다. 파일이 존재하거나 컴파일된다는 사실만으로 Step 전체를 완료 처리하지 않는다.
- `완료`는 해당 Step의 완료 기준과 필요한 테스트가 모두 통과했을 때만 사용한다.
- 회귀나 설계 변경으로 기준을 더 이상 만족하지 않으면 체크를 다시 열고 상태와 사유를 갱신한다.
- 날짜는 프로젝트 시간대인 KST의 `YYYY-MM-DD`를 사용한다.
- 관련 파일은 저장소 상대 경로로, 검증은 테스트 이름·플랫폼·결과가 드러나게 기록한다. 확인하지 않은 결과를 추정해 쓰지 않는다.
- 여러 Step에 걸친 변경은 실제로 영향받은 모든 Step을 갱신한다. 단순 문구 정리처럼 구현 진척이 없는 작업은 거짓 현황을 만들지 않는다.

## 설계 변경 규칙

- 구현과 설계가 다르면 먼저 버그인지 의도된 변경인지 구분한다.
- 의도된 변경이면 설계서의 관련 본문, `[확정]`/`[가설]` 표기, 테스트 및 MVP 수용 기준의 파급을 함께 검토한다.
- 밸런스 값은 가능하면 ScriptableObject에서 조정하고, 가설 변경을 이유로 규칙 코드에 임시 분기를 추가하지 않는다.
- 아직 결정되지 않은 사항은 구현 완료로 포장하지 말고 구현순서 문서에 차단 사유 또는 남은 작업으로 기록한다.

## `AGENTS.md` 자체 유지보수

- 이 파일에는 반복해서 적용할 수 있는 저장소 수준의 사실과 작업 계약만 둔다.
- 다음이 바뀌면 즉시 관련 내용을 고친다: 기준 문서의 이름/위치, Unity 기준 버전, 필수 패키지나 도구 전제, 폴더·asmdef 경계, 빌드/테스트 명령, 문서 현황 형식, 공통 완료 조건.
- 일회성 진행률, 임시 오류, 개인 메모, 특정 작업의 상세 내역은 이 파일에 누적하지 않는다. 그런 내용은 구현순서 문서의 해당 Step에 둔다.
- 새 규칙을 추가할 때 오래된 규칙과 중복되거나 충돌하는 문장을 함께 제거한다. 확인하지 않은 현재 상태를 사실처럼 적지 않는다.
- 이 파일을 수정한 작업은 마지막에 두 기준 문서와 실제 프로젝트 구조를 다시 대조한다.

## 현재 저장소 기준

- Unity Editor: `6000.5.1f1` (`ProjectSettings/ProjectVersion.txt` 기준). 이 Supported Update 버전을 프로젝트 승인 기준으로 고정하며, 사용자 승인과 기반 회귀 검증 없이 변경하지 않는다.
- 프로젝트 루트: 이 `AGENTS.md`가 있는 Unity 프로젝트 디렉터리
- 문서 언어: 한국어. 코드 식별자와 Unity 타입명은 원문 표기를 유지한다.
- Unity 관련 스크립트/에셋 변경 후에는 컴파일 종료를 기다리고 Console 오류를 확인한다. 테스트를 실행하지 못했으면 그 사실과 이유를 구현 현황에 명시한다.

## Unity 프로젝트 기반

- 게임 전용 에셋 루트는 `Assets/Ouroboros/`이며 입력은 `Input/OSInputActions.inputactions`, 씬은 `Scenes/00_Boot.unity`, `10_MainMenu.unity`, `20_Game.unity` 순서로 관리한다.
- asmdef 경계는 `Ouroboros.Core`, `Ouroboros.Runtime`, `Ouroboros.UI`, `Ouroboros.Editor`, `Ouroboros.Tests.EditMode`, `Ouroboros.Tests.PlayMode`다. Core가 Runtime/UI를 참조하거나 런타임 어셈블리가 Editor를 참조하지 않게 한다.
- 필수 기반 패키지는 Input System, TextMeshPro/uGUI, Unity Test Framework, URP 2D다. 입력 구현은 레거시 Input Manager가 아니라 New Input System을 사용한다.
- Step 01 기반을 다시 생성하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 01 Foundation`을 사용한다. 실행 후 생성 씬과 Build Profile을 덮어쓸 수 있는 변경이 없는지 먼저 확인한다.
- Step 02 기본 데이터와 Boot 참조를 다시 생성하거나 누락을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 02 Data Foundation`을 사용한다. 기본 SO는 `Assets/Ouroboros/Data/Balance/`, `Enemies/`, `Waves/`, `Upgrades/`에 두며 원본 CSV와 생성된 `OSWaveSchedule.asset`을 구분한다.
- 세션 시작 전 6종 기본 SO를 `OSDataValidator`로 교차 검증하고, 런타임에서는 `OSSessionRuntimeState.InitializeFrom`으로 새 복사본을 만든다. 데이터 계약 회귀는 `OSDataValidationEditModeTests`로 확인한다.
- Step 03 세션 기반을 다시 연결하거나 누락을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 03 Session Foundation`을 사용한다. 세션 상태와 `Time.timeScale`은 `OSGameSessionController`만 소유하고, Player/UI Action Map 전환과 입력 구독 생명주기는 `OSInputRouter`만 소유하며 두 Map을 동시에 활성화하지 않는다.
- 선택 요청은 순수 C# `OSSelectionQueue`를 통하며 Body 요청을 LevelUp보다 우선하고 같은 우선순위에서는 FIFO를 지킨다. 세션·입력 회귀는 `OSSelectionQueueEditModeTests`, `OSInputRouterPlayModeTests`, `OSSessionFlowPlayModeTests`로 확인한다.
- Step 04 이동·카메라 기반을 다시 연결하거나 누락을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 04 Movement Foundation`을 사용한다. 플레이 가능 범위는 48×30(`x=-24~24`, `y=-15~15`)이며 드문 장애물 7개와 4면 경계를 사용한다. 머리 Solid는 `PlayerHeadSolid`, 장애물·월드 경계는 `WorldBlocker` 레이어를 사용하고 `OSPlayerController`의 Kinematic Cast만 이동을 확정한다. 적·몸통이 머리를 물리적으로 밀도록 충돌 책임을 추가하지 않는다.
- 카메라는 `OSCameraFollower`가 머리만 고정 줌으로 추종하며 몸통 길이에 따라 줌을 바꾸지 않는다. 이동 회귀는 `OSPlayerMovementEditModeTests`, `OSPlayerMovementPlayModeTests`, `OSStep04ScenePlayModeTests`로 확인한다.
- Step 05 몸통 경로 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 05 Body Path Foundation`을 사용한다. 경로는 `OSPathSampleRingBuffer`의 누적 거리 샘플로만 평가하고 앞 세그먼트 Transform 추적이나 프레임별 컬렉션 생성을 추가하지 않는다. `PF_BodySegment` 64개는 세션 시작 전에 사전 생성하며 `PlayerBodyHurtbox` Trigger끼리 또는 머리·월드 장애물과 물리 충돌하지 않는다.
- 몸통 경로 회귀는 `OSPathSampleRingBufferEditModeTests`, `OSBodyChainPlayModeTests`, `OSStep05ScenePlayModeTests`로 2/20/40/64칸, 직선·원형·지그재그, 정지·재출발을 확인한다.
- Step 06 적 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 06 Enemy Foundation`을 사용한다. `enemy_chaser`는 세션 전에 200개를 사전 생성하고 전투 중 `Instantiate`/`Destroy`하지 않는다. 풀 반환은 반드시 `OSPoolRegistry.Return`의 명시적 생명주기를 통하며 `OnDisable`을 반환 신호로 사용하지 않는다. 용량 고갈과 중복 반환은 정상 거부 결과로 처리한다.
- 활성 적 표적 목록은 `OSEnemyRegistry`의 고정 배열과 안정 Runtime ID를 사용해 비할당 순회한다. `EnemyBody`는 다른 적·플레이어 머리·몸통과 물리 충돌하지 않고 `WorldBlocker`만 물리 대상으로 삼으며, 큰 물리 틱과 돌진에서도 비할당 Cast로 장애물 관통을 막고 접선 잔여 이동만 허용한다. 피해 후보는 `EnemyHurtbox`/`EnemyHitbox` Trigger로 분리한다. 적 풀·Registry 회귀는 `OSEnemyLifecyclePlayModeTests`와 `OSStep06ScenePlayModeTests`로 확인한다.
- Step 07 머리 자동 공격 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 07 Head Weapon`을 사용한다. `head_projectile`은 세션 전에 120개를 사전 생성하며 `PlayerProjectile` 레이어는 `EnemyHurtbox`와 `WorldBlocker`만 판정한다. 피해탄과 Control탄은 첫 `WorldBlocker` Cast/Trigger에서 피해·관통 없이 풀로 반환한다.
- 머리 자동 공격은 `OSEnemyRegistry.FindNearestTarget`의 비할당 탐색을 사용한다. 거리 동률이면 기존 표적을 유지하고 기존 표적이 없으면 작은 Runtime ID를 선택하며, 무대상 또는 투사체 풀 포화 시 발사 주기를 소비하지 않는다. 동일 투사체의 고유 적 중복 명중 방지는 `OSProjectile`이 소유하며 회귀는 `OSHeadWeaponPlayModeTests`와 `OSStep07ScenePlayModeTests`로 확인한다.
- Step 08 조각·몸통 성장 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 08 Body Growth`를 사용한다. `body_fragment_pickup`은 세션 전에 256개를 사전 생성하고, `Pickup`은 머리의 `PickupCollector` Trigger에만 수집되도록 한다. 근접한 같은 종류 픽업은 1.5 반경에서 amount를 먼저 병합하고 풀 포화 시에도 가장 가까운 같은 종류에 총량을 보존한다. 새로 대여하는 다른 종류 픽업은 활성 픽업과 최소 0.55 떨어진 결정적 원형 후보에 배치하며 후보 탐색에서 컬렉션을 만들지 않는다.
- 몸통 조각 6개마다 `OSBodyGrowthProgress`가 Body 선택 요청을 만들며 활성 세그먼트+대기 요청 64에서는 진행도 6을 유지한 채 1건을 보류한다. 공간이 생기면 보류 요청을 즉시 재개하고 64를 게임 규칙상 최대치로 표시하지 않는다. Body 역할은 `OSBodyRoleSelectionPanel`의 Shield/Attack/Laser/Control 고정 4택으로만 확정하며 일반 Submit이 선택을 자동 완료하게 만들지 않는다. 성장 회귀는 `OSBodyGrowthProgressEditModeTests`, `OSBodyGrowthPlayModeTests`, `OSStep08ScenePlayModeTests`, 길이 화력 회귀는 `OSHeadWeaponPlayModeTests`로 확인한다.
- Step 09 피해·절단 연결을 다시 적용하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 09 Damage And Cutting`을 사용한다. 한 적 공격이 동시에 접촉 중인 모든 플레이어 대상에 같은 `AttackEventId`를 전달하고, `OSCombatEventBuffer`에서 같은 공격의 머리+몸통 후보를 머리 1건으로 축약한다. `OSPlayerCombatResolver`만 한 물리 틱의 적대 피해 배치를 확정하며 머리 피해를 먼저 처리하고, 생존 시 가장 머리에 가까운 몸통 후보 1건만 절단한다.
- 머리 HP 피해는 `OSPlayerHealth`가 피격 무적 0.6초와 폭발 무적을 출처별로 관리하고 사망 즉시 세션을 중단한다. 몸통 절단은 `OSBodyChain.TryCutFrom`을 통해 0.35초 전체 절단 방지를 적용하며 제거 View는 꼬리→머리 역순으로 비활성화하고 외부 이벤트의 안정 ID는 원래 머리→꼬리 순서를 유지한다. 피해·절단 회귀는 `OSCombatEventBufferEditModeTests`, `OSPlayerDamagePlayModeTests`, `OSStep09ScenePlayModeTests`, 적 다중 접촉 공격 ID는 `OSEnemyLifecyclePlayModeTests`로 확인한다.
- Step 10 포위 폭발 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 10 Encirclement Explosion`을 사용한다. `OSExplosionController`는 `OSPlayerCombatResolver` 뒤 실행 순서에서 꼬리 안정 ID와 고정 위치를 예약하고, 예고 중 절단으로 사라진 예약을 제외한 합집합 고유 적에게만 `A×35` 피해를 1회 적용한 뒤 같은 처리에서 남은 예약 꼬리를 소비한다. 사망·A=0은 피해·소비·무적 없이 취소하며 성공 시 0.4초 폭발 무적과 Body 우선 선택 재개를 적용한다.
- Step 10 G0의 `OSEnemyDebugSpawner` 12마리 보충은 Step 13 적용 뒤 비활성 상태로 보존한다. 추적체 몸통 조각 드롭 가설 25%는 플레이테스트 결론 없이 변경하지 않는다. 포위 폭발 회귀는 `OSExplosionMathEditModeTests`, `OSExplosionControllerPlayModeTests`, `OSStep10ScenePlayModeTests`로 확인하고, `OSStep06ScenePlayModeTests`는 Step 13 웨이브가 G0 소유권을 대체한 상태를 검증한다.
- Step 11 역할 몸통 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 11 Body Roles`를 사용한다. `OSBodyRoleRegistry`만 몸통 추가·절단·폭발 소비 이벤트를 역할별 고정 배열 목록과 안정 ID 참조로 반영한다. Attack은 피해 10으로 기존 `head_projectile` 120개 풀을 공유하고, Control은 피해 0 전용 `body_control_projectile` 64개 사전 생성 풀을 사용하며 두 역할 모두 대여 성공 때만 해당 세그먼트의 발사 주기를 소비한다.
- Laser는 길이 14·폭 0.35의 시작점·방향 스냅샷과 0.2초 예고 후 같은 사각형으로 `EnemyHurtbox` Collider를 비할당 `Physics2D.OverlapBox` 조회한다. 범위에 조금이라도 겹친 모든 등록 적에게 한 빔당 1회 피해를 주고 같은 적의 여러 Collider는 Runtime ID로 중복 제거하며, 원본 세그먼트가 예고 중 제거되면 취소한다. Control 재적용은 이동·공격 정지의 남은 시간과 새 지속 시간 중 큰 값만 유지한다. Shield는 명백히 무효인 머리 무적·몸통 절단 방지 피격에는 소비하지 않으며 유효 피격만 `OSPlayerCombatResolver` 앞 단계에서 방어한다. 적 접촉·적 투사체의 Shield 범위 판정점은 대상 중심이 아니라 충돌 `Collider2D.ClosestPoint(공격원 위치)`이며 콜라이더가 없을 때만 대상 중심으로 대체한다. 역할 제거 시 신규 효과와 내부 충전·예고 상태는 폐기하되 이미 발사된 Attack/Control 투사체는 유지한다. 역할 회귀는 `OSBodyRoleMathEditModeTests`, `OSBodyRolesPlayModeTests`, `OSStep11ScenePlayModeTests`로 확인한다.
- Step 12 경험치·레벨업 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 12 Level Up And Upgrades`를 사용한다. 경험치 요구량은 15에서 시작해 이전 요구량의 1.18배를 올림하고 초과분과 다중 레벨업 요청을 보존한다. `OSUpgradeRunState`는 `OSUpgradeCatalog`의 런타임 복사본과 런 시드 기반 `OSRunRandom`만 사용해 후보 3개를 고유하게 뽑고 최대 단계를 제외하며, 첫 3회 화면마다 Firepower/Body/Survival을 하나씩 포함한다. Body 요청은 모든 LevelUp 요청보다 먼저 처리하고 LevelUp은 해당 요청 ID의 카드 클릭으로만 1회 확정하며 일반 Submit이나 Cancel로 완료하지 않는다.
- `OSLevelUpController`가 15종 업그레이드 단계와 불변 Modifier 스냅샷을 소유해 머리 무기, 몸통 성장·길이 화력, 역할 4종, 폭발, HP·이동·회복, 자석·경험치, 정예 우선 표적에 배포한다. 발사 주기 0.15초, 기본 조각 요구량 6·성장 효율 단계 5/4, 폭발 소비율 15%, 이동 속도 7.5/s 하한·상한을 유지하고 SO 원본은 수정하지 않는다. 경험치·몸통 조각·회복·절단 몸통은 기존 `body_fragment_pickup` 256개 풀을 `OSPickupType`별 효과·병합·색상으로 공유하며 `SeveredBody`는 원래 역할과 수량을 추가로 보존한다. 레벨업 회귀는 `OSLevelUpCoreEditModeTests`, `OSLevelUpPlayModeTests`, `OSStep12ScenePlayModeTests`로 확인한다.
- Step 13 시간 웨이브를 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 13 Timed Waves`를 사용한다. `OSWaveDirector`만 세션별 `OSWaveScheduleRuntime` 복사본, 런 시드 13013, 전투 시간, 누적 스폰 티켓과 3·6분 정예/9분 경고를 소유하며 선택 중에는 진행하지 않는다. 일반 적은 카메라 밖·머리 거리 7 이상·월드 경계 안·`WorldBlocker` 밖 후보를 최대 8회 검사하고, 실패 또는 활성 일반 적 180 상한에서는 티켓을 다음 틱으로 넘긴다. HP와 생성률은 각각 `1.12^(elapsed/60)`, `1.15^(elapsed/60)`을 SO 원본 변경 없이 적용하며 10:00 보스 이벤트는 Step 14의 `OSBossEncounterController`로 전달한다.
- `enemy_charger`는 0.65초 직선 예고 뒤 돌진·회복하고, `enemy_shooter`는 5~7 거리 유지와 0.45초 예고 뒤 `EnemyProjectile` 전용 레이어의 `enemy_projectile` 64개 풀을 사용하며 적·플레이어 투사체 합 120 포화 시 발사 주기를 소비하지 않는다. 적 투사체는 플레이어 머리·몸통 Hurtbox와 `WorldBlocker`만 판정하고 첫 장애물에서 피해 없이 풀로 반환한다. `enemy_splitter`는 사망 1회에 상한이 허용하는 `enemy_splitter_spawn` 최대 2개만 만들고 소형은 재분열하지 않는다. `enemy_elite_accelerator`는 반경 4.5 링과 영향 색을 표시하고 일반 적 이동 ×1.2·공격 주기 ×0.9를 비중첩 적용한다. 웨이브 회귀는 `OSWaveScheduleEditModeTests`, `OSStep13ScenePlayModeTests`와 전체 PlayMode 회귀로 확인한다.
- Step 15.1 맵·장애물 충돌 마감을 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.1 Arena Collision`을 사용한다. 이 메뉴는 48×30 경계와 장애물 7개를 재배치하고 정적 `World/Enemy_Chaser`, `World/Pickup` 표시용 오브젝트를 제거하며, 모든 적 프리팹과 플레이어 피해탄·Control탄·적 투사체의 비할당 `WorldBlocker` Cast 및 충돌 매트릭스를 복구한다. 회귀는 `OSStep15ArenaCollisionPlayModeTests`와 전체 PlayMode로 확인한다.
- Step 15.2 실드·성장 밸런스를 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.2 Shield And Growth`를 사용한다. 기본 몸통 요구량 6과 성장 효율 6→5→4를 복구하며, 실제 충돌점 Shield 방어는 `OSBodyRolesPlayModeTests`와 `OSStep15ShieldGrowthPlayModeTests`로 확인한다. WebGL 산출물은 `Builds/Step15_2/WebGL/`에 둔다.
- Step 15.3 픽업 분산을 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.3 Pickup Scatter`를 사용한다. `OSPickupSpawner`의 같은 종류 우선 병합과 다른 종류 최소 간격 0.55는 `OSBodyGrowthPlayModeTests`와 `OSStep15PickupScatterPlayModeTests`로 확인한다. WebGL 산출물은 `Builds/Step15_3/WebGL/`에 둔다.
- Step 15.4 레이저 Collider 범위 판정을 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.4 Laser Collider Coverage`를 사용한다. 실제 씬과 Step 11 재생성 경로의 `EnemyHurtbox` 마스크를 유지하고, 중심점은 빔 밖이지만 Hurtbox가 걸친 적·복수 적 관통·다중 Collider 중복 제거 회귀는 `OSBodyRolesPlayModeTests`로 확인한다. WebGL 산출물은 `Builds/Step15_4/WebGL/`에 둔다.
- Step 15.5 몸통 화력·절단 회수를 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.5 Body Arsenal And Recovery`를 사용한다. 이 메뉴는 Attack 피해 10, Laser 길이 14를 `OSBodyBalance.asset`에 고정하고 `20_Game`의 `OSSeveredBodyDropController` 참조를 복구한다. Cut 원인의 각 세그먼트만 비활성화 전 위치·역할로 `SeveredBody` 픽업이 되며 폭발·디버그 꼬리 제거는 드롭하지 않는다. 수집은 조각 게이지·선택 UI 없이 같은 역할을 즉시 꼬리에 복구하고 기술 가드 초과분은 픽업에 남긴다. 풀 포화 값은 같은 역할 병합 또는 4종 고정 대기량으로 보존한다. 회귀는 `OSBodyGrowthPlayModeTests`, `OSStep15PickupScatterPlayModeTests`와 전체 PlayMode로 확인하며 WebGL 산출물은 `Builds/Step15_5/WebGL/`에 둔다.
- Step 15.6 절단 몸통 비주얼을 다시 적용할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15.6 Severed Body Visual`을 사용한다. `SeveredBody`는 Shield/Attack/Laser/Control의 실제 `Body_*` Sprite를 사용하며 공용 픽업 0.42 스케일을 유지해 활성 몸통 0.72보다 작게 보인다. 수집 Collider는 바꾸지 않고 풀 반환·일반 픽업 재대여 시 공용 Sprite를 복구한다. Step 08과 Step 15.5 재적용 경로도 같은 Sprite 배열을 설정하며 회귀는 `OSStep15PickupScatterPlayModeTests`와 전체 PlayMode로 확인한다. WebGL 산출물은 `Builds/Step15_6/WebGL/`에 둔다.
- Step 14 보스·결과 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 14 Boss And Results`를 사용한다. `boss_swarm_core`는 전용 1칸 풀에서 10:00에 1회 생성하며 일반 적 180 상한과 별도다. `OSBossController`는 HP 6,000, 보호막 600, HP 70%/35% 페이즈, 90초 제한, 부채꼴 5/7발·일반 적 6/8마리 소환·충돌 안전 흡인·보호막 패턴을 소유하고 한 시점의 고위험 예고를 1개로 제한한다. 선택 중에는 패턴과 제한 시간을 정지하고 제어는 이동만 제한하며 보스 캐스팅을 취소하지 않는다.
- `OSBossEncounterController`는 플레이어 피해와 폭발 처리 뒤 보스 사망을 확정해 같은 틱의 플레이어·보스 사망은 플레이어 실패로 처리하며, 보스 처치와 90초 시간 초과를 서로 다른 결과로 세션에 전달한다. `OSRunSummaryController`는 몸통·처치·머리 피해·역할·업그레이드·런 시드/데이터 버전을 누적해 종료 시 불변 요약으로 만들고 `OSResultPanel`은 결과 중 전투 입력을 막은 채 재시작과 메인 메뉴를 제공한다. 보스·결과 회귀는 `OSBossMathEditModeTests`, `OSStep14ScenePlayModeTests`와 전체 EditMode/PlayMode 회귀로 확인한다.
- Step 15 HUD·판독성 기반을 다시 연결하거나 누락 설정을 보정할 때 Unity 메뉴 `Ouroboros/Setup/Apply Step 15 Readability`를 사용한다. `OSCombatHudPresenter`는 규칙 상태를 수정하지 않고 이벤트를 dirty 표시 모델로 모아 `LateUpdate`에서 프레임당 최대 1회 반영한다. 몸통 HUD에는 현재 수만 표시하고 기술 가드 64를 최대치·분모로 노출하지 않으며 Shield/Attack/Laser/Control은 색과 `[O][>][=][+]` 문양을 함께 사용한다.
- 머리 코어, 적·보스 위험, 레이저, 폭발 예약, 절단, 제어 원호, 실드의 Sorting Order는 각각 220, 200, 198, 190, 185, 182, 175다. `OSTutorialProgress`는 첫 세션 조건 전이와 unscaled 시간을 순수 상태로 소유하고, `OSCombatFeedbackPresenter`는 머리 피해와 절단 위치·꼬리 방향·상실 역할을 표시만 하며 규칙 결과에 관여하지 않는다. HUD·튜토리얼·고밀도 판독성 회귀는 `OSTutorialProgressEditModeTests`, `OSStep15ScenePlayModeTests`와 전체 EditMode/PlayMode 회귀로 확인한다.
- Step 14 이후 구현 순서는 Step 15 HUD·튜토리얼·전투 판독성 → Step 15.1 전장 충돌 → Step 15.2 실드·성장 밸런스 → Step 15.3 픽업 분산 → Step 15.4 레이저 Collider 범위 → Step 15.5 몸통 화력·절단 회수 → Step 15.6 절단 몸통 소형 비주얼 → Step 16 성능 최적화·WebGL → Step 17 사운드·VFX·연출 → Step 18 자동 테스트·빌드 게이트로 고정한다. Step 16에서 코어 전투의 성능 기준선과 연출 예산을 먼저 확정하고, Step 17은 그 예산 안에서 구현한 뒤 같은 피크 장면과 WebGL 메모리를 재측정한다.
- 기반 회귀 검증은 `Ouroboros.Tests.EditMode`와 `Ouroboros.Tests.PlayMode`를 실행한다. 단계별 플레이어 빌드는 별도 사용자 요청이 없는 한 `Assets/Ouroboros/BuildProfiles/WebGL Development.asset`만 사용하고 Windows 빌드는 실행하지 않는다. 산출물은 `Builds/StepXX/WebGL/`에 둔다.
- WebGL 빌드 성공만으로 완료 처리하지 않는다. `Tools/Serve-WebGL.ps1`로 빌드 디렉터리를 HTTP 제공하고 `index.html` 응답, 브라우저 Canvas 로드, Console Error/Exception 여부까지 확인한다. 예: `& ./Tools/Serve-WebGL.ps1 -BuildDirectory Builds/StepXX/WebGL -Port 8055 -OpenBrowser`.
