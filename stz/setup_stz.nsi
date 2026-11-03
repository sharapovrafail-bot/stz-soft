Unicode true
!define APP "STZ Расчёт зубьев"
!define PUB "ООО «СпецТехЗаказ»"
!ifndef VER
  !define VER "1.0.0-beta.19"
!endif
!define EXE "STZ.exe"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZRaschetZubev"
Name "${APP} ${VER}"
OutFile "STZ-Setup-${VER}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP}"
InstallDirRegKey HKCU "Software\SpecTehZakaz\STZ" "Dir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Сделано специально по заказу ${PUB} · Рафаил Шарапов · rafail@sharapov-cnc.ru"
VIProductVersion "1.0.0.19"
VIAddVersionKey /LANG=1049 "ProductName" "${APP}"
VIAddVersionKey /LANG=1049 "CompanyName" "${PUB}"
VIAddVersionKey /LANG=1049 "FileDescription" "Установка: ${APP} + Зубофреза 5К32А + Зуборез 525"
VIAddVersionKey /LANG=1049 "FileVersion" "${VER}"
VIAddVersionKey /LANG=1049 "LegalCopyright" "Сделано специально по заказу ${PUB}"
!include MUI2.nsh
!define MUI_ICON "stz.ico"
!define MUI_UNICON "stz.ico"
!define MUI_WELCOMEPAGE_TITLE "${APP}$\r$\nверсия ${VER}"
!define MUI_WELCOMEPAGE_TEXT "В установку входят три программы — на следующем шаге отметьте галочками, какие ставить:$\r$\n• «${APP}» — расчёт зубчатых (в т.ч. гипоидных), червячных, цепных, ремённых и шлицевых передач;$\r$\n• «Зубофреза 5К32А» — наладка зубофрезерного полуавтомата;$\r$\n• «Зуборез 525» — наладка зуборезного станка 525: конические (в т.ч. по ГОСТ 19326) и гипоидные колёса с круговым зубом.$\r$\nВ STZ есть кнопки запуска обеих программ наладки, а «Экспорт → … → эрозия» передаёт колесо в программу STZ WireCut (проволочно-вырезной станок, ставится отдельно).$\r$\n$\r$\nПрограмма бесплатная, без лицензии и ключей."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить STZ"
!insertmacro MUI_PAGE_WELCOME
!define MUI_COMPONENTSPAGE_SMALLDESC
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Section "-Подготовка"
  nsExec::Exec 'taskkill /F /IM "${EXE}"'
  nsExec::Exec 'taskkill /F /IM Zubofreza_5K32A.exe'
  nsExec::Exec 'taskkill /F /IM Zuborez_525.exe'
  Sleep 1000
SectionEnd

Section "STZ Расчёт зубьев (основная)" SecSTZ
  SectionIn RO
  SetOutPath "$INSTDIR"
  ; вся программа, кроме двух программ наладки (они ставятся своими галочками ниже)
  File /r /x hob /x z525 "dist\win-unpacked\*.*"
  SetOutPath "$INSTDIR"
  File "/oname=STZ.ico" "stz.ico"
  WriteUninstaller "$INSTDIR\Удалить.exe"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\STZ.ico"
  CreateShortcut "$SMPROGRAMS\${APP}\Удалить.lnk" "$INSTDIR\Удалить.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\STZ.ico"
  WriteRegStr HKCU "Software\SpecTehZakaz\STZ" "Dir" "$INSTDIR"
  WriteRegStr HKCU "Software\Classes\.zrp" "" "STZ.Project"
  WriteRegStr HKCU "Software\Classes\STZ.Project" "" "Проект STZ"
  WriteRegStr HKCU "Software\Classes\STZ.Project\DefaultIcon" "" "$INSTDIR\${EXE},0"
  WriteRegStr HKCU "Software\Classes\STZ.Project\shell\open\command" "" '"$INSTDIR\${EXE}" "%1"'
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "${PUB}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Удалить.exe"'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
SectionEnd

Section "Зубофреза 5К32А (зубофрезерный)" SecHob
  SetOutPath "$INSTDIR\resources\hob"
  File "dist\win-unpacked\resources\hob\Zubofreza_5K32A.exe"
  File "/oname=Zubofreza_5K32A.ico" "hob.ico"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\Зубофреза 5К32А.lnk" "$INSTDIR\resources\hob\Zubofreza_5K32A.exe" "" "$INSTDIR\resources\hob\Zubofreza_5K32A.ico"
  CreateShortcut "$DESKTOP\Зубофреза 5К32А.lnk" "$INSTDIR\resources\hob\Zubofreza_5K32A.exe" "" "$INSTDIR\resources\hob\Zubofreza_5K32A.ico"
SectionEnd

Section "Зуборез 525 (конические колёса)" SecZ525
  SetOutPath "$INSTDIR\resources\z525"
  File "dist\win-unpacked\resources\z525\Zuborez_525.exe"
  File "/oname=Zuborez_525.ico" "z525.ico"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\Зуборез 525.lnk" "$INSTDIR\resources\z525\Zuborez_525.exe" "" "$INSTDIR\resources\z525\Zuborez_525.ico"
  CreateShortcut "$DESKTOP\Зуборез 525.lnk" "$INSTDIR\resources\z525\Zuborez_525.exe" "" "$INSTDIR\resources\z525\Zuborez_525.ico"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecSTZ} "Расчёт передач, чертежи, экспорт. Ставится всегда."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecHob} "Наладка зубофрезерного полуавтомата 5К32А: гитары, карта наладки. Ярлык на рабочем столе."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecZ525} "Наладка зуборезного станка 525 для конических колёс с круговым зубом. Ярлык на рабочем столе."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM "${EXE}"'
  nsExec::Exec 'taskkill /F /IM Zubofreza_5K32A.exe'
  nsExec::Exec 'taskkill /F /IM Zuborez_525.exe'
  Sleep 800
  IfFileExists "$INSTDIR\${EXE}" 0 +2
  RMDir /r "$INSTDIR"
  Delete "$SMPROGRAMS\${APP}\*.lnk"
  RMDir "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  Delete "$DESKTOP\Зубофреза 5К32А.lnk"
  Delete "$DESKTOP\Зуборез 525.lnk"
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\SpecTehZakaz\STZ"
  DeleteRegKey HKCU "Software\Classes\.zrp"
  DeleteRegKey HKCU "Software\Classes\STZ.Project"
  ; проекты .zrp и детали в «Документы» НЕ удаляются
SectionEnd
