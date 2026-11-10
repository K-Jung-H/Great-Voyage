# Current State — 현재 개발 상태 / 에이전트 인수인계

> 갱신: 2026-11-09. 현재 사실·승인·검증·미결정만 유지한다. 기술 경로는 ProjectStructure.md, 단계 범위는 RoadMap.md를 따른다.

## 1. 단계·Git·환경

- 기존 해상 수면·파도 구현 및 검토 자료를 제거했다. W0은 사용자 통과 확인. W1~W3의 검토를 마쳤으며 W3는 Complete다. 사용자가 시각 문제 해결을 확인했고 검토 환경에서 수치와 성능을 검증했다. 제품 통합과 제품 화면 수용은 W7 범위다.

- P0~P4 Complete. P4의 이동·공간 전환은 사용자 통과 확인 및 명시 완료 승인. 오브젝트 배치·Scene 디자인은 P4 평가 제외였으며 시각 승인으로 확대하지 않는다.
- main / origin https://github.com/K-Jung-H/Great-Voyage.git. 현재 완료 기준은 P5 - 1_4_2 Complete(이전 공유 HEAD=b9190f9, P5 - 1_3 Complete)이며 해당 범위는 a6d3dfd로 main/origin에 공유되었다. Sea·Wave 설계 커밋 5b4ead8은 main/origin에 공유되었다. W3 완료 스냅샷의 커밋 제목은 W3 - Complete BaseWaterShader다. P5 전체 완료는 아니다. 기존 Galmuri11 SDF.asset·Galmuri11World.asset·ProjectSettings.asset 변경은 문서 작업에서 변경하지 않고 기존 작업 트리에 보존한다.
- Unity6000.6.4f1 / Input System1.20.0 / URP17.6.0 / Test Framework1.8.0 / CLI beta15 / Pipeline0.9.1-exp.1. 신규 패키지·스킬·모델 설치나 화풍/생성 방식 변경 없음. P4 때 CLI 업데이트는 자동 승인 검토가 범위 밖 도구 변경으로 거절해 기존 CLI를 사용했다.

## 2. 현재 제품 동작·상태 소유권

- Game.unity의 단일 GameSessionState/플레이어/시간/HUD 및7개 WorldArea·12방향 연결을 유지한다. 현재 구역만 활성화하며 다른 구역의 정의는 파괴하지 않는다. P4 상태는 References/P4와 P4 Complete 커밋을 따른다.
- **NavigationState**가 함선 ID·논리 해상 X/Y·방향·항속·항해 ID/순번·거리를 소유한다. CharacterState의 선내 AreaId/X/Y와 별도다. 도메인/Application은 Unity를 참조하지 않으며 View에 규칙을 복제하지 않는다.
- 함선·갑판·시설·충돌체의 Transform은 해상 좌표로 이동/회전하지 않는다. NavigationView가 같은 논리 위치·방향을 지도 아이콘/방위 표시에 투영한다. 구형 수면 구동 연결은 제거했고 새 해상 수면 표시는 W1 격리 환경에서 검증했다. 지도·수면 표현이 논리 상태를 되쓰지 않는다. 기존 Repeat 바닥을 유지한다.
- NavigationSettings의 초기 항속0.002해리/게임 초=7.2kn, 선회율0.2도/게임 초는 조정 가능한 검증값이다. 하루는 실제600초이며 Navigation도 GameClock과 같은 게임 시간 간격을 사용한다. 일정 조타 구간은 해석적 적분으로 계산한다. 전체 게임/다른 플랫폼의 비트 단위 결정론을 검증한 것은 아니다.
- 선장실 아래 계단 E→상갑판. 방향타는(-3.5,0.5)의1×1 Cell, 접근점은(-3.5,-0.45)다. 접근 후 E로 최소 항해 시작·조타 진입, A/D·좌우 방향키로 조타, E/Esc로 조작 종료한다. 조타 중 캐릭터 이동을 차단하고 방향타 쪽을 바라본다. 종료하면 현재 방향으로 계속 전진하며 선내 이동으로 복귀한다. 선원 자동 조타는 없다.
- 메뉴·일지·전환의 중첩 정지는 항해와 캐릭터에 모두 적용하며 정지 시간을 사후 보충하지 않는다. 메뉴 버튼으로 조타 중 메뉴를 열었다 닫으면 조타로 복귀하고, Esc는 조타 종료 후 다음 입력에서 메뉴를 연다.
- 항해 중 선장실↔상갑판 이동은 가능하고 항해는 계속된다. 하선은 대상 선택과 실행 양쪽에서 차단한다. **자동 입항은2단계이므로1단계에서 출항한 후 항구 복귀·하선으로 순환을 끝내는 기능은 없다.** 최소 시작을 전체 출입항 완성으로 보지 않는다.
- 기존 일지 ChartPage 비율·Day 탭·Skeleton Frame을 유지하면서 현재 위치/방위, 함선 아이콘, 확대/축소/내 위치를 연결한다. 지도 배경·격자·아이콘이 같은 확대/중심 투영을 사용하고 원본 지도 비율을 유지한다. 일지를 열면 시간/항해도 정지하므로 닫혀 있는 동안의 위치 변화가 다시 열 때 반영된다. 범위 밖 아이콘은 거짓 위치로 고정하지 않고 숨김/안내한다.
- 지도 배경은 기존 ChartSurface 시각 자산이다.1단계는 위치 투영이며 배경의 섬·해안·항구를 실제 논리 영역으로 연결한 상태가 아니다. 논리 항구 범위·재입항·입항 결과는2단계다. 계획선·기록·메모·Day 선택은 FrameDisabled 유지.

