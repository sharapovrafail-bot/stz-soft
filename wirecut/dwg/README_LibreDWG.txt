Конвертер DWG → DXF для STZ WireCut
==================================
Программа: GNU LibreDWG 0.14 (dwg2dxf.exe, libredwg-0.dll), сборка win32 с официальной страницы проекта:
  https://github.com/LibreDWG/libredwg/releases/tag/0.14  (файл libredwg-0.14-win32.zip)
Библиотеки времени выполнения MinGW: libgcc_s_dw2-1.dll (из того же архива), libwinpthread-1.dll (mingw-w64).
Лицензия: GNU General Public License версии 3 или новее — текст в COPYING.txt.
Исходный код LibreDWG: https://github.com/LibreDWG/libredwg/releases/download/0.14/libredwg-0.14.tar.xz
                       https://www.gnu.org/software/libredwg/
Файлы не изменялись. STZ WireCut запускает dwg2dxf.exe отдельной программой:
  dwg2dxf.exe -y --as r2000 -o выход.dxf вход.dwg
Если установлен бесплатный ODA File Converter (opendesign.com), STZ WireCut использует его первым.
