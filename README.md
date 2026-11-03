# Программы STZ — бесплатно для всех

Программы для металлообработки и наладки станков. **Без лицензии, пробного периода и ключей** — ставьте и пользуйтесь.

Готовые файлы — во вкладке **[Releases → latest](../../releases/latest)**. Собираются автоматически на компьютерах GitHub после каждого изменения.

| Программа | Что делает | Windows | macOS |
|---|---|---|---|
| **STZ Расчёт зубьев** | расчёт зубчатых (в т.ч. гипоидных), червячных, цепных, ремённых и шлицевых передач | `STZ-Setup-….exe` | `STZ-Raschet-zubev-mac-….dmg` |
| ↳ Зубофреза 5К32А | наладка зубофрезерного 5К32А: гитары, фреза, режимы, карта наладки | входит в STZ-Setup | входит в STZ |
| ↳ Зуборез 525 | наладка зуборезного 525: конические и гипоидные колёса с круговым зубом | входит в STZ-Setup | входит в STZ |
| **Симулятор УП Fanuc** | проверка УП для KVL 1000 (Fanuc 0i-MF) и PUMA 3050 до запуска на станке | `Simulyator_UP_Fanuc_Setup_….exe` | `Simulyator-UP-Fanuc-mac-….dmg` |
| **STZ FANUC LAN** | передача УП на стойки Fanuc по сети (FOCAS) и RS-232 | `STZ_FANUC_LAN-win.zip` | `STZ-FANUC-LAN-mac-….dmg` (только RS-232) |
| **STZ WireCut** | программы для проволочно-вырезного электроэрозионного станка | `STZ_WireCut_Setup_…_Win10-11.exe`, `…_Win7.exe` | — |

Просмотрщик моделей и чертежей **RafCAD View** — в отдельном репозитории `modelview`.

**macOS:** `arm64` — Mac на Apple M1–M4, `x64` — Mac на Intel. Программы без подписи Apple: первый запуск — правой кнопкой → «Открыть» → «Открыть».

**Передача УП на станок по сети** (Симулятор и STZ FANUC LAN) работает только в Windows и нужны библиотеки FANUC FOCAS `Fwlib64.dll` и `fwlibe64.dll`. Это файлы FANUC, в программы они не входят: положите их в `C:\PTT` (или рядом с программой).

## Исходники
| Папка | Технология |
|---|---|
| `stz/` | Electron (Node.js) |
| `naladka-5k32a/`, `zuborez-525/` | Go + встроенный веб-интерфейс |
| `simulyator/` | Electron + koffi (FOCAS) |
| `fanuc-lan/` | Python + tkinter, PyInstaller |
| `wirecut/` | C# WinForms (.NET Framework), Mono + NSIS |

Сборка — `.github/workflows/build.yml`.

Автор — Рафаил Шарапов, rafail@sharapov-cnc.ru. Сделано специально по заказу ООО «СпецТехЗаказ». Лицензия MIT (см. `LICENSE`). В STZ WireCut входит GNU LibreDWG (`wirecut/dwg`, GPL v3, без изменений, запускается отдельной программой).
