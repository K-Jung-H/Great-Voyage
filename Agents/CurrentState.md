# Current State — 현재 개발 상태 / 에이전트 인수인계

> 갱신: 2026-11-05. 현재 사실·승인·검증·미결정만 유지한다. 기술 경로 원본은 ProjectStructure.md다.

## 1. 단계·Git·환경

- **P0 통과 / P1 Complete / P2 Complete / P3 Complete / P4 Complete — 사용자 명시 완료 승인(2026-11-05).** 사용자가 이동·공간 전환 등 P4 목적에 대한 동작 조건을 모두 통과했다고 확인했다. 오브젝트 배치·Scene 디자인은 사용자가 이번 단계의 평가 범위에서 명시적으로 제외했으며, 시각적 승인이나 디자인 확정으로 해석하지 않는다. 신규 환경 시트 사용 승인은 유효하다. P5는 시작하지 않았다.
- 저장소 D:/Great_Voyage/Great_Voyage, origin https://github.com/K-Jung-H/Great-Voyage.git, main. 이번 인계 기준은 main의 **P4 Complete** 커밋이다. 직전 공유 기준은81fde7463d4bc911667ac0c48a671e54136da91b(P3 Complete)이며, 사용자가 P4 Complete main 커밋·푸시를 명시 지시했다. 기존 Galmuri11 SDF.asset 사용자 작업은 이 커밋에서 제외한다.
- 기존 Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset 사용자 변경은 보존한다. Dynamic 캐시가 추가 갱신될 수 있다. 원본 TTF/기본 폰트는 변경하지 않았다. 기존 Atlas 포화로 새 구역 문자가 누락되어 같은 Galmuri11 소스의 Galmuri11World.asset(Dynamic·복수 Atlas 허용; 최종 표시 검증 캐시100글자/1 Atlas)을 추가했다. 신규 라벨·상호작용 안내/메시지와 누락 글자가 있던 Frame 라벨만 연결하며 글자 크기는 유지한다.
- Unity6000.6.4f1 / Input System1.20.0 / URP17.6.0 / Test Framework1.8.0 / CLI beta15 / Pipeline0.9.1-exp.1. 새 패키지·스킬 설치 없음. CLI beta16 업데이트는 자동 승인 검토가 작업 범위 밖 도구 변경으로 거절해 수행하지 않았다. 사용 가능한 기존 CLI로 검증했다.

## 2. 현행 제품 Scene과 실행 동작

- Game.unity의 단일 CabinSessionRoot·GameSessionState·플레이어·HUD를 유지한다. WorldArea는 선장실/상갑판/선착장/항구 교차로/마을·조선소 구역/상점가/주점 내부의7개이며 하나만 활성화한다. 비표시 구역 Prefab과 정의는 파괴/재생성하지 않는다. 모든 함선을2층으로 고정하는 규칙이 아니다.
- 12방향 Portal: 선장실↔상갑판↔선착장↔교차로, 교차로↔마을/상점가, 마을↔주점 내부. D02/D08~D12의 방향 관계를 따른다. 시청은 위쪽 비활성 서비스 위치다. 선착장 게시판은 함선 옆이다.
- 접근/가림을 통과한 단일 우선 대상에만 강조·E 안내·실행을 연결한다. 현재 실제 대상은 일지 책상과 활성 구역의 Portal이다. UI 정지·범위 이탈·가림·비활성 시 해제한다.
- E로 이동 요청→전환 cover Fade-out→목적 구역 활성/도착 검증→동일 CharacterState의 위치 확정→물리/카메라 연결→Fade-in. 전체 전환 동안 시간·월드 입력을 차단하고 위치 확정 전 중복 요청/정상 종료를 거부한다. 실패 시 원 구역·위치를 유지하고 오류 메뉴를 표시한다.
- WorldArea의 카메라 Rect와 실제 walkableRegions는 별도다. 도착/복원은 walkableRegions 및 실제 WorldSolid 충돌을 함께 검사한다. 카메라에 보인다는 이유로 벽 밖을 유효 위치로 허용하지 않는다. 선착장 육지/부두는 연속 Rect 합집합이며 바다는 이동 불가다.
- 기존 WASD/방향키 가감속·Rigidbody2D 접촉·방향별 Human 파츠·걷기/눈 감기·발 기준 Y-sort·카메라 경계·일지/메뉴 중첩 정지를 유지한다. 하루는 실제600초이며 정지 후 시간을 보충하지 않는다.
- 기존 선장실 Prefab·가구 크기/비율·책상 접근점·충돌체 수치는 보존한다. 실제 P3 Complete 책상 접근점은 local(0,-0.5), outlinePixels는3이다. 이전 문서의 -0.9/약1.25px는 초기 값이다. 회귀 테스트는 실제 접근점을 읽는다. 가구 Root/CellArea/Visuals/Collision 형제 계약과 공유1 unit GridSettings를 유지한다. 책상2×1, 세로 침대1×2, 의자/상자 Cell 수는 미정(0,0)이다.
- 선장실 PlacementSpace는 원점(-5.5,-3), 두 GridArea 합집합66개-제외3개=63개 셀이다. 상갑판은 독립 PlacementSpace와 같은 GridSettings를 사용한다. Grid는 배치 정의이며 일반 이동/물리/바닥 반복과 독립적이다. 드래그·스냅·Preview·런타임 Overlay·시설 재배치는 없다.

