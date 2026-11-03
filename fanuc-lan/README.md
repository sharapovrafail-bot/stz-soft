# STZ & FANUC LAN

Передача УП между компьютером и стойками Fanuc: по сети (FOCAS) и по RS232. Python + tkinter, сборка PyInstaller.

Установлена на компьютере в `C:\PTT\dist\STZ_FANUC_LAN_1.exe`, настройки — `C:\PTT\dist\ptt_settings.json` (список станков и IP). Эти же настройки читает «Симулятор УП Fanuc» (`../simulyator-up/`) для кнопки «На станок».

## Сборка (Windows)
```
pip install pyserial pyinstaller
copy C:\PTT\Fwlib64.dll .  &  copy C:\PTT\fwlibe64.dll .     # библиотеки FANUC в репозиторий не входят
pyinstaller STZ_FANUC_LAN.spec
```
