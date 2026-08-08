# ETA to EasterAd Migration Guide

## English

EasterAd 1.4 renames the public SDK namespace and assemblies from `ETA` to `EasterAd`.

### What Changed

- `using ETA;` becomes `using EasterAd;`
- `ETA_Implementation` becomes `EasterAd_Implementation`
- `ETA_Dependencies` becomes `EasterAd_Dependencies`
- `ETA_Config.txt` becomes `EasterAd_Config.txt`
- `ETA_Axes.txt` becomes `EasterAd_Axes.txt`

### Recommended Migration

1. Replace `using ETA;` with `using EasterAd;`.
2. Replace any advanced/internal references to `ETA_Implementation` or `ETA_Dependencies` with the new names.
3. Open `Window > EasterAd` and save settings once. This writes `StreamingAssets/EasterAd_Config.txt`.
4. Remove the old `StreamingAssets/ETA_Config.txt` after confirming the new config works.
5. Regenerate or reimport the package so asmdefs reference `EasterAd_Implementation.dll` and `EasterAd_Dependencies.dll`.

### Temporary Compatibility

The package includes a temporary `ETA` namespace bridge for source compatibility. It is intended only to keep common source references compiling while you migrate. New code should use `EasterAd`.

The runtime still reads `ETA_Config.txt` when `EasterAd_Config.txt` is absent, but saving from the EasterAd editor writes the new file name.

## 한국어

EasterAd 1.4에서는 공개 SDK 네임스페이스와 어셈블리 이름이 `ETA`에서 `EasterAd`로 변경됩니다.

### 변경된 이름

- `using ETA;`는 `using EasterAd;`로 변경합니다.
- `ETA_Implementation`은 `EasterAd_Implementation`으로 변경합니다.
- `ETA_Dependencies`는 `EasterAd_Dependencies`로 변경합니다.
- `ETA_Config.txt`는 `EasterAd_Config.txt`로 변경합니다.
- `ETA_Axes.txt`는 `EasterAd_Axes.txt`로 변경합니다.

### 권장 마이그레이션

1. `using ETA;`를 `using EasterAd;`로 바꿉니다.
2. 내부 확장 코드에서 `ETA_Implementation`, `ETA_Dependencies`를 직접 참조했다면 새 이름으로 바꿉니다.
3. Unity에서 `Window > EasterAd`를 열고 설정을 한 번 저장합니다. 그러면 `StreamingAssets/EasterAd_Config.txt`가 생성됩니다.
4. 새 설정으로 동작을 확인한 뒤 기존 `StreamingAssets/ETA_Config.txt`를 제거합니다.
5. 패키지를 다시 생성하거나 재임포트해 asmdef가 `EasterAd_Implementation.dll`, `EasterAd_Dependencies.dll`을 참조하도록 합니다.

### 한시적 호환

패키지는 소스 호환을 위해 한시적인 `ETA` namespace bridge를 포함합니다. 이는 기존 소스가 migration 중에도 컴파일되도록 돕는 용도입니다. 새 코드는 `EasterAd` namespace를 사용해야 합니다.

런타임은 `EasterAd_Config.txt`가 없으면 기존 `ETA_Config.txt`를 fallback으로 읽습니다. 다만 EasterAd editor에서 저장하면 새 파일명으로 기록됩니다.
