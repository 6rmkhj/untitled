# SIGNAL HAUL

Unity 6.6용 1인칭 물리 등반/회수 협동 게임 프로토타입입니다.

최근 Steam에서 인기를 끈 협동 등반 게임의 **높이·스태미나 기반 긴장감**과 물리 기반 회수 게임의 **귀중품을 직접 들고 운반하는 위험/코미디**에서 착안하되, 세계관·레벨·규칙·코드는 독자적으로 구성했습니다.

현재 **1단계 2~4인 LAN 멀티플레이 → 2단계 deterministic 랜덤 맵 → 3단계 근접 음성채팅 → 4단계 다양한 무게/파손 물체**까지 Draft 브랜치로 순차 구현되어 있습니다. Unity 실행 검증이 아직 되지 않았으므로 각 단계는 `main`에 병합하지 않습니다.

## 게임 루프

1. 2~4명이 폭풍 속 폐기된 중계탑에 접속한다.
2. Host가 만든 랜덤 Seed로 모든 클라이언트가 같은 수직 타워를 구성한다.
3. 근처 팀원과 음성으로 소통하며 Signal Core와 가치 있는 Salvage를 찾는다.
4. 물체 무게에 따라 혼자 운반하거나, 매우 무거운 물체는 두 명이 동시에 붙잡는다.
5. 낙하/충돌로 내구도가 줄어들기 전에 Extraction Pad까지 가져간다.
6. Signal Core 3개를 모두 회수하면 승리하며, 추가 Salvage와 남은 내구도에 따라 회수 가치가 올라간다.

## 조작

- `WASD`: 이동
- `Mouse`: 시점
- `Left Shift`: 달리기
- `Space`: 점프 / 공중에서 벽을 바라보며 길게 누르면 등반
- `E`: 물체 잡기 / 놓기
- `Q`: 가벼운 1인 물체 던지기 / 그 외 물체 놓기
- `V` 길게 누르기: Push-to-Talk 근접 음성 송신
- `M`: 들어오는 음성 전체 음소거 / 해제
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
- 물리 Loot 잡기/놓기/던지기 서버 동기화
- 드론 이동/대미지 서버 권한 처리
- Core 회수 수, Salvage 가치, 폭풍 타이머, 승패 상태 동기화

## 2단계 — 랜덤 맵

랜덤 맵은 **Host가 하나의 정수 Seed를 만든 뒤 Netcode `NetworkVariable`로 공유**합니다. 모든 클라이언트는 같은 Seed와 동일한 생성 알고리즘을 사용하므로 수십 개의 정적 맵 오브젝트를 네트워크로 전송하지 않고도 같은 지형을 재현합니다.

현재 생성 요소:

- 한 판마다 10~13층 범위에서 타워 높이 변화
- 좌우/전후로 꺾이는 수직 진행 경로
- Standard / Narrow / Cargo / Hazard 네 가지 층 변형
- 랜덤 레일, 좁은 발판, 사이드 렛지, 장애물
- Seed에 따라 Signal Core 3개의 층 위치 재배치
- Seed에 따라 추가 Salvage 4개의 층 위치 재배치
- Seed에 따라 드론 3개의 순찰 높이 재배치
- HUD에 현재 `MAP` Seed 표시
- Host를 종료하고 새로 Host하면 새로운 Seed/맵 생성

Editor에서는 기존 직렬화 Tower가 기준/프리뷰로 남아 있고, 실제 세션 시작 시 `Generated Random Tower`가 생성되면서 기준 Tower는 비활성화됩니다. 랜덤 정적 발판/벽은 NetworkObject가 아니라 각 클라이언트에서 같은 Seed로 로컬 생성되고, Loot/Drone/라운드 상태만 서버 권한으로 동기화됩니다.

## 3단계 — 근접 음성채팅

현재 음성채팅은 외부 계정/클라우드 음성 서비스 없이 **LAN 프로토타입 자체 구현**입니다.

- 로컬 기본 마이크를 Unity `Microphone` API로 캡처
- 16 kHz / mono / signed 16-bit PCM
- 20 ms 단위(320 samples, 640 bytes) 패킷
- `V` Push-to-Talk 중에만 송신
- NGO Unreliable RPC로 Client → Server → Clients/Host 릴레이
- 각 원격 플레이어 GameObject의 3D `AudioSource`에서 재생
- Linear distance attenuation, 기본 `minDistance = 1.5m`, `maxDistance = 14m`
- `M`으로 들어오는 팀원 음성 전체 음소거
- 패킷 순번을 사용해 늦게 도착한 오래된 음성 패킷을 무시
- `NetworkPlayer.prefab`에 `ProximityVoiceChat` 컴포넌트 저장

