# Project Structure — Unity 환경·경로·자산 운영

> 초기 환경 확인: 2026-11-01. P3 실제 실행 구조 갱신: 2026-11-04. 최소 실행·입력·한글 표시는 P0 사용자 테스트 통과, P2 정적 아트/Scene은 사용자 수용 완료다. P3 자동/Editor 검증과 사용자 실조작 수용은 구분한다. 상세 현황은 CurrentState.md, 2.2절은 목표 구조, 2.3~2.4절은 현재 자산·실행 구조다.

## 1. 초기 환경 선정과 기록

- 엔진: 현재 프로젝트는 사용자가 준비한 Unity **6000.6.4f1**을 사용한다. 초기 LTS 우선 권고와 달리 6.6 환경으로 생성되어 있으며, 이 문서 갱신으로 엔진 변경을 지시하지 않는다. 이후 버전 변경은 6절의 환경 변경 프로토콜을 따른다.
- 플랫폼: PC 우선, 모바일 입력은 이후 별도 연결. 초기 세계/인물/상품 기능은 `GameDesign.md`를 따른다.
- 렌더링: 2D. **권장 기본 선택**은 Unity 2D URP 프로젝트. 실제 사용 여부·URP 버전·렌더러 설정은 최초 생성 후 기록한다.
- UI: Unity Editor에서 제작하는 uGUI Canvas/Prefab + TextMeshPro를 기본안으로 삼는다. UI Toolkit 등 다른 방식으로 바꿀 때는 Editor 제작·입력·레이아웃·모달 생명주기 계약을 만족하는지 비교 후 승인한다.
- 입력: Unity Input System으로 PC 입력을 공통 행동 명령으로 변환한다. 모바일은 같은 인터페이스·소스 활성/제어권 경계로 연결하고 중복 입력/충돌을 방지한다. 2026-11-02 사용자 결정에 따라 구체 모바일 동작·UI/UX는 기본 프레임워크 완성 및 테스트 통과 후 설계·구현한다. 게임 규칙에 장치 조건을 넣지 않는다.
- 테스트: EditMode 도메인/계약 테스트 + PlayMode 실제 Scene/입력·UI 테스트 + 사용자 Game View 수용. 성능은 적절한 단계부터 실제 Player 빌드의 Profiler로 관찰한다.
- 소스 관리: 새 Git 저장소. `.meta` 보존, Unity Asset Serialization **Force Text**, Version Control **Visible Meta Files** 설정을 권장한다. 프로젝트 설정 파일은 실제 Unity 버전의 메뉴/지원 기능을 확인하고 기록한다.

### 1.1 실제 환경과 검증 상태