## 3. 자산·저장·수명

- 신규 World Prefab6개는 UpperDeck/HarborDock/HarborCrossroads/HarborTown/HarborMarket/TavernInterior다. Portal·Arrival 및 HUD/전환 UI는 Scene에 직렬화했다. 런타임 임시 UI/월드 생성 없음. 기존 CaptainCabin Prefab에는 P4 설정을 덮어쓰지 않는다.
- 신규 PortTravelAtlas는 사용자 원본 샘플1/2만 입력한 고정 B안 생성물 중 사용 승인본이다. ArtRecipe.json에 템플릿/참조/해시/승인 범위를 기록한다. 생성물 재참조·Human 변경·스타일 규칙 변경 없음. 주점 원탁/카운터는 사용자 제공 기본 결과물4.png 원본 복사에서 Sprite Rect로 추출했다. 카운터의 무관한 시트 조각은 표준 Sprite Rect를 정확히 잘라 제외한다. 픽셀 피벗을 유지하며 원본 PNG·배치·충돌을 바꾸지 않고 런타임 메시 처리도 추가하지 않는다.
- 신규 물/석재는 WorldSurface 셰이더의 atlas 내부 mirror-repeat로 경계를 연결한다. 기존 선장실 Repeat 바닥에는 적용하지 않는다. 주점 파티션은 native0.5 unit 모듈을 Scale1로 반복하며 충돌은 run별 하나다.
- 위치 원본은 CharacterState 하나다. ordinary CommitResolvedPose는 Area 변경을 거부하고 명시적 TransferTo만 공간을 바꾼다. View/물리/카메라는 결과를 읽는다. 현재 없는 선원·업무의 비표시 시뮬레이션을 임시 데이터로 만들지 않았다.
- 저장은 기존 Saves/P3Cabin/session.json, 버전1 envelope/payload SHA-256/tmp flush/원자 교체/.bak 복구를 유지한다. 기존 AreaId/X/Y 표현으로 새 구역을 복원한다. 기존 버전1 선장실 저장을 지원하며 사용자 저장을 초기화하지 않는다.
- **정상 종료·승선·하선**이 현재 SaveEvent다. 층/일반 항구 구역 이동·일지 열기는 저장하지 않는다. 승하선 저장 실패 시 완료된 이동은 유지하고 오류 메뉴의 **저장 재시도**가 파일 쓰기만 반복한다. 성공 전 Esc 재개를 허용하지 않는다. 출항/입항/수면·하루 경과 SaveEvent는 후속 시스템 범위다.
- 자동 검증은 Temp Guid, 수동 측정은 Validation/P3_<Guid> 격리 슬롯을 쓴다. 최종 Scene 슬롯은 P3Cabin으로 원복한다. 실제 사용자 저장에는 검증 위치를 쓰지 않는다.

## 4. 검증·게이트

