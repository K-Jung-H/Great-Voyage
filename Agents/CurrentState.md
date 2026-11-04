# Current State — 현재 개발 상태 / 에이전트 인수인계

> 갱신: 2026-11-04. 현재 사실·승인·검증·미결정만 유지한다. 기술 경로·설정의 원본은 ProjectStructure.md다.

## 1. 단계·Git·환경

- **P0 통과 / P1 Complete / P2 Complete / P3 Complete.** 사용자가 현행 시각 테스트 통과 후 P3 Complete로 main 커밋·푸시를 명시 지시했다. 현재 Scene 설정값·가구 사이즈 비율을 유지한다. 완료 승인을 독립 Player 실행/성능의 측정 증거로 해석하지 않으며 해당 미확인은 아래에 유지한다. P4는 설명 단계이며 구현하지 않았다.
- 저장소 D:/Great_Voyage/Great_Voyage, 원격 https://github.com/K-Jung-H/Great-Voyage.git, main. 이번 공유 기준은 **P3 Complete** 커밋이며 직전 기준은 9558b2adbfdcd4231e822eec40ea1689aad7d16f(**P3 - Analyzing Test Results**)다. 정확한 HEAD·원격 일치·남은 사용자 변경은 실제 Git으로 확인한다.
- 기존 Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset 변경은 별도 사용자 작업으로 보존한다. 실행 중 Dynamic 캐시가 추가 갱신될 수 있다. 원본 TTF·기본 폰트 설정은 변경하지 않았다.
- Unity 6000.6.4f1 / Input System 1.20.0 / URP 17.6.0 / Test Framework 1.8.0 / CLI 1.0.0-beta.15 / Pipeline 0.9.1-exp.1. 새 패키지·스킬·모델·생성 파이프라인 추가 없음.

## 2. 구현된 첫 Slice

