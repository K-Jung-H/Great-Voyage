# Current State — 현재 개발 상태 / 에이전트 인수인계

> 갱신: 2026-11-04. 현재 사실·승인·검증·미결정만 유지한다. 기술 경로·설정의 원본은 ProjectStructure.md다.

## 1. 단계·Git·환경

- **P0 통과 / P1 Complete / P2 Complete. P3 구현·검증 진행, 사용자 실조작 수용 대기.** 사용자 “P3 진행” 지시로 첫 선장실 Slice를 구현했다. P4 이후는 시작하지 않았다.
- 저장소 D:/Great_Voyage/Great_Voyage, 원격 https://github.com/K-Jung-H/Great-Voyage.git, main. 사용자 지정 공유 마일스톤은 **P3 - Analyzing Test Results**다. 현재 P3 구현·검증 자료·관리 문서를 커밋·푸시하는 지시이며 P3 Complete 승인이 아니다. 정확한 HEAD/원격 일치는 실제 Git으로 확인한다.
- 기존 Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset 변경은 별도 사용자 작업으로 보존한다. 실행 중 Dynamic 캐시가 추가 갱신될 수 있다. 원본 TTF·기본 폰트 설정은 변경하지 않았다.
- Unity 6000.6.4f1 / Input System 1.20.0 / URP 17.6.0 / Test Framework 1.8.0 / CLI 1.0.0-beta.15 / Pipeline 0.9.1-exp.1. 새 패키지·스킬·모델·생성 파이프라인 추가 없음.

## 2. 구현된 첫 Slice

- 실제 진입점은 Assets/Game/Scenes/Game.unity다. 기존 선장실/책상/플레이어/일지 Prefab과 승인된 B안 소품·Human 9파트·Repeat 바닥을 사용한다. 아트 PNG·고정 스타일 규칙·Human 손 크기를 바꾸지 않았다.
- WASD/방향키 가감속 이동, 벽·침대·상자·의자·책상 충돌, 방향별 파츠 전환·걷기 흔들림·정지 눈 감기, 발 기준 깊이 정렬, 제한된 카메라 추적을 연결했다. 현재 16:9는 방 전체가 보여 카메라가 중앙에 머문다.
- 책상 접근 범위·가림 검사를 통과하면 E로 일지를 연다. 세계 입력·물리·게임 시간·캐릭터 동작이 정지한다. 실제 닫기 버튼/Esc로 닫고 복귀한다.
- 메뉴 버튼/Esc의 정지 메뉴를 일지 위에 중첩할 수 있다. 메뉴만 닫으면 일지의 정지 사유가 유지된다. UI 포커스와 월드 입력을 분리했다.
- GameClock의 하루는 실제 600초다. UI는 원본 시계만 읽고 정지 중 경과시간을 복귀 후 보충하지 않는다.
- 첫 실행 배치와 Day 1 08:00은 선장실 Slice 설정이다. 전체 새 게임의 초기 항구·재산·시각·게임 오버 규칙을 구현/확정한 것이 아니다.

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
| G2 시각 | 기존 아트 P2 사용자 수용 유지. P3 HUD·메뉴·동작은 신규 사용자 수용 대기 |
| G3 기능 | 실제 Scene/Prefab 직렬화 연결. Runtime UI/월드 임시 생성 없음 |
| 도메인·파일 저장 | EditMode 8/8 통과: 하루 600초, 중첩 토큰, 거부·중복 요청, 정상 종료만 저장, JSON 왕복, 손상 백업·실패 파일 보존. References/P3/EditModeTests.json |
| 실제 입력 통합 | 최종 PlayMode 1/1 통과(18.06초): 실제 Input System 키/포인터 이벤트로 벽·가구→책상→일지→중첩 메뉴→닫기→재이동→저장/Scene 재진입. 문 경계 접촉 저장/복원 포함. PlayModeTests.json |
| 프레임 변화 | 최종 요청 FPS 제한 30/60/120에서 약 2.412/2.452/2.428 unit/s. 요청 제한값이며 실측 FPS 고정 보장이 아님. MovementRates.json |
| 종료·반복 Play | 실제 종료 버튼으로 쓰기 실패→오류 유지→재시도 종료→Play 재시작 확인. 동일 PlayId/root seed/벽 접촉 위치, UI 닫힘·토큰 0 복원. BeforeFailedExit/FailedExit/AfterNormalExitReentry.json |
| 성능 | Editor ProfilerRecorder 600프레임 2조건: Idle frame median 3.22ms/p95 7.08ms, 벽 접촉 정지 3.45ms/p95 7.08ms, Physics2D p95 0.0105/0.0156ms. Editor·CLI·캡처 overhead 포함. GC와 최대 spike는 JSON 그대로 기록. Player·다수 유닛·최소 사양은 미확인 |
| G4/G5 | 자동/Editor 통합 근거 확보. 사용자 Game View·이동·손 크기·조작감 수용 및 Player 성능은 미확인. **P3 Complete로 판정하지 않음** |

