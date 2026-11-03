#!/bin/bash
# STZ WireCut — сборка двух вариантов и установщиков в Linux (Ubuntu / WSL)
#   sudo apt install mono-devel nsis
#   ./build.sh            — собрать exe (out7 — Windows 7, out10 — Windows 10/11)
#   ./build.sh setup      — ещё и установщики в dist/
set -e
cd "$(dirname "$0")"
mkdir -p out7 out10
A=/usr/lib/mono/2.0-api
mcs -nostdlib -noconfig -sdk:2 -langversion:7 -target:winexe -optimize+ -win32icon:src/app.ico -codepage:utf8 \
    -r:$A/mscorlib.dll -r:$A/System.dll -r:$A/System.Windows.Forms.dll -r:$A/System.Drawing.dll \
    -resource:src/logo.png,WireCut.logo.png -out:out7/STZ_WireCut.exe src/*.cs 2>&1 | grep -v warning || true
cp src/app.config out7/STZ_WireCut.exe.config
B=/usr/lib/mono/4.8-api
mcs -nostdlib -noconfig -sdk:4.5 -langversion:7 -define:NET48 -target:winexe -optimize+ -win32icon:src/app.ico -codepage:utf8 \
    -r:$B/mscorlib.dll -r:$B/System.dll -r:$B/System.Windows.Forms.dll -r:$B/System.Drawing.dll \
    -resource:src/logo.png,WireCut.logo.png -out:out10/STZ_WireCut.exe src/*.cs 2>&1 | grep -v warning || true
cp src/app48.config out10/STZ_WireCut.exe.config
echo "exe собраны: out7/ и out10/"
if [ "$1" = "setup" ]; then
  VER=$(grep "!define VER" setup.nsi | cut -d\" -f2)
  (cd tools && python3 make_manual.py "$VER")
  export LANG=C.UTF-8 LC_ALL=C.UTF-8
  makensis -V2 -DED=Win7 -DSRC=out7 setup.nsi
  makensis -V2 -DED=Win10-11 -DSRC=out10 setup.nsi
  echo "установщики: dist/"
fi
