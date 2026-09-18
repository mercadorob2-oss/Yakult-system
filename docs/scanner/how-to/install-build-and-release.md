# Work Instruction: Yakult Scanner - Install, Build, and Release

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + developers |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document describes how to prepare the Android scanner app for device installation, how to build available flavors from the repository, and what release limitations currently exist in the project.

## 2. Scope

Covered:

- local project build prerequisites
- Gradle wrapper usage
- available build flavors
- debug and release assembly
- installation to test devices

Not covered:

- enterprise signing infrastructure outside the repository
- Play Store or public app-store distribution
- MDM configuration outside the repository

## 3. Build Prerequisites

Required tools:

- JDK compatible with Android Gradle Plugin 8.4.0
- Android Studio or Android command-line SDK
- Android SDK platform 34
- Gradle wrapper included in the repository

Project files of interest:

- `Latest_sys/YakultScanner/build.gradle.kts`
- `Latest_sys/YakultScanner/app/build.gradle.kts`
- `Latest_sys/YakultScanner/gradle/wrapper/gradle-wrapper.properties`

Gradle wrapper currently points to:

- `gradle-8.13-bin.zip`

## 4. Build Flavors

The app defines two environment flavors:

- `dev`
- `prod`

Flavor differences:

- `dev`
  - application ID suffix `.dev`
  - version name suffix `-dev`
  - base URL default port `80`
- `prod`
  - no application ID suffix
  - base URL default port `7326`

## 5. Build Types

Configured build types:

- `debug`
- `release`

Current release characteristics:

- `isMinifyEnabled = false`
- no explicit signing configuration is defined in `app/build.gradle.kts`

Support implication:

- release APK/AAB signing must be handled outside the checked-in Gradle configuration or by local IDE signing setup

## 6. Build Procedure

From:

- `Latest_sys/YakultScanner`

Typical commands:

```powershell
.\gradlew assembleDevDebug
.\gradlew assembleProdDebug
.\gradlew assembleProdRelease
```

If Android Studio is used:

1. open the `YakultScanner` project
2. sync Gradle
3. select desired build variant
4. build APK from Android Studio

## 7. Expected Outputs

Standard Android output location:

- `Latest_sys/YakultScanner/app/build/outputs/apk/`

Typical output grouping:

- by flavor
- by build type

Because the repository already contains build artifacts, support should treat the source-controlled Gradle configuration as authoritative, not previously generated APK folders.

## 8. Device Installation Procedure

### 8.1 Debug/test installation

Install by:

- Android Studio run/install
- `adb install`
- enterprise/internal distribution process

### 8.2 Basic acceptance after install

1. app launches
2. login screen appears
3. settings dialog opens
4. Test Connection works
5. login works
6. camera scanner opens

## 9. Release Readiness Checklist

Before handing a build to support or operations:

- confirm correct flavor
- confirm target base URL expectation
- confirm version name and version code
- confirm login works
- confirm dispatch scan works
- confirm direct serial handoff works
- confirm pending/processed screens load

## 10. Known Release Gaps

Current repository limitations that should be called out during release preparation:

- no signing config defined in source
- cleartext HTTP is still enabled
- API endpoints point to hardcoded internal IP defaults
- no automated release pipeline is defined in the repository

## 11. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
