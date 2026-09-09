# SIGNAL HAUL

Unity 6.6용 1인칭 물리 등반/회수 협동 게임 프로토타입입니다.

최근 Steam에서 인기를 끈 협동 등반 게임의 **높이·스태미나 기반 긴장감**과 물리 기반 회수 게임의 **귀중품을 직접 들고 운반하는 위험/코미디**에서 착안하되, 세계관·레벨·규칙·코드는 독자적으로 구성했습니다.

현재 확장 1단계로 Unity Netcode for GameObjects 기반 **2~4인 LAN 협동 멀티플레이**가 추가되어 있습니다.

## 게임 루프

1. 2~4명이 폭풍 속 폐기된 중계탑에 접속한다.
2. 상층부에 흩어진 Signal Core 3개를 찾는다.
3. `E`로 코어를 잡고 낙하/드론을 피해 지상 Extraction Pad까지 운반한다.
4. 제한 시간 안에 코어 3개를 모두 회수하면 팀 전체가 승리한다.

추락과 드론 충돌은 체력을 깎고 들고 있던 코어를 떨어뜨립니다. 달리기와 벽 등반은 스태미나를 소비합니다.

## 조작

- `WASD`: 이동
- `Mouse`: 시점
- `Left Shift`: 달리기
- `Space`: 점프 / 공중에서 벽을 바라보며 길게 누르면 등반
- `E`: Signal Core 잡기 / 놓기
- `Q`: 잡은 Core 던지기
- `Esc`: 마우스 커서 해제/잠금

## 멀티플레이 1단계

사용 패키지:

- `com.unity.netcode.gameobjects` 2.7.0
- `com.unity.transport` 2.6.0

현재 네트워크 범위:

- Host / Client LAN 접속
- 최대 4명 Connection Approval
- 접속자별 Player Object 자동 스폰 및 소유권
- 플레이어 이동/시점 상태 동기화
- Signal Core 물리/잡기/놓기/던지기 서버 동기화
- 드론 이동/대미지 서버 권한 처리
- Core 회수 수와 폭풍 타이머, 승패 상태 동기화

이 단계는 Steam Relay/매치메이킹이 아닌 **직접 IP 기반 LAN/포트 접속**입니다. 인터넷 공개방/초대 기능은 이후 네트워크 서비스 단계에서 붙이는 것이 안전합니다.

## 멀티플레이 테스트

### 같은 PC에서 테스트

가장 단순한 방법은 Editor와 Standalone Build를 하나씩 실행하는 것입니다.

1. Unity Editor에서 `Main` 씬을 열고 Play합니다.
2. `HOST GAME`을 눌러 Host를 시작합니다.
3. Windows/Mac Standalone Build를 하나 실행합니다.
4. Client의 Host IP에 `127.0.0.1`을 입력하고 `JOIN GAME`을 누릅니다.
5. 추가 Build를 실행하면 최대 4명까지 확인할 수 있습니다.

### 같은 공유기의 다른 PC에서 테스트

1. Host PC에서 `HOST GAME`을 누릅니다.
2. Host PC의 로컬 IPv4 주소를 확인합니다. 예: `192.168.0.10`.
3. Client PC에서 해당 IPv4를 입력하고 `JOIN GAME`을 누릅니다.
4. 기본 UDP 포트는 `7777`입니다. OS 방화벽이 차단하면 Unity/빌드 또는 UDP 7777을 허용해야 합니다.

## 씬 구조

환경과 네트워크 오브젝트는 `Assets/Scenes/Main.unity`에 실제 GameObject/Component로 저장됩니다. 플레이어는 멀티플레이 관례에 따라 `Assets/Prefabs/NetworkPlayer.prefab`에서 접속 시 생성됩니다.

```text
SIGNAL_HAUL
├─ Managers
│  ├─ GameManager              [NetworkObject]
│  └─ Network Manager          [NetworkManager, UnityTransport]
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
│  ├─ Player Spawn Points
│  │  ├─ Spawn 1
│  │  ├─ Spawn 2
│  │  ├─ Spawn 3
│  │  └─ Spawn 4
│  ├─ Extraction
│  │  └─ Extraction Pad
│  └─ Signal Cores
│     ├─ CORE-ALPHA           [NetworkObject]
│     ├─ CORE-BETA            [NetworkObject]
│     └─ CORE-GAMMA           [NetworkObject]
└─ Hazards
   ├─ Storm Drone 01          [NetworkObject]
   ├─ Storm Drone 02          [NetworkObject]
   └─ Storm Drone 03          [NetworkObject]

Assets/Prefabs/
└─ NetworkPlayer.prefab       [NetworkObject, PlayerController]
```

## 실행 / 씬 재생성

1. Unity Hub에서 **Unity 6000.6.0f1 (Unity 6.6)** 로 이 폴더를 엽니다.
2. 첫 실행에서는 Netcode/Transport 패키지 설치와 스크립트 재컴파일에 시간이 걸릴 수 있습니다.
3. Scene Version이 오래된 기존 `Main` 씬은 새 멀티플레이 구조로 한 번 재생성됩니다.
4. `Main` 씬을 열고 Play합니다.

자동 재생성이 되지 않으면 Unity 메뉴에서 **Tools > Signal Haul > Rebuild Main Scene**을 실행하세요. 이 명령은 `Main` 씬과 `NetworkPlayer.prefab`을 초기 상태로 다시 만들기 때문에 직접 수정한 맵/플레이어 Prefab이 있다면 먼저 백업하세요.

## 단계별 확장 로드맵

1. **2~4인 멀티플레이** ← 현재 단계
2. 랜덤 맵
3. 근접 음성채팅
4. 다양한 무게/파손 물체
5. 몬스터
6. 상점/업그레이드
7. 한 판 15~25분 구조 및 밸런싱

멀티플레이 기반이 실제 2~4 클라이언트에서 안정적으로 확인된 후에 2단계 랜덤 맵으로 넘어갑니다.
