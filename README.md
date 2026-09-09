# SIGNAL HAUL

Unity 6.6용 1인칭 물리 등반/회수 협동 게임 프로토타입입니다.

최근 Steam에서 인기를 끈 협동 등반 게임의 **높이·스태미나 기반 긴장감**과 물리 기반 회수 게임의 **귀중품을 직접 들고 운반하는 위험/코미디**에서 착안하되, 세계관·레벨·규칙·코드는 독자적으로 구성했습니다.

현재 확장 1단계인 **2~4인 LAN 협동 멀티플레이** 위에 2단계 **deterministic 랜덤 맵**을 쌓는 중입니다. Unity 실행 검증이 아직 되지 않았으므로 두 단계 모두 Draft 상태로 유지합니다.

## 게임 루프

1. 2~4명이 폭풍 속 폐기된 중계탑에 접속한다.
2. Host가 만든 랜덤 시드로 모든 클라이언트가 같은 수직 타워를 구성한다.
3. 상층부에 흩어진 Signal Core 3개를 찾는다.
4. `E`로 코어를 잡고 낙하/드론을 피해 지상 Extraction Pad까지 운반한다.
5. 제한 시간 안에 코어 3개를 모두 회수하면 팀 전체가 승리한다.

## 조작

- `WASD`: 이동
- `Mouse`: 시점
- `Left Shift`: 달리기
- `Space`: 점프 / 공중에서 벽을 바라보며 길게 누르면 등반
- `E`: Signal Core 잡기 / 놓기
- `Q`: 잡은 Core 던지기
- `Esc`: 마우스 커서 해제/잠금

## 1단계 — 2~4인 멀티플레이

사용 패키지:

- `com.unity.netcode.gameobjects` 2.7.0
- `com.unity.transport` 2.6.0

현재 네트워크 범위:

- Host / Client 직접 IP LAN 접속
- 최대 4명 Connection Approval
- 접속자별 Player Object 자동 스폰 및 소유권
- 플레이어 이동/시점 상태 동기화
- Signal Core 물리/잡기/놓기/던지기 서버 동기화
- 드론 이동/대미지 서버 권한 처리
- Core 회수 수와 폭풍 타이머, 승패 상태 동기화

## 2단계 — 랜덤 맵

랜덤 맵은 **Host가 하나의 정수 Seed를 만든 뒤 Netcode `NetworkVariable`로 공유**합니다. 모든 클라이언트는 같은 Seed와 동일한 생성 알고리즘을 사용하므로 별도의 수십 개 맵 오브젝트를 네트워크로 전송하지 않고도 같은 지형을 재현합니다.

현재 생성 요소:

- 한 판마다 10~13층 범위에서 타워 높이 변화
- 좌우/전후로 꺾이는 수직 진행 경로
- Standard / Narrow / Cargo / Hazard 네 가지 층 변형
- 랜덤 레일, 좁은 발판, 사이드 렛지, 장애물
- Seed에 따라 Signal Core 3개의 층 위치 재배치
- Seed에 따라 드론 3개의 순찰 높이 재배치
- HUD에 현재 `MAP` Seed 표시
- Host를 종료하고 새로 Host하면 새로운 Seed/맵 생성

Editor에서는 기존 직렬화 Tower가 맵 편집용 기준/프리뷰로 남아 있고, 실제 세션 시작 시 `Generated Random Tower`가 생성되면서 기준 Tower는 비활성화됩니다. 랜덤으로 생성되는 정적 발판/벽은 NetworkObject가 아니라 각 클라이언트에서 같은 Seed로 로컬 생성되고, Core/Drone/라운드 상태만 서버 권한으로 동기화됩니다.

## 멀티플레이 + 랜덤 맵 테스트

Unity를 테스트할 수 있을 때 다음 순서로 검증합니다.

1. `feat/random-map` 브랜치를 체크아웃합니다.
2. Unity 6000.6.0f1에서 패키지/스크립트 컴파일을 완료합니다.
3. 필요하면 `Tools > Signal Haul > Rebuild Main Scene`을 실행합니다.
4. Editor에서 Host를 시작합니다.
5. Standalone Build에서 `127.0.0.1`로 Client를 연결합니다.
6. 양쪽 HUD의 `MAP` 숫자가 같은지 확인합니다.
7. 양쪽에서 발판/벽/장애물 배치가 정확히 같은지 확인합니다.
8. Core와 Drone이 같은 층에 보이는지 확인합니다.
9. Host를 종료하고 다시 Host했을 때 `MAP` 숫자와 타워 배치가 달라지는지 확인합니다.
10. 3~4인까지 연결해 이동, Core 운반, 드론 대미지, 회수 수/타이머 공유를 확인합니다.

## 씬 구조

환경과 네트워크 오브젝트는 `Assets/Scenes/Main.unity`에 실제 GameObject/Component로 저장됩니다. 플레이어는 `Assets/Prefabs/NetworkPlayer.prefab`에서 접속 시 생성됩니다.

```text
SIGNAL_HAUL
├─ Managers
│  ├─ GameManager              [NetworkObject, Map Seed 소유]
│  └─ Network Manager          [NetworkManager, UnityTransport]
├─ Lighting
│  └─ Storm Light
├─ Environment
│  ├─ Ground
│  ├─ Base Deck
│  ├─ Tower                    [Editor 기준/프리뷰]
│  └─ Generated Random Tower   [Play 시 로컬 생성]
├─ Gameplay
│  ├─ Player Spawn Points
│  ├─ Extraction
│  └─ Signal Cores             [NetworkObject]
└─ Hazards
   └─ Storm Drones             [NetworkObject]

Assets/Prefabs/
└─ NetworkPlayer.prefab        [NetworkObject, PlayerController]
```

## 실행 / 씬 재생성

1. Unity Hub에서 **Unity 6000.6.0f1 (Unity 6.6)** 로 이 폴더를 엽니다.
2. 첫 실행에서는 Netcode/Transport 패키지 설치와 스크립트 재컴파일에 시간이 걸릴 수 있습니다.
3. `Main` 씬을 열고 Play합니다.
4. 멀티플레이 구조를 처음부터 다시 만들려면 **Tools > Signal Haul > Rebuild Main Scene**을 사용합니다.

## 단계별 확장 로드맵

1. **2~4인 멀티플레이** — 구현, Unity 검증 대기
2. **랜덤 맵** — 구현, Unity 검증 대기
3. 근접 음성채팅
4. 다양한 무게/파손 물체
5. 몬스터
6. 상점/업그레이드
7. 한 판 15~25분 구조 및 밸런싱

Unity 테스트가 가능한 시점에는 1~2단계를 먼저 함께 검증하고, 문제가 있으면 그 지점부터 수정한 뒤 Draft PR을 순서대로 병합합니다.