현재 방식은 LAN 기능 검증을 위한 무압축 PCM입니다. 실제 Steam 배포 단계에서는 Opus/Vivox/Steam Voice 같은 압축·AEC/노이즈 억제 지원 솔루션으로 교체할 예정입니다.

## 4단계 — 무게 / 가치 / 내구도 / 파손 / 협동 운반

모든 운반 물체는 이제 `PhysicsLoot` 공통 컴포넌트를 사용합니다. Signal Core도 같은 시스템 위에서 동작합니다.

### 공통 규칙

- **Weight**: Rigidbody mass와 플레이어 이동/스태미나 페널티에 사용
- **Base Value**: 온전할 때의 최대 회수 가치
- **Durability**: 충격을 받을 때 감소
- **Impact Threshold**: 이 속도 이하의 충돌은 내구도 손실 없음
- **Current Value**: 파손되지 않은 물체는 남은 내구도가 낮을수록 회수 가치도 낮아짐
- **Required Carriers**: 실제로 들어 올리기 위해 필요한 최소 플레이어 수
- **Max Carriers**: 동시에 잡을 수 있는 플레이어 수
- 물체 물리와 내구도/파손 판정은 Server-authoritative
- 위치/회전, 운반자 슬롯, 내구도, 회수/파손 상태를 모든 클라이언트에 동기화

### 현재 실제 Scene Loot

| 물체 | 무게 | 가치 | 내구도 | 운반 인원 | 특징 |
| --- | ---: | ---: | ---: | ---: | --- |
| SIGNAL CORE | 5 kg | $1000 | 120 | 1 | 승리에 필요한 필수 목표 |
| DATA CACHE | 4 kg | $220 | 65 | 1 | 가볍고 빠르게 운반 가능 |
| INDUSTRIAL BATTERY | 14 kg | $420 | 120 | 1 | 이동/스태미나 페널티가 큰 1인 물체 |
| GLASS RELIC | 3.5 kg | $700 | 35 | 1 | 가볍지만 충격에 매우 약한 고가 물체 |
| REACTOR ASSEMBLY | 30 kg | $1100 | 180 | **2** | 두 사람이 동시에 잡아야 정상적으로 들어 올릴 수 있음 |

`REACTOR ASSEMBLY`는 첫 번째 플레이어 혼자 잡으면 땅에 무게가 남은 채 천천히 끌리는 수준이고, 두 번째 플레이어가 합류해야 두 사람의 Hold Point 중간을 목표로 정상 운반됩니다. 한 사람이 손을 놓으면 즉시 다시 중력 영향을 받습니다.

운반 중 플레이어에는 물체의 **1인당 분담 무게**에 따라 이동 속도 감소와 스태미나 소비 증가가 적용됩니다. 충분한 인원이 붙지 않은 물체는 달리기/점프가 제한되고, 지나치게 무거운 상태에서는 벽 등반도 할 수 없습니다.

### 파손

서버는 Rigidbody 충돌의 상대 속도를 확인합니다. 물체별 `Impact Threshold`를 넘은 충돌만 내구도 대미지를 발생시킵니다. 내구도가 0이 되면:

- 원본 NetworkObject의 Renderer/Collider 비활성화
- 운반 중이던 플레이어 슬롯 즉시 해제
- 모든 클라이언트에서 동일 물체 위치에 비충돌 시각 파편 생성
- 파편은 게임플레이 Physics에 영향을 주지 않고 잠시 후 제거
- 해당 물체의 회수 가치는 0
- HUD의 `BROKEN` 수 증가

추가 Salvage는 부서져도 핵심 목표는 계속 진행할 수 있습니다. Signal Core는 필수 목표이므로 실제 밸런싱 단계에서 파손 허용 강도/라운드 실패 규칙을 다시 조절합니다.

### 회수 가치

Extraction Pad는 이제 Signal Core뿐 아니라 모든 `PhysicsLoot`를 받습니다. 회수 직전 남은 내구도로 최종 가치가 계산되어:

- `SALVAGE $...`
- `RECOVERED ...`
- `BROKEN ...`

상태가 팀 전체 HUD에 동기화됩니다. Signal Core 3개 회수는 여전히 승리 조건이고, 추가 Salvage는 선택적인 점수/경제 기반입니다. 이후 6단계 상점/업그레이드에서 이 금액을 그대로 재화로 사용할 수 있습니다.

## 나중에 한 번에 할 Unity 검증

Unity를 사용할 수 있게 되면 `feat/physics-loot` 브랜치 하나로 1~4단계를 연속 검증할 수 있습니다.

