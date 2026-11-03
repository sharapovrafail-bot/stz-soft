# STZ WireCut 3.3 — описание для разработчика

Программа подготовки управляющих программ (УП) для электроэрозионного проволочно-вырезного станка.
Сделано специально по заказу ООО «СпецТехЗаказ».

## Что это
Windows-приложение (C# WinForms, без сторонних библиотек), одно исполняемое `STZ_WireCut.exe` ≈ 200 КБ.
Своё черчение «как в AutoCAD / Fusion 360» → траектория проволоки → УП в форматах **3B** (AutoCut / HL / HF) и **ISO** → передача на станок.

## Два варианта из одних исходников
| Вариант | Платформа | Символ | Особенности |
|---|---|---|---|
| Win7 | .NET Framework 3.5 (CLR 2.0) | — | работает на ПК станка с Windows 7 |
| Win10/11 | .NET Framework 4.8 | `NET48` | DPI-aware, шрифт Segoe UI, скругления |

**Правило совместимости:** вне блоков `#if NET48` нельзя использовать API новее .NET 2.0:
нет LINQ, System.Core, HashSet, Func/Action с 2+ параметрами (есть свои делегаты `Act2`, `Fn`, `Fn1` в Geom.cs),
Tuple, async/await, `string.IsNullOrWhiteSpace`, `Enum.TryParse`, методов-расширений. Язык — C# ≤ 7.

## Сборка
### Linux / WSL (так собираются выпуски)
```
sudo apt install mono-devel nsis
./build.sh          # out7/ и out10/
./build.sh setup    # + руководство и оба установщика в dist/
```
Компилятор — `mcs` с ссылочными сборками `/usr/lib/mono/2.0-api` и `/usr/lib/mono/4.8-api`.
### Windows
`STZ_WireCut.csproj` — для Visual Studio 2019+ или `dotnet build -c Release` (нужен доступ к NuGet для
Microsoft.NETFramework.ReferenceAssemblies). Проект не проверялся — выпуски собираются через build.sh.
Установщики — NSIS 3 (`makensis -DED=Win7 -DSRC=out7 setup.nsi`).

## Файлы
| Файл | Что внутри |
|---|---|
| `Program.cs` | вход, InvariantCulture (точка в числах), один экземпляр (Mutex), DPI |
| `MainForm.cs` | главное окно: меню, лента, свойства, командная строка, симуляция, команды `Run()` |
| `CadCanvas.cs` | чертёжное поле: зум/панорама, привязки, рамки, отрисовка, линейки, размеры |
| `Geom.cs` | V2, Seg (отрезок/дуга: центр, R, A0, Sw), Contour, сборка контуров, **эквидистанта**, пересечения |
| `Dxf.cs` | чтение DXF (LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE, ELLIPSE; Z=-1 Fusion; $INSUNITS), запись R12 |
| `Job.cs` | JobSettings (все свойства, сохранение ini), режимы проходов, профиль станка, **Toolpath.Build** (порядок, сторона, заходы, проходы, перемычка) |
| `Post.cs` | постпроцессоры **ISO** и **3B**, карта наладки |
| `Regimes.cs` | таблица режимов материал × толщина (regimes.csv), окно «Режимы резания» |
| `Library.cs` | шестерня / внутренний венец (эвольвента), шпоночный паз ГОСТ 23360 (с радиусами r в углах дна, `Gost23360.RRange`, `KeyParams.WireLimit`), шлицы ГОСТ 1139 / 6033 |
| `Sketch.cs` | Отрезок, Прямоугольник, Круг, Дуга с полями размеров (Tab / Enter) как во Fusion |
| `Dims.cs` | размеры (линейный/радиус/диаметр/угол), привязка к объектам, линейки, габарит, подсказка |
| `DimEdit.cs` | изменение детали через щелчок по размеру (растяжение, длина, R/Ø) |
| `Commands.cs` | базовый `Cmd`, выбор, стереть/перенести/копировать/повернуть/зеркало/масштаб, заправка, сторона захода |
| `Tools.cs` | подобие, обрезать, удлинить, сопряжение, фаска, массив, проверка и починка чертежа |
| `PostForm.cs` | окно УП, сохранение, отправка (папка AutoCut / COM-порт) |
| `CodeEditor.cs` | редактор УП с номерами строк и подсветкой (TOM RichEdit, в try/catch) |
| `AcadLink.cs` | LISP-команда WCUT для AutoCAD, приём контуров через %APPDATA%\STZ_WireCut\acad_exchange.dxf |
| `Theme.cs` | тёмная тема, иконки (рисуются кодом), заставка |
| `History.cs` | история как во Fusion 360: снимки чертежа (объекты, размеры с привязкой, точки заправки, стороны захода), отмена/повтор, шкала `HistoryBar`; шаг пишется после действия (`EndCmd`, конец `Run`, таймер-страховка 0.4 с) |
| `Leads.cs` | `LeadMark` (вход / заправка / выход / сход для контура), команда `LeadCmd` (ВХОД), `OriginCmd` (НОЛЬ), отрисовка нуля и значков входа/выхода; в `Toolpath.Build` — перекрытие (`Contour.Upto`) или перемычка до выхода, сход |
| `Ribbon.cs` | лента с вкладками (страницы `ribbonPages`), сворачивание ленты, скрытие панели свойств, сохранение вида окна в `%APPDATA%\STZ_WireCut\ui.ini` |
| `app.ico`, `logo.png` | значок (16…256 px) и логотип заставки — встроен ресурсом `WireCut.logo.png` (`-resource` в build.sh); исходная картинка — `art/icon_original_ChatGPT.png` |
| `StzLink.cs` | приём колеса из «STZ Расчёт зубьев»: `MiniJson` (разбор JSON для .NET 2.0), `StzGear` (формат `STZ-WireCut-gear` v1), `ImportStz`, открытие файла в уже запущенной программе (`open_request.txt` + FileSystemWatcher) |
| `Machine.cs` | «На станок» (`SendToMachine`): BuildPath → Post.Make → запись в `S.Folder` (AutoCut Work) / флешку / RS-232 без окна УП |
| `Tips.cs` | тексты подсказок к кнопкам — **из них же генерируется руководство** (`tools/make_manual.py`) |

## Ключевые соглашения
- Дуга: `A0` — начальный угол, `Sw` — раствор со знаком (+ против часовой). Полная окружность — |Sw| = 2π.
- Смещение проволоки = Ø/2 + искровой зазор + припуск ± коррекция размера; знак: + влево по ходу.
- Внутренние контуры (вложенность) режутся первыми и как матрица; `ContourJob.Side` — ручная сторона.
- 3B: мкм, `B x B y B J G Z` (GX/GY, L1–L4, SR1–4 / NR1–4), файл на контур, `DD` в конце.
- ISO: N10 шаг 10, G92 в точке заправки, G02/G03 с I J.
- Настройки пользователя: `%APPDATA%\STZ_WireCut\settings.ini`, `regimes.csv`, `STZ_WIRECUT.lsp`.

## Тесты
`test/*.cs` — консольные проверки (геометрия, 3B/ISO, шестерни, шлицы, починка, заходы). Пример:
```
A=/usr/lib/mono/2.0-api
mcs -nostdlib -noconfig -sdk:2 -langversion:7 -r:$A/mscorlib.dll -r:$A/System.dll -r:$A/System.Windows.Forms.dll -r:$A/System.Drawing.dll -out:t.exe test/L.cs src/*.cs -main:L && mono t.exe
```
