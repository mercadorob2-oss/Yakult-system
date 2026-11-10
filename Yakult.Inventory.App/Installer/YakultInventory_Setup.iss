; Compatibility entry point for manual Inno compilation.
; Build-DesktopInstaller.cmd also builds the app, stamps its version, and signs
; the release metadata, so it is the recommended release entry point.
#ifndef ReleaseVersion
  #define ReleaseVersion GetFileVersion(AddBackslash(SourcePath) + "..\bin\Release\Yakult.Inventory.App.exe")
#endif
#include "DesktopRelease.iss"