- 실제 진입점은 Assets/Game/Scenes/Game.unity다. 기존 선장실/책상/플레이어/일지 Prefab과 승인된 B안 소품·Human 9파트·Repeat 바닥을 사용한다. 아트 PNG·고정 스타일 규칙·Human 손 크기를 바꾸지 않았다.
- WASD/방향키 가감속 이동, 벽·침대·상자·의자·책상 충돌, 방향별 파츠 전환·걷기 흔들림·정지 눈 감기, 발 기준 깊이 정렬, 제한된 카메라 추적을 연결했다. 현재 16:9는 방 전체가 보여 카메라가 중앙에 머문다.
- 책상 접근 범위·가림 검사를 통과하면 단일 선택 대상으로 지정하고 얇은 금색 내부 윤곽선과 “E 항해 일지 열기”를 표시한다. E는 같은 선택 ID를 재검증해 실행한다. 일지/메뉴 정지·범위 이탈·가림·대상 비활성/제거 시 선택과 강조를 해제한다. 세계 입력·물리·게임 시간·캐릭터 동작은 정지하고 실제 닫기 버튼/Esc로 복귀한다. 현재 다른 가구는 상호작용 대상이 아니다.
- 메뉴 버튼/Esc의 정지 메뉴를 일지 위에 중첩할 수 있다. 메뉴만 닫으면 일지의 정지 사유가 유지된다. UI 포커스와 월드 입력을 분리했다.
- GameClock의 하루는 실제 600초다. UI는 원본 시계만 읽고 정지 중 경과시간을 복귀 후 보충하지 않는다.
- 첫 실행 배치와 Day 1 08:00은 선장실 Slice 설정이다. 전체 새 게임의 초기 항구·재산·시각·게임 오버 규칙을 구현/확정한 것이 아니다.
- **현행 Grid/가구:** 공유 GridSettings.asset의 표준은 **1 unit**이다. CaptainCabin Root의 PlacementSpace가 로컬 원점(-5.5,-3)과 제외 셀3개를 소유한다. 바닥의 두 GridArea Rect(0,0,6,6)/(5,0,6,6)는 같은 원점 기준이며 중복 제거 후66개, 제외 후63개 셀이다. 같은 Space의 이음부를 걸친 전체 Footprint도 검사한다. 출입구 제외3 Cell은 배치 금지이며 이동 차단이 아니다. 영역 형상·통로 폭의 최종 사용자 수용은 대기다.
- 책상/침대/의자/상자는 각각 독립 Prefab이며 논리 Root 아래 CellArea / Visuals / Collision 형제 계층이다. Root·CellArea·Collision Scale1, Visuals만 uniform scale이다. 책상2×1 Cell(2×1 unit)·현재 세로 침대1×2 Cell(1×2 unit)에 원본 비율로 맞췄다. 실제 Sprite는 책상 약1.368×1, 침대1×1.769 unit이다. 책상 초기 위치(2.5,1.5)/접근점local(0,-0.9), 침대(-4,1)이다. Collision에는 독립 크기를 적용해 Visuals 배율을 바꿔도 영향을 받지 않는다.
- Root의 CellSize는 Cell 수·공유 GridSettings·종류 정의·CellArea를 연결한다. FurnitureDefinition은 종류 ID만 갖고 점유 크기를 중복 소유하지 않는다. 의자/상자의(0,0)은 사용자가 결정을 연기한 미정 표식이며 배치 가능 크기가 아니다. 기존 시각 크기·초기 위치·인물 크기를 유지했다. 두 가구의 비율/Grid 적합성은 아직 통과가 아니다.
- Editor Scene Gizmos 활성 시 선택 없이 가구 셀 경계·점유·크기와 미정 표식이 보인다. CaptainCabin 선택 시 최종 허용/제외 셀을, 바닥 선택 시 Inspector의 두 GridArea Rect를 확인한다. 적합성은 CellArea 중심의 Space 로컬 위치를 사용한다. GridSettings 변경은 셀 시각화/판정을 갱신하지만 Transform을 자동 보정하지 않는다. Runtime Overlay/드래그/스냅/Preview/시설 재배치는 없다. 후속 계약은 Architecture 9.5.1이다.

## 3. 상태·의존성·저장

- Domain/Application은 Unity 미참조 asmdef다. GameSessionState가 플레이 ID·root seed·플레이어·시간 원본을 소유한다. 화면·모터·저장에 별도 인물 상태 복사본을 두지 않는다.
- Dynamic Rigidbody2D가 FixedUpdate에서 접촉을 해소하고 위치를 CharacterState에 반영한다. 로드는 명시적 위치 전환으로 Transform/물리 위치를 함께 초기화한다. 일반 이동은 좌표 점프로 대체하지 않는다.
- 현재 SaveEvent는 **정상 종료만 구현**했다. 메뉴의 저장 후 종료 및 애플리케이션 정상 종료에 연결한다. 프레임 저장·F5 저장·일지 열기 저장 없음. 승선·하선·항해 중 하루 경과·수면은 해당 시스템과 함께 후속 구현한다.
- 저장 경로는 Application.persistentDataPath/Saves/P3Cabin/session.json. 버전 1 JSON envelope·SHA-256 손상 검사·임시 파일 flush 후 교체·직전 정상 .bak을 사용한다. 보안/치트 방지 기능은 아니다.
- 본 파일 손상 시 정상 백업을 복구한다. 둘 다 유효하지 않으면 파일을 보존하고 오류 메뉴를 연다. 저장 실패 시 종료하지 않고 재시도할 수 있다. 강제 종료·전원 차단을 정상 종료 SaveEvent로 보장하지 않는다.
- 검증은 Guid 격리 슬롯만 사용했다. NextLoadSaveDirectory는 일회성 검증 override이며 Play 진입 시 초기화한다. Editor 검증은 Saves/Validation/P3_<Guid> 사용. 최종 제품 Scene 슬롯은 P3Cabin이어야 한다.

