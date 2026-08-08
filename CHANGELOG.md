# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

-   Added Dashboard integration in `Window > EasterAd` to connect with the EasterAd Developer Dashboard by API key and apply dashboard Game ID, SDK Key, and Ad Unit IDs to Unity SDK settings and placements.
-   Added unified Inventory and Placement management in `Window > EasterAd`, where scene-placed inventory is Active and unplaced inventory is shown in Archive.
-   Added Managed Scenes in `Window > EasterAd` Settings to scope duplicate Ad Unit ID scans, auto-add placement scenes, and prune scenes with no EasterAd placements.
-   Added sidebar Menu Settings to arrange predefined dashboard components into folders through a single-line drag-and-drop list.
-   Added automatic runtime bootstrapping from `Window > EasterAd` SDK settings.
-   Added Window-based SDK and placement management for Plane and CanvasItem ads.

### Changed

-   Redesigned `Window > EasterAd` as a UI Toolkit console aligned with the EasterAd web dashboard layout, colors, and folder/component structure.
-   Moved dashboard API key, organization, and game selection into Settings and persist the selected dashboard project in EditorPrefs.
-   Changed dashboard data updates to run when the window opens, tabs/settings are used, dashboard project selection changes, or the error banner's Refresh button is pressed instead of on a fixed interval.
-   Hid normal dashboard update progress and success states from the UI so only actionable update failures or paused states are shown.
-   Updated Menu Settings dragging so the dragged component leaves the list, stays as a floating preview, and opens a real spacer between rows before drop.
-   Removed standalone placement creation from the Inventory workflow; new inventory now creates a default scene `Plane`, and existing inventory is reused from Archive.
-   Hid per-inventory placement settings behind a row-level icon toggle by default, with only one inventory settings panel open at a time.
-   Removed the row-level Archive button from active inventory; scene placements are removed from the row Settings panel instead.
-   Changed duplicate Ad Unit ID handling to warn within Managed Scenes instead of blocking placement creation or Play Mode.
-   Simplified EasterAdSdk, Plane, and CanvasItem inspectors to direct users to the EasterAd window.

## [1.4.0] - 2025.10.22

### Added

-   Added lazy initialization option for Item components to allow adUnitId post-initialization setup
-   Added CanvasItem support for RectTransform/UI based ad surfaces
-   Added privacy controls for child-directed treatment, consent, offline mode, ad request enablement, and ad category policy
-   Added ad request diagnostics event for observing load outcomes
-   Added ad-hidden capture scope and external navigation interaction helper
-   Added bounds-based visibility fallback when GPU AdSegmentation is unavailable
-   Added an ETA-to-EasterAd migration guide
-   Added a temporary `ETA` namespace source-compatibility bridge for migration

### Changed

-   Renamed source folders, namespaces, assemblies, asmdefs, and build scripts from `ETA` naming to `EasterAd` naming
-   Renamed runtime config files from `ETA_Config.txt`/`ETA_Axes.txt` to `EasterAd_Config.txt`/`EasterAd_Axes.txt`
-   Reworked pending Item initialization so SDK-ready processing no longer re-invokes Unity Awake()
-   Centralized ItemStatus transitions for load, impression, retry, and interaction flows
-   Changed retry scheduling to use typed internal commands instead of raw string queue entries

### Fixed

-   Added fallback reads for legacy `ETA_Config.txt` and `ETA_Axes.txt` during migration
-   Prevented AdSegmentation ID double-unregister from duplicating IDs in the available pool
-   Hardened ad response parsing against malformed JSON and cleaned up request coroutine components on early failures

## [1.3.2] - 2025.10.22

### Fixed

-   Fixed "Saving Prefab to immutable folder" error when installing AdSegmentationRendererFeature by using SaveAssetIfDirty() instead of SaveAssets() to only save the modified RendererData asset

## [1.3.1] - 2025.10.22

### Fixed

-   Fixed camera synchronization between EasterAdSDK and CameraManager for multi-camera environments
-   Fixed RendererFeature installation failure that resulted in "Missing RendererFeature" by using direct type reference

