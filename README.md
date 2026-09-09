# SIGNAL HAUL

Unity 6.6용 2~4인 1인칭 물리 등반/회수 협동 게임 프로토타입입니다.

최근 Steam에서 인기를 끈 협동 등반 게임의 높이/스태미나 긴장감과 물리 기반 회수 게임의 위험한 운반 코미디에서 착안하되, 세계관/레벨/규칙/코드는 독자적으로 구성합니다.

현재 Draft 브랜치 스택으로 다음 단계까지 구현되어 있습니다.

1. 2~4인 LAN 멀티플레이
2. deterministic 랜덤 맵
3. 근접 음성채팅
4. 무게/가치/내구도/파손/2인 협동 운반
5. 몬스터
6. 상점/업그레이드
7. 15~25분 라운드 구조 및 밸런싱

Unity 실행 검증이 아직 되지 않았으므로 테스트 전 단계는 `main`에 병합하지 않습니다.

## 기본 게임 루프

1. 2~4명이 폐기된 폭풍 중계탑에 접속합니다.
2. Host Seed로 모든 클라이언트가 같은 수직 타워를 생성합니다.
3. 근접 음성으로 소통하며 Signal Core와 추가 Salvage를 찾습니다.
4. 무게에 따라 혼자 운반하거나 두 명이 동시에 무거운 물체를 듭니다.
5. 낙하/충돌/몬스터에게서 Loot를 지키며 Extraction Pad로 운반합니다.
6. Signal Core 3개를 회수하면 승리하고, 추가 Salvage와 남은 내구도가 최종 회수 가치를 결정합니다.

## 조작

- `WASD`: 이동
- `Mouse`: 시점
- `Left Shift`: 달리기
- `Space`: 점프 / 벽 등반
- `E`: 물체 잡기 / 놓기
- `Q`: 가벼운 1인 물체 던지기 / 무거운 물체 놓기
- `V` 길게 누르기: Push-to-Talk 근접 음성
- `M`: 수신 음성 음소거 / 해제
- `Esc`: 마우스 커서 해제 / 잠금

## 1단계 — 2~4인 멀티플레이

사용 패키지:

- `com.unity.netcode.gameobjects` 2.7.0
- `com.unity.transport` 2.6.0

현재 범위:

- 직접 IP 기반 Host / Client LAN 접속
- 최대 4명 Connection Approval
- 접속자별 Player Object 스폰/소유권
- 플레이어 이동/시점 동기화
- Loot 물리/운반/대미지/회수 서버 권한 처리
- Drone/Monster AI 서버 권한 처리
- Core 목표, Salvage 가치, 폭풍 타이머, 승패 동기화

## 2단계 — deterministic 랜덤 맵

Host가 정수 Seed를 만들고 Netcode `NetworkVariable`로 공유합니다. 정적 발판/벽은 NetworkObject로 전송하지 않고 모든 peer가 같은 Seed로 로컬 재생성합니다.

- 10~13층
- X/Z 방향으로 꺾이는 수직 진행 경로
- Standard / Narrow / Cargo / Hazard 층 변형
- Core / 추가 Salvage / Drone / Monster를 deterministic 레벨 위치로 재배치
- HUD에 현재 MAP Seed 표시

## 3단계 — 근접 음성채팅

LAN 프로토타입 자체 구현입니다.

- Unity `Microphone` 기본 장치 사용
- 16 kHz mono signed 16-bit PCM
- 20 ms 패킷
- `V` Push-to-Talk
- NGO Unreliable RPC로 Client → Server → Clients/Host 릴레이
- 원격 플레이어 위치의 3D `AudioSource`에서 재생
- 약 1.5m 최대 음량 / 14m 부근 무음
- `M` 수신 음소거

Stage 5부터 실제 음성 크기가 일정 이상이면 서버에 **Voice Noise** 이벤트가 생성됩니다. 따라서 Stalker 근처에서 `V`로 크게 말하면 몬스터가 그 위치를 조사할 수 있습니다.

현재 PCM 구현은 LAN 검증용입니다. 실제 Steam 배포 단계에서는 Opus/Vivox/Steam Voice 계열 압축, AEC, 노이즈 억제, 장치 선택 기능으로 교체할 예정입니다.

