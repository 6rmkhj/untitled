# SIGNAL HAUL

Unity 6.6용 1인칭 물리 등반/회수 게임 프로토타입입니다.

최근 Steam에서 인기를 끈 협동 등반 게임의 **높이·스태미나 기반 긴장감**과 물리 기반 회수 게임의 **귀중품을 직접 들고 운반하는 위험/코미디**에서 착안하되, 세계관·레벨·규칙·코드는 독자적으로 구성했습니다.

## 게임 루프

1. 폭풍 속 폐기된 중계탑을 오른다.
2. 상층부에 흩어진 Signal Core 3개를 찾는다.
3. `E`로 코어를 물리적으로 붙잡고 낙하/드론을 피해 지상 Extraction Pad까지 운반한다.
4. 제한 시간 안에 3개를 모두 회수하면 승리한다.

추락과 드론 충돌은 체력을 깎고 들고 있던 코어를 떨어뜨립니다. 달리기와 벽 등반은 스태미나를 소비합니다.

## 조작

- `WASD`: 이동
- `Mouse`: 시점
- `Left Shift`: 달리기
- `Space`: 점프 / 공중에서 벽을 바라보며 길게 누르면 등반
- `E`: 물체 잡기 / 놓기
- `Q`: 잡은 물체 던지기
- `R`: 게임 종료 후 재시작
- `Esc`: 마우스 커서 해제/잠금

## 씬 구조

게임 월드는 Play 시 생성되지 않습니다. `Assets/Scenes/Main.unity` 안에 실제 GameObject와 Component로 저장됩니다.

```text
SIGNAL_HAUL
├─ Managers
│  └─ GameManager
├─ Lighting
│  └─ Storm Light
├─ Environment
│  ├─ Ground
│  ├─ Base Deck
│  └─ Tower
│     ├─ Level_00
│     ├─ ...
│     └─ Level_09
├─ Gameplay
│  ├─ Player
│  │  └─ Camera
│  │     └─ HoldPoint
│  ├─ Extraction
│  │  └─ Extraction Pad
│  └─ Signal Cores
│     ├─ CORE-ALPHA
│     ├─ CORE-BETA
│     └─ CORE-GAMMA
└─ Hazards
   ├─ Storm Drone 01
   ├─ Storm Drone 02
   └─ Storm Drone 03
```

각 Mesh, Collider, Rigidbody, CharacterController, Camera, Light 및 게임 스크립트 Component를 Hierarchy/Inspector에서 직접 편집할 수 있습니다. 머티리얼도 `Assets/Materials` 아래 실제 `.mat` 에셋으로 생성됩니다.

## 실행

1. Unity Hub에서 **Unity 6000.6.0f1 (Unity 6.6)** 로 이 폴더를 엽니다.
2. 첫 스크립트 컴파일 시 기존의 빈/구버전 `Main` 씬을 감지하면 실제 GameObject 구조로 한 번 업그레이드하고 `Assets/Scenes/Main.unity`에 저장합니다.
3. `Main` 씬에서 Hierarchy를 확인한 뒤 Play를 누릅니다.

씬이 자동 생성되지 않았거나 처음부터 다시 만들고 싶다면 Unity 메뉴에서 **Tools > Signal Haul > Rebuild Main Scene**을 실행하세요. 이 명령은 `Main` 씬을 초기 상태로 다시 만들기 때문에 직접 수정한 맵이 있다면 먼저 백업하세요.

자동 업그레이드는 Scene Version이 오래된 경우에만 실행됩니다. 정상 생성된 이후에는 스크립트가 사용자의 씬 수정을 매번 덮어쓰지 않습니다.

## 프로토타입 범위

- 실제 씬 GameObject 기반 레벨 구성
- 1인칭 CharacterController + 실제 Camera/AudioListener
- 점프/달리기/벽 등반 + 스태미나
- Rigidbody 기반 Signal Core 잡기/놓기/던지기
- 10층 수직 스캐폴드 타워
- Signal Core 3개 회수 및 Extraction Pad
- 순찰/추격 드론, 충돌 피해
- 폭풍 제한 시간, 체력/스태미나/목표 HUD
- 승리/패배/재시작 루프

## 다음 확장 추천

- Unity Netcode 또는 Steamworks 기반 2~4인 협동
- 레벨을 Prefab 단위로 분리해 수작업/절차 생성 혼합
- 코어별 무게·파손·가치 시스템
- 음성 근접 채팅과 물리 상호작용
- Steam Deck/Gamepad 입력
- URP 후처리, 사운드, 애니메이션 및 오리지널 아트