| 항목 | 실제 값/검증 상태 |
|---|---|
| Unity Editor 정확한 버전 | `6000.6.4f1`, revision `12bfff696524`. `ProjectSettings/ProjectVersion.txt` 기준 |
| URP / Renderer / Color Space | URP `17.6.0` / `Assets/Settings/UniversalRP.asset` → `Renderer2D.asset`(기본 인덱스 0) / Linear. Quality Settings의 6개 품질 항목이 같은 URP 자산 참조 |
| Input System 패키지 버전 / Active Input | `1.20.0` / Input System Package (New), `activeInputHandler: 1`. `Assets/Settings/InputSystem_Actions.inputactions` 존재. Scene의 EventSystem에 `InputSystemUIInputModule` 연결. 최소 실행·입력은 사용자 직접 테스트 통과(2026-11-01) |
| TMP / uGUI | uGUI `2.6.0`에 TMP 포함. `Assets/TextMesh Pro/`에 Essential Resources 임포트됨. 별도 `com.unity.textmeshpro` 의존성 없음 |
| 기본 한국어 폰트 | `TMP Settings.asset`의 기본 폰트는 `Galmuri11 SDF.asset`(GUID `11f430421abc0d84297aa673a343400e`). 원본 `Galmuri11.ttf` 참조, Dynamic Atlas. Bold·Condensed TTF/SDF도 존재. 전역 Fallback은 비어 있음. 한글 표시·폰트 입력·적용은 사용자 직접 테스트 통과(2026-11-01). 제품 UI의 해상도별 줄바꿈·클리핑은 이후 해당 화면에서 검증 |
| 타깃 PC OS, 빌드 아키텍처 | 활성 빌드 대상 `StandaloneWindows64` / Standalone. 최종 지원 Windows 버전·최소 사양은 미정 |
| 기준 해상도·화면비·Canvas 스케일 정책 | Player 기본 크기 `1920×1080`(16:9). P2 Game Canvas는 Screen Space - Camera, Reference `1920×1080`, Scale With Screen Size / Match Width Or Height `0.5`. 1280×720 / 1600×900 / 1920×1080 실제 카메라 캡처 확인. 최종 지원 화면비·모바일 규격 확정은 아님. SampleScene의 기존 Overlay/Constant Pixel Size는 유지 |
| PPU / 세계 Cell Size / 피벗·스프라이트 기준점 | 기본 P2 Sprite PPU128, 피벗 중앙(0.5,0.5), Full Rect. 파티션 조각 native폭0.5 unit/Scale1은 유지한다. 현재 로컬 Placement Cell은1 unit이며 Unity Grid/Tilemap 컴포넌트로 월드를 구성하지 않는다. 바닥은 native8×8 unit 독립 Repeat Sprite를11.6×6.5 영역에 표시하고 배치 Cell과 독립적이다. 2.3절·3절의 현행 가구 구성을 따른다 |
| 테스트 실행 명령/방법 | Test Framework 1.8.0. CLI command test run --mode editor --filter CabinDomainTests / --mode playmode --filter CabinPlayTests --async_tests true. Temp/pipeline_test_status.json에서 completed/summary 확인. EditMode 8/8·실제 Game Scene PlayMode 1/1 통과. Player 빌드 검증은 미확인 |
| 물리 레이어·충돌 매트릭스 / 공간 질의 정책 | 기본 Layer와 WorldSolid(8), CharacterBody(9). 글로벌 매트릭스는 기본값 유지, Collider excludeLayers로 Actor↔Solid/Actor만 허용. 정적 가구/벽에는 Rigidbody 없음. 책상 거리·Linecast 및 로드 OverlapCircle은 WorldSolid만 질의 |
| 물리 스텝 | Fixed Timestep `0.02초`, Maximum Allowed Timestep `0.33333334초`, Time Scale `1`. 엔진 기본 설정이며 게임의 하루 600초를 구현했다는 뜻은 아님 |
| 플레이어·선원 이동/렌더 스텝·보간 정책 | 플레이어 Dynamic Rigidbody2D, gravity0/회전 고정/Continuous/Interpolate. FixedUpdate 가감속, LateUpdate 해소 위치→단일 CharacterState. CharacterView/카메라 LateUpdate. 선원은 미구현 |
| Play Mode 진입 설정 | m_EnterPlayModeOptionsEnabled:1 / m_EnterPlayModeOptions:1: Domain Reload 생략, Scene Reload 유지. P3 세션·입력·정지 토큰·저장 슬롯의 반복 Scene/Play 진입을 검증했다. Test Runner 종료의 Editor 내부 assertion 1건은 CurrentState의 미해결 관찰을 따른다 |
| 직렬화 / 메타 관리 | Force Text(`m_SerializationMode: 2`) / Visible Meta Files |
| 기준 테스트 환경 / Profiler 계측 방법 | 현재 Development Build·Allow Debugging·Connect With Profiler 모두 꺼짐. 측정용 Player·하드웨어·계측 조건은 미확정 |
| 초기 성능 기준선(장면, 유닛 수, Frame Time·GC) | P3 단일 플레이어 선장실 Editor ProfilerRecorder 측정, References/P3/Editor*Profile.json. Idle/접촉 정지/일지 각600프레임. Editor·CLI·캡처 비용 포함. Player·다수 유닛은 미확인, 최종 FPS/메모리 합격 수치는 미정 |
| Unity CLI / Pipeline | CLI `1.0.0-beta.15`, `com.unity.pipeline` `0.9.1-exp.1`. CLI/MCP 연결 확인. 다의적인 MCP 명령은 실패할 수 있어 읽기 전용 빌드 조회는 CLI `command build get_settings`로 확인 |

**원칙:** 위 `미설정`은 반드시 채워야 할 환경 정보이지 임의 기본값을 써 넣어도 되는 항목이 아니다. 미설정인 핵심 항목이 필요한 작업을 진행할 때는 그 작업의 게이트에서 차단한다.

### 1.2 패키지와 확인 근거

- 버전 원본: `Packages/manifest.json` 및 `Packages/packages-lock.json`. 아래 직접 의존성의 manifest·lock 버전 일치를 확인했다.
- 2D Animation `16.0.1`, Aseprite `6.0.0`, PSD Importer `15.0.1`, Sprite `1.0.0`, SpriteShape `16.0.0`, Tilemap `1.0.0`, Tilemap Extras `9.0.1`, 2D Tooling `4.0.1`, Timeline `6.6.0`.
- 렌더링: `ProjectSettings/QualitySettings.asset`, `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Renderer2D.asset` 및 각 `.meta`의 GUID 연결을 확인했다.
- 입력·UI·화면: `ProjectSettings/ProjectSettings.asset`, `EditorBuildSettings.asset`, `Assets/Scenes/SampleScene.unity`의 직렬화 설정을 확인했다. 자산·참조 존재와 실제 동작 검증을 구분한다.
- 폰트: `Assets/TextMesh Pro/Resources/TMP Settings.asset` → `Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset` → `Galmuri11.ttf` 참조를 확인했다. 기본 폰트 설정은 기존 개별 TMP 컴포넌트의 명시적 폰트 선택을 일괄 교체하지 않는다.
- LiberationSans 메타데이터 Importer 버전 경고는 사용자 지시로 수용한 비차단 항목이다. 이를 수정하거나 모든 경고를 무시하는 정책으로 확대하지 않는다.
- P0 최소 실행·입력·한글 표시와 폰트 입력·적용은 사용자가 직접 테스트하고 통과를 보고했다(2026-11-01, 현재 대화). 에이전트의 재실행·자동 테스트·Player 빌드 검증으로 표현하지 않는다. 제품 기능·조작감·저장·성능 검증으로 확대하지 않는다.
- P0 완료 후 main 푸시 예정이라는 사용자 방침을 따른다. 이번 환경 기록 작업은 테스트 실행·커밋·푸시 또는 P1 착수를 뜻하지 않는다.
- 환경·폴더·자산·검증 상태가 바뀌면 해당 작업에서 이 문서와 `CurrentState.md`를 함께 현행화한다. 이미 해소된 미확인 항목을 유지하거나 과거 상태를 누적하지 않는다.