## 3. 자산·공간·저장

- 신규 HelmView.prefab은 Root HelmStation/CellSize, CellArea/Visuals/Collision 형제 및 ApproachPoint다. Root/CellArea Scale1, Visuals0.75. 기존 공유1 unit GridSettings와 상갑판 PlacementSpace의 유효1×1 Cell을 사용한다. 구역을 숨겨도 Grid 정의는 유지하며 GridArea.enabled를 명시적으로 끈 경우에만 제외한다. 원본 사용자 OriginalTavernProps.png에서 표준 Sprite Rect로 추출했으며 새 이미지 생성/픽셀 편집 없음.
- 기존 선장실과4가구 Prefab·비율·접근점·충돌을 보존했다. PreservedCabinPrefabs.json으로5종 내용 보존 확인. 기존 책상2×1/침대 세로1×2, 의자·상자 Cell 미정(0,0), 선장실63유효 셀, 배치 드래그/스냅/Preview/Overlay 미구현을 유지한다.
- 저장 envelope는 **버전2**로 Navigation을 포함한다. 버전1은 읽을 때 메모리에서 정박 상태를 보완하고 파일을 자동 덮어쓰거나 초기화하지 않는다. v2에서 Navigation 누락은 오류다. 새 버전 저장을 구버전 앱이 읽는 역호환은 제공하지 않는다. SHA/tmp flush/원자 교체/.bak 복구는 유지한다.
- 정상 종료·승선·하선의 기존 SaveEvent를 유지한다. 항해 시작·지도 열기·매 프레임 저장을 추가하지 않았다. 정상 종료 시 항해 위치/방향/ID/거리를 보존하고 조타 입력 소유는 복원하지 않는다.2단계 출입항 저장 경계·일관성은 미구현이다.
- 자동 검증은 Temp/GreatVoyageP5Tests/<Guid> 등 격리 슬롯만 사용한다. 실제 사용자 저장 초기화/수정 없음. 제품 Scene 저장 슬롯은 P3Cabin이다. 기본 Galmuri11/원본 TTF는 유지하고 신규 표시는 동일 소스의 Galmuri11World를 쓴다.

## 4. 검증·수용 상태

