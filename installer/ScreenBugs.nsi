; Screen Bugs installer. Built by build/build-installer.ps1, which supplies VERSION,
; ASSETS_DIR, PUBLISH_DIR and OUT_FILE as absolute paths.
; Design: docs/superpowers/specs/2026-09-04-installer-design.md

Unicode true

!ifndef VERSION
  !error "VERSION must be defined, e.g. makensis -DVERSION=1.0.0"
!endif
!ifndef ASSETS_DIR
  !error "ASSETS_DIR must be defined: the directory holding ScreenBugs.ico and the wizard bitmaps."
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR must be defined: the self-contained publish to package."
!endif
!ifndef OUT_FILE
  !error "OUT_FILE must be defined: the setup executable to write."
!endif

Name "Screen Bugs"
OutFile "${OUT_FILE}"
SetCompressor /SOLID lzma

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Screen Bugs"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "Addam Boord"
VIAddVersionKey "LegalCopyright" "Copyright © 2026 Addam Boord"
VIAddVersionKey "FileDescription" "Screen Bugs setup"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define MUTEX_NAME "Local\ScreenBugs.SingleInstance"

; --- Scope. Stock MultiUser does the per-scope defaulting, including reading a prior
;     install's location back off the uninstall key, so none of that is hand-written.
;     MULTIUSER_USE_PROGRAMFILES64 is not optional: without it the all-users default is
;     32-bit Program Files, which is wrong for a win-x64 payload.
!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_INSTALLMODE_INSTDIR "ScreenBugs"
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "${UNINSTALL_KEY}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallLocation"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "${UNINSTALL_KEY}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallLocation"

!define MUI_ABORTWARNING
!define MUI_ICON "${ASSETS_DIR}\ScreenBugs.ico"
!define MUI_UNICON "${ASSETS_DIR}\ScreenBugs.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${ASSETS_DIR}\wizard-header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ASSETS_DIR}\wizard-side.bmp"
; MUI_UNWELCOMEFINISHPAGE_BITMAP is deliberately unset: the uninstaller has no welcome or
; finish page for it to appear on.

; An empty MUI_FINISHPAGE_RUN is what makes the checkbox appear at all; the FUNCTION alone
; renders no checkbox and never runs.
!define MUI_FINISHPAGE_RUN ""
!define MUI_FINISHPAGE_RUN_TEXT "Run Screen Bugs"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!include MUI2.nsh
!include MultiUser.nsh
!include nsDialogs.nsh
!include LogicLib.nsh
!include FileFunc.nsh
!include WinVer.nsh
!include x64.nsh

!insertmacro GetParameters
!insertmacro GetOptions
!insertmacro GetSize
!insertmacro un.GetParameters
!insertmacro un.GetOptions

; $DesktopShortcut, not $Desktop: "Desktop" is in use by the $DESKTOP constant and will
; not compile.
Var BugType
Var BugCount
Var Startup
Var DesktopShortcut
Var Upgrade
Var DeleteData
Var LocalData