최종 구조 근거 StructureValidation.json: Scene clean/Play 종료, Missing Script/Sprite/세션 참조0, Rigidbody1, 정적 Collider10/캐릭터 Collider1, Canvas order1000, 4개 버튼 클릭 감지·Day Toggle 비활성 확인. 제품 저장 슬롯은 P3Cabin으로 원복했다. ArtIntegrity.json의 소품/Human/바닥 SHA-256은 사용자 합격 원본과 모두 일치한다. Journal_Play 3해상도·Journal_Menu·SaveFailure 캡처를 보존했다. 일지 정지 시각은 JournalOpened/JournalStillPaused에서 동일하다.

입력 증거는 에이전트의 자동 Input System 이벤트다. 사용자 물리 키보드 실조작 평가·최종 게임 품질 승인을 대신하지 않는다. P0의 사용자 최소 입력·한글 통과도 P3 수용으로 확대하지 않는다.

## 5. 미구현·위험·다음 작업

- 일지의 Day 선택·계획·지도 조작·항적·기록·메모·항구/사건 내용, 침대 수면·상자 보관·층/항구 이동은 비활성 Frame이다. 출입구는 통과하지 못하게 경계와 안내만 연결했다. 사건·전투·미니게임·모바일 상세는 연기된 범위다.
- 선원 간 동적 접촉·경로 탐색·비표시 층 시뮬레이션은 미구현이다. 단일 플레이어 검증을 다수 선원 품질로 확대하지 않는다.
- P3 수용은 움직임만의 검토가 아니다. 현재 선장실의 인물·가구·문 크기 비율, 배치·접근점·통행 폭, 보이는 빈 공간과 충돌 영역의 일치, 앞뒤 이동 시 Sort/차폐도 같은 단계의 통과 기준이다. P2 정적 수용 후 실제 이동에서 드러난 문제는 P3에서 수정한다. 현재 Collider 크기·발 기준 Y-sort는 첫 구현값이며 최종 수용값이 아니다. 다른 층·항구 연결, 가변 시설 배치 시스템·다수 선원 이동은 후속 확장 범위다.
- Editor GC 기준선을 제품 코드 할당량으로 단정하지 않는다. Player 빌드 Profiler와 최종 지원 사양은 후속 확인이 필요하다.
- 최종 PlayMode 테스트는 통과했으나 Test Runner 종료 시 UnityEditor.EditorApplication.Internal_PlayModeStateChanged의 “Assertion failed” 1건이 관찰됐다. 원인·제품 영향은 미확인, EditorAssertion.json에 원문 보존. Test Runner 밖의 추가 격리 Play 시작/종료에서는 재현되지 않았고 최종 콘솔 오류/경고0·컴파일 실패false다(FinalConsole.json). 미해결 관찰을 테스트 실패나 해결 완료로 바꾸지 않는다.
- P2 Editor 도구에는 P2 계층/모든 UI 비활성 가정이 남는다. P3의 Visuals·활성 닫기·Canvas 순서를 가진 Scene에 P2 제작 도구를 재실행하지 않는다. 최초 연결용 P3SceneAuthoring도 연결된 Scene에 다시 실행하지 않는다.
- Galmuri 배포 라이선스 동봉·정확한 NudeBase SVG 경로 재사용/눈 외 픽셀 불변은 미확인이다. 현재 아트 수용 범위를 확대하지 않는다.
- **다음 작업:** 사용자 Game Scene 실조작에서 이동·접촉·방향/손 표현·일지/메뉴 중첩·저장 재진입을 평가하고 같은 P3에서 필요한 결함을 수정한다. 수용 전 P4로 진행하지 않는다.

CurrentState/ProjectStructure는 상태가 바뀐 작업에서 함께 갱신한다. 별도 과정 로그 MD는 생성하지 않는다. 승인 없는 Git 변경·사용자 저장 초기화는 수행하지 않는다.
