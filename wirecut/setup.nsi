Unicode true
SetCompressor /SOLID lzma
!define APP "STZ WireCut"
!define VER "3.4"
Name "${APP} ${VER} (${ED})"
!ifndef ED
!define ED "Win7"
!endif
!ifndef SRC
!define SRC "out7"
!endif
OutFile "dist\STZ_WireCut_Setup_${VER}_${ED}.exe"
!if "${ED}" == "Win10-11"
InstallDir "$PROGRAMFILES64\STZ WireCut"
!else
InstallDir "$PROGRAMFILES\STZ WireCut"
!endif
RequestExecutionLevel admin
Icon "src\app.ico"
UninstallIcon "src\app.ico"
BrandingText "Сделано специально по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru"
!include "MUI2.nsh"
!define MUI_ICON "src\app.ico"
!define MUI_UNICON "src\app.ico"
!define MUI_WELCOMEPAGE_TITLE "Установка ${APP} ${VER} для ${ED}"
!define MUI_WELCOMEPAGE_TEXT "Программа подготовки управляющих программ для электроэрозионных проволочно-вырезных станков (Wire Cut).$\r$\n$\r$\nИмпорт DXF из AutoCAD и Fusion 360, черчение с размерами как во Fusion, история изменений (Ctrl+Z / Ctrl+Y), ручные вход / выход проволоки, библиотека по ГОСТ (шестерни, шлицы, шпоночные пазы с радиусами), приём колёс из «STZ Расчёт зубьев», расчёт смещения на проволоку, форматы ISO и 3B (HL / HF / AutoCut), симуляция, передача на станок одной кнопкой «На станок».$\r$\n$\r$\nСделано специально по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru."
!define MUI_FINISHPAGE_RUN "$INSTDIR\STZ_WireCut.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить STZ WireCut"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Function .onInit
!if "${ED}" == "Win10-11"
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 528040 ok 0 ok
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "Не найден .NET Framework 4.8.$\r$\nВ Windows 10 (с 2019 г.) и Windows 11 он встроен. Если это Windows 7 — используйте установщик Win7.$\r$\n$\r$\nПродолжить установку?" IDYES ok
    Abort
  ok:
!else
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5" "Install"
  ReadRegDWORD $1 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Install"
  IntOp $2 $0 | $1
  IntCmp $2 1 ok 0 ok
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "Не найден .NET Framework 3.5 или 4.x.$\r$\nВ Windows 7 включите: Панель управления → Программы → Включение компонентов Windows → Microsoft .NET Framework 3.5.1.$\r$\n$\r$\nПродолжить установку?" IDYES ok
    Abort
  ok:
!endif
FunctionEnd

Section "Main"
  SetOutPath "$INSTDIR"
  File "${SRC}\STZ_WireCut.exe"
  File "${SRC}\STZ_WireCut.exe.config"
  File "dist\Инструкция.txt"
  File "manual\Руководство.html"
  SetOutPath "$INSTDIR\img"
  File "manual\img\*.jpg"
  SetOutPath "$INSTDIR"
  File "test\plate.dxf"
  File "src\app.ico"
  ; конвертер DWG → DXF (GNU LibreDWG 0.14, GPL v3)
  SetOutPath "$INSTDIR\dwg"
  File "dwg\dwg2dxf.exe"
  File "dwg\libredwg-0.dll"
  File "dwg\libgcc_s_dw2-1.dll"
  File "dwg\libwinpthread-1.dll"
  File "dwg\COPYING.txt"
  File "dwg\README_LibreDWG.txt"
  SetOutPath "$INSTDIR"
  ; DWG: только «Открыть с помощью → STZ WireCut», AutoCAD остаётся программой по умолчанию
  WriteRegStr HKCR ".dwg\OpenWithProgids" "STZWireCut.Drawing" ""
  WriteRegStr HKCR "STZWireCut.Drawing" "" "Чертёж для эрозии (STZ WireCut)"
  WriteRegStr HKCR "STZWireCut.Drawing\DefaultIcon" "" "$INSTDIR\app.ico,0"
  WriteRegStr HKCR "STZWireCut.Drawing\shell\open\command" "" '"$INSTDIR\STZ_WireCut.exe" "%1"'
  WriteUninstaller "$INSTDIR\Удалить.exe"
  ; файлы колёс из «STZ Расчёт зубьев» открываются двойным щелчком
  WriteRegStr HKCR ".stzgear" "" "STZWireCut.Gear"
  WriteRegStr HKCR "STZWireCut.Gear" "" "Колесо из STZ Расчёт зубьев (для эрозии)"
  WriteRegStr HKCR "STZWireCut.Gear\DefaultIcon" "" "$INSTDIR\app.ico,0"
  WriteRegStr HKCR "STZWireCut.Gear\shell\open\command" "" '"$INSTDIR\STZ_WireCut.exe" "%1"'
  ; старые ярлыки удаляем и создаём заново — иначе Windows показывает старую иконку из кэша
  Delete "$DESKTOP\STZ WireCut.lnk"
  Delete "$SMPROGRAMS\STZ WireCut\STZ WireCut.lnk"
  CreateDirectory "$SMPROGRAMS\STZ WireCut"
  CreateShortcut "$SMPROGRAMS\STZ WireCut\STZ WireCut.lnk" "$INSTDIR\STZ_WireCut.exe" "" "$INSTDIR\app.ico" 0
  CreateShortcut "$SMPROGRAMS\STZ WireCut\Инструкция.lnk" "$INSTDIR\Инструкция.txt"
  CreateShortcut "$SMPROGRAMS\STZ WireCut\Руководство пользователя.lnk" "$INSTDIR\Руководство.html"
  CreateShortcut "$SMPROGRAMS\STZ WireCut\Удалить.lnk" "$INSTDIR\Удалить.exe"
  CreateShortcut "$DESKTOP\STZ WireCut.lnk" "$INSTDIR\STZ_WireCut.exe" "" "$INSTDIR\app.ico" 0
  ; обновить кэш значков Windows (новая иконка сразу на рабочем столе и в «Пуске»)
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0x1000, p 0, p 0)'
  IfFileExists "$SYSDIR\ie4uinit.exe" 0 +3
    nsExec::Exec '"$SYSDIR\ie4uinit.exe" -show'
    nsExec::Exec '"$SYSDIR\ie4uinit.exe" -ClearIconCache'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut" "DisplayName" "${APP} ${VER} (${ED})"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut" "UninstallString" "$INSTDIR\Удалить.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut" "DisplayIcon" "$INSTDIR\STZ_WireCut.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut" "Publisher" "ООО «СпецТехЗаказ»"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut" "DisplayVersion" "${VER}"
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\STZ_WireCut.exe"
  Delete "$INSTDIR\STZ_WireCut.exe.config"
  Delete "$INSTDIR\Инструкция.txt"
  Delete "$INSTDIR\Руководство.html"
  RMDir /r "$INSTDIR\img"
  RMDir /r "$INSTDIR\dwg"
  DeleteRegValue HKCR ".dwg\OpenWithProgids" "STZWireCut.Drawing"
  DeleteRegKey HKCR "STZWireCut.Drawing"
  Delete "$INSTDIR\plate.dxf"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\Удалить.exe"
  RMDir "$INSTDIR"
  Delete "$DESKTOP\STZ WireCut.lnk"
  RMDir /r "$SMPROGRAMS\STZ WireCut"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\STZWireCut"
  DeleteRegKey HKCR ".stzgear"
  DeleteRegKey HKCR "STZWireCut.Gear"
SectionEnd