!include "${__FILEDIR__}\options-page.nsh"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_DIRECTORY
Page custom OptionsPage OptionsPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
UninstPage custom un.OptionsPage un.OptionsPageLeave
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; Before MULTIUSER_INIT, deliberately. The macro performs the HKLM reads that pre-select a
  ; prior install's scope and directory, and those reads obey the registry view; the stub is
  ; 32-bit, so in the default view they resolve under WOW6432Node instead of where the
  ; uninstall key is written, and every all-users upgrade silently falls back to the defaults.
  SetRegView 64

  ; NSIS fills $INSTDIR from /D= before .onInit runs, and MULTIUSER_INIT then overwrites it with
  ; the per-scope default -- which would make /D= silently do nothing. This script sets no
  ; InstallDir, so a non-empty $INSTDIR here can only have come from /D=: keep it and put it back.
  StrCpy $R9 $INSTDIR
  !insertmacro MULTIUSER_INIT
  ${If} $R9 != ""
    StrCpy $INSTDIR $R9
  ${EndIf}

  ; Everything below must stay after the macro, which overwrites $INSTDIR and
  ; $MultiUser.InstallMode.
  ; Spec 5.5 lists these as the install section's first step; here they are, deliberately,
  ; in .onInit, so an unsupported machine is turned away before the wizard rather than after.
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Screen Bugs requires 64-bit Windows."
    Abort
  ${EndIf}
  ; Windows 10 1607 is .NET 10's floor; ${AtLeastWin10} would admit earlier builds.
  ${IfNot} ${AtLeastBuild} 14393
    MessageBox MB_OK|MB_ICONSTOP "Screen Bugs requires Windows 10 version 1607 or later."
    Abort
  ${EndIf}

  StrCpy $BugType "BlackGardenAnt"
  StrCpy $BugCount "5"
  StrCpy $Startup "1"
  StrCpy $DesktopShortcut "0"

  ${GetParameters} $R0
  ${GetOptions} $R0 "/BUGTYPE=" $BugType
  ${GetOptions} $R0 "/BUGCOUNT=" $BugCount
  ${GetOptions} $R0 "/STARTUP=" $Startup
  ${GetOptions} $R0 "/DESKTOP=" $DesktopShortcut
  Call ValidateOptions
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MULTIUSER_UNINIT

  StrCpy $Upgrade "0"
  StrCpy $DeleteData "0"
  ${un.GetParameters} $R0
  ${un.GetOptions} $R0 "/UPGRADE=" $Upgrade
  ${un.GetOptions} $R0 "/DELETEDATA=" $DeleteData

  ; With SetShellVarContext all, $LOCALAPPDATA resolves to C:\ProgramData, so the real
  ; per-user path is captured here and the context put back where MULTIUSER_UNINIT left it.
  SetShellVarContext current
  StrCpy $LocalData "$LOCALAPPDATA\ScreenBugs"
  ${If} $MultiUser.InstallMode == "AllUsers"
    SetShellVarContext all
  ${EndIf}
FunctionEnd

Function LaunchApp
  ; Privileges, NOT install mode. An administrator is elevated before the mode page, so one
  ; who then picks "only for me" is still elevated; an elevated tray app would run its
  ; click-through overlay elevated and write its HKCU values into the wrong hive.
  ${If} $MultiUser.Privileges == "Admin"
  ${OrIf} $MultiUser.Privileges == "Power"
    Exec '"$WINDIR\explorer.exe" "$INSTDIR\ScreenBugs.exe"'
  ${Else}
    Exec '"$INSTDIR\ScreenBugs.exe"'
  ${EndIf}
FunctionEnd