- 2026-11-09 사용자 확정 Sea·Wave 계약을 Architecture/ProjectStructure/RoadMap/VisualStandards에 반영했다. W1에서 첨부 Skeleton을 Presentation/Ocean 및 Content/Art/Ocean에 배치하고 세션 시간과 해상 투영 경로를 추가했다.
- 원본은 References/OceanFramework/GreatVoyage_Ocean_Framework_Locked_Skeleton.zip으로 보존했다. 원본/복사본 SHA256은 6769c15e3a5057deac77114bbb0e1eae43a1e7e7ba0062ba2eff4ef5a739fa41로 일치한다. 압축 내부 지시문은 자동 작업 허가가 아니다.
- 확정: 유체 솔버 배제, Base/Delta/Final/Foam/Wave 책임, Navigation 단일 원본 및 계산용 선박 표현, 외부 시간·정지/복귀, 월드 정렬·정수 texel 이동·겹침 History 보존과 창 밖 정책의 분리.
- W1 Unity C# 컴파일 통과. Ocean EditMode 4/4 통과(CPU 보간 3, GPU BuildBaseFlow 경계 보간·동일 Seed 재현 1). Compute의 HLSL 예약어 point를 edgePoint로 수정했다. 격리 PlayMode 1/1 통과. OceanSessionPlayTests에서 합성 접수 형상으로 Base/Delta/Final/Foam 경로와 카메라의 BaseFlow 디버그 픽셀, 항해 좌표 투영·고정 갑판, 메뉴 정지 중 위상/포말 보존 및 복귀 후 시간 진행을 검증했다. 제품 상갑판 시각·성능과 사용자 수용은 미확인이다.
- Skeleton은 고정 창/단일 Polygon/미리보기 파형이다. GPU BaseFlow 외 출력, 창 이동 및 연속 항해 검증은 미실시다. W0은 사용자 통과 확인을 근거로 한다.

- W2: UpperDeck의 TravelHull 스프라이트에서 접수 외곽을 수동 추적한 10점 단일 경로 OceanStartingHull.prefab을 추가했다. 선수 돌출 장식은 접수 범위에서 제외했다. 계산용 선수 +Y, 제품 갑판의 화면상 선수 -X(180도), 접수 폭 약 4.33·길이 약 13.54 Unity 단위다. 비활성 PolygonCollider2D의 path를 읽고 선내 물리 접촉은 만들지 않는다. W2 검증용 Prefab이며 제품 Scene 연결은 후속 단계다.
- W2 Unity C# 컴파일 통과, OceanSessionPlayTests 2/2 통과. 실제 Prefab의 접수 점·범위·선수와 해상 위치/방향 투영 및 물리 격리를 확인했다. W1 카메라 출력·정지/복귀 회귀도 통과했다. 제품 화면에서의 접수 위치 수용, 선체 흐름 품질·GPU 성능은 미확인이다.

- W3: OceanSurface2D의 위치별 방향 투영을 월드 기준 큰 파도·교차 파도·잔물결 패턴 및 제한된 Flow 이동의 두 위상 교차 혼합으로 교체했다. Flow 상쇄점에서는 두 샘플 위치가 일치한다. 세션 Ocean 시간을 사용한다.
- W3 Unity C# 컴파일 및 OceanSessionPlayTests 2/2 통과. URP 카메라 GPU 출력에서 월드 X=1000의 반대 흐름 경계, 위상 되감기 전후 픽셀 연속성, 시간 진행에 따른 수면 색 변화 및 W1~W2 회귀를 확인했다. 제품 화면의 시각 수용·장시간 파형 및 GPU 성능은 미확인이다.

- W3 수동 검토: Assets/Game/Scenes/Review/W3OceanReview.unity를 단독으로 열고 Play한다. 중앙에서 좌우 반대 흐름을 만나는 기본 파도를 확인한다. W3OceanSurface 머티리얼의 Debug Mode 0은 수면, 1은 BaseFlow다. 검토 씬은 빌드 목록에 넣지 않았다.

## 5. 미구현·다음 작업

- 다음 작업: W4 선수 분기·선측 흐름 및 BaseFlow 불변 검증. 제품 시각 수용은 별도 기록한다. W0은 사용자 통과 확인을 반영했으며 재실행하지 않았다.
- 구현 전 구체화: 해리/계산 공간 축척과 시간 단위, 창 밖 이력의 수명·여유 영역/청크 보존 범위 및 새 노출 영역 초기화/복원, 로드 시 시각 이력 정책. 구체 수치·운영 선택은 미정이며 창 밖 이력 복원이나 영구 보존을 구현했다고 간주하지 않는다.