1. Unity 6000.6.0f1에서 프로젝트를 열고 패키지/스크립트 컴파일을 완료합니다.
2. `Tools > Signal Haul > Rebuild Main Scene`을 실행해 Scene Version 6 구조를 확실히 생성합니다.
3. `Gameplay/Salvage Loot` 아래에 DATA CACHE / INDUSTRIAL BATTERY / GLASS RELIC / REACTOR ASSEMBLY가 실제 GameObject로 존재하는지 확인합니다.
4. Editor Host + Standalone Client를 `127.0.0.1`로 연결합니다.
5. 양쪽 MAP Seed와 생성 타워가 같은지 확인합니다.
6. 가벼운 DATA CACHE가 혼자 정상 운반/던지기 되는지 확인합니다.
7. INDUSTRIAL BATTERY를 들었을 때 플레이어 속도와 스태미나 소비가 눈에 띄게 달라지는지 확인합니다.
8. REACTOR ASSEMBLY를 한 명만 잡았을 때 제대로 들리지 않고, 두 명이 잡으면 함께 이동하는지 확인합니다.
9. 두 명 중 한 명이 `E`로 놓았을 때 REACTOR가 다시 떨어지는지 확인합니다.
10. GLASS RELIC를 높은 곳에서 떨어뜨려 내구도가 감소하고 큰 충격에서 파손되는지 확인합니다.
11. 파손 시 양쪽 화면에서 원본이 사라지고 시각 파편이 보이며 HUD `BROKEN`이 증가하는지 확인합니다.
12. 손상된 물체를 Extraction Pad에 넣었을 때 온전한 상태보다 적은 가치가 들어오는지 확인합니다.
13. Core/Drone/근접 음성채팅을 함께 켠 상태로 3~4인까지 확장 검증합니다.

## 씬 / Prefab 구조

환경과 네트워크 오브젝트는 `Assets/Scenes/Main.unity`에 실제 GameObject/Component로 저장됩니다. 플레이어는 `Assets/Prefabs/NetworkPlayer.prefab`에서 접속 시 생성됩니다.

```text
SIGNAL_HAUL
├─ Managers
│  ├─ GameManager              [NetworkObject, Map Seed, Salvage 총액]
│  └─ Network Manager          [NetworkManager, UnityTransport]
├─ Lighting
├─ Environment
│  ├─ Ground
│  ├─ Base Deck
│  ├─ Tower                    [Editor 기준/프리뷰]
│  └─ Generated Random Tower   [Play 시 로컬 생성]
├─ Gameplay
│  ├─ Player Spawn Points
│  ├─ Extraction
│  ├─ Signal Cores
│  │  ├─ CORE-ALPHA            [NetworkObject, PhysicsLoot, SignalCore]
│  │  ├─ CORE-BETA
│  │  └─ CORE-GAMMA
│  └─ Salvage Loot
│     ├─ DATA CACHE             [NetworkObject, PhysicsLoot]
│     ├─ INDUSTRIAL BATTERY     [NetworkObject, PhysicsLoot]
│     ├─ GLASS RELIC            [NetworkObject, PhysicsLoot]
│     └─ REACTOR ASSEMBLY       [NetworkObject, PhysicsLoot, 2 carriers]
└─ Hazards
   └─ Storm Drones              [NetworkObject]

Assets/Prefabs/
└─ NetworkPlayer.prefab
   ├─ NetworkObject
   ├─ CharacterController
   ├─ PlayerController
   ├─ ProximityVoiceChat
   ├─ Body
   └─ Camera
      └─ HoldPoint
```

## 실행 / 씬 재생성

1. Unity Hub에서 **Unity 6000.6.0f1 (Unity 6.6)** 로 이 폴더를 엽니다.
2. 첫 실행에서는 Netcode/Transport 패키지 설치와 스크립트 재컴파일에 시간이 걸릴 수 있습니다.
3. `Main` 씬을 열고 Play합니다.
4. 현재 Stage 4 Scene 구조를 처음부터 다시 만들려면 **Tools > Signal Haul > Rebuild Main Scene**을 사용합니다.
5. Voice 컴포넌트만 Prefab에 다시 확인/설치하려면 **Tools > Signal Haul > Ensure Voice Chat On Player Prefab**을 사용합니다.

## 단계별 확장 로드맵

1. **2~4인 멀티플레이** — 구현, Unity 검증 대기
2. **랜덤 맵** — 구현, Unity 검증 대기
3. **근접 음성채팅** — 구현, Unity 검증 대기
4. **다양한 무게/파손 물체 + 2인 협동 운반** — 구현, Unity 검증 대기
5. 몬스터
6. 상점/업그레이드
7. 한 판 15~25분 구조 및 밸런싱

Unity 테스트가 가능한 시점에는 가장 위 스택인 `feat/physics-loot`에서 전체를 먼저 검증하고, 문제가 있으면 가장 아래 단계부터 수정한 뒤 Draft PR을 순서대로 병합합니다.
