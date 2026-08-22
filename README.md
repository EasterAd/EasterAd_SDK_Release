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

## Platform Ad Ownership

> **2.0.0 breaking runtime behavior:** Android and iOS builds no longer use EasterAd's built-in session, ad request, texture, impression, refresh, or presentation path. Unity WebGL is fail-closed and unsupported until a browser serving contract is implemented and verified.

| Actual runtime platform | Ad owner | `Plane` / `CanvasItem` behavior |
| --- | --- | --- |
| Android and iOS | A host-supplied `IEasterAdMobileAdProvider` | `Item.Load()` delegates one load-and-show operation to the provider. The provider owns demand, creative, presentation, refresh, impression, and click reporting. |
| Windows, macOS, Linux, and other supported non-WebGL non-mobile platforms | EasterAd | The existing EasterAd session, request, texture, in-game `Plane`/`CanvasItem`, refresh, and impression flow remains active. |
| Unity WebGL | None | `Item.Load()` ends as `Disabled` with `Skipped / UnsupportedPlatform`. EasterAd performs no session, ad request, provider call, texture mutation, impression, or refresh, and keeps the in-game surface hidden. |

Routing always uses the actual Unity runtime platform. The Custom Platform setting is telemetry metadata only and cannot force or bypass mobile routing.

Unity WebGL cannot reuse the current first-party session transport: Unity's Web platform does not support .NET `System.Net` networking, and the browser owns restricted headers such as `Cookie`. WebGL support therefore requires an explicit backend CORS/SameSite/browser-cookie session contract, a UnityWebRequest/Fetch transport, and a real browser end-to-end gate. Until all three exist, WebGL fails closed rather than falling back to the Android/iOS provider or displaying a stale in-game surface. See Unity's [Web networking](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-networking.html) and [UnityWebRequest header restrictions](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.SetRequestHeader.html).

On supported first-party platforms, Unity consumes generated serving contracts from the exact submodule commit `5748c8765ec8e3925864062741647828d9481391`. The built-in transport obtains each method and path from `ServingHttpBindings` and sends protobuf request and response bodies as `application/proto`. Session creation sends `appId`, `sdkVersion`, one `NativeApp` with a non-empty bundle and version, and typed AdCOM device context. A non-empty `sdkKey` is included for a live session; an empty key omits that optional protobuf field for a test session. Reinitialization first creates and adopts a replacement session and then deletes the previous session; shutdown deletes the active session. The current contract has no session-update operation.

The current serving protobuf does not contain privacy, consent, child-directed, personalized-ad, region, or category-list fields. `AdRequestsAllowed`, offline mode, and the kill switch are SDK-local gates that suppress the request or provider invocation. The other privacy and category values remain local policy state and are not automatically sent to EasterAd or an external mobile provider; `SetAdCategoryPolicy` does not itself enforce server-side or vendor-side filtering. The host must apply the selected vendor's privacy and category controls separately on Android/iOS.

On Android and iOS, the host application must initialize its vendor ad SDK and register one provider before the first `Item.Load()` (and before any `loadOnStart` Item can load):

```csharp
IEasterAdMobileAdProvider provider = new MyMobileAdProvider();
EasterAdSdk.RegisterMobileAdProvider(provider);

// Existing placement code remains the same on every platform.
canvasItem.Load();
```

If vendor initialization is asynchronous, set `loadOnStart=false`, wait for initialization and registration to succeed, and then call `Load()` explicitly.

The provider contract is deliberately vendor-neutral:

```csharp
IDisposable LoadAndShow(
    EasterAdMobileAdRequest request,
    Action<EasterAdMobileAdResult> completion);
```

The request contains an EasterAd logical placement key and a `Plane`/`Canvas` surface hint. The provider maps those values to its vendor placement or ad-unit identifier and reports one typed terminal result with `EasterAdMobileAdResult.Displayed()`, `.NoFill()`, or `.Failed(EasterAdMobileAdFailure)`. For a non-cancelled lifecycle, it invokes `completion` exactly once after the entire load-and-show operation ends. If EasterAd disposes the handle to cancel, the provider must not invoke it afterward; late or duplicate callbacks are ignored. `Displayed` means the ad was actually shown and then completed or closed normally; it does not mean merely loaded or started, and it is not an instruction to grant a reward. The host owns provider/vendor initialization and final shutdown. EasterAd disposes the per-request `IDisposable` after processing the terminal result.

