# 유니티 스크립트별 기능과 알고리즘

대상: `DomitoryProject/Assets/Scripts` (실행 중에 쓰이는 C# 스크립트).

## 앱 연결 (실행 중에 자동으로 만들어지는 스크립트)

### AppBridge.cs
- **기능**: Flutter 앱과 Unity 사이의 메시지 통로. 씬과 상관없이 하나만 살아 있음(DontDestroyOnLoad)
- **알고리즘**
  1. 게임 시작 전에 "AppBridge" 오브젝트를 자동 생성
  2. 씬이 로드될 때마다 AppUiFixes.Apply 호출 → 첫 씬이면 "ready", 방 씬이면 "room:타입"을 앱에 전송
  3. OnCommand(msg): "show_floor:N" → FloorView 로드 / "go_room:타입" → RoomSession 설정 후 방 씬 로드 / 그 외는 경고
  4. Send(msg): WebGL은 jslib(AppBridge_Send) → 웹 페이지(window.appSend)로 전달, 그 외는 콘솔 로그만
- 참고: 이름 규칙: 메시지는 "이름" 또는 "이름:값" 문자열 한 줄

### AppUiFixes.cs
- **기능**: 씬이 열릴 때마다 공통 보정: 한글 글꼴, 동그란 아이콘 버튼(뒤로가기 ←, 위에서 보기), 방 입장 안내
- **알고리즘**
  1. 모든 Text의 글꼴을 Resources/KoreanFont로 교체(WebGL은 OS 글꼴이 없어서)
  2. 층·복도·세탁/체력/세미나 "위에서 본 화면" → 왼쪽 위 동그란 ← 뒤로가기 버튼 생성(Main이 빌드에 있을 때만)
  3. 침실 3종 → RoomTopView.Create() / 세탁실·체력단련실·세미나실(+View) → 팀 카메라 아이콘 숨기고 같은 모양의 아이콘 버튼으로 두 씬을 서로 전환
  4. 버튼은 별도 캔버스(기준 720×1280, Expand)와 SafeArea 안에 배치 → 화면 크기에 비례 (지름 96, 누르면 어두워짐)
  5. 씬에 이미 있는 "화장실" / "방으로" 버튼은 StyleActionButton 으로 같은 어두운 알약 모양으로 맞춤
  6. 침실·시설 방에 들어올 때마다 FirstEntryHint.Show("터치로 화면 제어")
- 참고: 코드로 만든 UI라 씬 파일을 고치지 않음

### UiIcons.cs
- **기능**: 아이콘 이미지를 파일 없이 코드로 그려서 쓰는 도우미(뒤로 ←, 평면도, 눈, 물방울, 문)
- **알고리즘**
  1. 128×128 텍스처에 도형(선분·원·다각형)의 "거리"를 계산해 안티앨리어싱된 흰색 아이콘을 그림
  2. Disc(): 둥근 버튼 배경(9-slice 테두리 포함 → 알약 모양에도 재사용)
  3. Get(UiIcon): 한 번 그린 스프라이트는 캐시
- 참고: 이미지 에셋 없음 · 어떤 해상도에서도 선명

### FirstEntryHint.cs
- **기능**: 방에 들어갈 때마다 "터치로 화면 제어" 안내를 잠깐 보여줌
- **알고리즘**
  1. Show(message): 이전 안내를 지우고 새 캔버스·알약 생성(배경 투명도 40%)
  2. CanvasGroup 알파: 0.25초 나타남 → 1.4초 유지 → 0.4초 사라짐 → 오브젝트 삭제
  3. blocksRaycasts = false → 안내가 터치를 가로채지 않음
- 참고: 약 2초

### RoomTopView.cs
- **기능**: 침실에서 "위에서 보기" — 천장을 숨기고 카메라를 방 바로 위로 올림
- **알고리즘**
  1. 누르면 카메라 위치·회전·시야각을 저장, RoomLookCamera를 끄고 화장실/방 버튼 숨김
  2. 천장(Ceiling*, Hall Ceiling)과 창밖 배경판(Sky, Outside) 렌더러를 끔
  3. 남은 렌더러의 Bounds를 합쳐 방 크기 계산
  4. 필요한 거리 d = max(세로/tan(fov/2), 가로/(tan(fov/2)×화면비)) × 1.12 — 0°/90° 두 방향 중 d가 작은 쪽 선택
  5. 위치 = (중심x, 최고 y + d, 중심z), 회전 = Euler(90, yaw, 0)로 0.45초 동안 smoothstep 이동
  6. 다시 누르면 저장해 둔 자세로 돌아간 뒤 천장 복원, RoomLookCamera 켬
- 참고: 이동 중 다시 눌러도 무시(중복 방지)

## 방 화면 (침실·화장실, 세탁실 등 1인칭)

### RoomLookCamera.cs
- **기능**: 1인칭 둘러보기 카메라(로드뷰 느낌). 위치 고정, 드래그로 시선만 회전
- **알고리즘**
  1. 마우스 왼쪽 드래그 / 터치 한 손가락 드래그 → yaw·pitch 변경(손가락을 따라 화면이 움직이는 "잡아끌기" 방식)
  2. 한 화면 높이만큼 드래그하면 시야각(FOV)만큼 회전 → pitch는 -80~80°로 제한
  3. 휠 / 두 손가락 핀치 → FOV를 35~85°로 조절
  4. 지수 보간 k = 1 - exp(-s·dt)로 yaw·pitch·FOV·위치를 부드럽게 따라감
  5. MoveAlong(경로, yaw, pitch): 웨이포인트를 차례로 지나 이동(0.05m 안에 들어오면 다음 점)
  6. PointerGate로 UI 위에서 시작한 드래그는 무시, R 키로 초기화
- 참고: 카메라 위치는 Lerp, 회전은 매 프레임 Euler로 설정

### RoomViewController.cs
- **기능**: 방 화면의 버튼(화장실 / 방으로 / 돌아가기)과 제목
- **알고리즘**
  1. "화장실" → RoomLookCamera.MoveAlong(pathToBath…) 호출 + 버튼을 "방으로"로 교체(반대도 동일)
  2. "돌아가기": RoomSession.returnScene 으로 이동, 없으면 Main(빌드에 있을 때)
  3. 돌아가기 버튼이 씬에 없으면 코드로 생성(왼쪽 위 동그란 ← 아이콘), 씬에 있으면 같은 모양으로 바꿈(StyleAsRoundIcon)
  4. SetButtonsHidden(true): "위에서 보기" 동안 화장실/방 버튼 숨김, 끝나면 현재 위치(방/화장실)에 맞게 복원
- 참고: 방 안/화장실 상태를 기억(inBathNow)

### RoomSession.cs
- **기능**: 방 이름과 "돌아갈 씬"을 씬 사이에 넘기는 정적 변수 두 개
- **알고리즘**
  1. roomLabel("1인실"…) / returnScene: 씬을 바꾸기 전에 쓰고 다음 씬에서 읽음
  2. Main 씬이 로드되면 AppUiFixes.Apply 가 두 값을 비움 → Main 카드로 들어간 방의 ← 는 Main 으로 돌아감
- 참고: 데이터만 있는 static 클래스

### RoomEntryButtons.cs
- **기능**: 층·복도 화면 아래의 1인실 / 2인실 / 장애인실 버튼
- **알고리즘**
  1. Awake에서 EventSystem 보장 → 버튼 3개에 클릭 리스너 연결
  2. 클릭 시 RoomSession(label, returnScene = 현재 씬 이름)을 설정하고 해당 RoomView_* 씬 로드

## 층 화면·지도

### FloorViewCamera.cs
- **기능**: 층 탑뷰(천장 없는 평면) 궤도 카메라: 이동·기울기·회전·줌
- **알고리즘**
  1. 마우스: 왼쪽 드래그 = 이동(Pan), 오른쪽 드래그 = 기울기(tilt)·회전(yaw), 휠 = 줌 / 터치: 한 손가락 = 이동, 두 손가락 = 기울기·회전 + 핀치 줌
  2. 위치 = target + (회전된(-planUp×sin(tilt)) + up×cos(tilt)) × 거리, LookAt(target, planUp)
  3. Pan: 화면 위 = planUp, 오른쪽 = up×planUp 방향으로 target 이동(이동량은 거리에 비례), X·Z 범위로 제한(clamp)
  4. tilt 0~70°, 거리 8~70 제한, 지수 스무딩(smoothing)으로 부드럽게
  5. PointerGate로 UI 위 드래그 제외, ResetView()로 처음 상태
- 참고: 남/여 층 화면 모두 사용

### SceneChanger.cs
- **기능**: 버튼 OnClick에 연결하는 씬 이동 함수 모음(Main 화면 사진 카드 등)
- **알고리즘**
  1. GoToMain / GoToGym / GoToStudy / GoToLaundry / GoToRoom1·2·D / GoToFloor
  2. GymCameraView / StudyCameraView / LaundryCameraView: 각 방의 "위에서 본 씬" 이동
  3. 모두 SceneManager.LoadScene(이름)만 호출
- 참고: 팀 원본 스크립트

### HoldButtonController.cs
- **기능**: Main 지도의 카드를 누르는 동안 버튼이 커지고 GPS 핀이 통통 튐
- **알고리즘**
  1. IPointerDown: 버튼 크기 ×1.1, GPS 아이콘 바운스 코루틴 시작(기존 코루틴은 중단)
  2. 코루틴: 아이콘을 bounceHeight(15)만큼 위아래로 bounceSpeed(15)로 왕복(사인파)
  3. IPointerUp: 크기와 아이콘 위치를 원래대로 복원

### CarMove.cs · CloudSpawner.cs · CloudMover.cs
- **기능**: Main 지도의 장식 애니메이션(자동차, 구름)
- **알고리즘**
  1. CarMove: 매 프레임 앞으로 1.45m/s 이동, rotTime(2초)마다 Y축 90° 회전 → 네모 경로로 순환
  2. CloudSpawner: 4초마다 구름 프리팹 중 무작위 1개를 x=-4, y∈[1.8, 3]에 생성(Y 90° 회전)
  3. CloudMover: +x 방향으로 moveSpeed로 이동, x가 destroyX(4)를 넘으면 Destroy
- 참고: 무한 반복 · 오래된 구름은 자동 삭제

### GymCamera.cs
- **기능**: 내용이 없는 빈 스크립트(Start/Update만 있음, 현재 미사용)

## 공통 도우미 (여러 씬이 함께 사용)

### UiUtil.cs
- **기능**: UI 관련 도우미 함수 모음
- **알고리즘**
  1. EnsureEventSystem(): EventSystem이 없으면 만들고 InputSystemUIInputModule(새 Input System) 추가
  2. CreateKoreanFont(): Resources/KoreanFont가 있으면 사용, 없으면 OS 글꼴(맑은 고딕 등) 이름 목록으로 생성
  3. IsOverUi(화면좌표): EventSystem.RaycastAll로 해당 위치에 UI가 있는지 검사

### PointerGate (UiUtil.cs 안)
- **기능**: "누르기 시작한 곳이 UI였는지"를 기억해서 카메라 드래그와 버튼 클릭을 구분
- **알고리즘**
  1. 프레임당 한 번 갱신: 마우스/터치가 막 눌린 프레임에 IsOverUi로 blocked 결정
  2. 누르고 있는 동안 유지, 손을 떼면 해제 → 카메라 스크립트가 Blocked 이면 드래그 무시

### SafeAreaFitter.cs
- **기능**: 노치·시스템 바를 피해서 UI 영역 맞추기
- **알고리즘**
  1. Screen.safeArea를 화면 크기로 나눠 앵커(min/max)에 대입, 오프셋 0
  2. 화면 크기나 안전 영역이 바뀔 때만 다시 계산

### KoreanFont.cs
- **기능**: 캔버스 아래 모든 Text의 글꼴을 한글이 나오는 글꼴로 교체
- **알고리즘**
  1. Awake에서 UiUtil.CreateKoreanFont(32) → 자식 Text 전부에 적용
- 참고: AppUiFixes가 모든 씬에 한 번 더 적용(보완)