## 4. 검증·게이트

| 항목 | 현재 판정·근거 |
|---|---|
| G0/G1 범위·설계 | 통과 — GameDesign A02/A03/A27의 첫 Slice 범위, Architecture의 단일 상태·중첩 정지·저장 경계 적용. 전체 A항목 통과가 아님 |
| G2 시각 | 원본 아트 화풍의 P2 수용 유지. 사용자가 현행 Grid/Interaction Highlighting 시각 테스트를 통과시켰고 현재 Scene 설정·가구 비율 유지를 지시했다. 정지 화면 수용을 이동·충돌·동작의 실조작 수용으로 확대하지 않는다 |
| G3 기능 | 실제 Scene/Prefab 직렬화 연결. Runtime UI/월드 임시 생성 없음 |
| 도메인·파일 저장·Grid·선택 | EditMode20/20 통과: 기존 상태/저장8개·Grid6개·가구/공유 설정2개·상호작용 선택4개. 합집합 중복 제거·제외 우선·이음부 Footprint·음수 좌표·별도 Space·크기 공유·우선순위/유지 여유·동일 대상 재검증을 검사. GridInteraction/EditModeTests.json |
| 실제 입력 통합 | PlayMode1/1 통과(19.14초): 실제 입력·충돌·일지/메뉴 중첩·강조 전환·저장/재진입·30/60/120 요청 FPS·Scene 로드 후4개 가구 계층과 공유 설정/63셀 적합성. GridInteraction/PlayModeTests.json |
| 강조 시각·생명주기 | Ready.png에서 얇은 금색 가장자리/한글 안내, Journal.png에서 일지 확인. TargetLifecycle.json의 실제 컴포넌트 비활성/복구·WorldSolid 가림/제거 때 선택과 강조가 함께 해제/복구됨. 화면 캡처용 위치는 검사 위치로 직접 설정했으며 실제 이동 입력 증거는 별도 PlayMode 테스트 |
| 프레임 변화 | 요청 FPS 제한30/60/120 이동 속도는 GridDefinition_1Unit/MovementRates.json의 현재 측정값을 따른다. 요청 제한이며 실측 FPS 고정 보장이 아님 |
| 종료·반복 Play | 실제 종료 버튼으로 쓰기 실패→오류 유지→재시도 종료→Play 재시작 확인. 동일 PlayId/root seed/벽 접촉 위치, UI 닫힘·토큰 0 복원. BeforeFailedExit/FailedExit/AfterNormalExitReentry.json |
| 성능 | 현행 강조 활성 Editor ProfilerRecorder600프레임: frame median3.41ms/p95 5.36ms/max78.36ms, Physics2D p95 0.0098ms, GC median8513B/p95 8569B/max4324916B. ReadyProfile.json에 전체 Editor/도구 비용 포함, 제품 코드 할당량이나 이전 대비 개선으로 해석하지 않는다. Player·다수 유닛·최소 사양은 미확인 |
| G4/G5 | 자동/Editor 통합 근거·사용자 시각 수용·P3 Complete 명시 승인 확보. 별도 사용자 실조작 결과 기록과 독립 Player 실행/성능 측정은 없음. 승인과 실행 증거를 구분하며 Player 검증은 후속 확인 항목으로 유지 |

현행 구조·테스트·시각 근거는 References/P3/GridInteraction이다. Structure.json은 공유 설정·Rect·제외 셀·가구 계층/적합성, PlacementSpaceEditor.png는 실제 Unity의 허용/제외 셀과 가구 Gizmos를 확인한다. GridDefinition_1Unit의 선택 없는 가구 셀 캡처는 같은1 unit 축척의 이전 구조 증거다. GridDefinition의0.5 unit 자료와 기존 P3 루트 캡처는 현재 시각 승인 근거로 확대하지 않는다.