### Changed

-   Refactored camera API - targetCamera property now internally handles CameraManager synchronization
-   Deprecated SetCamera() method in favor of cleaner targetCamera property approach
-   Simplified Update() logic by removing redundant camera synchronization code
-   Improved GetGlobalTargetCamera() with try-catch for Editor mode stability

### Removed

-   Removed unused Settings class from AdSegmentationRendererFeature and AdSegmentationScriptableRenderPass
-   Removed legacy manual camera synchronization code from Update() method

## [1.3.0] - 2025.10.21

### Fixed

-   Streamlined early return logic in MinSizeAndPixelRequirementFilter for improved code clarity

### Added

-   Added GPU-based ad visibility measurement system using compute shaders and segmentation rendering
-   Added AdSegmentationManager for automatic ID assignment and management of ad objects
-   Added URP support with AdSegmentationFeatureManager and render pipeline integration
-   Added comprehensive impression logging with detailed visibility metrics (areaVisible, visibleRatio)
-   Added pixel counting implementation with ComputeBuffer management
-   Added GPU-based visibility filtering replacing traditional raycast approach

## [1.2.2] - 2025.10.12

### Fixed

-   Fix "Saving Prefab to immutable folder" error by properly recording prefab overrides to scene instead of package asset.
-   Fix transform restoration by using consistent local coordinates.

## [1.2.1] - 2025.10.12

### Fixed

-   Added missing meta files
-   Remove Error from EasterAdSdk.cs

## [1.2.0] - 2025.10.11

### Added

-   Added unified shader system supporting all render pipelines (Built-in, URP, HDRP) automatically
-   Added migration helper tool for automatic transition from legacy assets to new unified system
-   Added custom device info settings (Device Type, Platform, Language) in EasterAd editor window
-   Added SDK re-initialization feature through editor interface
-   Added interaction tracking system with start/end interaction methods
-   Added interactable toggle option for ad items
-   Added refresh functionality with enable/disable toggle for ad items
-   Added VR/XR support with sample scene and XR Origin integration

### Changed

-   Renamed `EasterAdSdk` class to `EasterAdSdk` for better clarity
-   Renamed package identifier from `com.autovertise.easterad` to `com.easterad.easterad`
-   Updated company references from "Autovertise" to "EasterAd" throughout the project
-   Changed minimum Unity version from 2020.2 to 2021.3
-   Changed minimum Android SDK version from 22 to 23
-   Changed target framework from .NET Standard 2.0 to .NET Standard 2.1
-   Improved EasterAd editor window UI with current/new value comparison display
-   Enhanced Item component with interaction and refresh control options
-   Updated prefab references to use package-based assets directly
-   Improved shader system to automatically detect and support render pipeline

### Removed

-   Removed legacy render pipeline-specific Unity packages (EasterAd_BuiltIn, EasterAd_URP, EasterAd_HDRP)
-   Removed manual render pipeline asset import workflow

## [1.1.4] - 2025.09.30

### Changed

-   Updated the SDK to support Unity 6000

## [1.1.3] - 2024.11.24

### Added

-   If Item`s request is fail, default Material will be used.
-   If Load is fail, retry 3 times with 5 seconds delay.

## [1.1.2] - 2024.11.13

### Changed

-   Fixed scale issue with the Plane Item prefab when it has a parent object.
-   Fixed the color of the gizmo when a valid impression occurs.

## [1.1.1] - 2024.11.05

### Changed

-   Hotfix for the internal problem with the EasterAd service.
-   No changes in the SDK itself.

### Removed

## [1.1.0] - 2024.11.05

### Changed

-   C# 8.0 Support
-   NetStandard 2.0 support
-   Updated the SDK to support Unity 2020.2

### Removed

-   Nothing. It's the first release, nothing to remove.

## [1.0.0] - 2024.10.01

### Added

-   First Published Version of the EasterAd SDK.

## [0.0.1] - 2024.09.18

### Added

This is the first version of the changelog.

### Changed

-   The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),

### Removed

-   Nothing. It's the first release, nothing to remove.
