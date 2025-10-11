# Changelog

All notable changes to this project will be documented in this file.

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

-   Renamed `EtaSdk` class to `EasterAdSdk` for better clarity
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