## 2. 현재 구조와 목표 저장소 트리

### 2.1 현재 실제 구조

- 저장소·Unity 프로젝트 루트: `D:/Great_Voyage/Great_Voyage`. 다른 기기에서는 클론한 루트를 기준으로 상대 경로를 사용한다.
- 원격 저장소: `https://github.com/K-Jung-H/Great-Voyage.git`.
- 루트 `AGENTS.md`는 작업 진입 안내이며, 관리 문서 8개와 참고 자료는 `Agents/`에 있다. `Agents/References/`에 D01~D12·E01~E11·G01~G03 PNG 26장과 보관 기획서가 있다. 현행 `GameDesign.md`의 이미지 링크는 유효하다.
- `Agents/`는 Git 공유 대상이며 `Assets/` 밖에 두어 Unity Project 창·AssetDatabase의 자동 임포트·게임 빌드 콘텐츠와 분리한다. 운영체제 파일 접근을 차단하는 보안 경계는 아니다.
- `Agents/References/ArtStyle_2026-11-02/`에 사용자 제공 화풍 자료를 원본 이름·내용으로 보존했다: `Samples/` PNG 2개, `Sheets/` PNG 9개, `SourceDocuments/` MD 3개. 복사 후 원본과 SHA-256 일치를 확인했고 `P1 Complete` 커밋으로 main에 공유했다. 하위 `Agent.md`는 참고용 프롬프트이며 관리 지침이 아니다. 제품 Sprite는 `Assets/Game/Content/Art/`에 별도로 제작·임포트한다. 원본 기술 확인은 `VisualStandards.md` 1.2절을 따른다.
- `Assets/Game/Scenes/Game.unity`가 현재 유일한 활성 Build Settings Scene이다. 기존 `Assets/Scenes/SampleScene.unity`는 파일을 유지하고 빌드 목록에서 비활성화했다. `Assets/Settings/Scenes/URP2DSceneTemplate.unity`는 템플릿 Scene이다.
- `Assets/Settings/`에는 렌더러·입력 등 템플릿 설정, `Assets/Welcome/`에는 템플릿 안내 자산·스크립트, `Assets/TextMesh Pro/`에는 TMP 리소스·폰트가 있다.
- 제품용 Assets/Game/에 P2 표현 자산과 P3 Domain/Application/Infrastructure/Presentation/Composition 실행 코드·EditMode/PlayMode 테스트가 존재한다(2.3~2.4절). 아래 목표 트리는 전체 구현 완료 목록이 아니다.

### 2.2 목표 저장소 트리

```text
GreatVoyage/
├─ AGENTS.md                     # Agents/AGENTS.md 진입 안내
├─ Agents/                       # Git 공유 문서, Unity 임포트 제외
│  ├─ AGENTS.md
│  ├─ GameDesign.md
│  ├─ VisualStandards.md
│  ├─ Architecture.md
│  ├─ ProjectStructure.md
│  ├─ DevelopmentRules.md
│  ├─ RoadMap.md
│  ├─ CurrentState.md
│  └─ References/                # D/E/G 원본 이미지와 보관 기획서
├─ Assets/
│  ├─ TextMesh Pro/              # 현재 TMP 공통 리소스·폰트
│  └─ Game/
│     ├─ Scenes/
│     │  ├─ Game.unity             # 단일 정식 게임 진입점(초기 기본안)
│     │  └─ Tests/                # 테스트 전용 Scene (제품 진입점 복제 금지)
│     ├─ Runtime/
│     │  ├─ Domain/
│     │  │  ├─ Common/
│     │  │  ├─ World/
│     │  │  ├─ Ships/
│     │  │  ├─ Characters/
│     │  │  ├─ Work/
│     │  │  ├─ Navigation/
│     │  │  ├─ Economy/
│     │  │  ├─ Journal/
│     │  │  ├─ Events/
│     │  │  └─ Combat/
│     │  ├─ Application/
│     │  │  ├─ Session/
│     │  │  ├─ Flow/
│     │  │  ├─ Commands/
│     │  │  └─ Simulation/
│     │  ├─ Infrastructure/
│     │  │  ├─ Input/
│     │  │  ├─ Persistence/
│     │  │  ├─ Physics/
│     │  │  └─ Loading/
│     │  ├─ Presentation/
│     │  │  ├─ World/
│     │  │  ├─ Characters/
│     │  │  ├─ UI/
│     │  │  ├─ Camera/
│     │  │  └─ Audio/
│     │  └─ Composition/
│     ├─ Content/
│     │  ├─ Definitions/          # SO 등 읽기 전용 콘텐츠/밸런스 정의
│     │  ├─ Prefabs/
│     │  │  ├─ World/
│     │  │  ├─ Characters/
│     │  │  ├─ Facilities/
│     │  │  └─ UI/
│     │  └─ Art/
│     │     ├─ Characters/
│     │     ├─ Environment/
│     │     ├─ Map/
│     │     ├─ UI/
│     │     └─ Animation/
│     ├─ Editor/                  # 제품 Runtime이 참조하지 않는 Editor 코드
│     └─ Tests/
│        ├─ EditMode/
│        ├─ PlayMode/
│        └─ Performance/             # 대표 부하·성능 회귀 시험(필요 시)
├─ Packages/
├─ ProjectSettings/
└─ .gitignore
```