The operation handle's `Dispose()` must be idempotent, synchronous with cancellation ownership transfer, and non-throwing. EasterAd retains the global presentation/provider lease while `LoadAndShow` is still returning and disposes a canceled returned handle before releasing that lease. If cancellation cleanup throws, the SDK deliberately keeps mobile presentation fail-closed for the rest of the process, rejects provider unregistration, and does not allow a new SDK client to overlap an operation whose cleanup is unknown. Treat this as an integration fault that requires fixing the provider and restarting the app; there is no runtime reset API.

Registering the same provider instance again is a no-op. Replacing a registered provider with a different instance, or unregistering while an operation is active, is rejected. Let operations finish or destroy/remove their Items, let pending callbacks drain, call `EasterAdSdk.UnregisterMobileAdProvider(provider)` at a safe application lifecycle point, and then shut down the host-owned provider. Merely disabling a component or GameObject is not an explicit operation-cancellation API.

Privacy policy, offline mode, ad-request disablement, and the kill switch are evaluated before provider invocation. A missing provider, invalid request, provider error, or no-fill fails closed: EasterAd does not create an internal mobile session or request, apply an EasterAd texture, record its own impression, or fall back to an in-game ad. Provider callbacks are queued and applied on Unity's main thread.

On Android/iOS, `Item.allowImpression`, `interactable`, `enableRefresh`, `refreshTime`, and `hideDuringCapture` do not control the external SDK's UI, measurement, click, refresh, or capture behavior. `EasterAdCaptureScope` also cannot hide a vendor-owned overlay. Implement those policies in the provider/host using the selected vendor's APIs. These fields retain their existing meaning only on supported EasterAd-rendered platforms.

EasterAd does not embed a specific mobile ad SDK. Production Android/iOS validation therefore requires the chosen vendor SDK, its EasterAd provider module, test credentials/placements, and real devices. H5 DOM/VAST presentation and browser lifecycle code are not included in the Unity mobile path.

## Runtime Features

- `EasterAdSdk` owns SDK initialization, privacy settings, diagnostics, platform routing, and, on supported non-WebGL non-mobile platforms, session lifecycle and the target camera used for impression measurement.
- On supported non-WebGL non-mobile platforms, `Plane` supports 3D world-space ad surfaces and `CanvasItem` supports RectTransform/UI ad surfaces.
- `Item` supports lazy/manual initialization for runtime `adUnitId` assignment. Items created before the SDK finishes initialization are initialized through a pending lifecycle queue.
- Ad loading classifies loaded, no-fill, policy-disabled, unsupported-content, retryable network, retryable image, and invalid-response outcomes.
- Supported in-game platforms use GPU AdSegmentation for impression measurement when available and fall back to bounds-based visibility when GPU measurement is unavailable.
- URP GPU visibility requires the AdSegmentation renderer feature. Basic ad loading continues to work without URP.

## Operational Notes

- `NoFill`, policy-disabled, unsupported-content, and invalid server responses are not retried.
- Only retryable network and image failures are scheduled for limited retry.
- Android/iOS provider failures and no-fill results never retry through or fall back to the EasterAd in-game renderer.
- On supported in-game platforms, the renderer accepts only absolute HTTP(S) media and click URLs without URL userinfo. Relative URLs and `javascript:`, `data:`, `file:`, or userinfo URLs are rejected. Creative image downloads do not follow redirects. Click navigation is handed to the host platform/browser, so the publisher is responsible for validating and controlling any redirect chain after the initial click URL. Unity owns the platform cookie store used by `UnityWebRequest`, so serve creative media from a dedicated CDN origin that does not share application or vendor authentication cookies.
- In-game creative media is limited to PNG/JPEG raster images, a 10-second image request, 8 MiB encoded bytes, 8,192 pixels per edge, and 16,777,216 decoded pixels. PNG/JPEG dimensions are validated from the encoded header before Unity decoding, and invalid or oversized creatives are never retried. HTML, VAST, SVG, GIF, and unknown MIME content remain unsupported/no-fill; Unity does not add an H5/WebView fallback.
- GPU AdSegmentation supports up to 255 registered ad objects.
- Bounds fallback does not measure real occlusion; it is an availability fallback, not an accuracy equivalent to GPU pixel counting.