GridInteraction/FinalValidation.json은 Scene clean/Play 종료·컴파일 중 아님·Missing Script/Sprite0·Collider11·Rigidbody1·P3Cabin 슬롯 원복·최종 콘솔 오류/경고0을 확인했다. P3 완료의 사용자 승인은 별도 명시 지시에 근거하며 이 JSON을 사용자 실조작 결과로 대체하지 않는다.

입력 증거는 에이전트의 자동 Input System 이벤트다. 사용자 물리 키보드 실조작 평가·최종 게임 품질 승인을 대신하지 않는다. P0의 사용자 최소 입력·한글 통과도 P3 수용으로 확대하지 않는다.

## 5. 미구현·위험·다음 작업

- 일지의 Day 선택·계획·지도 조작·항적·기록·메모·항구/사건 내용, 침대 수면·상자 보관·층/항구 이동은 비활성 Frame이다. 출입구는 통과하지 못하게 경계와 안내만 연결했다. 사건·전투·미니게임·모바일 상세는 연기된 범위다.
- 선원 간 동적 접촉·경로 탐색·비표시 층 시뮬레이션은 미구현이다. 단일 플레이어 검증을 다수 선원 품질로 확대하지 않는다.
- P3 수용은 움직임만의 검토가 아니다. 현재 선장실의 인물·가구·문 크기 비율, 배치·접근점·통행 폭, 보이는 빈 공간과 충돌 영역의 일치, 앞뒤 이동 시 Sort/차폐도 같은 단계의 통과 기준이다. P2 정적 수용 후 실제 이동에서 드러난 문제는 P3에서 수정한다. 현재 Collider 크기·발 기준 Y-sort는 첫 구현값이며 최종 수용값이 아니다. 다른 층·항구 연결, 가변 시설 배치 시스템·다수 선원 이동은 후속 확장 범위다.
- Editor GC 기준선을 제품 코드 할당량으로 단정하지 않는다. Player 빌드 Profiler와 최종 지원 사양은 후속 확인이 필요하다.
- 최종 PlayMode 테스트는 통과했으나 Test Runner 종료 시 UnityEditor.EditorApplication.Internal_PlayModeStateChanged의 “Assertion failed” 1건이 관찰됐다. 원인·제품 영향은 미확인, EditorAssertion.json에 원문 보존. Test Runner 밖의 추가 격리 Play 시작/종료에서는 재현되지 않았고 최종 콘솔 오류/경고0·컴파일 실패false다(FinalConsole.json). 미해결 관찰을 테스트 실패나 해결 완료로 바꾸지 않는다.
- P2 Editor 도구에는 P2 계층/모든 UI 비활성 가정이 남는다. P3의 Visuals·활성 닫기·Canvas 순서를 가진 Scene에 P2 제작 도구를 재실행하지 않는다. 최초 연결용 P3SceneAuthoring도 연결된 Scene에 다시 실행하지 않는다.
- Galmuri 배포 라이선스 동봉·정확한 NudeBase SVG 경로 재사용/눈 외 픽셀 불변은 미확인이다. 현재 아트 수용 범위를 확대하지 않는다.
- **다음 작업:** P4의 함선 층·항구 구역 연결과 동일 세계 상태 유지 범위를 설명하고 실제 구현 전 대표 이동 구간·접근점·상태/뷰 수명을 구체화한다. 현행 Scene 설정·가구 비율을 유지한다. 의자/상자의 Cell 수는 미정이며 후속 배치 편집 전에 결정하고 임의 변경하지 않는다. 드래그/배치 편집은 후속 단계다. 독립 Player 실행/성능 검증은 미확인 항목으로 추적한다.

CurrentState/ProjectStructure는 상태가 바뀐 작업에서 함께 갱신한다. 별도 과정 로그 MD는 생성하지 않는다. 승인 없는 Git 변경·사용자 저장 초기화는 수행하지 않는다.
