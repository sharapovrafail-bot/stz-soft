# -*- mode: python ; coding: utf-8 -*-
# Библиотеки FANUC FOCAS (Fwlib64.dll, fwlibe64.dll) — файлы FANUC, в репозиторий не входят.
# Если положить их рядом с этим файлом, они войдут в exe; иначе программа ищет их рядом с собой и в C:\\PTT.
import os
FOCAS = [(n, '.') for n in ('Fwlib64.dll', 'fwlibe64.dll') if os.path.exists(n)]


a = Analysis(
    ['program_transfer_tool.py'],
    pathex=[],
    binaries=FOCAS,
    datas=[('stz.ico', '.')],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='STZ_FANUC_LAN_1',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=['stz.ico'],
)
