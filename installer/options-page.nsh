; The custom options page (spec 5.3) and the validation shared with silent mode (spec 5.4).
; Included by ScreenBugs.nsi, which declares the $BugType/$BugCount/$Startup/$DesktopShortcut vars.

Var Dialog
Var TypeBox
Var CountBox
Var CountUpDown
Var StartupBox
Var DesktopBox

Function OptionsPage
  !insertmacro MUI_HEADER_TEXT "Options" "Choose what Screen Bugs starts with."

  nsDialogs::Create 1018
  Pop $Dialog
  ${If} $Dialog == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 2u 60u 12u "Bug type"
  Pop $R0
  ${NSD_CreateDroplist} 62u 0 120u 12u ""
  Pop $TypeBox
  ; Random first, then the nine species in SpeciesId order. BugTypeNameFor below maps the
  ; selected index back to the enum name, and the two lists sit next to each other on purpose.
  ${NSD_CB_AddString} $TypeBox "Random"
  ${NSD_CB_AddString} $TypeBox "Hissing cockroach"
  ${NSD_CB_AddString} $TypeBox "Black garden ant"
  ${NSD_CB_AddString} $TypeBox "Red fire ant"
  ${NSD_CB_AddString} $TypeBox "Praying mantis"
  ${NSD_CB_AddString} $TypeBox "Seven-spot ladybug"
  ${NSD_CB_AddString} $TypeBox "Stag beetle"
  ${NSD_CB_AddString} $TypeBox "House spider"
  ${NSD_CB_AddString} $TypeBox "Centipede"
  ${NSD_CB_AddString} $TypeBox "Stink bug"
  ${NSD_CB_SelectString} $TypeBox "Black garden ant"

  ${NSD_CreateLabel} 0 22u 60u 12u "Bugs on screen"
  Pop $R0
  ; A spinner, not a slider: nsDialogs routes WM_NOTIFY but not the WM_HSCROLL a trackbar
  ; sends, so a live value label beside a slider would need dialog subclassing.
  ${NSD_CreateNumber} 62u 20u 30u 12u "$BugCount"
  Pop $CountBox
  ${NSD_CreateAutoUpDown} $CountBox
  Pop $CountUpDown
  ${NSD_UD_SetRange32} $CountUpDown 1 50

  ${NSD_CreateCheckBox} 0 42u 100% 12u "Run Screen Bugs when I sign in to Windows"
  Pop $StartupBox
  ${If} $Startup == "1"
    ${NSD_SetState} $StartupBox ${BST_CHECKED}
  ${EndIf}

  ${NSD_CreateCheckBox} 0 56u 100% 12u "Create a desktop shortcut"
  Pop $DesktopBox
  ${If} $DesktopShortcut == "1"
    ${NSD_SetState} $DesktopBox ${BST_CHECKED}
  ${EndIf}

  ; Unconditional on purpose. Detecting whether *this* user already has a settings.json means
  ; reading a per-user path from a possibly elevated installer, which under over-the-shoulder
  ; elevation reads the wrong profile and would show a misleading hint. This sentence is always true.
  ${NSD_CreateLabel} 0 76u 100% 24u "These apply the first time each user runs Screen Bugs. If you've used it before, your saved settings are kept — change them from Options in the tray menu."
  Pop $R0

  nsDialogs::Show
FunctionEnd

Function OptionsPageLeave
  ${NSD_GetText} $CountBox $BugCount
  ${NSD_GetState} $StartupBox $Startup
  ${NSD_GetState} $DesktopBox $DesktopShortcut
  ${NSD_CB_GetSelectionIndex} $TypeBox $R0
  Call BugTypeNameFor
  Call ValidateOptions
FunctionEnd

; Index in the droplist above -> the SpeciesId name the seed file needs.
Function BugTypeNameFor
  ${Switch} $R0
    ${Case} 0
      StrCpy $BugType "Random"
      ${Break}
    ${Case} 1
      StrCpy $BugType "HissingCockroach"
      ${Break}
    ${Case} 2
      StrCpy $BugType "BlackGardenAnt"
      ${Break}
    ${Case} 3
      StrCpy $BugType "RedFireAnt"
      ${Break}
    ${Case} 4
      StrCpy $BugType "PrayingMantis"
      ${Break}
    ${Case} 5
      StrCpy $BugType "SevenSpotLadybug"
      ${Break}
    ${Case} 6
      StrCpy $BugType "StagBeetle"
      ${Break}
    ${Case} 7
      StrCpy $BugType "HouseSpider"
      ${Break}
    ${Case} 8
      StrCpy $BugType "Centipede"
      ${Break}
    ${Case} 9
      StrCpy $BugType "StinkBug"
      ${Break}
    ${Default}
      StrCpy $BugType "BlackGardenAnt"
      ${Break}
  ${EndSwitch}
FunctionEnd

; Silent-mode values come off the command line, so they need the same clamping the page does.
; An unknown type falls back to the default rather than failing a deployment over a cosmetic
; option; verify-install.ps1 asserts the written seed, so a typo in a script surfaces there.
Function ValidateOptions
  ${If} $BugType != "Random"
  ${AndIf} $BugType != "HissingCockroach"
  ${AndIf} $BugType != "BlackGardenAnt"
  ${AndIf} $BugType != "RedFireAnt"
  ${AndIf} $BugType != "PrayingMantis"
  ${AndIf} $BugType != "SevenSpotLadybug"
  ${AndIf} $BugType != "StagBeetle"
  ${AndIf} $BugType != "HouseSpider"
  ${AndIf} $BugType != "Centipede"
  ${AndIf} $BugType != "StinkBug"
    StrCpy $BugType "BlackGardenAnt"
  ${EndIf}

  ; A non-numeric value compares as 0 here, so it clamps to 1 rather than reaching the seed.
  ${If} $BugCount < 1
    StrCpy $BugCount "1"
  ${ElseIf} $BugCount > 50
    StrCpy $BugCount "50"
  ${EndIf}

  ${If} $Startup != "0"
    StrCpy $Startup "1"
  ${EndIf}
  ${If} $DesktopShortcut != "1"
    StrCpy $DesktopShortcut "0"
  ${EndIf}
FunctionEnd