- 이 트리는 **디렉터리 책임 표준**이지 모든 빈 디렉터리·스크립트·SO를 첫날 생성하라는 지시가 아니다. 첫 Slice에 필요한 폴더만 만들고 기능이 생길 때 확장한다.
- `Assets/Game/Runtime/Domain`은 UI/Transform/Prefab/Scene/파일 IO에 의존하지 않는 순수 C# 게임 규칙을 우선한다.
- `Application`은 도메인 간 사용 사례와 전환·원자적 명령을 담당한다. `Infrastructure`는 구체 기술 어댑터, `Presentation`은 보이는 것과 입력 피드백만 담당한다.
- `Composition`에는 시작 시 참조 연결만 둔다. 기능별 호출을 모아 놓은 거대 Update 클래스를 만들지 않는다.
- `Definitions`와 `Content/Prefabs`는 구분한다. SO 정의가 Runtime State 역할을 대신하지 않는다.
- `References`는 **기획 참고용 원본**이며 Unity에 자동 임포트할 제품 Sprite가 아니다. 실제 게임 자산은 검수 후 `Content/Art`에 별도로 추가한다.

### 2.3 P2 실제 표현 자산과 검증 규격

| 종류 | 현재 실제 파일·구성 |
|---|---|
| 제품 진입 | Assets/Game/Scenes/Game.unity: Main Camera / WorldContent / ScreenCanvas / EventSystem. 기존 Prefab 인스턴스 연결로 새 아트를 반영했다. 검토 상태를 복원한 뒤 저장했고 Scene 파일 자체에는 Git 차이가 없다 |
| 월드 | Content/Prefabs/World/CaptainCabin.prefab: 11.6×6.5 unit 바닥, 파티션, 아래 출입 틈, 침대·상자·의자 및 중첩 책상 |
| 책상 | Content/Prefabs/Facilities/NavigationDeskView.prefab: Artwork·ApproachPoint. 실제 상호작용 미구현 |
| 캐릭터 | Content/Prefabs/Characters/PlayerView.prefab: Head / Body / LeftHand / RightHand, SortingGroup. BackFacingParts·ThreeQuarterFacingParts·ClosedEyesParts는 비활성 표시 파츠 보관 |
| UI | Content/Prefabs/UI/JournalScreen.prefab: 기존 수용된 가죽 Day 텍스트 탭·점선·Skeleton 유지. 기본 Scene에서는 비활성, Toggle/나가기 interactable=false |
| 적용된 소품 | Content/Art/Environment/CaptainCabinAtlas.png: 합격 StyleB_OriginalSamplesOnly_01.png와 바이트 동일, 1536×1024·6 Sprite |
| 적용된 인물 | Content/Art/Characters/CaptainPartsAtlas.png: 합격 Captain_Parts_FinalCandidate01.png와 바이트 동일, 1254×1254·9 Sprite |
| UI 아트 | Content/Art/UI/JournalMaterialsAtlas.png: 기존 1536×1024·4 Sprite, 이번 변경 없음 |
| 파티션 | Content/Art/Environment/PartitionWallSegment.asset: 새 소품 아틀라스 구간을 참조하는 독립 Sprite. native 0.5×약0.350529 unit. 70개 연결, 부모/조각 Scale (1,1,1), 직선 구간별 BoxCollider2D 5개 |
| 바닥 | Content/Art/Environment/CabinFloorSegment.asset: 독립 CabinPlanksTileable.png 전체 Rect (0,0,1254,1254)를 참조. native8×8 unit/PPU156.75, Tiled로11.6×6.5 영역, Scale1. Texture Wrap=Repeat/NPOT=None/Bilinear/Uncompressed/MipmapOff. 원본 후보2와 바이트 동일, 리샘플/페인팅 없음 |
| 기존 바닥 PNG | Content/Art/Environment/CabinPlanks.png는 보존하지만 현재 선장실의 바닥 렌더러가 참조하지 않음 |
| Editor 도구 | Editor/P2PresentationAuthoring.cs·.World.cs·.ApprovedArt.cs. ApplyApprovedArt로 승인 아트 분할/Prefab 연결, ValidateApprovedArt로 구조 확인. 기존 Build는 Scene 존재 시 재생성 거부. 제품 Runtime 기능 추가 없음 |

