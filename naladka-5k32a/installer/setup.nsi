Unicode true
!define APP "Зубофреза 5К32А"
!define PUB "ООО «СпецТехЗаказ»"
!define VER "1.3.21"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Zubofreza5K32A"
Name "${APP} ${VER}"
OutFile "Ustanovka_Zubofreza_5K32A_v1.3.21.exe"
InstallDir "$PROGRAMFILES64\СпецТехЗаказ\${APP}"
InstallDirRegKey HKLM "Software\SpecTehZakaz\Zubofreza5K32A" "Dir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "Сделано специально по заказу ${PUB} · Рафаил Шарапов · rafail@sharapov-cnc.ru"
VIProductVersion "${VER}.0"
VIAddVersionKey /LANG=1049 "ProductName" "${APP}"
VIAddVersionKey /LANG=1049 "CompanyName" "${PUB}"
VIAddVersionKey /LANG=1049 "FileDescription" "Установка: ${APP} (сделано специально по заказу ${PUB})"
VIAddVersionKey /LANG=1049 "FileVersion" "${VER}"
VIAddVersionKey /LANG=1049 "LegalCopyright" "Сделано специально по заказу ${PUB}"

!include MUI2.nsh
!define MUI_ICON "app.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "header.bmp"
!define MUI_UNICON "app.ico"
!define MUI_WELCOMEPAGE_TITLE "Сделано специально по заказу$\r$\n${PUB}"
!define MUI_WELCOMEPAGE_TITLE_3LINES
!define MUI_WELCOMEPAGE_TEXT "Программа наладки зубофрезерного полуавтомата 5К32А: расчёт сменных шестерён, угла бабки, подач и чисел оборотов, карты наладки.$\r$\n$\r$\nВЕРСИЯ ${VER}.$\r$\nНажмите «Далее» для установки."
!define MUI_FINISHPAGE_TITLE "Сделано специально по заказу$\r$\n${PUB}"
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_TEXT "Программа «${APP}», версия ${VER}, установлена в: $INSTDIR. Ярлык — на рабочем столе и в меню «Пуск». Детали сохраняются в папке «Документы\Зубофреза 5К32А\Детали»."
!define MUI_FINISHPAGE_RUN "$INSTDIR\Zubofreza_5K32A.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить программу"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Section "Install"
  nsExec::Exec 'taskkill /F /IM Zubofreza_5K32A.exe'
  Sleep 800
  SetOutPath "$INSTDIR"
  File "Zubofreza_5K32A.exe"
  File "app.ico"
  WriteUninstaller "$INSTDIR\Удалить.exe"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\Zubofreza_5K32A.exe" "" "$INSTDIR\app.ico"
  CreateShortcut "$SMPROGRAMS\${APP}\Удалить.lnk" "$INSTDIR\Удалить.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\Zubofreza_5K32A.exe" "" "$INSTDIR\app.ico"
  WriteRegStr HKLM "Software\SpecTehZakaz\Zubofreza5K32A" "Dir" "$INSTDIR"
  WriteRegStr HKLM "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKLM "${UNKEY}" "Publisher" "${PUB}"
  WriteRegStr HKLM "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKLM "${UNKEY}" "DisplayIcon" "$INSTDIR\app.ico"
  WriteRegStr HKLM "${UNKEY}" "UninstallString" '"$INSTDIR\Удалить.exe"'
  WriteRegDWORD HKLM "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\Zubofreza_5K32A.exe"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\Удалить.exe"
  RMDir "$INSTDIR"
  RMDir "$PROGRAMFILES64\СпецТехЗаказ"
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Удалить.lnk"
  RMDir "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  DeleteRegKey HKLM "${UNKEY}"
  DeleteRegKey HKLM "Software\SpecTehZakaz"
  ; папка «Документы\Зубофреза 5К32А\Детали» с вашими деталями НЕ удаляется
SectionEnd
