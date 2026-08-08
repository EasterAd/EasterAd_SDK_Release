# EasterAd

This is the EasterAd SDK for Unity.

한글 README는 [여기](README.ko.md)에서 확인할 수 있습니다.

## Installation

### From Git Repository

1. Open the Unity Package Manager (`Window` -> `Package Manager`).
2. Click on the `+` button and select `Add package from git URL...`.
3. Enter the package Git URL provided by EasterAd.
4. Click on the `Add` button.
5. The package will be added to your project.
6. You can now use the EasterAd SDK in your project.
7. If you want to update the package, you can do so by clicking on the `Update to` button in the Package Manager.

### Manual

1. Download the latest package provided by EasterAd.
2. Import the package into your Unity project.
3. The package will be added to your project.
4. You can now use the EasterAd SDK in your project.

## Usage

For information on how to use the EasterAd SDK, please refer to the [documentation](https://dev.easterad.com/assets/docs/sdk/en/index.html).

## Dashboard

In `Window > EasterAd`, use the Settings folder to connect Unity Editor to the EasterAd Developer Dashboard with a dashboard API key and choose the project organization/game. The Inventory folder combines dashboard inventory with scene placements: new inventory is registered on the dashboard and placed in the scene as a default `Plane`, inventory placed in the current scene is shown as Active, and inventory not placed in the scene is shown in Archive. Reuse existing inventory from Archive to create one `Plane` or `CanvasItem` placement for that Ad Unit ID. Per-inventory placement settings stay hidden until the row's settings icon is opened, and placement removal is handled from that settings panel.

Dashboard data updates silently when the EasterAd window opens and when you change folders, open row settings, change the dashboard project, or use the error banner's Refresh button. Normal update activity is hidden from the UI; after 5 consecutive update failures, dashboard updates pause and show an error banner.

Managed Scenes in Settings define the scene scope used for duplicate Ad Unit ID checks. Creating or restoring a placement automatically adds its scene to the list; scans remove managed scenes that no longer contain EasterAd placements. Duplicate Ad Unit IDs in managed scenes are shown as dashboard warnings and logged before Play Mode, but they do not block execution.

Use the sidebar's bottom Menu Settings button to arrange predefined dashboard components in a single drag-and-drop menu list. Folder rows use plain editable names, while component rows are indented with `↳`; a dragged component leaves the list and stays as a floating preview, hovering between rows opens a real spacer, and dropping in a component row center creates a new folder with both. Alerts and warnings are shown directly under the active folder title before folder components.

The dashboard API key and selected dashboard project are stored only in per-user Unity `EditorPrefs`; they are not written to `StreamingAssets/EasterAd_Config.txt`. The runtime config file continues to store only the Game ID, SDK Key, log settings, and custom device info.

## Migration From ETA

EasterAd 1.4 renamed the public namespace and assemblies from `ETA` to `EasterAd`. New code should use `using EasterAd;`.

For existing projects, see [ETA to EasterAd Migration Guide](MIGRATION_ETA_TO_EASTERAD.md). A temporary `ETA` namespace bridge and `ETA_Config.txt` fallback are included to make migration safer, but they should not be used for new code.

## Runtime Features

- `EasterAdSdk` owns SDK initialization, session lifecycle, privacy settings, diagnostics, and the target camera used for impression measurement.
- `Plane` supports 3D world-space ad surfaces, and `CanvasItem` supports RectTransform/UI ad surfaces.
- `Item` supports lazy/manual initialization for runtime `adUnitId` assignment. Items created before the SDK finishes initialization are initialized through a pending lifecycle queue.
- Ad loading classifies loaded, no-fill, policy-disabled, unsupported-content, retryable network, retryable image, and invalid-response outcomes.
- Impression measurement uses GPU AdSegmentation when available and falls back to bounds-based visibility when GPU measurement is unavailable.
- URP GPU visibility requires the AdSegmentation renderer feature. Basic ad loading continues to work without URP.

## Operational Notes

- `NoFill`, policy-disabled, unsupported-content, and invalid server responses are not retried.
- Only retryable network and image failures are scheduled for limited retry.
- GPU AdSegmentation supports up to 255 registered ad objects.
- Bounds fallback does not measure real occlusion; it is an availability fallback, not an accuracy equivalent to GPU pixel counting.