- 위 Content/Editor 경로는 Assets/Game/ 기준이다. 새 원본 PNG 두 개는 승인본을 그대로 복사했으며 기존 경로·GUID와 기존 Sprite 이름/ID를 유지했다. Human의 사선 머리·몸통과 눈 감은 머리 3개만 추가했다. 적용 아틀라스 내부 19 Sprite와 독립 바닥/파티션 2개가 제품 표현에 준비되어 있다. 별도 보존된 기존 바닥 Sprite 1개는 현 Scene에서 사용하지 않는다.
- PNG importer는 PPU128 / Bilinear / Mipmap Off / Uncompressed / Max2048 / Full Rect. 실제 알파 경계 기준으로 각 사물을 분할하고 4px 여백을 둔다. 15개 소품/인물 Sprite Rect의 alpha>=128 경계 접촉은 모두 0이다. 실제 Rect는 References/P2/ApprovedArtValidation.json을 따른다.
- 인물 전 파츠의 동일 uniform scale은 약0.2518033이다. 몸통 표시 크기 약0.6×0.4485, 모자 포함 머리 약0.598×0.4957, 손 약0.183~0.185×0.185 unit. 승인된 손/몸통 원본 비율을 유지하며 손만 축소하지 않았다. Body 중심 (0,-0.12), Head 중심 (0,0.27), 손 (±0.4,-0.14). 후면·사선·눈 감기 표시도 같은 배율/기준 위치다.
- 애니메이션 준비: 정면/후면/약한 사선 머리·몸통과 눈 감은 머리를 같은 배율·조립 기준으로 준비하고 Editor 정적 표시를 확인했다. P3의 Idle / Moving / Interacting 표현은 기존 인물 행동 상태를 읽어 연결하며 실제 방향 전환·손 움직임·정지/재개 피드백과 재생 품질은 P3에서 검증한다. 이번 단계에서 Runtime 애니메이션을 구현한 것은 아니다.
- 가구는 기존 배치와 최대 표시 범위 안에서 종횡비를 보존하는 uniform scale을 적용했다. 책상 최대2.9×1.93, 침대1.45×2.3, 상자1.7×1.12, 의자0.95×1.14 unit. 기존 비균등 늘리기 대신 원본 비율을 유지한다.
- 카메라는 Orthographic Size5.5, (0,0,-10). Canvas는 동일 카메라/planeDistance5/**sortingOrder1000**, 기준1920×1080/Match0.5. P3에서 Canvas를 월드 깊이 정렬보다 높였다. 바닥 sortingOrder-100, 벽/가구는 발 기준 고정 Y정렬, 플레이어 SortingGroup은 매 프레임 발 Y정렬(100-round((y-0.2)*20)), 내부 Body0/Hand1/Head2다. 좁은 화면의 카메라는 방 경계 내 추적, 현재16:9에서는 방 전체가 보여 중앙에 머문다.
- 제품 TMP10개는 Galmuri11 SDF를 명시 참조하며 이번 카메라에서 한글 표시를 확인했다. 작업 전 폰트 아틀라스 초기화 변경이 있었고, Editor의 한글 UI 표시/자산 저장 과정에서 Dynamic 글리프·아틀라스 캐시가 다시 채워져 미커밋 차이도 변했다. 이를 원래 상태와 바이트 동일하게 보존했다고 주장하지 않는다. 원본 TTF·기본 폰트 설정은 변경하지 않았으며 공유하지 않았다.
- 검증 근거: ApprovedArtValidation.json, CaptainCabin_Approved_1280x720·1600x900·1920x1080.png, Journal_Approved_동일3해상도.png, Human_Approved_Front·Back·ThreeQuarter·ClosedEyes.png, FloorRepeat_Approved_3x3.png. SpriteRenderer84(비활성 파츠 포함), Missing Script/Sprite0, 활성 Frame 조작0, 파티션70/Collider5/비단위 배율0, 모든 Human 파츠 동일 배율.
- 현재 독립 Repeat 바닥은 CaptainCabin_RepeatFloor_3해상도와 FloorStyleReview/SceneApplication.json에서 검증했다. 대응 경계 색 차이 좌우약2.84/상하약6.05(RGB0~255)는 남아 있어 대응 경계 픽셀 완전 동일성과 사용자 시각 수용을 구분한다. Scene의 바닥 표현은 사용자 수용됐다. 사용자가 Repeat 바닥까지 반영한 조립 Scene 표현을 수용하여 P2 정적 첫 화면 게이트를 통과했다. 이 수용이 경계 픽셀 완전 동일성이나 실제 행동 수용을 뜻하지 않는다. Editor 카메라 확인이며 실제 입력·연속 이동·충돌·애니메이션·저장·Player 성능 통과를 뜻하지 않는다.
- 고정 제작 자료는 ArtFactory/FactoryStyle.json·StyleB.locked.prompt.txt(소품), HumanStyle.json·HumanParts.locked.prompt.txt(인물). 기존 생성물은 새 생성의 입력/수정/긍정·부정 참조로 금지한다. 이번에는 새 이미지 생성·후처리·스킬 설치 없이 승인된 PNG를 적용했다. 추가18종은 References의 검토 자료로 보존하며 선장실에 전부 배치하지 않았다.

### 2.4 P3 실제 실행 구조

- Runtime/Domain: GameSessionState, CharacterState, WorldPosition, GameClock, PauseReasons. Runtime/Application: GameSession, Action/Interaction/Store 계약과 SaveCoordinator. 두 asmdef는 noEngineReferences=true다.
- Runtime/Infrastructure: JsonFileSessionStore. 기존 Newtonsoft 패키지의 Unity.Newtonsoft.Json 어셈블리를 참조한다. Runtime/Presentation: PcInputSource, PlayerMotor, CharacterView, JournalDesk, CabinCamera, CabinHud 및 기존 DashedBorderGraphic. Runtime/Composition: CabinSessionRoot가 단일 세션·입력·뷰·저장 어댑터의 수명을 연결한다. Domain에는 Unity/파일 IO/UI 의존성이 없다.
- Scene의 GameSession은 기존 PlayerView/NavigationDeskView/JournalScreen/ScreenCanvas를 직렬화 참조한다. 실행 중 임시 UI/월드 객체를 생성하지 않는다. PlayerView의 기존 9파트를 identity Visuals 부모 아래 묶었으며 원본 파츠 배율/스프라이트는 유지했다. 물리 루트는 좌우 반전하지 않는다.
- Content/Input/GameControls.inputactions: World/Move(WASD·방향키), World/Interact(E), UI/Back(Esc). 런타임 입력 자산은 세션별 clone이며 종료 시 Disable/구독 해제/Destroy한다. UI 포인터/선택은 기존 InputSystemUIInputModule을 사용한다. 모바일 구현 없음.
- 플레이어 CircleCollider2D offset(0,-0.2), radius0.22; 속도2.4 unit/s, 가속18/감속26 unit/s². Content/Physics/CabinContact.asset은 friction0/bounce0이다. 벽5개 직선 Collider/가구 footprint/미구현 문 경계가 WorldSolid다. 바닥·장식·손/머리 각각에 Collider/Rigidbody를 추가하지 않는다.
- JournalDesk는 approach point 거리0.8 및 장애물 Linecast로 접근을 판별한다. 로드는 선장실 범위와 WorldSolid OverlapCircle(radius0.209)로 실제 접촉 위치를 검증한다. 정상 접촉 오차를 고려한 로드 여유이며 일반 이동 충돌 반경을 줄이지 않는다.
- Scene의 CabinHud/SessionMenu/메뉴 버튼은 Editor에서 작성됐다. 메뉴의 계속하기·저장 후 종료, 일지 CloseJournal만 활성 기능이다. 일지 5개 Day Toggle·경로/기록/메모는 비활성이다. 일지 닫기는 raycastTarget=true 및 ColorTint 피드백을 사용한다.
- CabinSessionRoot의 제품 슬롯은 P3Cabin, 첫 Slice 설정은 현재 선장실 배치·Day1 08:00/rootSeed1717이다. 전체 게임의 초기 항구/재산을 의미하지 않는다. 정상 종료만 저장하고 다른 SaveEvent는 해당 시스템 개발 시 연결한다.
- 저장은 Application.persistentDataPath/Saves/P3Cabin/session.json, 버전1 JSON envelope·payload SHA-256, tmp flush 후 원자 교체와 .bak 복구다. 손상/버전 불일치/잘못된 배치가 모두 복구 불가능하면 초기화하지 않고 오류를 표시한다. 무결성은 보안 기능이 아니다.
- Tests/Editor/CabinDomainTests와 Tests/PlayMode/CabinPlayTests는 각각 격리 Temp Guid 저장소를 사용한다. PlayMode는 테스트용 InputSettings clone과 Keyboard/Mouse를 쓰고 종료 시 복원한다. 실제 사용자 저장을 삭제하지 않는다. FPS 제한 테스트도 targetFrameRate/vSyncCount를 원복한다.
- Editor/P3SceneAuthoring은 최초 연결용으로 기존 연결 Scene 재실행을 금지한다. Editor/P3Validation은 격리 슬롯 설정/복원·상태 증거·ProfilerRecorder 측정용이며 제품 빌드에 포함되지 않는다. P2 도구에는 이전 계층/비활성 UI 가정이 있어 P3 Scene에 재실행하지 않는다.
- 검증 증거는 Agents/References/P3에 둔다. 모든 제품 Assets에는 .meta를 유지한다. 새 패키지나 전역 Physics/Input 설정 변경은 없다. 사용자 시각 테스트 통과 및 P3 Complete 명시 승인을 받았고 현행 Scene 설정·가구 비율을 유지한다. 독립 Player 실행/성능 측정은 미확인 항목으로 추적한다.

## 3. Scene/Prefab/스크립트 책임

### P3 로컬 Grid·가구 Prefab의 현재 구성 (2026-11-04)

- 최신 표준 Cell은 **1 world unit**이며 Content/Definitions/GridSettings.asset이 유일한 월드 셀 크기 원본이다. CaptainCabin.prefab Root의 PlacementSpace는 로컬 원점(-5.5,-3)·설정 참조·제외 셀(4,0)/(5,0)/(6,0)을 갖는다. 바닥 객체의 두 GridArea는 같은 원점 기준 RectInt(0,0,6,6)/(5,0,6,6)이다. 두 영역을 합치고 중복을 제거한66개에서 제외3개를 뺀63개 셀만 배치 허용한다. 제외 영역은 하단 출입구 여유이며 이동 차단 규칙이 아니다. 다른 함선/층은 별도 PlacementSpace를 사용한다.
- Runtime/Domain/PlacementGrid·GridCell·GridRect는 Unity 미참조의 합집합/제외·전체 Footprint·연속 중심 적합성 검사다. PlacementSpace가 같은 파생 정의를 검사와 Editor Gizmo에 제공한다. Area 경계를 걸쳐도 전체 셀이 같은 Space의 허용 집합이면 유효하다. 이동/물리/바닥 Repeat는 연속 좌표를 유지하며 Runtime 스냅/Overlay는 없다.
- 각 가구 Root의 CellSize가 공유 GridSettings·FurnitureDefinition·Cell 수·CellArea를 참조한다. Content/Definitions의 네 FurnitureDefinition은 종류 ID만 갖는다. 점유 Cell은 책상2×1·침대1×2, 의자/상자는 미정(0,0)이며 중복된 점유 원본은 없다. CellArea 중심의 소유 Space 로컬 위치로 적합성을 검사한다. 이전 PlacementZone/PlacementGridDefinition/FurnitureFootprint 및 CaptainCabinGrid 자산은 제거했다.
- Facilities/NavigationDeskView.prefab·CaptainBedView.prefab·CaptainChairView.prefab·SeaChestView.prefab은 CaptainCabin 내부의 nested Prefab으로 연결된다. 벽/바닥은 가구 Prefab으로 추출하지 않았다. 총 제품 Prefab7개다.
- 각 가구 Root 아래 **CellArea(FurnitureCellArea) / Visuals(SpriteRenderer) / Collision(BoxCollider2D)**가 형제이며 책상 ApproachPoint도 형제다. 렌더러에는 Collider가 없고 Collision에는 Renderer가 없다. Root·CellArea·Collision Scale1, Visuals만 원본 비율을 유지하는 uniform scale이다. 셀 영역2×1 안의 책상 Sprite는 약1.368×1, 세로 침대1×2 안의 Sprite는1×1.769 unit이다. 의자/상자는 이전 크기다. 실제 충돌 경계는 별도 크기로 유지하며 정렬은 시각 바운드의 발 기준이다.
- 책상 초기 위치(2.5,1.5), approach local(0,-0.9), 침대(-4,1)이다. Collider를 Visuals에서 분리할 때 월드 크기를 독립적인 Collision.size/offset으로 옮겼다. 이후 Visuals 배율 변경은 Collider와 CellArea에 영향을 주지 않는다. PlayMode 테스트는 실제 접근점·Collider의 충돌 기대 위치와 Scene 재진입 후 계층/점유 적합성을 검사한다. 사용자 저장 파일/저장 스키마 변경 없음.
- Editor/P3GridInteractionAuthoring.Apply는 공유 설정·CellSize·바닥 GridArea·PlacementSpace·책상 Highlight와 Scene 참조를 연결한다. 기존 Cell 수/Rect/제외 셀은 보존하고 Prefab별 저장·강제 재임포트 후 Scene을 저장한다. 기존 P2/P3 전체 제작 도구는 현행 계층에 재실행하지 않는다. Scene Gizmos 활성 시 각 가구의 셀·크기는 선택 없이 표시한다. CaptainCabin 선택 시 허용 합집합/제외 셀을 확인하고 바닥 선택 시 두 GridArea Rect를 Inspector에서 편집한다. 설정 변경은 자동 재배치가 아니다.
- Runtime/Application/InteractionResolver는 IInteractionTarget을 조회해 단일 선택 결과를 GameSession에 제공한다. Composition/CabinSessionRoot.interactionTargets에는 현재 책상만 직렬화 연결한다. Presentation/World/InteractionHighlight와 Content/Art/InteractionOutline.shader·.mat은 원본 SpriteRenderer의 내부 알파 가장자리를 약1.25 화면 픽셀의 따뜻한 색으로 강조하며 해제 시 원래 재질/속성을 복구한다. HUD와 E 입력도 같은 선택 ID를 사용한다. 이미지·저장 스키마는 변경하지 않았다.
- 현행 근거는 References/P3/GridInteraction의 구조·EditMode20/20·PlayMode1/1·대상 생명주기·실화면·Editor 캡처다. GridDefinition_1Unit은 이전 구조의1 unit 축척 자료이며 GridDefinition은 이전0.5 unit 자료다. 드래그·배치 Preview/Overlay·확정/취소·점유 트랜잭션은 후속 계약만 있고 미구현이다.

| 자산 종류 | 소유·수명 | 규칙 |
|---|---|---|
| `Game.unity` | 정식 시작점 1개 | 동일 세션/입력/시간/저장 원본을 구성. 별도 게임용 Scene 중복 생성 금지 |
| 월드 구역 Prefab | 각 지역 표현·Colliders·접근점 | 디자이너가 Editor에서 편집 가능. 구역 표시 제거가 게임 상태 삭제를 의미하지 않음 |
| 캐릭터/시설 Prefab | 인스턴스 **표현** | 도메인 ID와 바인딩, 상태 소유·가격/계약 규칙 포함 금지 |
| UI Screen/Item Prefab | 화면 기본 계층/반복 행 | 기본 화면은 Editor 제작, 동적 목록은 사전 제작 Item Prefab만 복제 |
| ScriptableObject Definitions | 종류와 콘텐츠 기본값 | 플레이 중 변하지 않는 참조 데이터. 게임 저장 원본으로 이용 금지 |
| 테스트 Scene/Fixture | 특수 조건·주입 데이터 | 정식 게임 구조를 호출하되 별도 도메인 로직 복제 금지 |

- Domain·Application·Presentation의 경계는 가능하면 asmdef 단위로 분리한다. 최초 권고 의존성: `Domain` ← `Application` ← `Presentation/Infrastructure`, `Composition`이 최종 조립. 다만 무의미한 빈 assembly·인터페이스 생성은 금지한다.
- 스크립트 배치는 클래스명보다 **실제 책임**을 기준으로 한다. UI에서 규칙을 계산한다면 UI 폴더에 있다는 이유로 허용되지 않는다.
- Screen UI의 크기/앵커/폰트·World의 배치/Collider/피벗은 제작 에디터의 직렬화된 값으로 확인 가능해야 한다. 물리 컴포넌트는 엔티티 역할에 맞게 추가하고, 정적 장식·탐지·상호작용 대상의 충돌 정책을 혼동하지 않는다.

## 4. 명명·식별·직렬화

- 코드 타입: `PascalCase`, 메서드/프로퍼티: `PascalCase`, 매개변수·지역: `camelCase`, private 필드: `camelCase` 또는 `_camelCase` 중 프로젝트 최초 선택 후 일관 적용. SerializedField는 private 기본.
- Prefab/Scene/SO: 의미가 분명한 명사 이름 (`CaptainCabin`, `JournalScreen`, `BedDefinition` 등). `Test`, `Temp`, `Prototype`을 제품 아트/씬 명칭으로 둔갑시키지 않는다.
- 물리·화면 ID와 게임 내 고유 ID를 구분한다. 게임 내 정체성은 `UnityEngine.Object.GetInstanceID`, Hierarchy 경로, Prefab 이름으로 결정하지 않는다.
- 파일 이동/리네임은 가능하면 Unity Editor를 통해 수행하며, 연관 `.meta`·직렬화 참조·GUID·Prefab 연결 손실 여부를 검사한다.
- Unity 직렬화 대상의 변경은 저장 DTO와 별개다. 실제 저장 데이터 포맷 변경 시 `Architecture.md`의 버전/복원 경계를 검토한다.
- 유닛의 Sprite·애니메이션·UI 아틀라스 규격과 import 설정은 첫 아트 적용 시 여기 실제 값으로 기록한다. 처음부터 임의 수치로 고정하지 않는다.

## 5. Git 관리 범위

**추적해야 할 것:** `Assets`(원본 에셋과 모든 `.meta`), `Packages/manifest.json` 및 잠금 파일(존재 시), `ProjectSettings`, 루트 진입 안내 `AGENTS.md`, `Agents/`의 8개 MD와 `Agents/References` 원본.

**무시해야 할 것:** Unity가 재생성 가능한 `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`(프로젝트 공유 대상이 아닌 로컬 설정), 빌드 산출물, IDE 사용자 캐시, 로컬 실제 저장 슬롯, 개인정보·비밀키. Unity 공식 템플릿 기반 `.gitignore`를 사용하고 팀의 실제 경로와 대조한다. 임의 `git clean`으로 원본 에셋을 삭제하지 않는다.

- Scene·Prefab YAML의 대량 변경은 범위가 맞는지 diff로 확인한다.
- 에이전트가 동시에 같은 Scene/Prefab/ScriptableObject YAML을 수정하지 않도록 **소유 범위 또는 브랜치**를 분리한다.
- 다른 컴퓨터에서 Unity로 열 수 있도록 샘플 이미지·메타·버전·패키지 잠금을 함께 보존한다.

## 6. 환경 변경 프로토콜

새 패키지, Unity 버전, 렌더러, 입력 시스템, UI 기반, Sprite 규격, 핵심 폴더 구조 변경 전에 **목적·대안·영향 파일·마이그레이션·성능/저장 위험**을 제시한다. 사용자 승인 후 변경하며 `ProjectStructure.md`의 실제 값과 `CurrentState.md`의 진행 상태를 같은 작업에 갱신한다. 새 기술을 도입했다는 이유만으로 기획 규칙을 바꾸지 않는다.

## 7. 물리·이동·성능 환경의 실제 관리

프로젝트 생성 후 아래 내용은 **선택한 Unity 환경에서 확인한 값**만 기록한다. 이 문서는 역할·경로의 원본이고, 기능별 물리 정책의 이유와 책임은 `Architecture.md` 9절을 따른다.

- **레이어/충돌 매트릭스:** 정적 장애물, 플레이어, 선원, 트리거/상호작용, 감지 대상, 시각 전용 등의 실제 Layer·Physics2D 충돌 허용표를 구성한다. 프로젝트에 없는 레이어를 기록하지 않는다. 동일 역할이라도 전부 Collider가 필요한 것은 아니다.
- **이동/표현 동기화:** 선택한 Rigidbody2D 이동 방식·Fixed Timestep·위치 보간/카메라 갱신 방식과 예외(포털·층 전환)를 기록한다. 프로젝트 전체의 연속 이동 품질을 실험으로 확인하고 채택한다.
- **대표 성능 환경:** 최초 실행 가능 Slice부터 테스트 Editor/Player 버전·해상도·환경, 대상 유닛 수/시설 수, 프레임 시간·물리 시간·GC 할당을 기록한다. **최종 수치 합격 목표가 미정인 것과 측정을 아예 하지 않는 것은 다르다.**
- **프로파일링 산출물:** 로그·화면 캡처·Profiler 원본은 민감 정보나 용량을 고려해 추적 여부를 정한다. 미추적 실험 자료는 `CurrentState.md`에 재현 명령/절차와 증거 위치·결과 요약을 남기고, 다른 기기로 인계할 때는 재현 가능한 테스트로 보완한다.
- **회귀 관리:** 선원 수·UI 항목·시설 수 증가 등 비용이 달라지는 구현에서는 이전 동일 조건과 비교한다. 성능 악화를 무조건 특정 최적화 기술로 해결하지 말고 우선 병목을 확인한다.

- 바닥 Repeat 후속 검토 자료: Agents/References/P2/FloorStyleReview/CabinPlanks_Tileable01.png·CabinPlanks_Tileable02.png와 각각의 Repeat3x3.png/EdgeValidation.json. 내장 imagegen 원본은1254×1254/완전 불투명이다. 후속 사용자 지시로 후보2를 제품 CabinPlanksTileable.png에 적용했다. 적용 결과는 같은 폴더 SceneApplication.json, 생성 당시 기록과 기술적 한계는 Generation.json을 따른다.
