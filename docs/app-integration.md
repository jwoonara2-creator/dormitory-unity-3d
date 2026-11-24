# 앱(Flutter) ↔ Unity 연결

Unity를 **WebGL로 빌드**해서 Flutter 앱의 시설 탭(가운데 탭)에 넣습니다.
웹(Chrome/Edge)은 iframe, 휴대폰(Android/iOS)은 WebView로 같은 페이지를 띄웁니다.

## 메시지 약속
메시지는 문자열 한 줄: `이름` 또는 `이름:값`. 앱 쪽 정의는 `lib/features/facility/data/unity_channel.dart`, Unity 쪽은 `Assets/Scripts/AppBridge/AppBridge.cs`. **바꿀 때는 양쪽을 같이 고칩니다.**

| 방향 | 메시지 | 뜻 |
|---|---|---|
| Unity → 앱 | `ready` | 첫 씬이 떴다. 이 뒤부터 앱이 명령을 보낸다 (앱은 `ready`가 없어도 20초 뒤 층 선택을 연다) |
| Unity → 앱 | `room:1person` / `room:2person` / `room:accessible` | 방 화면에 들어왔다 (씬이 로드될 때 자동 전송) |
| Unity → 앱 | `hotspot:<id>` | 3D에서 시설(핫스팟)을 눌렀다. 예) `hotspot:laundry_3f`. **id 목록은 아직 없음** — 정해지면 앱의 `_onUnityMessage`에서 화면 이동을 연결 |
| 앱 → Unity | `show_floor:<n>` | n층을 보여줘 (지금은 층 구분 없이 `FloorView`를 연다) |
| 앱 → Unity | `go_room:1person` / `2person` / `accessible` | 해당 방 화면을 열어줘 |

Unity에서 메시지를 보낼 때: `AppBridge.Send("hotspot:laundry_3f");` (WebGL이 아닌 곳에서는 콘솔 로그만 남김)

## 구성 파일
| 파일 | 역할 |
|---|---|
| `Assets/Scripts/AppBridge/AppBridge.cs` | 씬과 상관없이 자동 생성(`DontDestroyOnLoad`). 앱 명령 처리 + `ready`/`room:` 전송 |
| `Assets/Plugins/WebGL/AppBridge.jslib` | C# → 웹 페이지(`window.appSend`) |
| `Assets/WebGLTemplates/AppEmbed/index.html` | 호스팅 페이지: Unity 로딩, `window.unityCommand`, 앱과 postMessage |
| `Assets/Editor/WebGLBuild.cs` | WebGL 빌드(메뉴 `Tools > Dorm > Build WebGL (for app)`) — 7개 흐름 씬 등록, 템플릿/압축 설정 |
| `Assets/Resources/KoreanFont.ttf` | Noto Sans KR(SIL OFL). WebGL은 OS 글꼴이 없어서 이 파일을 먼저 쓴다 |

웹 ↔ 휴대폰 전달 방식 (페이지 안에서 자동으로 고름)
- 웹(iframe): Unity→앱 `parent.postMessage('{"type":"unity","msg":"…"}')`, 앱→Unity `postMessage('{"type":"app","msg":"…"}')`. 앱은 Unity 페이지의 origin과 같은 메시지만 받는다.
- 휴대폰(WebView): Unity→앱 `FlutterBridge.postMessage('…')`, 앱→Unity `window.unityCommand('…')`.

## 빌드 방법
1. Unity Hub에서 6000.3.22f1에 **Web Build Support** 모듈 설치(1회).
2. 메뉴 `Tools > Dorm > Build WebGL (for app)` → `Builds/WebGL`에 결과가 나온다. (명령줄: `Unity -batchmode -quit -projectPath <경로> -executeMethod WebGLBuild.BuildFromCommandLine -outDir <폴더>`)
3. 결과 폴더를 정적 서버/호스팅에 올리고, 앱을 `--dart-define=UNITY_URL=<주소>`로 빌드한다. 주소가 비어 있으면 시설 탭은 "Unity 3D 건물 뷰" 자리 표시만 보인다.

## 주의
- 압축은 **Disabled**로 빌드한다(호스팅이 따로 압축). Firebase Hosting 등은 서버가 압축해서 내려준다.
- 앱(웹)과 Unity를 **다른 주소(origin)**에 올려도 되지만, 앱이 받는 주소는 `UNITY_URL`의 origin이다.
- 처음 빌드는 오래 걸리고 결과가 큽니다. 휴대폰 메모리 한도 때문에 빌드 크기/조명/그림자는 줄여 가야 합니다.
