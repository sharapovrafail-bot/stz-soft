Unicode true
ManifestDPIAware true
!define APPNAME "Симулятор УП Fanuc"
!define APPDIR "SimulyatorUP"
!define APPEXE "SimulyatorUP.exe"
!define VERSION "9.0.0"
!define PUBLISHER "Rafail Sharapov"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SimulyatorUP"

Name "${APPNAME}"
OutFile "Simulyator_UP_Fanuc_Setup_9.0.0.exe"
InstallDir "$PROGRAMFILES64\${APPDIR}"
InstallDirRegKey HKLM "Software\${APPDIR}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
BrandingText "${APPNAME} ${VERSION}"

VIProductVersion "9.0.0.0"
VIAddVersionKey /LANG=1049 "ProductName" "${APPNAME}"
VIAddVersionKey /LANG=1049 "FileDescription" "Установка ${APPNAME}"
VIAddVersionKey /LANG=1049 "CompanyName" "${PUBLISHER}"
VIAddVersionKey /LANG=1049 "LegalCopyright" "© 2026 ${PUBLISHER}"
VIAddVersionKey /LANG=1049 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1049 "ProductVersion" "${VERSION}"

!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Установка: ${APPNAME} ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Программа установит на компьютер симулятор управляющих программ Fanuc для фрезерного KVL 1000 и токарного Puma 3050 LM с отправкой УП на станок по сети (FOCAS, вместе с STZ & FANUC LAN).$\r$\n$\r$\nНажмите «Далее», чтобы продолжить."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APPEXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить ${APPNAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Нужна 64-разрядная Windows 10 или 11."
    Abort
  ${EndIf}
  SetRegView 64
  ; закрыть запущенную копию перед обновлением
  nsExec::Exec 'taskkill /F /IM ${APPEXE}'
FunctionEnd

Section "Программа (обязательно)" SecMain
  SectionIn RO
  SetShellVarContext all
  SetOutPath "$INSTDIR"
  RMDir /r "$INSTDIR\resources"
  File /r "win-unpacked\*.*"
  File "icon.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0
  CreateShortCut "$SMPROGRAMS\${APPNAME}\Удалить ${APPNAME}.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\${APPDIR}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${APPEXE},0"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "EstimatedSize" 290000
SectionEnd

Section "Ярлык на рабочем столе" SecDesk
  SetShellVarContext all
  Delete "$DESKTOP\${APPNAME}.lnk"
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0
SectionEnd

Section /o "Открывать файлы .nc двойным щелчком" SecAssoc
  WriteRegStr HKLM "Software\Classes\.nc" "" "SimulyatorUP.nc"
  WriteRegStr HKLM "Software\Classes\SimulyatorUP.nc" "" "Управляющая программа ЧПУ"
  WriteRegStr HKLM "Software\Classes\SimulyatorUP.nc\DefaultIcon" "" "$INSTDIR\icon.ico"
  WriteRegStr HKLM "Software\Classes\SimulyatorUP.nc\shell\open\command" "" '"$INSTDIR\${APPEXE}" "%1"'
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

Function .onInstSuccess
  ; обновить иконки: Windows хранит старую картинку в кеше
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
  nsExec::Exec '"$SYSDIR\ie4uinit.exe" -show'
FunctionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "Файлы программы и ярлык в меню «Пуск»."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesk} "Ярлык программы на рабочем столе."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAssoc} "Файлы .nc будут открываться в симуляторе."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function un.onInit
  SetRegView 64
  nsExec::Exec 'taskkill /F /IM ${APPEXE}'
FunctionEnd

Section "Uninstall"
  SetShellVarContext all
  Delete "$DESKTOP\${APPNAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APPNAME}"
  ReadRegStr $0 HKLM "Software\Classes\.nc" ""
  ${If} $0 == "SimulyatorUP.nc"
    DeleteRegKey HKLM "Software\Classes\.nc"
  ${EndIf}
  DeleteRegKey HKLM "Software\Classes\SimulyatorUP.nc"
  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegKey HKLM "Software\${APPDIR}"
  RMDir /r "$INSTDIR"
SectionEnd