## 4단계 — Physics Loot

모든 운반 물체는 `PhysicsLoot` 공통 컴포넌트를 사용합니다. Signal Core도 같은 시스템 위에서 동작합니다.

| 물체 | 무게 | 가치 | 내구도 | 운반 인원 | 특징 |
| --- | ---: | ---: | ---: | ---: | --- |
| SIGNAL CORE | 5 kg | $1000 | 120 | 1 | 승리 필수 목표 |
| DATA CACHE | 4 kg | $220 | 65 | 1 | 가벼운 Salvage |
| INDUSTRIAL BATTERY | 14 kg | $420 | 120 | 1 | 이동/스태미나 페널티 큼 |
| GLASS RELIC | 3.5 kg | $700 | 35 | 1 | 고가지만 매우 깨지기 쉬움 |
| REACTOR ASSEMBLY | 30 kg | $1100 | 180 | **2** | 두 명이 함께 들어야 정상 운반 |

무게는 Rigidbody mass와 이동속도/스태미나 소비에 영향을 줍니다. 충격 속도가 각 물체의 Impact Threshold를 넘으면 서버가 내구도를 감소시킵니다. 내구도가 낮아질수록 회수 가치가 낮아지고 0이 되면 파손되어 가치가 0이 됩니다.

## 5단계 — 몬스터

`MonsterController`는 세 몬스터의 판단, 이동, 공격을 서버에서만 실행하고 위치/회전/AI 상태를 클라이언트에 동기화합니다. NavMesh에 의존하지 않고 수직 랜덤 타워에서 움직일 수 있도록 공중/벽면을 넘나드는 비정상 생물 형태의 직접 3D 추적을 사용합니다.

### STALKER — 소리를 듣는 추적자

- 시야성 직접 감지 범위는 비교적 짧음
- 대신 최대 약 24m 범위의 최근 Noise Event를 조사
- 플레이어 달리기, 음성채팅, 큰 Loot 충돌/파손 소리에 반응
- 소리가 난 위치로 이동한 뒤 가까운 플레이어를 발견하면 추적
- 근접 공격은 중간 수준 HP 피해와 작은 knockback

즉, 팀원이 떨어져 있을 때 음성으로 계속 떠드는 행동 자체가 위험 요소가 됩니다.

### BRUTE — 추락을 만드는 돌격형

- 약 12m 범위에서 플레이어를 직접 추적
- 근접 공격 시 높은 HP 피해
- 소유 클라이언트의 CharacterController에 수평+상향 knockback을 전달
- 발판 가장자리에서 맞으면 실제로 추락할 수 있음
- 공격 자체도 큰 Noise Event를 발생시켜 주변 Stalker를 끌어들일 수 있음

### SCAVENGER — Loot를 노리는 약탈자

- 플레이어보다 `PhysicsLoot`를 우선 대상으로 평가
- **운반 중인 물체**, 고가 물체, Signal Core에 더 높은 target score 적용
- 약 22m 안의 가치 있는 Loot를 추적
- 공격 시 현재 carrier 슬롯을 서버에서 즉시 해제
- Loot에 직접 내구도 대미지를 주고 충격 velocity를 적용
- GLASS RELIC 같은 취약한 고가 물체는 짧은 시간 안에 파손될 수 있음

이 때문에 단순히 플레이어만 살아남는 것이 아니라 **가치 있는 물체를 몬스터에게서 보호하는 역할 분담**이 필요합니다.

## Noise System

`MonsterNoiseSystem`은 서버 전용 최근 소음 이벤트 버퍼입니다. 현재 소음 발생원:

- 일반 이동: 작은 범위
- Sprint 수준 이동: 더 큰 범위
- `V` 음성채팅: 실제 PCM amplitude에 비례한 범위
- Loot 던지기/강한 충돌/파손
- Brute/Stalker 공격

소음 이벤트는 위치, 반경, 발생 플레이어, 종류, 시간을 저장하고 약 4초 뒤 폐기됩니다. Monster는 자신의 hearing range 안에 있는 최근 이벤트 중 가장 강한 후보를 선택해 조사합니다.

