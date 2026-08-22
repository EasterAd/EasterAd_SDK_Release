# EasterAd

Unity용 EasterAd SDK입니다.

English README는 [여기](README.md)에서 확인할 수 있습니다.

## 설치

### Git 저장소에서

1. Unity Package Manager를 엽니다 (`Window` -> `Package Manager`).
2. `+` 버튼을 클릭하고 `Add package from git URL...`을 선택합니다.
3. EasterAd에서 제공한 패키지 Git URL을 입력합니다.
4. `Add` 버튼을 클릭합니다.
5. 패키지가 프로젝트에 추가됩니다.
6. 이제 프로젝트에서 EasterAd SDK를 사용할 수 있습니다.
7. 패키지를 업데이트하려면 Package Manager에서 `Update to` 버튼을 클릭하면 됩니다.

### 수동

1. EasterAd에서 제공한 최신 패키지를 다운로드합니다.
2. Unity 프로젝트에 패키지를 임포트합니다.
3. 패키지가 프로젝트에 추가됩니다.
4. 이제 프로젝트에서 EasterAd SDK를 사용할 수 있습니다.

## 사용법

EasterAd SDK 사용 방법에 대한 정보는 [문서](https://dev.easterad.com/assets/docs/sdk/ko/index.html)를 참조해주세요.

## Dashboard

`Window > EasterAd`의 Settings 폴더에서 EasterAd Developer Dashboard API 키를 입력하고 프로젝트 조직/게임을 선택할 수 있습니다. Inventory 폴더는 대시보드 인벤토리와 씬 배치를 통합해서 보여줍니다. 새 인벤토리는 대시보드에 등록된 뒤 기본 `Plane`으로 씬에 바로 배치되고, 현재 씬에 배치된 인벤토리는 Active로 표시되며, 씬에 배치되지 않은 인벤토리는 Archive로 표시됩니다. 기존 인벤토리는 Archive에서 재사용해 해당 Ad Unit ID의 `Plane` 또는 `CanvasItem` 배치 1개를 만들 수 있습니다. 인벤토리별 배치 설정은 row의 설정 아이콘을 열 때만 표시되고, 씬 배치 제거는 그 설정 패널에서 수행합니다.

대시보드 데이터는 EasterAd 창을 열거나 폴더 전환, row 설정 열기, 대시보드 프로젝트 변경, 에러 배너의 Refresh 버튼처럼 사용자가 조작할 때 조용히 최신 상태로 갱신됩니다. 정상 갱신 과정은 UI에 표시하지 않습니다. 대시보드 갱신이 5회 연속 실패하면 갱신이 멈추고 에러 배너가 표시됩니다.

Settings의 Managed Scenes는 Ad Unit ID 중복 검사를 수행할 씬 범위를 정의합니다. placement를 생성하거나 Archive에서 복구하면 해당 씬이 자동으로 목록에 추가되고, 스캔 시 EasterAd placement가 하나도 없는 managed scene은 목록에서 제거됩니다. Managed Scenes 안의 중복 Ad Unit ID는 대시보드 경고와 Play Mode 진입 전 콘솔 에러로 표시되지만 실행은 막지 않습니다.

사이드바 하단의 Menu Settings 버튼에서 사전 정의된 대시보드 컴포넌트를 한 줄짜리 드래그앤드롭 메뉴 리스트로 배치할 수 있습니다. 폴더 row는 기호 없이 편집 가능한 이름으로 보이고, 컴포넌트 row는 들여쓰기된 `↳`로 구분됩니다. 드래그 중인 컴포넌트는 목록에서 빠진 뒤 떠 있는 preview로만 남고, row 사이에 가져가면 실제 spacer가 열리며, row 중앙에 드롭하면 두 컴포넌트를 담은 새 폴더가 만들어집니다. 알림과 경고는 현재 폴더 제목 바로 아래에 표시된 뒤 폴더 컴포넌트가 이어집니다.

Dashboard API 키와 선택한 대시보드 프로젝트는 사용자별 Unity `EditorPrefs`에만 저장되며 `StreamingAssets/EasterAd_Config.txt`에는 저장되지 않습니다. `EasterAd_Config.txt`에는 기존처럼 Game ID, SDK Key, 로그 설정, custom device info만 저장됩니다.

## ETA에서 마이그레이션

EasterAd 1.4에서는 공개 namespace와 assembly 이름이 `ETA`에서 `EasterAd`로 변경되었습니다. 새 코드는 `using EasterAd;`를 사용해야 합니다.

기존 프로젝트는 [ETA to EasterAd Migration Guide](MIGRATION_ETA_TO_EASTERAD.md)를 확인해주세요. 마이그레이션을 안전하게 하기 위해 한시적인 `ETA` namespace bridge와 `ETA_Config.txt` fallback을 포함했지만, 새 코드에서는 사용하지 않는 것을 권장합니다.

## 플랫폼별 광고 소유권

> **2.0.0 런타임 동작의 호환성 단절:** Android/iOS 빌드는 더 이상 EasterAd 자체 session, 광고 request, texture, impression, refresh, presentation 경로를 사용하지 않습니다. Unity WebGL은 브라우저 serving 계약을 구현하고 검증하기 전까지 fail-closed 비지원입니다.

| 실제 런타임 플랫폼 | 광고 소유자 | `Plane` / `CanvasItem` 동작 |
| --- | --- | --- |
| Android, iOS | 호스트가 제공한 `IEasterAdMobileAdProvider` | `Item.Load()`가 provider의 단일 load-and-show operation에 위임됩니다. demand, creative, presentation, refresh, impression, click reporting은 provider가 모두 소유합니다. |
| Windows, macOS, Linux 및 그 밖의 지원되는 비-WebGL 비모바일 플랫폼 | EasterAd | 기존 EasterAd session, request, texture, 게임 내 `Plane`/`CanvasItem`, refresh, impression 흐름이 유지됩니다. |
| Unity WebGL | 없음 | `Item.Load()`는 `Disabled`, `Skipped / UnsupportedPlatform`으로 종료됩니다. EasterAd session, 광고 request, provider 호출, texture 변경, impression, refresh는 모두 0이며 게임 내 지면은 숨긴 상태를 유지합니다. |

라우팅은 항상 Unity가 보고한 실제 런타임 플랫폼을 사용합니다. Custom Platform 설정은 telemetry metadata일 뿐이며 모바일 라우팅을 강제하거나 우회할 수 없습니다.

현재 first-party session transport는 Unity WebGL에서 재사용할 수 없습니다. Unity Web 플랫폼은 .NET `System.Net` 네트워킹을 지원하지 않고 `Cookie` 같은 제한 헤더는 브라우저가 소유합니다. 따라서 WebGL 지원에는 backend CORS/SameSite/browser-cookie session 계약, UnityWebRequest/Fetch transport, 실제 브라우저 end-to-end gate가 모두 필요합니다. 세 조건이 갖춰지기 전에는 Android/iOS provider로 fallback하거나 오래된 게임 내 지면을 보이지 않고 fail-closed로 종료합니다. Unity 공식 [Web networking](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-networking.html) 및 [UnityWebRequest header 제한](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.SetRequestHeader.html)을 참고하세요.

지원되는 first-party 플랫폼에서 Unity는 정확한 submodule 커밋 `5748c8765ec8e3925864062741647828d9481391`에서 생성된 serving 계약을 사용합니다. 기본 transport는 각 method와 path를 `ServingHttpBindings`에서 가져오고 protobuf request와 response body를 `application/proto`로 전송합니다. 세션 생성은 `appId`, `sdkVersion`, 비어 있지 않은 bundle과 version을 가진 `NativeApp` 하나, typed AdCOM device 문맥을 보냅니다. 비어 있지 않은 `sdkKey`는 live 세션에 포함하고, 빈 Key는 test 세션을 위해 optional protobuf 필드 자체를 생략합니다. 재초기화는 대체 세션을 먼저 생성하고 채택한 뒤 이전 세션을 삭제하며, shutdown은 활성 세션을 삭제합니다. 현재 계약에는 session update operation이 없습니다.

현재 serving protobuf에는 privacy, consent, child-directed, personalized-ad, region, category-list 필드가 없습니다. `AdRequestsAllowed`, offline mode, kill switch는 request 또는 provider 호출을 억제하는 SDK 로컬 gate입니다. 나머지 privacy/category 값은 로컬 정책 상태로만 유지되며 EasterAd 또는 외부 모바일 provider에 자동 전송되지 않습니다. `SetAdCategoryPolicy` 자체도 server-side 또는 vendor-side 필터링을 시행하지 않습니다. Android/iOS에서는 호스트가 선택한 vendor의 privacy/category 제어를 별도로 적용해야 합니다.

Android/iOS에서는 호스트 앱이 vendor 광고 SDK를 초기화하고 첫 `Item.Load()` 전에(`loadOnStart` Item이 로드되기 전에도) provider 하나를 등록해야 합니다.

```csharp
IEasterAdMobileAdProvider provider = new MyMobileAdProvider();
EasterAdSdk.RegisterMobileAdProvider(provider);

// 기존 placement 코드는 모든 플랫폼에서 동일합니다.
canvasItem.Load();
```

Vendor 초기화가 비동기라면 `loadOnStart=false`로 두고 초기화와 등록 성공을 기다린 다음 `Load()`를 명시적으로 호출합니다.

Provider 계약은 특정 vendor에 종속되지 않습니다.

```csharp
IDisposable LoadAndShow(
    EasterAdMobileAdRequest request,
    Action<EasterAdMobileAdResult> completion);
```

Request에는 EasterAd logical placement key와 `Plane`/`Canvas` surface hint가 들어갑니다. Provider는 이 값을 vendor placement 또는 ad-unit identifier로 매핑하고 `EasterAdMobileAdResult.Displayed()`, `.NoFill()`, `.Failed(EasterAdMobileAdFailure)` 중 하나의 typed terminal result만 전달합니다. 취소되지 않은 lifecycle은 전체 load-and-show operation이 끝난 뒤 `completion`을 정확히 한 번 호출해야 합니다. EasterAd가 handle을 dispose해 취소한 뒤에는 호출하지 않아야 하며, 늦거나 중복된 callback은 무시됩니다. `Displayed`는 광고가 실제 표시된 뒤 정상 완료 또는 닫힘까지 끝났다는 뜻이며, 단순 load/표시 시작이나 reward 지급 명령이 아닙니다. Provider/vendor 초기화와 최종 종료는 호스트가 소유합니다. EasterAd는 terminal result 처리 뒤 request별 `IDisposable`을 dispose합니다.

Operation handle의 `Dispose()`는 멱등이고, 취소 소유권 이전이 끝날 때까지 동기적으로 완료되며, 예외를 던지지 않아야 합니다. EasterAd는 `LoadAndShow`가 반환 중인 동안 global presentation/provider lease를 유지하고, 취소된 뒤 반환된 handle을 먼저 dispose한 후 lease를 해제합니다. 취소 cleanup이 예외를 던지면 cleanup 상태를 확신할 수 없으므로 해당 process가 종료될 때까지 모바일 presentation을 의도적으로 fail-closed로 고정하고 provider 등록 해제와 새 SDK client의 겹치는 호출을 차단합니다. 이는 provider를 수정하고 앱을 재시작해야 하는 연동 오류이며 runtime reset API는 없습니다.

같은 provider 인스턴스를 다시 등록하면 no-op입니다. 다른 provider로의 교체 또는 operation이 활성화된 동안의 등록 해제는 거부됩니다. Operation이 끝나기를 기다리거나 해당 Item을 제거/파괴하고 pending callback 처리가 끝난 안전한 앱 lifecycle 지점에서 `EasterAdSdk.UnregisterMobileAdProvider(provider)`를 호출한 뒤, 호스트가 provider를 종료해야 합니다. Component 또는 GameObject를 단순 비활성화하는 것은 명시적인 operation cancel API가 아닙니다.

개인정보 정책, offline mode, 광고 요청 비활성화, kill switch는 provider 호출 전에 평가됩니다. Provider 미등록, 잘못된 request, provider 오류, no-fill은 fail-closed로 처리됩니다. EasterAd는 모바일 내부 session/request를 만들거나 EasterAd texture를 적용하거나 자체 impression을 기록하거나 게임 내 광고로 fallback하지 않습니다. Provider callback은 queue를 거쳐 Unity main thread에서 반영됩니다.

Android/iOS에서는 `Item.allowImpression`, `interactable`, `enableRefresh`, `refreshTime`, `hideDuringCapture`가 외부 SDK UI, measurement, click, refresh, capture 동작을 제어하지 않습니다. `EasterAdCaptureScope`도 vendor가 소유한 overlay를 숨길 수 없습니다. 해당 정책은 선택한 vendor API를 사용해 provider/host에서 구현해야 합니다. 이 필드들은 지원되는 EasterAd renderer 플랫폼에서만 기존 의미를 유지합니다.

EasterAd는 특정 모바일 광고 SDK를 내장하지 않습니다. 따라서 Android/iOS production 검증에는 선택한 vendor SDK, 해당 EasterAd provider module, test credential/placement, 실제 기기가 필요합니다. H5의 DOM/VAST presentation과 browser lifecycle 코드는 Unity 모바일 경로에 포함되지 않습니다.

## 런타임 기능

- `EasterAdSdk`는 SDK 초기화, 개인정보 설정, 진단 이벤트, 플랫폼 라우팅과 지원되는 비-WebGL 비모바일 플랫폼의 세션 생명주기 및 impression 측정용 target camera를 관리합니다.
- 지원되는 비-WebGL 비모바일 플랫폼에서 `Plane`은 3D 월드 공간 광고 지면을 지원하고, `CanvasItem`은 RectTransform/UI 광고 지면을 지원합니다.
- `Item`은 런타임 `adUnitId` 설정을 위한 lazy/manual initialization을 지원합니다. SDK 초기화 전에 생성된 Item은 pending lifecycle queue를 통해 초기화됩니다.
- 광고 로드는 loaded, no-fill, policy-disabled, unsupported-content, retryable network, retryable image, invalid-response 결과로 분류됩니다.
- 지원되는 게임 내 플랫폼의 impression 측정은 가능한 경우 GPU AdSegmentation을 사용하고, GPU 측정을 사용할 수 없으면 bounds 기반 visibility fallback을 사용합니다.
- URP GPU visibility에는 AdSegmentation renderer feature가 필요합니다. URP가 없어도 기본 광고 로드는 계속 동작합니다.

## 운영 참고사항

- `NoFill`, policy-disabled, unsupported-content, invalid server response는 retry하지 않습니다.
- retryable network 및 image 실패만 제한적으로 retry합니다.
- Android/iOS provider 오류와 no-fill은 EasterAd 게임 내 renderer를 통한 retry나 fallback으로 이어지지 않습니다.
- 지원되는 게임 내 플랫폼의 renderer는 URL userinfo가 없는 absolute HTTP(S) media/click URL만 허용합니다. Relative URL과 `javascript:`, `data:`, `file:`, userinfo URL은 거부하며 creative image download는 redirect를 따라가지 않습니다. Click navigation은 host platform/browser에 넘기므로 최초 click URL 이후 redirect chain의 검증과 통제는 publisher 책임입니다. `UnityWebRequest`의 플랫폼 cookie 저장소는 Unity가 관리하므로 creative media는 앱 또는 vendor 인증 cookie를 공유하지 않는 전용 CDN origin에서 제공해야 합니다.
- 게임 내 creative는 PNG/JPEG raster image만 지원하고, image request 10초, encoded 8 MiB, 한 변 8,192 pixels, decoded 16,777,216 pixels 제한을 적용합니다. Unity decode 전에 PNG/JPEG encoded header에서 dimensions를 검증하며, invalid/oversized creative는 retry하지 않습니다. HTML, VAST, SVG, GIF, unknown MIME은 unsupported/no-fill이며 Unity에 H5/WebView fallback을 추가하지 않습니다.
- GPU AdSegmentation은 최대 255개의 등록된 광고 오브젝트를 지원합니다.
- Bounds fallback은 실제 occlusion을 측정하지 않습니다. GPU pixel counting과 동일한 정확도가 아니라 가용성 fallback입니다.
