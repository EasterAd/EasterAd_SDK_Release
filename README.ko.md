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

## 런타임 기능

- `EasterAdSdk`는 SDK 초기화, 세션 생명주기, 개인정보 설정, 진단 이벤트, impression 측정용 target camera를 관리합니다.
- `Plane`은 3D 월드 공간 광고 지면을 지원하고, `CanvasItem`은 RectTransform/UI 광고 지면을 지원합니다.
- `Item`은 런타임 `adUnitId` 설정을 위한 lazy/manual initialization을 지원합니다. SDK 초기화 전에 생성된 Item은 pending lifecycle queue를 통해 초기화됩니다.
- 광고 로드는 loaded, no-fill, policy-disabled, unsupported-content, retryable network, retryable image, invalid-response 결과로 분류됩니다.
- Impression 측정은 가능한 경우 GPU AdSegmentation을 사용하고, GPU 측정을 사용할 수 없으면 bounds 기반 visibility fallback을 사용합니다.
- URP GPU visibility에는 AdSegmentation renderer feature가 필요합니다. URP가 없어도 기본 광고 로드는 계속 동작합니다.

## 운영 참고사항

- `NoFill`, policy-disabled, unsupported-content, invalid server response는 retry하지 않습니다.
- retryable network 및 image 실패만 제한적으로 retry합니다.
- GPU AdSegmentation은 최대 255개의 등록된 광고 오브젝트를 지원합니다.
- Bounds fallback은 실제 occlusion을 측정하지 않습니다. GPU pixel counting과 동일한 정확도가 아니라 가용성 fallback입니다.
