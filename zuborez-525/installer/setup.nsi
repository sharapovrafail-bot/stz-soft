Unicode true
!define APP "Зуборез 525"
!define PUB "ООО «СпецТехЗаказ»"
!define VER "0.7.3"
!define EXE "Zuborez_525.exe"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Zuborez525"
Name "${APP} ${VER}"
OutFile "Ustanovka_Zuborez_525_v0.7.3.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP}"
InstallDirRegKey HKCU "Software\SpecTehZakaz\Zuborez525" "Dir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Сделано специально по заказу ${PUB} · Рафаил Шарапов · rafail@sharapov-cnc.ru"
VIProductVersion "${VER}.0"
VIAddVersionKey /LANG=1049 "ProductName" "${APP}"
VIAddVersionKey /LANG=1049 "CompanyName" "${PUB}"
VIAddVersionKey /LANG=1049 "FileDescription" "Установка: ${APP}"
VIAddVersionKey /LANG=1049 "FileVersion" "${VER}"
VIAddVersionKey /LANG=1049 "LegalCopyright" "Сделано специально по заказу ${PUB}"

!include MUI2.nsh
!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "header.bmp"
!define MUI_WELCOMEPAGE_TITLE "Сделано специально по заказу$\r$\n${PUB}"
!define MUI_WELCOMEPAGE_TITLE_3LINES
!define MUI_WELCOMEPAGE_TEXT "Программа наладки зуборезного полуавтомата 525 для конических и гипоидных колёс с круговым зубом: геометрия пары, угол бабки, эксцентрик и люлька, сменные шестерни деления, обкатки, скорости и подачи, карта наладки.$\r$\nНовое в 0.7: шаги наладки по «Руководству к станку 525» (люлька по риске «центр обкатки»/«конец врезания», эксцентрик, бабка, центр станка, разделение припуска), место ведущей шестерни скорости (правое/левое резание), смещение стола по нониусу 0,02, давление зажима по таблице 2, свои головки, время партии, фото пятна в удачных наладках. В 0.6: гитары скорости и подачи по табличкам вашего станка (скорость — две шестерни A/B, A+B=101; подача — 4…75 с, два варианта колёс), новые фото гитар, табличек, механизма врезания и гидравлики. В 0.5: конические пары по ГОСТ 19326 с чертежа (xn, xτ, форма II, двусторонний развод). В 0.4: ГИПОИДНЫЕ пары (методика СЗТЗС 1967: геометрия, головки, все установки станка, поправки по пятну). В 0.3: пошаговая наладка с фото и стрелками, отсчёты «шкала + нониус», режим «у станка», карты А4 на каждый проход, мастер пятна контакта, удачные наладки, подбор гитар только из своего набора.$\r$\n$\r$\nВЕРСИЯ ${VER} (проверяйте по пятну контакта).$\r$\nНажмите «Далее» для установки."
!define MUI_FINISHPAGE_TEXT "Программа «${APP}», версия ${VER}, установлена в: $INSTDIR. Ярлык — на рабочем столе и в меню «Пуск». Пары сохраняются в папке «Документы\Зуборез 525\Детали»."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить программу"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Section "Install"
  nsExec::Exec 'taskkill /F /IM ${EXE}'
  Sleep 800
  SetOutPath "$INSTDIR"
  File "${EXE}"
  File "app.ico"
  WriteUninstaller "$INSTDIR\Удалить.exe"
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\app.ico"
  CreateShortcut "$SMPROGRAMS\${APP}\Удалить.lnk" "$INSTDIR\Удалить.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\app.ico"
  WriteRegStr HKCU "Software\SpecTehZakaz\Zuborez525" "Dir" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "${PUB}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\app.ico"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Удалить.exe"'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM ${EXE}'
  Sleep 500
  Delete "$INSTDIR\${EXE}"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\Удалить.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Удалить.lnk"
  RMDir "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\SpecTehZakaz\Zuborez525"
  ; пары в «Документы\Зуборез 525\Детали» НЕ удаляются
SectionEnd