| 항목 | 현재 판정·근거 |
|---|---|
| 구조/컴파일 | P4 의존 방향·단일 상태·직렬화된7구역/12연결. Structure.json 및 실제 Scene/Prefab 검사. 신규 표면 셰이더의 렌더 시 컴파일 오류는 include 순서를 수정한 후 통합 실행 통과 |
| EditMode | 27/27 통과: 기존20개+전환 정지/거부/원위치 유지/중복 완료/승하선 저장/저장 재시도7개. References/P4/EditModeTests.json |
| 실제 입력 통합 | 최종 마감 자산을 포함해2/2 재검증 통과(64.65초): 선장실 회귀와7구역 왕복·rollback·저장/복원·전환 입력/시간 차단. PlayModeTests.json |
| 저장/재진입 | 실제 이동→승하선 저장→상점가 정상 종료→같은 PlayId/위치로 로드→선장실 복귀 검증. 잘못된 도착점 rollback·한 구역만 활성·전환 입력/시간 차단 포함 |
| 사용자 동작 수용 | 이동·공간 전환 등 P4 목적에 대한 동작 조건 모두 통과라는 사용자 확인. 자동 검증·측정과 별도의 사용자 검증 근거이며, Player 성능 측정이나 미구현 선원/업무 검증으로 확대하지 않음 |
| 시각 | 원본 Human/소품 스타일 수용 유지, 신규 환경 시트 사용 승인 및7구역 캡처 확보. 오브젝트 배치·Scene 디자인은 사용자 지시로 이번 P4 평가에서 제외. 시각 승인·디자인 확정 상태가 아니며 P4 동작 수용의 대기 조건으로 두지 않음 |
| 성능 | 선착장·물/석재·단일 인물 Editor600프레임: frame median3.31ms/p95 4.92ms/max61.60ms, Physics2D p95 0.0098ms, GC median8593B/p95 8649B/max75168B. DockProfile.json. 층 전환 포함 별도600프레임은 median3.06ms/p95 4.50ms/max463.92ms(FloorTransitionProfile.json). Editor/CLI/캡처 overhead 포함이며 Player/최소 사양·다수 유닛 품질 보장이 아님 |
| Windows 빌드·기동 | WindowsBuild.json: StandaloneWindows64 빌드 성공, 오류0·TMP 셰이더 폐기 예정 pragma 경고1, 약149.7MB/196.85초. Builds/P4Validation/GreatVoyage.exe는 격리 슬롯을 내장한 검증용 산출물이다. PlayerStartup.json/PlayerStartup.log: D3D11·입력 초기화 및 프로세스 기동 확인. 숨김 실행이므로 Player 실조작·시각·성능·정상 종료 저장은 검증하지 않았으며 소유 검증 프로세스는 종료했다 |
| 최종 상태 | FinalValidation.json: Scene clean/Play 종료/P3Cabin 슬롯/한 구역 활성/누락 Script·Sprite0·콘솔 오류0·빌드 경고1. 과거 TestRunner assertion/글리프 경고 이력은 ConsoleHistory.json에 보존. PreservedCabinPrefabs.json은 기존 선장실·가구5종의 직렬화 공백 외 내용 보존을 확인. 빌드 중 자동 직렬화된 URP/보조 폰트 캐시는 작업 시작 전 내용으로 복원했으며 기존 사용자 Galmuri11 자산은 유지 |
| 단계 판정 | P4 Complete — 사용자 명시 완료 승인. 현재 구현된 목적 범위의 동작 수용 통과, 배치·Scene 디자인은 평가 제외다. 전체 함선 모델/선원·업무 등 미구현 범위와 Player 성능은 별도다. P5는 내용 설명만 요청받았으며 구현 착수 지시는 없음 |

## 5. 미구현·위험·다음 작업

- 상인/게시판/주점/조선소/거래 대행인/시청 서비스, 출항·입항·선원·업무·수면·일지 계획/기록/메모는 FrameDisabled 또는 후속 범위다. 항구 이동을 항해 완료·계약 정산으로 대체하지 않는다.
- 지금은 선내2구역과 항구 대표 구역의 공간 연결이다. 전체 함선 모델/가변 시설 목록/다수 선원의 비표시 층 시뮬레이션·경로 탐색은 미구현이다. 단일 인물 왕복을 A03의 업무 유지 전체 통과로 확대하지 않는다.
- P4 목적 범위의 이동·공간 전환 동작은 사용자 통과 확인을 받았다. 오브젝트 배치·Scene 디자인은 이번 평가에서 제외했으며 향후 평가 시점은 정하지 않았다. 현행 선장실 설정·가구 비율은 유지하고 의자/상자 Cell 수는 후속 배치 전에 결정한다.
- 독립 Player 빌드·숨김 기동은 확인했지만 Player 실조작/시각/성능/정상 종료 저장, 최종 지원 사양, Galmuri 배포 라이선스 동봉은 미확인이다. 기존 TestRunner 종료의 Editor assertion 관찰은 References/P3/EditorAssertion.json에 보존하며 제품 오류 해결로 단정하지 않는다.
- P2/P3 최초 제작 도구를 현행 Scene에 재실행하지 않는다. P4WorldAuthoring.Apply도 최초 작성 후 재실행을 거부한다. 후속 변경은 기존 직렬화된 구역/연결을 수정한다.
- 다음 작업은 P5 범위·통과 조건 설명 후 사용자의 후속 지시에 따른다. P4 동작 평가를 다시 대기 조건으로 요구하지 않는다. Game Scene은 WASD/방향키 이동, 접근 후 E 전환, Esc 메뉴를 사용하며 선장실→상갑판→선착장→교차로→마을/상점가 및 주점 내부를 왕복할 수 있다. 인계 시 Play 종료·제품 슬롯 P3Cabin 복원 상태다. 승인 없는 커밋/푸시·사용자 저장 초기화·P5 착수는 하지 않는다.