Section "Install"
  ; --- Close a running instance, detected through the app's own single-instance mutex.
  ;     The mutex is session-local, so an elevated installer in the same session still sees
  ;     it. An instance under a different user is invisible here and will instead lock the
  ;     files, which NSIS's standard retry prompt covers.
  System::Call 'kernel32::OpenMutex(i 0x00100000, i 0, t "${MUTEX_NAME}") p .r0'
  ${If} $0 <> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    ${IfNot} ${Silent}
      MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
        "Screen Bugs is running and must be closed to continue." IDOK +2
      Abort "Installation cancelled: Screen Bugs is still running."
    ${EndIf}
    ; /F denies the app its OnExit, which is harmless: settings are saved when the Options
    ; dialog is accepted, not at exit, and SingleInstanceGuard treats an abandoned mutex as
    ; a free slot.
    nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'
    Pop $0
    Pop $1
    Sleep 500
  ${EndIf}

  ; --- A prior install somewhere else: remove it so the machine cannot end up with two
  ;     copies. _?= keeps the uninstaller from relocating itself to $TEMP, which is what
  ;     makes ExecWait actually wait; it also stops it deleting its own file, hence the two
  ;     lines after. /UPGRADE=1 keeps the user's Run value, which only the app can re-point.
  StrCpy $R0 ""
  ReadRegStr $R0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $R0 == ""
    ReadRegStr $R0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${EndIf}
  ${If} $R0 != ""
  ${AndIf} $R0 != "$INSTDIR"
  ${AndIf} ${FileExists} "$R0\Uninstall.exe"
    DetailPrint "Removing the previous installation in $R0..."
    ExecWait '"$R0\Uninstall.exe" /S /UPGRADE=1 _?=$R0'
    Delete "$R0\Uninstall.exe"
    RMDir "$R0"
  ${EndIf}

  ; --- Files
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"

  ; --- The seed (spec 2.3). One slot at speed 1, 60 fps: install time offers no control
  ;     over slot count, per-slot speed, frame rate or type-change behaviour.
  DetailPrint "Writing install-defaults.json..."
  FileOpen $0 "$INSTDIR\install-defaults.json" w
  FileWrite $0 '{$\r$\n'
  FileWrite $0 '  "TypeSlots": [ { "Type": "$BugType", "Speed": 1 } ],$\r$\n'
  FileWrite $0 '  "BugCount": $BugCount,$\r$\n'
  FileWrite $0 '  "FrameRate": 60,$\r$\n'
  FileWrite $0 '  "OnTypeChange": "RespawnAll",$\r$\n'
  ${If} $Startup == "1"
    FileWrite $0 '  "StartAtLogin": true$\r$\n'
  ${Else}
    FileWrite $0 '  "StartAtLogin": false$\r$\n'
  ${EndIf}
  FileWrite $0 '}$\r$\n'
  FileClose $0

  ; --- Shortcuts. Start Menu always; desktop opt-in, because Screen Bugs lives in the tray
  ;     and is rarely relaunched by hand.
  CreateShortcut "$SMPROGRAMS\Screen Bugs.lnk" "$INSTDIR\ScreenBugs.exe"
  ${If} $DesktopShortcut == "1"
    CreateShortcut "$DESKTOP\Screen Bugs.lnk" "$INSTDIR\ScreenBugs.exe"
  ${EndIf}

  ; --- Uninstaller and Add/Remove Programs. InstallLocation is also what the
  ;     MULTIUSER_INSTALLMODE_*_REGISTRY_* defines read back on the next upgrade.
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayName" "Screen Bugs"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "Publisher" "Addam Boord"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ScreenBugs.exe,0"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "EstimatedSize" "$0"
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ; --- Close a running instance first, so it cannot re-create error.log after the delete.
  System::Call 'kernel32::OpenMutex(i 0x00100000, i 0, t "${MUTEX_NAME}") p .r0'
  ${If} $0 <> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'
    Pop $0
    Pop $1
    Sleep 500
  ${EndIf}

  ; --- Optional data removal. $LocalData was captured in un.onInit with the current-user
  ;     context, because under SetShellVarContext all, $LOCALAPPDATA is C:\ProgramData.
  ${If} $DeleteData == "1"
    RMDir /r "$LocalData"
  ${EndIf}

  ; --- The Run value. The app may have created it, and leaving it behind would make Windows
  ;     try to launch a deleted executable at every sign-in. Kept only under /UPGRADE=1,
  ;     where the installer that invoked this is about to install a copy the app will
  ;     re-point the value at.
  ${If} $Upgrade != "1"
    DeleteRegValue HKCU "${RUN_KEY}" "ScreenBugs"
  ${EndIf}

  Delete "$SMPROGRAMS\Screen Bugs.lnk"
  Delete "$DESKTOP\Screen Bugs.lnk"

  ; Guarded so a bad $INSTDIR cannot delete an unrelated folder. Uninstall.exe goes with it:
  ; NSIS relocates a normally-launched uninstaller to $TEMP, so the original is not in use.
  ${If} ${FileExists} "$INSTDIR\ScreenBugs.exe"
    RMDir /r "$INSTDIR"
  ${Else}
    DetailPrint "Skipping $INSTDIR: it does not look like a Screen Bugs installation."
  ${EndIf}

  DeleteRegKey SHCTX "${UNINSTALL_KEY}"
SectionEnd