## 몬스터와 Physics Loot 상호작용

Stage 5를 위해 `PhysicsLoot`에 몬스터 충격 API가 추가되었습니다.

- Scavenger 공격 → carrier 전부 해제
- 중력 즉시 복귀
- 서버 내구도 피해
- 서버 Rigidbody velocity 충격
- 큰 충돌 소음 발생
- 내구도 0 → 기존 Stage 4 파손/파편/HUD 로직 그대로 재사용

따라서 이후 몬스터를 추가하더라도 별도의 Loot 시스템을 새로 만들 필요가 없습니다.

## 씬 구조

`Tools > Signal Haul > Rebuild Main Scene`을 실행하면 Scene Version 7 구조가 생성됩니다.

```text
SIGNAL_HAUL
├─ Managers
│  ├─ GameManager
│  └─ Network Manager
├─ Environment
│  ├─ Ground
│  ├─ Base Deck
│  ├─ Tower
│  └─ Generated Random Tower
├─ Gameplay
│  ├─ Player Spawn Points
│  ├─ Extraction
│  ├─ Signal Cores
│  └─ Salvage Loot
└─ Hazards
   ├─ Storm Drone 01
   ├─ Storm Drone 02
   ├─ Storm Drone 03
   └─ Monsters
      ├─ STALKER      [NetworkObject, MonsterController]
      ├─ BRUTE        [NetworkObject, MonsterController]
      └─ SCAVENGER    [NetworkObject, MonsterController]

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

## Unity 테스트가 가능해졌을 때

최상단 `feat/monsters` 브랜치를 체크아웃하면 1~5단계를 한 번에 검증할 수 있습니다.

1. Unity **6000.6.0f1**에서 프로젝트를 엽니다.
2. 패키지/스크립트 컴파일을 완료합니다.
3. **Tools > Signal Haul > Rebuild Main Scene**을 실행합니다.
4. `Hazards/Monsters` 아래 STALKER / BRUTE / SCAVENGER가 실제 GameObject로 저장됐는지 확인합니다.
5. Editor Host + Standalone Client를 `127.0.0.1`로 연결합니다.
6. 양쪽 MAP Seed와 타워/Loot/Monster 시작 위치가 일치하는지 확인합니다.
7. Stalker에서 떨어진 곳에서 천천히 걸을 때와 Sprint할 때 반응 차이를 확인합니다.
8. Stalker 근처에서 `V`로 말해 소리 위치를 조사하는지 확인합니다.
9. Brute에게 발판 가장자리에서 맞아 양쪽 화면에서 HP 감소와 knockback/추락이 일치하는지 확인합니다.
10. Scavenger가 운반 중인 Loot를 추적하고 공격 시 플레이어 손에서 물체가 빠지는지 확인합니다.
11. GLASS RELIC를 Scavenger에게 공격받게 해 내구도 감소/파손/HUD BROKEN 증가가 모든 peer에서 같은지 확인합니다.
12. REACTOR ASSEMBLY를 두 명이 들고 있을 때 Scavenger 공격으로 두 carrier가 모두 풀리는지 확인합니다.
13. 3~4인에서 이동, Core, 랜덤 맵, 음성, 물리 Loot, 세 Monster를 함께 검증합니다.

현재 이 코드는 Unity Editor에서 실제 컴파일/멀티클라이언트 플레이 테스트를 아직 수행하지 못했습니다. 검증 전까지 각 단계 PR은 Draft로 유지합니다.

## 단계별 브랜치

```text
main
└─ feat/multiplayer-foundation   # Stage 1
   └─ feat/random-map            # Stage 2
      └─ feat/proximity-voice    # Stage 3
         └─ feat/physics-loot    # Stage 4
            └─ feat/monsters     # Stage 5
```

다음 단계는 **6단계 상점/업그레이드**입니다. Stage 4에서 이미 동기화 중인 `SALVAGE` 금액을 재화로 사용해, 라운드 사이에 팀 업그레이드 또는 개인 장비를 구매하는 구조로 확장할 수 있습니다.