- 2단계: 논리 항구 영역·이탈 후 재입항 재활성화·자동 입항·항해1회 확정/중복 방지·출입항 저장/항구 공간 연결/시작 항구 통합. 아직 구현하지 않았다.
- 상점·게시판·주점/조선소 서비스, 선원·업무·계약, 자동 조타·경로 기록·수면·하루 경과 SaveEvent, 사건·전투·미니게임·모바일 상세 입력은 후속 범위다. 비표시 층의 선원 업무 시뮬레이션이나 전체 함선 모델은 구현했다고 주장하지 않는다.

- W3 시각 재작업: 기존 셰이더의 HLSL 예약어 line 오류 및 검토 머티리얼 Debug Mode 2 상태를 발견했다. 새 StylizedOcean.shader를 작성해 검토 씬에 연결하고 Debug Mode 0으로 저장했다. 실제 카메라 출력은 References/P5/W3/StylizedOceanReview.png다. 기존 W3 기술 통과를 최종 시각 수용으로 해석하지 않는다. 이후 사용자가 수면 경계와 빛 무늬 연속성 문제의 해결을 확인했다.

- 2026-11-10 재개 검증: StylizedOcean의 Noise와 방향 패치 보간을 5차 보간으로 바꿔 경계 곡률을 연속화했다. 기존 시간 연속성 허용 오차 0.005를 유지하여 OceanSessionPlayTests 2/2 통과했다. 셰이더 임포트 오류 0. 실제 W3OceanReview의 카메라 출력과 약 15초 후 변화는 References/P5/W3/StylizedOceanReview.png 및 StylizedOceanLater.png다.
- 현재 검토 머티리얼의 Connected light patterns=0, Surface relief=0.397을 확인했다. 이 설정에서 실제로 보이는 것은 연결 무늬가 꺼진 표면 반사다. 사용자 설정을 유지했다. 레퍼런스에 대한 시각 수용은 계속 미확인이다.

- W3 시각 문제는 사용자 해결 확인. 32초 좌표 왕복 잔차는 ARGBHalf에서 0.07526, RGFloat에서 0.0000000373으로 측정됐다. Connected Light는 RGFloat UAV 지원 시 해당 포맷을 사용하며 동일한 허용치 0.04로 GPU 회귀를 통과했다. ARGBHalf 대체 경로에는 기존 정밀도 한계가 남는다.
- W3 셰이더 최적화: 중심 표면 좌표/RG 기여 샘플 재사용, 렌더 Normal 이중 정규화 제거, 디버그용 FinalFlow 읽기를 디버그 분기로 이동. Connected Light 강도 0 및 포말 밀도 0.12 이하에서 관련 패턴 계산을 생략한다. 셰이더 강제 임포트 메시지 0. GPU 시간 개선 수치는 미측정이며 40회 기본 Height 평가는 유지한다.

- W3 파형 미분 최적화. Noise, Warp, 패치 혼합을 해석적으로 미분하고, GPU 텍스처 입력의 기울기는 기존 간격의 차분으로 계산한다. 포말 표시 영역의 곡률은 기존 높이 차분을 사용한다. OCEAN_REFERENCE_SLOPE 키워드는 기존 경로의 A/B 비교용이다. MX150 960×540 블릿과 1픽셀 동기 읽기, 기본 포말 입력에서 기존 21.436/18.529ms, 최적화 4.685/4.520ms 중앙값을 기록했다. 순수 GPU 타임스탬프와 제품 프레임 비용은 미측정이다.

- W3 Complete. 검토 환경 최종 검증 통과. 임시 GPU 계측 셰이더로 256×144 출력 내부 35,280픽셀을 비교했다. 해석적 미분과 기존 중앙 차분의 Height 최대 차이 0, 공통 Normal 각도 RMS 0.388°, P99 1.305°, 최대 3.549°. 합성 R 입력의 중앙 높이 증가 0.285625, X 기울기 기대 +0.005 대비 최대 오차 0.0001173. G +0.25의 포말 활성 Crest 반영 오차 5.96e-8. 사용자 시각 확인, 성능 A/B 및 RGFloat 장치에서의 Ocean 회귀 7/7과 함께 W3 검토 기준을 충족했다. ARGBHalf 대체 경로의 좌표 정밀도와 제품 GPU 프레임 시간은 별도 검증 대상이다.
