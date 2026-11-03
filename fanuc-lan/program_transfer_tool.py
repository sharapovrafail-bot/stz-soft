import ctypes
import json
import os
import queue
import re
import string
import struct
import sys
import threading
import time
import tkinter as tk
from ctypes import Structure, byref, c_char, c_long, c_short, c_ulong, c_ushort, create_string_buffer
from tkinter import filedialog, messagebox, scrolledtext, ttk

import serial
import serial.tools.list_ports

APP_DIR = os.path.dirname(os.path.abspath(sys.argv[0]))
if sys.platform == "darwin" and ".app/Contents/" in APP_DIR:
    # на Mac настройки хранятся в папке пользователя (внутрь программы писать нельзя)
    _DATA = os.path.join(os.path.expanduser("~"), "Library", "Application Support", "STZ FANUC LAN")
    os.makedirs(_DATA, exist_ok=True)
    CONFIG_PATH = os.path.join(_DATA, "ptt_settings.json")
else:
    CONFIG_PATH = os.path.join(APP_DIR, "ptt_settings.json")

BAUD_RATES = [300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200]
DATA_BITS = {"7": serial.SEVENBITS, "8": serial.EIGHTBITS}
PARITY = {"None": serial.PARITY_NONE, "Even": serial.PARITY_EVEN, "Odd": serial.PARITY_ODD}
STOP_BITS = {"1": serial.STOPBITS_ONE, "2": serial.STOPBITS_TWO}
FLOW_CONTROL = ["XON/XOFF", "RTS/CTS", "None"]

APP_NAME = "STZ & FANUC LAN"
APP_VERSION = "1.0.0"
COMPANY = "ООО «СпецТехЗаказ»"

C = {
    "dark": "#1c1c1c",
    "dark2": "#2b2b2b",
    "dark3": "#3a3a3a",
    "orange": "#e8590c",
    "orange_h": "#ff6d1f",
    "orange_p": "#c94a08",
    "bg": "#f2f2ef",
    "panel": "#ffffff",
    "border": "#dcdcd8",
    "head": "#ecece8",
    "text": "#1f1f1f",
    "muted": "#6e6e6e",
    "ok": "#3ddc84",
    "bad": "#ff6b6b",
    "wait": "#ffb347",
    "tip": "#e8f0fb",
    "tip_border": "#c4d6f0",
    "tip_text": "#2c4a73",
}
UI_FONT = ("Segoe UI", 10)

DEFAULTS = {
    "port": "",
    "baud": 4800,
    "data_bits": "7",
    "parity": "Even",
    "stop_bits": "2",
    "flow": "XON/XOFF",
    "wrap_percent": True,
    "line_delay_ms": 0,
    "machines": [],
    "machine_index": 0,
    "pc_last_dir": "",
    "focas_lib": "",
}


def load_settings():
    merged = DEFAULTS.copy()
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                merged.update(json.load(f))
        except Exception:
            pass
    if not merged["machines"]:
        merged["machines"] = [{"name": "KVL-1000", "ip": merged.get("focas_ip") or "192.168.1.100", "port": 8193}]
    return merged


def save_settings(settings):
    try:
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(settings, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def human_size(n):
    try:
        n = int(n)
    except (TypeError, ValueError):
        return ""
    if n < 1024:
        return f"{n} Б"
    if n < 1024 * 1024:
        return f"{n / 1024:.1f} КБ"
    return f"{n / 1024 / 1024:.1f} МБ"


def prepare_program(path, wrap_percent):
    with open(path, "rb") as f:
        text = f.read().decode("ascii", errors="replace")
    lines = [ln.rstrip("\r") for ln in text.split("\n")]
    lines = [ln for ln in lines if ln.strip() != ""]
    if wrap_percent:
        if not lines or lines[0].strip() != "%":
            lines.insert(0, "%")
        if lines[-1].strip() != "%":
            lines.append("%")
    return lines


def build_focas_payload(path):
    with open(path, "rb") as f:
        text = f.read().decode("ascii", errors="replace")
    lines = [ln.rstrip("\r") for ln in text.split("\n")]
    lines = [ln for ln in lines if ln.strip() and ln.strip() != "%"]
    if not lines:
        raise ValueError("файл пуст")
    added = None
    first = lines[0].lstrip()
    if not (first[:1].upper() == "O" or first.startswith("<")):
        stem = os.path.splitext(os.path.basename(path))[0]
        added = f"O{stem.zfill(4)}" if stem.isdigit() else f"<{stem}>"
        lines.insert(0, added)
    payload = ("\n" + "\n".join(lines) + "\n%").encode("ascii", errors="replace")
    return payload, lines[0], added


def program_name_from_header(line):
    m = re.match(r"\s*(O\d+)", line, re.I)
    if m:
        return m.group(1).upper()
    m = re.match(r"\s*<([^>]+)>", line)
    if m:
        return m.group(1)
    return None


# ======================= FOCAS =======================

EW_OK = 0
EW_NUMBER = 3
EW_BUFFER = 10

FOCAS_ERRORS = {
    1: "функция не выполнена / не поддерживается этой стойкой",
    2: "ошибка длины данных",
    3: "ошибка номера",
    4: "ошибка атрибута",
    5: "ошибка данных (возможно, программа с таким номером уже есть или ошибка в тексте)",
    6: "нет нужной опции на стойке",
    7: "защита от записи (ключ защиты памяти / параметр защиты)",
    8: "переполнение памяти стойки",
    9: "ошибка параметра CNC",
    11: "неверный путь на стойке",
    12: "неверный режим стойки (переключите в EDIT)",
    13: "стойка отклонила запрос (идёт обработка, аларм или фоновое редактирование)",
    15: "на стойке аларм",
    17: "защита паролем",
    -1: "стойка занята",
    -2: "передача прервана RESET/STOP на стойке",
    -8: "соединение потеряно — переподключитесь",
    -15: "не найдена библиотека fwlibe64.dll (должна лежать рядом с Fwlib64.dll)",
    -16: "нет связи со стойкой (ошибка сокета)",
    -17: "ошибка протокола",
}


class FocasError(Exception):
    def __init__(self, msg, code=None):
        super().__init__(msg)
        self.code = code


class ODBPDFDRV(Structure):
    _fields_ = [("max_num", c_short), ("dummy", c_short), ("drive", (c_char * 12) * 16)]


class IDBPDFADIR(Structure):
    _fields_ = [
        ("path", c_char * 212),
        ("req_num", c_short),
        ("size_kind", c_short),
        ("type", c_short),
        ("dummy", c_short),
    ]


class ODBPDFADIR(Structure):
    _fields_ = [
        ("data_kind", c_short),
        ("year", c_short),
        ("mon", c_short),
        ("day", c_short),
        ("hour", c_short),
        ("min", c_short),
        ("sec", c_short),
        ("dummy", c_short),
        ("dummy2", c_long),
        ("size", c_long),
        ("attr", c_ulong),
        ("d_f", c_char * 36),
        ("comment", c_char * 52),
        ("o_time", c_char * 12),
    ]


def find_focas_dlls(custom=None):
    is64 = struct.calcsize("P") == 8
    names = ["Fwlib64.dll", "fwlibe64.dll"] if is64 else ["Fwlib32.dll", "fwlibe1.dll"]
    dirs = []
    if custom and os.path.isfile(custom):
        dirs.append(os.path.dirname(custom))
    if getattr(sys, "_MEIPASS", None):
        dirs.append(sys._MEIPASS)
    dirs += [APP_DIR, os.path.dirname(os.path.abspath(__file__)), r"C:\PTT\dist", r"C:\PTT"]

    result = []
    if custom and os.path.isfile(custom):
        result.append(custom)
    for d in dirs:
        for n in names:
            p = os.path.join(d, n)
            if os.path.isfile(p) and os.path.normcase(p) not in [os.path.normcase(x) for x in result]:
                result.append(p)
    return result


class FocasClient:
    CHUNK = 1280

    def __init__(self, dll_path):
        dll_dir = os.path.dirname(os.path.abspath(dll_path))
        try:
            ctypes.windll.kernel32.SetDllDirectoryW(dll_dir)
        except Exception:
            pass
        try:
            os.add_dll_directory(dll_dir)
        except Exception:
            pass
        self.dll_path = dll_path
        self.dll = ctypes.WinDLL(dll_path)
        self.h = c_ushort(0)
        self.connected = False

    def _call(self, fname, *args):
        try:
            func = getattr(self.dll, fname)
        except AttributeError:
            raise FocasError(f"{fname}: функция отсутствует в {os.path.basename(self.dll_path)}")
        func.restype = c_short
        return func(*args)

    def _check(self, fname, ret):
        if ret != EW_OK:
            raise FocasError(f"{fname}: {FOCAS_ERRORS.get(ret, 'ошибка')} (код {ret})", ret)

    def connect(self, ip, port, timeout_sec=10):
        ret = self._call("cnc_allclibhndl3", ip.encode("ascii"), c_ushort(port), c_long(timeout_sec), byref(self.h))
        self._check("Подключение", ret)
        self.connected = True

    def disconnect(self):
        if self.connected:
            try:
                self._call("cnc_freelibhndl", self.h)
            except Exception:
                pass
            self.connected = False

    def drives(self):
        drv = ODBPDFDRV()
        self._check("Чтение дисков стойки", self._call("cnc_rdpdf_drive", self.h, byref(drv)))
        out = []
        for i in range(max(0, min(drv.max_num, 16))):
            name = drv.drive[i].value.decode("ascii", errors="replace").strip()
            if name:
                out.append(name)
        return out

    def count_dir(self, path):
        inp = IDBPDFADIR()
        inp.path = path.encode("ascii", errors="replace")
        out = (c_short * 32)()
        try:
            ret = self._call("cnc_rdpdf_subdirn", self.h, byref(inp), out)
        except FocasError:
            return None
        if ret != EW_OK:
            return None
        return max(0, out[0]) + max(0, out[1])

    def _read_batch(self, path, start, batch):
        inp = IDBPDFADIR()
        inp.path = path.encode("ascii", errors="replace")
        inp.req_num = start
        inp.size_kind = 1
        inp.type = 1
        out = (ODBPDFADIR * batch)()
        num = c_short(batch)
        ret = self._call("cnc_rdpdf_alldir", self.h, byref(num), byref(inp), out)
        items = []
        if ret == EW_OK:
            for i in range(max(0, min(num.value, batch))):
                o = out[i]
                name = o.d_f.decode("ascii", errors="replace").strip()
                if not name:
                    continue
                date = ""
                if o.year:
                    date = f"{o.year:04d}-{o.mon:02d}-{o.day:02d} {o.hour:02d}:{o.min:02d}"
                items.append({
                    "name": name,
                    "is_dir": o.data_kind == 0,
                    "size": o.size,
                    "comment": o.comment.decode("ascii", errors="replace").strip(),
                    "date": date,
                })
        return ret, items, num.value

    def list_dir(self, path):
        total = self.count_dir(path)
        if total == 0:
            return []
        batch = 10
        last_ret = EW_OK
        for base in (0, 1):
            entries = []
            start = base
            failed = False
            for _ in range(1000):
                ret, items, n = self._read_batch(path, start, batch)
                if ret == EW_NUMBER:
                    if entries:
                        break
                    failed = True
                    last_ret = ret
                    break
                self._check(f"Чтение папки {path}", ret)
                entries += items
                if n < batch or (total is not None and len(entries) >= total):
                    break
                start += n
            if not failed:
                return entries
        if total is None and last_ret == EW_NUMBER:
            return []
        self._check(f"Чтение папки {path}", last_ret)
        return []

    def read_program(self, path, progress=None):
        self._check(f"Начало чтения {path}", self._call("cnc_upstart4", self.h, c_short(0), path.encode("ascii")))
        data = bytearray()
        ok = False
        try:
            stalls = 0
            while True:
                buf = create_string_buffer(self.CHUNK + 1)
                length = c_long(self.CHUNK)
                ret = self._call("cnc_upload4", self.h, byref(length), buf)
                if ret == EW_BUFFER:
                    stalls += 1
                    if stalls > 600:
                        raise FocasError("Стойка не отдаёт данные (таймаут)")
                    time.sleep(0.05)
                    continue
                self._check("Чтение программы", ret)
                n = length.value
                if n > 0:
                    stalls = 0
                    data += buf.raw[:n]
                    if progress:
                        progress(len(data))
                else:
                    stalls += 1
                    if stalls > 600:
                        break
                    time.sleep(0.02)
                stripped = bytes(data).strip()
                if len(stripped) > 1 and stripped.endswith(b"%"):
                    break
            ok = True
        finally:
            ret = self._call("cnc_upend4", self.h)
            if ok:
                self._check("Завершение чтения", ret)
        return bytes(data)

    def write_program(self, folder, payload, progress=None):
        self._check(f"Начало записи в {folder}", self._call("cnc_dwnstart4", self.h, c_short(0), folder.encode("ascii")))
        ok = False
        try:
            pos = 0
            total = len(payload)
            stalls = 0
            while pos < total:
                chunk = payload[pos:pos + self.CHUNK]
                length = c_long(len(chunk))
                buf = create_string_buffer(chunk, len(chunk))
                ret = self._call("cnc_download4", self.h, byref(length), buf)
                if ret == EW_BUFFER or (ret == EW_OK and length.value <= 0):
                    stalls += 1
                    if stalls > 600:
                        raise FocasError("Стойка не принимает данные (таймаут)")
                    time.sleep(0.05)
                    continue
                self._check("Запись программы", ret)
                stalls = 0
                pos += length.value
                if progress:
                    progress(pos, total)
            ok = True
        finally:
            ret = self._call("cnc_dwnend4", self.h)
            if ok:
                self._check("Завершение записи (стойка проверила программу)", ret)

    def delete(self, path):
        self._check(f"Удаление {path}", self._call("cnc_pdf_del", self.h, path.encode("ascii")))


class Worker:
    """Все вызовы FOCAS выполняются в одном фоновом потоке: дескриптор FANUC привязан к потоку."""

    def __init__(self, app):
        self.app = app
        self.q = queue.Queue()
        threading.Thread(target=self._run, daemon=True).start()

    def submit(self, func, on_ok=None, on_err=None):
        self.app.busy(+1)
        self.q.put((func, on_ok, on_err))

    def _run(self):
        while True:
            func, on_ok, on_err = self.q.get()
            try:
                res = func()
            except Exception as e:
                self.app.post(self.app.busy, -1)
                self.app.post(on_err or self.app.job_error, e)
            else:
                self.app.post(self.app.busy, -1)
                if on_ok:
                    self.app.post(on_ok, res)


def make_tree(parent, columns, headings, widths, selectmode="extended"):
    frame = ttk.Frame(parent)
    tree = ttk.Treeview(frame, columns=columns, show="tree headings", selectmode=selectmode)
    vsb = ttk.Scrollbar(frame, orient="vertical", command=tree.yview)
    tree.configure(yscrollcommand=vsb.set)
    tree.grid(row=0, column=0, sticky="nsew")
    vsb.grid(row=0, column=1, sticky="ns")
    frame.rowconfigure(0, weight=1)
    frame.columnconfigure(0, weight=1)
    for col, head, w in zip(("#0",) + tuple(columns), headings, widths):
        tree.heading(col, text=head, anchor="w")
        tree.column(col, width=w, minwidth=40, stretch=(col in ("#0", "comment")), anchor="e" if col == "size" else "w")
    return frame, tree


class MachineDialog(tk.Toplevel):
    def __init__(self, parent, title, machine=None):
        super().__init__(parent)
        self.title(title)
        self.transient(parent)
        self.resizable(False, False)
        self.configure(bg=C["bg"])
        self.result = None
        m = machine or {"name": "", "ip": "192.168.1.100", "port": 8193}

        frm = ttk.Frame(self, padding=16)
        frm.pack()
        self.v_name = tk.StringVar(value=m["name"])
        self.v_ip = tk.StringVar(value=m["ip"])
        self.v_port = tk.StringVar(value=str(m["port"]))
        rows = [("Название станка:", self.v_name), ("IP-адрес стойки:", self.v_ip), ("Порт FOCAS (обычно 8193):", self.v_port)]
        first = None
        for r, (label, var) in enumerate(rows):
            ttk.Label(frm, text=label).grid(row=r, column=0, sticky="w", pady=4)
            e = ttk.Entry(frm, textvariable=var, width=26)
            e.grid(row=r, column=1, pady=4, padx=(8, 0))
            first = first or e
        btns = ttk.Frame(frm)
        btns.grid(row=len(rows), column=0, columnspan=2, pady=(10, 0), sticky="e")
        ttk.Button(btns, text="Сохранить", style="Accent.TButton", command=self._ok).pack(side="left", padx=4)
        ttk.Button(btns, text="Отмена", command=self.destroy).pack(side="left")
        self.bind("<Return>", lambda e: self._ok())
        self.bind("<Escape>", lambda e: self.destroy())
        self.grab_set()
        first.focus_set()
        self.wait_window()

    def _ok(self):
        name = self.v_name.get().strip()
        ip = self.v_ip.get().strip()
        try:
            port = int(self.v_port.get().strip())
        except ValueError:
            port = 0
        if not name or not ip or not (0 < port < 65536):
            messagebox.showerror("Ошибка", "Заполните название, IP и порт.", parent=self)
            return
        self.result = {"name": name, "ip": ip, "port": port}
        self.destroy()


def show_text_window(parent, title, text):
    top = tk.Toplevel(parent)
    top.title(f"{APP_NAME} — {title}")
    top.geometry("820x660")
    top.configure(bg=C["bg"])
    frm = ttk.Frame(top)
    frm.pack(fill="both", expand=True)
    txt = tk.Text(
        frm, wrap="none", font=("Consolas", 10), undo=False, bg=C["dark"], fg="#e8e8e8",
        insertbackground="white", selectbackground=C["orange"], relief="flat", borderwidth=0,
        padx=10, pady=8, highlightthickness=0,
    )
    vsb = ttk.Scrollbar(frm, orient="vertical", command=txt.yview)
    hsb = ttk.Scrollbar(frm, orient="horizontal", command=txt.xview)
    txt.configure(yscrollcommand=vsb.set, xscrollcommand=hsb.set)
    txt.grid(row=0, column=0, sticky="nsew")
    vsb.grid(row=0, column=1, sticky="ns")
    hsb.grid(row=1, column=0, sticky="ew")
    frm.rowconfigure(0, weight=1)
    frm.columnconfigure(0, weight=1)
    txt.insert("1.0", text)
    txt.configure(state="disabled")
    lines = text.count("\n") + 1
    ttk.Label(top, text=f"Строк: {lines}   ·   Символов: {len(text)}", style="Hint.TLabel").pack(anchor="w", padx=10, pady=4)


# ======================= Приложение =======================

class ProgramTransferTool(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(f"{APP_NAME} — версия {APP_VERSION} · Сделано по заказу {COMPANY}")
        self.geometry("1400x900")
        self.minsize(1150, 700)

        self.settings = load_settings()
        self.ui_q = queue.Queue()
        self._busy = 0
        self.worker = Worker(self)
        self.clients = {}
        self.machine_drives = {}
        self.cnc_cur_path = None
        self.cnc_entries = {}
        self.pc_cur_dir = None
        self._drag = None
        self._last_list = "pc"

        self.rs_cancel = threading.Event()
        self.rs_worker = None

        self._setup_style()
        self._set_icon()
        self._build_header()
        self._build_footer()

        body = ttk.Frame(self)
        body.pack(fill="both", expand=True)
        self.net_tab = ttk.Frame(body)
        self.rs_tab = ttk.Frame(body)
        self.pages = {"net": (self.net_tab, self.seg_net), "rs": (self.rs_tab, self.seg_rs)}

        self._build_net_tab()
        self._build_rs232_tab()
        self._show_page("net")

        self.protocol("WM_DELETE_WINDOW", self._on_close)
        self.after(40, self._poll)

    # ---------- инфраструктура ----------

    @property
    def client(self):
        m = self._current_machine() if hasattr(self, "machine_combo") else None
        return self.clients.get(id(m)) if m else None

    def _setup_style(self):
        s = ttk.Style(self)
        s.theme_use("clam")
        self.configure(bg=C["bg"])
        s.configure(".", background=C["bg"], foreground=C["text"], font=UI_FONT, bordercolor=C["border"],
                    lightcolor=C["panel"], darkcolor=C["border"], troughcolor=C["head"], focuscolor=C["orange"])
        s.configure("TFrame", background=C["bg"])
        s.configure("Panel.TFrame", background=C["panel"])
        s.configure("Header.TFrame", background=C["dark"])
        s.configure("Seg.TFrame", background=C["dark2"])
        s.configure("Footer.TFrame", background=C["head"])

        s.configure("TLabel", background=C["bg"], foreground=C["text"])
        s.configure("Panel.TLabel", background=C["panel"])
        s.configure("Section.TLabel", background=C["panel"], foreground=C["muted"], font=("Segoe UI", 9, "bold"))
        s.configure("Hint.TLabel", background=C["bg"], foreground=C["muted"], font=("Segoe UI", 9))
        s.configure("PanelHint.TLabel", background=C["panel"], foreground=C["muted"], font=("Segoe UI", 9))
        s.configure("Header.TLabel", background=C["dark"], foreground="#bdbdbd", font=("Segoe UI", 9, "bold"))
        s.configure("Title.TLabel", background=C["dark"], foreground="white", font=("Segoe UI", 12, "bold"))
        s.configure("Status.TLabel", background=C["dark"], font=("Segoe UI", 10, "bold"))
        s.configure("Footer.TLabel", background=C["head"], foreground=C["muted"], font=("Segoe UI", 9))
        s.configure("FooterBold.TLabel", background=C["head"], foreground=C["text"], font=("Segoe UI", 9, "bold"))
        s.configure("Busy.TLabel", background=C["head"], foreground=C["orange"], font=("Segoe UI", 9, "bold"))

        s.configure("TButton", background=C["panel"], foreground=C["text"], bordercolor="#c4c4bf", borderwidth=1,
                    lightcolor=C["panel"], darkcolor=C["panel"], padding=(12, 5), relief="solid")
        s.map("TButton", background=[("pressed", C["head"]), ("active", "#f7f7f4")],
              bordercolor=[("active", "#b8b8b3")])
        s.configure("Accent.TButton", background=C["orange"], foreground="white", bordercolor=C["orange"],
                    lightcolor=C["orange"], darkcolor=C["orange"], font=("Segoe UI", 10, "bold"), padding=(16, 6))
        s.map("Accent.TButton",
              background=[("pressed", C["orange_p"]), ("active", C["orange_h"])],
              lightcolor=[("active", C["orange_h"])], darkcolor=[("active", C["orange_h"])],
              bordercolor=[("active", C["orange_h"])])
        s.configure("Dark.TButton", background=C["dark"], foreground="white", bordercolor="#5a5a5a",
                    lightcolor=C["dark"], darkcolor=C["dark"], padding=(10, 4))
        s.configure("Small.TButton", padding=(6, 3))
        s.configure("SmallDark.TButton", background=C["dark"], foreground="white", bordercolor="#5a5a5a",
                    lightcolor=C["dark"], darkcolor=C["dark"], padding=(6, 3))
        s.map("SmallDark.TButton", background=[("pressed", C["dark3"]), ("active", C["dark2"])])
        s.map("Dark.TButton", background=[("pressed", C["dark3"]), ("active", C["dark2"])],
              bordercolor=[("active", "#8a8a8a")])
        for name, bg, fg in (("Seg.TButton", C["dark2"], "#a8a8a8"), ("SegOn.TButton", C["bg"], C["dark"])):
            s.configure(name, background=bg, foreground=fg, bordercolor=bg, lightcolor=bg, darkcolor=bg,
                        padding=(16, 4), font=("Segoe UI", 9, "bold"))
        s.map("Seg.TButton", background=[("active", C["dark3"])], foreground=[("active", "white")])
        s.map("SegOn.TButton", background=[("active", "white")])

        s.configure("Treeview", background=C["panel"], fieldbackground=C["panel"], foreground=C["text"],
                    rowheight=25, bordercolor=C["border"], borderwidth=0, font=UI_FONT)
        s.map("Treeview", background=[("selected", C["dark"])], foreground=[("selected", "white")])
        s.configure("Treeview.Heading", background=C["head"], foreground=C["muted"], font=("Segoe UI", 9, "bold"),
                    relief="flat", bordercolor=C["border"], lightcolor=C["head"], darkcolor=C["border"], padding=(6, 4))
        s.map("Treeview.Heading", background=[("active", "#e2e2de")])

        s.configure("TEntry", fieldbackground="white", bordercolor=C["border"], lightcolor="white", padding=4)
        s.map("TEntry", bordercolor=[("focus", C["orange"])], lightcolor=[("focus", C["orange"])])
        s.configure("TCombobox", fieldbackground="white", background="white", bordercolor=C["border"],
                    arrowcolor=C["text"], padding=3)
        s.map("TCombobox", fieldbackground=[("readonly", "white")], bordercolor=[("focus", C["orange"])])
        s.configure("Header.TCombobox", fieldbackground=C["dark2"], background=C["dark2"], foreground="white",
                    arrowcolor="white", bordercolor="#555555", lightcolor=C["dark2"], darkcolor=C["dark2"], padding=4)
        s.map("Header.TCombobox",
              fieldbackground=[("readonly", C["dark2"])], foreground=[("readonly", "white")],
              selectbackground=[("readonly", C["dark2"])], selectforeground=[("readonly", "white")],
              background=[("active", C["dark3"])])
        self.option_add("*TCombobox*Listbox.background", "white")
        self.option_add("*TCombobox*Listbox.foreground", C["text"])
        self.option_add("*TCombobox*Listbox.selectBackground", C["dark"])
        self.option_add("*TCombobox*Listbox.selectForeground", "white")
        self.option_add("*TCombobox*Listbox.font", UI_FONT)

        s.configure("TSpinbox", fieldbackground="white", bordercolor=C["border"], arrowcolor=C["text"], padding=3)
        s.configure("TCheckbutton", background=C["panel"], foreground=C["text"])
        s.map("TCheckbutton", background=[("active", C["panel"])], indicatorcolor=[("selected", C["orange"])])
        s.configure("Accent.Horizontal.TProgressbar", background=C["orange"], troughcolor="#d6d6d2",
                    bordercolor=C["head"], lightcolor=C["orange"], darkcolor=C["orange"], thickness=10)
        s.configure("TPanedwindow", background=C["bg"])
        s.configure("Vertical.TScrollbar", background=C["head"], troughcolor=C["panel"], bordercolor=C["panel"],
                    arrowcolor=C["muted"], lightcolor=C["head"], darkcolor=C["head"])
        s.configure("Horizontal.TScrollbar", background=C["head"], troughcolor=C["panel"], bordercolor=C["panel"],
                    arrowcolor=C["muted"], lightcolor=C["head"], darkcolor=C["head"])

    def _set_icon(self):
        for d in (getattr(sys, "_MEIPASS", None), APP_DIR):
            ico = os.path.join(d, "stz.ico") if d else None
            if ico and os.path.isfile(ico):
                try:
                    self.iconbitmap(default=ico)
                    return
                except tk.TclError:
                    pass
        try:
            img = tk.PhotoImage(width=32, height=32)
            img.put(C["orange"], to=(0, 0, 32, 32))
            for x0, y0, x1, y1 in ((8, 7, 24, 11), (8, 7, 12, 17), (8, 14, 24, 18), (20, 14, 24, 25), (8, 21, 24, 25)):
                img.put("white", to=(x0, y0, x1, y1))
            self.iconphoto(True, img)
            self._icon = img
        except tk.TclError:
            pass

    def _panel(self, parent, title):
        outer = tk.Frame(parent, bg=C["panel"], highlightthickness=1, highlightbackground=C["border"])
        head = ttk.Frame(outer, style="Panel.TFrame", padding=(12, 8, 10, 4))
        head.pack(fill="x")
        ttk.Label(head, text=title.upper(), style="Section.TLabel").pack(side="left")
        body = ttk.Frame(outer, style="Panel.TFrame", padding=(8, 2, 8, 8))
        body.pack(fill="both", expand=True)
        return outer, head, body

    def _build_header(self):
        hdr = ttk.Frame(self, style="Header.TFrame", padding=(10, 8))
        hdr.pack(fill="x")

        self.settings_menu = tk.Menu(self, tearoff=0)
        self.settings_menu.add_command(label="Указать библиотеку FOCAS (Fwlib64.dll)...", command=self._choose_dll)
        self.settings_menu.add_separator()
        self.settings_menu.add_command(label="О программе", command=self._about)
        gear = ttk.Button(hdr, text="⚙", width=2, style="SmallDark.TButton")
        gear.configure(command=lambda: self.settings_menu.tk_popup(gear.winfo_rootx(), gear.winfo_rooty() + gear.winfo_height()))
        gear.pack(side="right", padx=(8, 0))

        seg = ttk.Frame(hdr, style="Seg.TFrame", padding=2)
        seg.pack(side="right")
        self.seg_net = ttk.Button(seg, text="ETHERNET", style="SegOn.TButton", command=lambda: self._show_page("net"))
        self.seg_net.pack(side="left")
        self.seg_rs = ttk.Button(seg, text="RS232", style="Seg.TButton", command=lambda: self._show_page("rs"))
        self.seg_rs.pack(side="left")

        tk.Label(hdr, text="STZ", bg=C["orange"], fg="white", font=("Segoe UI", 10, "bold"), padx=8, pady=3).pack(side="left")
        ttk.Label(hdr, text="FANUC LAN", style="Title.TLabel").pack(side="left", padx=(10, 8))
        tk.Label(
            hdr, text=f"ВЕРСИЯ {APP_VERSION}", bg=C["dark"], fg=C["orange"], font=("Consolas", 9, "bold"),
            padx=6, pady=1, highlightthickness=1, highlightbackground=C["orange"],
        ).pack(side="left")
        tk.Frame(hdr, bg="#444444", width=1, height=24).pack(side="left", padx=16)

        ttk.Label(hdr, text="СТАНОК", style="Header.TLabel").pack(side="left", padx=(0, 6))
        self.machine_combo = ttk.Combobox(hdr, state="readonly", width=26, style="Header.TCombobox")
        self.machine_combo.pack(side="left")
        self.machine_combo.bind("<<ComboboxSelected>>", self._on_machine_selected)
        self.connect_btn = ttk.Button(hdr, text="Подключить", style="Accent.TButton", command=self._toggle_connect)
        self.connect_btn.pack(side="left", padx=(8, 4))
        for txt, cmd in (("＋", self._machine_add), ("✎", self._machine_edit), ("✕", self._machine_delete)):
            ttk.Button(hdr, text=txt, width=2, style="SmallDark.TButton", command=cmd).pack(side="left", padx=2)
        self.conn_label = ttk.Label(hdr, text="● Не подключено", style="Status.TLabel", foreground=C["bad"])
        self.conn_label.pack(side="left", padx=12)

        self._refresh_machine_combo()

    def _build_footer(self):
        ft = ttk.Frame(self, style="Footer.TFrame", padding=(12, 5))
        ft.pack(side="bottom", fill="x")
        ttk.Label(ft, text=f"Разработано для {COMPANY}", style="FooterBold.TLabel").pack(side="left")
        ttk.Label(ft, text="·", style="Footer.TLabel").pack(side="left", padx=8)
        self.net_status = ttk.Label(ft, text="Готово", style="Footer.TLabel")
        self.net_status.pack(side="left")
        ttk.Label(
            ft, text="Двойной щелчок — просмотр · перетаскивание мышью — передача · Del — удалить на станке",
            style="Footer.TLabel",
        ).pack(side="right")
        self.net_progress = ttk.Progressbar(ft, length=200, mode="determinate", style="Accent.Horizontal.TProgressbar")
        self.net_progress.pack(side="right", padx=14)
        self.busy_label = ttk.Label(ft, text="", style="Busy.TLabel")
        self.busy_label.pack(side="right")

    def _show_page(self, name):
        for key, (frame, btn) in self.pages.items():
            if key == name:
                frame.pack(fill="both", expand=True)
                btn.configure(style="SegOn.TButton")
            else:
                frame.pack_forget()
                btn.configure(style="Seg.TButton")

    def _about(self):
        messagebox.showinfo(
            "О программе",
            f"{APP_NAME}\nверсия {APP_VERSION}\n\nПередача УП на стойки FANUC по Ethernet (FOCAS) и RS232.\n"
            f"Разработано для {COMPANY}.",
        )

    def post(self, fn, *args):
        self.ui_q.put((fn, args))

    def _poll(self):
        try:
            while True:
                fn, args = self.ui_q.get_nowait()
                try:
                    fn(*args)
                except Exception as e:
                    self._log(f"Ошибка интерфейса: {e}")
        except queue.Empty:
            pass
        self.after(40, self._poll)

    def busy(self, delta):
        self._busy = max(0, self._busy + delta)
        self.busy_label.configure(text="⏳ работаю..." if self._busy else "")
        self.configure(cursor="watch" if self._busy else "")

    def job_error(self, e):
        self._log(f"✖ {e}")
        self.net_progress["value"] = 0
        self.net_status.configure(text="Ошибка")
        hint = ""
        if isinstance(e, FocasError) and e.code in (-8, -16, -17):
            hint = "\n\nСвязь со стойкой потеряна — нажмите «Отключить», затем «Подключить»."
        messagebox.showerror("Ошибка", f"{e}{hint}")

    def _log(self, text):
        self.net_log.configure(state="normal")
        self.net_log.insert("end", time.strftime("%H:%M:%S  ") + text + "\n")
        self.net_log.see("end")
        self.net_log.configure(state="disabled")

    def _progress(self, done, total, text):
        pct = int(done / total * 100) if total else 0
        self.net_progress["value"] = pct
        self.net_status.configure(text=text)

    def _progress_done(self, text):
        self.net_progress["value"] = 0
        self.net_status.configure(text=text)
        self._log("✔ " + text)

    # ---------- вкладка «Станок по сети» ----------

    def _build_net_tab(self):
        f = self.net_tab

        tip = tk.Frame(f, bg=C["tip"], highlightthickness=1, highlightbackground=C["tip_border"])
        tip.pack(fill="x", padx=10, pady=(10, 0))
        tk.Label(tip, text="СОВЕТ", bg=C["tip"], fg=C["tip_text"], font=("Segoe UI", 9, "bold")).pack(side="left", padx=(12, 8), pady=6)
        tk.Label(
            tip, bg=C["tip"], fg=C["tip_text"], font=("Segoe UI", 9), anchor="w",
            text="Большие программы отправляйте на CF_BIN (карта памяти, работает как память стойки). "
                 "Новую УП сначала проверьте на тестовом номере и сверьте текст на экране стойки.",
        ).pack(side="left", fill="x", expand=True, pady=6)
        tk.Button(
            tip, text="✕", bg=C["tip"], fg=C["tip_text"], relief="flat", borderwidth=0, cursor="hand2",
            activebackground=C["tip_border"], command=tip.destroy,
        ).pack(side="right", padx=8)

        logouter, _, logbody = self._panel(f, "Журнал")
        logouter.pack(side="bottom", fill="x", padx=10, pady=(0, 10))
        self.net_log = scrolledtext.ScrolledText(
            logbody, height=5, state="disabled", font=("Consolas", 9), bg="#fbfbfa", fg=C["text"],
            relief="flat", borderwidth=0, highlightthickness=0,
        )
        self.net_log.pack(fill="both", expand=True)

        vp = ttk.Panedwindow(f, orient="vertical")
        vp.pack(fill="both", expand=True, padx=10, pady=10)

        # --- компьютер ---
        pc_outer, pc_head, pc_body = self._panel(vp, "Компьютер")
        vp.add(pc_outer, weight=1)
        ttk.Button(pc_head, text="⟳", width=2, style="Small.TButton", command=lambda: self._pc_show(self.pc_cur_dir)).pack(side="right")
        self.pc_path_var = tk.StringVar()
        pe = ttk.Entry(pc_head, textvariable=self.pc_path_var, font=("Consolas", 10))
        pe.pack(side="right", fill="x", expand=True, padx=6)
        pe.bind("<Return>", lambda e: self._pc_go())
        ttk.Button(pc_head, text="↑", width=2, style="Small.TButton", command=self._pc_up).pack(side="right")
        hp = ttk.Panedwindow(pc_body, orient="horizontal")
        hp.pack(fill="both", expand=True)
        fr, self.pc_dirs = make_tree(hp, (), ["Папки"], [270], selectmode="browse")
        hp.add(fr, weight=1)
        fr2, self.pc_files = make_tree(hp, ("type", "size", "date"), ["Имя", "Тип", "Размер", "Изменён"], [340, 90, 90, 140])
        hp.add(fr2, weight=3)

        # --- кнопки передачи ---
        mid = ttk.Frame(vp, padding=(0, 8))
        vp.add(mid, weight=0)
        ttk.Button(mid, text="▼  Отправить на станок", style="Accent.TButton", command=self._send_selected).pack(side="left", padx=(0, 6))
        ttk.Button(mid, text="▲  Получить на компьютер", command=self._receive_selected).pack(side="left", padx=6)
        ttk.Button(mid, text="Просмотр", command=self._view_selected).pack(side="left", padx=6)
        ttk.Button(mid, text="Удалить на станке", command=self._delete_selected).pack(side="left", padx=6)
        ttk.Label(mid, text="Файлы можно перетаскивать мышью между панелями", style="Hint.TLabel").pack(side="right")

        # --- станок ---
        cnc_outer, cnc_head, cnc_body = self._panel(vp, "Станок")
        vp.add(cnc_outer, weight=1)
        ttk.Button(cnc_head, text="⟳", width=2, style="Small.TButton",
                   command=lambda: self.cnc_cur_path and self._cnc_show(self.cnc_cur_path)).pack(side="right")
        self.cnc_path_var = tk.StringVar(value="— не подключено —")
        ce = ttk.Entry(cnc_head, textvariable=self.cnc_path_var, font=("Consolas", 10))
        ce.pack(side="right", fill="x", expand=True, padx=6)
        ce.bind("<Return>", lambda e: self._cnc_go())
        ttk.Button(cnc_head, text="↑", width=2, style="Small.TButton", command=self._cnc_up).pack(side="right")
        hp2 = ttk.Panedwindow(cnc_body, orient="horizontal")
        hp2.pack(fill="both", expand=True)
        fr3, self.cnc_dirs = make_tree(hp2, (), ["Папки станка"], [270], selectmode="browse")
        hp2.add(fr3, weight=1)
        fr4, self.cnc_files = make_tree(
            hp2, ("type", "size", "comment", "date"),
            ["Имя", "Тип", "Размер", "Комментарий", "Дата"], [180, 90, 90, 240, 140],
        )
        hp2.add(fr4, weight=3)

        # события
        self.pc_dirs.bind("<<TreeviewOpen>>", self._pc_tree_open)
        self.pc_dirs.bind("<<TreeviewSelect>>", self._pc_tree_select)
        self.pc_files.bind("<Double-1>", self._pc_files_dbl)
        self.cnc_dirs.bind("<<TreeviewOpen>>", self._cnc_tree_open)
        self.cnc_dirs.bind("<<TreeviewSelect>>", self._cnc_tree_select)
        self.cnc_files.bind("<Double-1>", self._cnc_files_dbl)
        self.cnc_files.bind("<Delete>", lambda e: self._delete_selected())
        self.pc_files.bind("<FocusIn>", lambda e: setattr(self, "_last_list", "pc"))
        self.cnc_files.bind("<FocusIn>", lambda e: setattr(self, "_last_list", "cnc"))
        for tree, src in ((self.pc_files, "pc"), (self.cnc_files, "cnc")):
            tree.bind("<ButtonPress-1>", lambda e, s=src: self._drag_start(e, s), add="+")
            tree.bind("<B1-Motion>", self._drag_motion, add="+")
            tree.bind("<ButtonRelease-1>", self._drag_end, add="+")

        self._pc_init_tree()
        start = self.settings.get("pc_last_dir") or os.path.expanduser("~")
        self._pc_show(start if os.path.isdir(start) else os.path.expanduser("~"))

    # ---------- список станков ----------

    def _refresh_machine_combo(self):
        names = []
        for i, m in enumerate(self.settings["machines"]):
            mark = "🟢" if id(m) in self.clients else "⚪"
            names.append(f"{mark} {i + 1:03d}. {m['name']}  ({m['ip']})")
        self.machine_combo["values"] = names
        idx = min(self.settings.get("machine_index", 0), len(names) - 1)
        if names:
            self.machine_combo.current(max(idx, 0))

    def _show_machine_state(self):
        m = self._current_machine()
        self.cnc_files.delete(*self.cnc_files.get_children())
        self.cnc_cur_path = None
        self.cnc_entries = {}
        if m and id(m) in self.clients:
            self.connect_btn.configure(text="Отключить")
            self.conn_label.configure(text="● Подключено", foreground=C["ok"])
            self._cnc_build_tree(self.machine_drives.get(id(m)) or ["CNC_MEM"])
            self.cnc_path_var.set("выберите папку слева или впишите путь, напр. //CF_BIN/")
        else:
            self.connect_btn.configure(text="Подключить")
            self.conn_label.configure(text="● Не подключено", foreground=C["bad"])
            self.cnc_dirs.delete(*self.cnc_dirs.get_children())
            self.cnc_path_var.set("— не подключено —")
        n = len(self.clients)
        self.title(f"Program Transfer Tool — подключено станков: {n}" if n else "Program Transfer Tool")

    def _current_machine(self):
        idx = self.machine_combo.current()
        if idx < 0 or idx >= len(self.settings["machines"]):
            return None
        return self.settings["machines"][idx]

    def _on_machine_selected(self, event=None):
        self.settings["machine_index"] = self.machine_combo.current()
        save_settings(self.settings)
        self._show_machine_state()

    def _machine_add(self):
        d = MachineDialog(self, "Новый станок")
        if d.result:
            self.settings["machines"].append(d.result)
            self.settings["machine_index"] = len(self.settings["machines"]) - 1
            save_settings(self.settings)
            self._refresh_machine_combo()
            self._show_machine_state()

    def _machine_edit(self):
        m = self._current_machine()
        if not m:
            return
        d = MachineDialog(self, "Изменить станок", m)
        if d.result:
            m.update(d.result)
            save_settings(self.settings)
            self._refresh_machine_combo()

    def _machine_delete(self):
        m = self._current_machine()
        if not m or len(self.settings["machines"]) <= 1:
            messagebox.showinfo("Станки", "Должен остаться хотя бы один станок.")
            return
        if messagebox.askyesno("Удалить станок", f"Удалить «{m['name']}» из списка?"):
            if id(m) in self.clients:
                self._disconnect()
            self.settings["machines"].remove(m)
            self.settings["machine_index"] = 0
            save_settings(self.settings)
            self._refresh_machine_combo()
            self._show_machine_state()

    def _choose_dll(self):
        path = filedialog.askopenfilename(title="Выберите Fwlib64.dll", filetypes=[("Библиотека FOCAS", "*.dll")])
        if path:
            self.settings["focas_lib"] = path
            save_settings(self.settings)
            self._log(f"Библиотека FOCAS: {path}")

    # ---------- подключение ----------

    def _toggle_connect(self):
        if self.client:
            self._disconnect()
            return
        m = self._current_machine()
        if not m:
            return
        candidates = find_focas_dlls(self.settings.get("focas_lib"))
        if not candidates:
            messagebox.showwarning(
                "Нет библиотеки FOCAS",
                "Не найден Fwlib64.dll рядом с программой.\nУкажите его вручную (нужен также fwlibe64.dll в той же папке).",
            )
            self._choose_dll()
            candidates = find_focas_dlls(self.settings.get("focas_lib"))
            if not candidates:
                return

        ip, port = m["ip"], m["port"]
        self._log(f"Подключение к {m['name']} ({ip}:{port})...")
        self.conn_label.configure(text="● Подключение...", foreground=C["wait"])

        def job():
            last = None
            for dll in candidates:
                try:
                    c = FocasClient(dll)
                    c.connect(ip, port)
                except FocasError as e:
                    last = e
                    if e.code == -15:
                        continue
                    raise
                except OSError as e:
                    last = FocasError(f"Не удалось загрузить {os.path.basename(dll)}: {e}")
                    continue
                drives, derr = [], None
                try:
                    drives = c.drives()
                except FocasError as e:
                    derr = str(e)
                return c, dll, drives, derr
            raise last or FocasError("Библиотека FOCAS не найдена")

        def ok(res):
            c, dll, drives, derr = res
            drives = list(drives or ["CNC_MEM"])
            if "CF_BIN" not in drives:
                drives.insert(1 if "CNC_MEM" in drives else 0, "CF_BIN")
            self.clients[id(m)] = c
            self.machine_drives[id(m)] = drives
            self._log(f"✔ {m['name']}: подключено ({os.path.basename(dll)})")
            if derr:
                self._log(f"{m['name']}: не удалось прочитать список дисков: {derr}. Показываю CNC_MEM.")
            self._refresh_machine_combo()
            if self._current_machine() is m:
                self._show_machine_state()
                self.net_status.configure(text="Выберите папку станка слева (например CNC_MEM → USER → PATH1)")

        def err(e):
            if self._current_machine() is m:
                self.conn_label.configure(text="● Не подключено", foreground=C["bad"])
            self.job_error(e)

        self.worker.submit(job, ok, err)

    def _disconnect(self):
        m = self._current_machine()
        c = self.clients.pop(id(m), None) if m else None
        if c:
            self.worker.submit(c.disconnect)
            self._log(f"{m['name']}: отключено")
        self._refresh_machine_combo()
        self._show_machine_state()

    def _require_conn(self):
        if not self.client:
            messagebox.showinfo("Нет подключения", "Сначала подключитесь к станку (кнопка «Подключить»).")
            return False
        return True

    # ---------- панель компьютера ----------

    def _pc_init_tree(self):
        drives = [f"{l}:\\" for l in string.ascii_uppercase if os.path.exists(f"{l}:\\")] if os.name == "nt" else ["/"]
        for d in drives:
            self.pc_dirs.insert("", "end", iid=d, text=f"💽 {d}")
            self.pc_dirs.insert(d, "end", iid=d + "|dummy", text="")

    def _pc_fill_node(self, node):
        self.pc_dirs.delete(*self.pc_dirs.get_children(node))
        try:
            subs = sorted(
                (e for e in os.scandir(node) if e.is_dir(follow_symlinks=False) and not e.name.startswith(("$", "."))),
                key=lambda e: e.name.lower(),
            )
        except OSError:
            return
        for e in subs:
            if self.pc_dirs.exists(e.path):
                continue
            self.pc_dirs.insert(node, "end", iid=e.path, text="📁 " + e.name)
            self.pc_dirs.insert(e.path, "end", iid=e.path + "|dummy", text="")

    def _pc_tree_open(self, event):
        node = self.pc_dirs.focus()
        kids = self.pc_dirs.get_children(node)
        if len(kids) == 1 and kids[0].endswith("|dummy"):
            self._pc_fill_node(node)

    def _pc_tree_select(self, event):
        sel = self.pc_dirs.selection()
        if sel and not sel[0].endswith("|dummy"):
            self._pc_show(sel[0])

    def _pc_show(self, path):
        if not path:
            return
        try:
            entries = sorted(os.scandir(path), key=lambda e: (not e.is_dir(), e.name.lower()))
        except OSError as e:
            messagebox.showerror("Ошибка", f"Не удалось открыть папку:\n{e}")
            return
        self.pc_files.delete(*self.pc_files.get_children())
        for e in entries:
            if e.name.startswith("$"):
                continue
            try:
                st = e.stat()
                is_dir = e.is_dir()
            except OSError:
                continue
            date = time.strftime("%Y-%m-%d %H:%M", time.localtime(st.st_mtime))
            if is_dir:
                self.pc_files.insert("", "end", iid=e.path, text="📁 " + e.name, values=("Папка", "", date))
            else:
                ext = os.path.splitext(e.name)[1].lstrip(".").upper() or "Файл"
                self.pc_files.insert("", "end", iid=e.path, text="📄 " + e.name, values=(ext, human_size(st.st_size), date))
        self.pc_cur_dir = path
        self.pc_path_var.set(path)

    def _pc_go(self):
        p = self.pc_path_var.get().strip()
        if os.path.isdir(p):
            self._pc_show(p)
        else:
            messagebox.showerror("Ошибка", "Папка не найдена.")

    def _pc_up(self):
        cur = self.pc_cur_dir or ""
        parent = os.path.dirname(cur.rstrip("\\/"))
        if parent.endswith(":"):
            parent += "\\"
        if parent and os.path.normcase(parent) != os.path.normcase(cur):
            self._pc_show(parent)

    def _pc_files_dbl(self, event):
        if self.pc_files.identify_region(event.x, event.y) not in ("tree", "cell"):
            return
        item = self.pc_files.focus()
        if not item:
            return
        if os.path.isdir(item):
            self._pc_show(item)
        else:
            self._view_pc_file(item)

    def _view_pc_file(self, path):
        try:
            if os.path.getsize(path) > 5 * 1024 * 1024:
                messagebox.showinfo("Просмотр", "Файл слишком большой для просмотра.")
                return
            with open(path, "rb") as f:
                text = f.read().decode("ascii", errors="replace").replace("\r\n", "\n")
        except OSError as e:
            messagebox.showerror("Ошибка", str(e))
            return
        show_text_window(self, f"ПК: {path}", text)

    # ---------- панель станка ----------

    def _cnc_build_tree(self, drives):
        self.cnc_dirs.delete(*self.cnc_dirs.get_children())
        m = self._current_machine() or {"name": "Станок"}
        root = self.cnc_dirs.insert("", "end", iid="cnc_root", text=f"🖥 {m['name']}", open=True)
        for d in drives:
            p = f"//{d}/"
            self.cnc_dirs.insert(root, "end", iid=p, text="💾 " + d)
            self.cnc_dirs.insert(p, "end", iid=p + "|dummy", text="")

    def _cnc_tree_open(self, event):
        node = self.cnc_dirs.focus()
        if not node or node == "cnc_root" or not self.client:
            return
        kids = self.cnc_dirs.get_children(node)
        if len(kids) == 1 and kids[0].endswith("|dummy"):
            client = self.client

            def ok(entries):
                if self.client is not client or not self.cnc_dirs.exists(node):
                    return
                self.cnc_dirs.delete(*self.cnc_dirs.get_children(node))
                for e in entries:
                    if e["is_dir"]:
                        p = node + e["name"] + "/"
                        if not self.cnc_dirs.exists(p):
                            self.cnc_dirs.insert(node, "end", iid=p, text="📁 " + e["name"])
                            self.cnc_dirs.insert(p, "end", iid=p + "|dummy", text="")

            self.worker.submit(lambda: client.list_dir(node), ok)

    def _cnc_tree_select(self, event):
        sel = self.cnc_dirs.selection()
        if sel and sel[0] != "cnc_root" and not sel[0].endswith("|dummy"):
            self._cnc_show(sel[0])

    def _cnc_show(self, path):
        if not self.client:
            return
        client = self.client
        self.net_status.configure(text=f"Чтение {path} ...")

        def ok(entries):
            if self.client is not client:
                return
            self.cnc_files.delete(*self.cnc_files.get_children())
            self.cnc_entries = {}
            entries.sort(key=lambda e: (not e["is_dir"], e["name"].lower()))
            for e in entries:
                if e["name"] in self.cnc_entries:
                    continue
                self.cnc_entries[e["name"]] = e
                if e["is_dir"]:
                    self.cnc_files.insert("", "end", iid=e["name"], text="📁 " + e["name"], values=("Папка", "", e["comment"], e["date"]))
                else:
                    self.cnc_files.insert(
                        "", "end", iid=e["name"], text="📄 " + e["name"],
                        values=("Программа", human_size(e["size"]), e["comment"], e["date"]),
                    )
            self.cnc_cur_path = path
            self.cnc_path_var.set(path)
            n_files = sum(1 for e in entries if not e["is_dir"])
            self.net_status.configure(text=f"{path}: программ {n_files}, папок {len(entries) - n_files}")

        self.worker.submit(lambda: client.list_dir(path), ok)

    def _cnc_go(self):
        if not self._require_conn():
            return
        p = self.cnc_path_var.get().strip().replace("\\", "/")
        if not p:
            return
        if not p.startswith("//"):
            p = "//" + p.lstrip("/")
        if not p.endswith("/"):
            p += "/"
        self._cnc_show(p)

    def _cnc_up(self):
        if not self.cnc_cur_path:
            return
        parent = self.cnc_cur_path.rstrip("/").rsplit("/", 1)[0] + "/"
        if parent.strip("/"):
            self._cnc_show(parent)

    def _cnc_files_dbl(self, event):
        if self.cnc_files.identify_region(event.x, event.y) not in ("tree", "cell"):
            return
        item = self.cnc_files.focus()
        e = self.cnc_entries.get(item)
        if not e:
            return
        if e["is_dir"]:
            self._cnc_show(self.cnc_cur_path + e["name"] + "/")
        else:
            self._view_cnc_program(e["name"])

    def _view_cnc_program(self, name):
        if not self._require_conn():
            return
        client, path = self.client, self.cnc_cur_path + name

        def ok(data):
            self.net_progress["value"] = 0
            self.net_status.configure(text="Готово")
            show_text_window(self, f"Станок: {path}", data.decode("ascii", errors="replace").replace("\r\n", "\n"))

        self.worker.submit(
            lambda: client.read_program(path, progress=lambda n: self.post(self.net_status.configure, {"text": f"Чтение {name}: {n} байт"})),
            ok,
        )

    # ---------- передача ----------

    def _send_selected(self):
        if not self._require_conn():
            return
        files = [i for i in self.pc_files.selection() if os.path.isfile(i)]
        if not files:
            messagebox.showinfo("Отправка", "Выберите файл(ы) УП в панели «Компьютер».")
            return
        folder = self.cnc_cur_path
        if not folder:
            messagebox.showinfo("Отправка", "Выберите папку на станке (например CNC_MEM → USER → PATH1).")
            return

        plan = []
        for path in files:
            name = os.path.basename(path)
            try:
                payload, header, added = build_focas_payload(path)
            except (OSError, ValueError) as e:
                self._log(f"✖ {name}: {e}")
                continue
            cnc_name = program_name_from_header(header)
            replace = False
            if cnc_name and cnc_name in self.cnc_entries:
                ans = messagebox.askyesnocancel("Программа уже есть", f"На станке в {folder} уже есть {cnc_name}.\n\nЗаменить её файлом {name}?")
                if ans is None:
                    return
                if not ans:
                    continue
                replace = True
            if added:
                self._log(f"{name}: в файле нет номера программы — добавлен заголовок {added}")
            plan.append((path, name, payload, cnc_name, replace))
        if not plan:
            return

        client = self.client

        def job():
            for path, name, payload, cnc_name, replace in plan:
                if replace:
                    self.post(self._log, f"Удаляю старую {cnc_name} ...")
                    client.delete(folder + cnc_name)
                self.post(self._log, f"→ {name}  →  {folder}")
                client.write_program(
                    folder, payload,
                    progress=lambda s, t, n=name: self.post(self._progress, s, t, f"Отправка {n}: {s} / {t} байт"),
                )
                self.post(self._log, f"✔ {name} записан на станок")
            return len(plan)

        def ok(n):
            self._progress_done(f"Отправлено на станок: {n}")
            if self.client is client:
                self._cnc_show(folder)

        def err(e):
            self.job_error(e)
            if self.client is client:
                self._cnc_show(folder)

        self.worker.submit(job, ok, err)

    def _receive_selected(self):
        if not self._require_conn():
            return
        names = [i for i in self.cnc_files.selection() if i in self.cnc_entries and not self.cnc_entries[i]["is_dir"]]
        if not names:
            messagebox.showinfo("Получение", "Выберите программу(ы) в панели «Станок».")
            return
        target = self.pc_cur_dir
        if not target or not os.path.isdir(target):
            messagebox.showinfo("Получение", "Выберите папку на компьютере, куда сохранить.")
            return

        plan = []
        for n in names:
            fname = n if os.path.splitext(n)[1] else n + ".nc"
            dest = os.path.join(target, fname)
            if os.path.exists(dest):
                ans = messagebox.askyesnocancel("Файл уже есть", f"{dest}\nуже существует. Перезаписать?")
                if ans is None:
                    return
                if not ans:
                    continue
            plan.append((self.cnc_cur_path + n, dest, n))
        if not plan:
            return

        client = self.client

        def job():
            for src, dest, n in plan:
                self.post(self._log, f"← {src}  →  {dest}")
                data = client.read_program(src, progress=lambda b, n=n: self.post(self.net_status.configure, {"text": f"Получение {n}: {b} байт"}))
                data = data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
                with open(dest, "wb") as fo:
                    fo.write(data)
                self.post(self._log, f"✔ {n} сохранён ({len(data)} байт)")
            return len(plan)

        def ok(n):
            self._progress_done(f"Получено со станка: {n}")
            self._pc_show(target)

        self.worker.submit(job, ok)

    def _delete_selected(self):
        if not self._require_conn():
            return
        names = [i for i in self.cnc_files.selection() if i in self.cnc_entries and not self.cnc_entries[i]["is_dir"]]
        if not names:
            messagebox.showinfo("Удаление", "Выберите программу(ы) в панели «Станок».")
            return
        if not messagebox.askyesno("Удаление на станке", "Удалить со станка:\n\n" + "\n".join(names) + "\n\nЭто действие нельзя отменить."):
            return
        client, folder = self.client, self.cnc_cur_path

        def job():
            for n in names:
                client.delete(folder + n)
                self.post(self._log, f"🗑 удалено: {folder}{n}")
            return len(names)

        def ok(n):
            self._progress_done(f"Удалено программ: {n}")
            if self.client is client:
                self._cnc_show(folder)

        self.worker.submit(job, ok)

    def _view_selected(self):
        if self._last_list == "cnc":
            sel = [i for i in self.cnc_files.selection() if i in self.cnc_entries and not self.cnc_entries[i]["is_dir"]]
            if sel:
                self._view_cnc_program(sel[0])
                return
        sel = [i for i in self.pc_files.selection() if os.path.isfile(i)]
        if sel:
            self._view_pc_file(sel[0])

    # ---------- перетаскивание ----------

    def _drag_start(self, e, src):
        self._drag = {"src": src, "x": e.x_root, "y": e.y_root, "moved": False, "widget": e.widget}

    def _drag_motion(self, e):
        d = self._drag
        if d and not d["moved"] and abs(e.x_root - d["x"]) + abs(e.y_root - d["y"]) > 12:
            d["moved"] = True
            d["widget"].configure(cursor="hand2")

    def _drag_end(self, e):
        d = self._drag
        self._drag = None
        if not d:
            return
        d["widget"].configure(cursor="")
        if not d["moved"]:
            return
        target = self.winfo_containing(e.x_root, e.y_root)
        if d["src"] == "pc" and target in (self.cnc_files, self.cnc_dirs):
            self._send_selected()
        elif d["src"] == "cnc" and target in (self.pc_files, self.pc_dirs):
            self._receive_selected()

    # ======================= RS232 =======================

    def _build_rs232_tab(self):
        f = self.rs_tab
        pad = {"padx": 10, "pady": (10, 0)}

        fo, _, fb = self._panel(f, "Файл УП")
        fo.pack(fill="x", **pad)
        self.rs_file = tk.StringVar()
        ttk.Entry(fb, textvariable=self.rs_file, font=("Consolas", 10)).pack(side="left", fill="x", expand=True, padx=(4, 6), pady=4)
        ttk.Button(fb, text="Обзор...", command=self._rs_browse).pack(side="left", padx=4)

        co, _, conn = self._panel(f, "Параметры COM-порта")
        co.pack(fill="x", **pad)
        grid = ttk.Frame(conn, style="Panel.TFrame", padding=(4, 4))
        grid.pack(fill="x")

        def lbl(text, r, c):
            ttk.Label(grid, text=text, style="Panel.TLabel").grid(row=r, column=c, sticky="w", padx=(0, 8), pady=5)

        lbl("COM-порт", 0, 0)
        pf = ttk.Frame(grid, style="Panel.TFrame")
        pf.grid(row=0, column=1, sticky="w", pady=5)
        self.port_combo = ttk.Combobox(pf, width=12, state="readonly")
        self.port_combo.pack(side="left")
        ttk.Button(pf, text="⟳", width=2, style="Small.TButton", command=self._refresh_ports).pack(side="left", padx=4)
        lbl("Скорость, бод", 0, 2)
        self.baud_combo = ttk.Combobox(grid, width=10, state="readonly", values=BAUD_RATES)
        self.baud_combo.set(self.settings["baud"])
        self.baud_combo.grid(row=0, column=3, sticky="w", pady=5)
        lbl("Управление потоком", 0, 4)
        self.flow_combo = ttk.Combobox(grid, width=12, state="readonly", values=FLOW_CONTROL)
        self.flow_combo.set(self.settings["flow"])
        self.flow_combo.grid(row=0, column=5, sticky="w", pady=5)

        lbl("Биты данных", 1, 0)
        self.data_combo = ttk.Combobox(grid, width=6, state="readonly", values=list(DATA_BITS.keys()))
        self.data_combo.set(self.settings["data_bits"])
        self.data_combo.grid(row=1, column=1, sticky="w", pady=5)
        lbl("Чётность", 1, 2)
        self.parity_combo = ttk.Combobox(grid, width=8, state="readonly", values=list(PARITY.keys()))
        self.parity_combo.set(self.settings["parity"])
        self.parity_combo.grid(row=1, column=3, sticky="w", pady=5)
        lbl("Стоп-биты", 1, 4)
        self.stop_combo = ttk.Combobox(grid, width=6, state="readonly", values=list(STOP_BITS.keys()))
        self.stop_combo.set(self.settings["stop_bits"])
        self.stop_combo.grid(row=1, column=5, sticky="w", pady=5)

        lbl("Задержка между кадрами, мс", 2, 0)
        self.delay_var = tk.IntVar(value=self.settings["line_delay_ms"])
        ttk.Spinbox(grid, from_=0, to=1000, width=6, textvariable=self.delay_var).grid(row=2, column=1, sticky="w", pady=5)
        for col in (1, 3):
            grid.columnconfigure(col, minsize=180)

        self.wrap_var = tk.BooleanVar(value=self.settings["wrap_percent"])
        ttk.Checkbutton(conn, text="Автоматически добавлять % в начало и конец программы",
                        variable=self.wrap_var).pack(anchor="w", padx=4, pady=(2, 4))

        act = ttk.Frame(f, padding=(0, 10, 0, 0))
        act.pack(fill="x", padx=10)
        self.rs_send_btn = ttk.Button(act, text="▼  Отправить на станок", style="Accent.TButton", command=self._rs_send)
        self.rs_send_btn.pack(side="left")
        self.rs_cancel_btn = ttk.Button(act, text="Отмена", command=self._rs_on_cancel, state="disabled")
        self.rs_cancel_btn.pack(side="left", padx=8)
        self.rs_status = ttk.Label(act, text="Готово", style="Hint.TLabel")
        self.rs_status.pack(side="left", padx=10)
        self.rs_progress = ttk.Progressbar(act, orient="horizontal", mode="determinate", length=300,
                                           style="Accent.Horizontal.TProgressbar")
        self.rs_progress.pack(side="right")

        lo, _, lb = self._panel(f, "Журнал")
        lo.pack(fill="both", expand=True, padx=10, pady=10)
        self.rs_log_w = scrolledtext.ScrolledText(
            lb, height=10, state="disabled", font=("Consolas", 9), bg="#fbfbfa", fg=C["text"],
            relief="flat", borderwidth=0, highlightthickness=0,
        )
        self.rs_log_w.pack(fill="both", expand=True)

        self._refresh_ports()

    def _rs_log(self, text):
        self.rs_log_w.configure(state="normal")
        self.rs_log_w.insert("end", time.strftime("%H:%M:%S  ") + text + "\n")
        self.rs_log_w.see("end")
        self.rs_log_w.configure(state="disabled")

    def _rs_browse(self):
        path = filedialog.askopenfilename(
            title="Выберите файл УП",
            filetypes=[("NC программы", "*.nc *.txt *.tap *.eia"), ("Все файлы", "*.*")],
        )
        if path:
            self.rs_file.set(path)

    def _refresh_ports(self):
        ports = [p.device for p in serial.tools.list_ports.comports()]
        self.port_combo["values"] = ports
        if self.settings["port"] in ports:
            self.port_combo.set(self.settings["port"])
        elif ports:
            self.port_combo.set(ports[0])
        else:
            self.port_combo.set("")

    def _rs_settings(self):
        return {
            "port": self.port_combo.get(),
            "baud": int(self.baud_combo.get()),
            "data_bits": self.data_combo.get(),
            "parity": self.parity_combo.get(),
            "stop_bits": self.stop_combo.get(),
            "flow": self.flow_combo.get(),
            "wrap_percent": self.wrap_var.get(),
            "line_delay_ms": self.delay_var.get(),
        }

    def _rs_send(self):
        path = self.rs_file.get().strip()
        if not path or not os.path.isfile(path):
            messagebox.showerror("Ошибка", "Выберите существующий файл УП.")
            return
        s = self._rs_settings()
        if not s["port"]:
            messagebox.showerror("Ошибка", "Выберите COM-порт.")
            return
        self.settings.update(s)
        save_settings(self.settings)

        self.rs_send_btn.configure(state="disabled")
        self.rs_cancel_btn.configure(state="normal")
        self.rs_cancel.clear()
        self.rs_progress["value"] = 0
        self.rs_status.configure(text="Отправка...")
        self.rs_worker = threading.Thread(target=self._rs_worker, args=(path, s), daemon=True)
        self.rs_worker.start()

    def _rs_on_cancel(self):
        self.rs_cancel.set()
        self._rs_log("Отмена запрошена...")

    def _rs_worker(self, path, s):
        try:
            lines = prepare_program(path, s["wrap_percent"])
            total = len(lines)
            ser = serial.Serial(
                port=s["port"],
                baudrate=s["baud"],
                bytesize=DATA_BITS[s["data_bits"]],
                parity=PARITY[s["parity"]],
                stopbits=STOP_BITS[s["stop_bits"]],
                xonxoff=s["flow"] == "XON/XOFF",
                rtscts=s["flow"] == "RTS/CTS",
                timeout=1,
                write_timeout=10,
            )
            self.post(self._rs_log, f"Порт {s['port']} открыт. Передача {total} кадров...")
            delay = s["line_delay_ms"] / 1000.0
            try:
                for i, line in enumerate(lines, start=1):
                    if self.rs_cancel.is_set():
                        self.post(self._rs_log, "Передача прервана пользователем.")
                        break
                    ser.write((line + "\n").encode("ascii", errors="replace"))
                    ser.flush()
                    if delay > 0:
                        time.sleep(delay)
                    pct = int(i / total * 100)
                    self.post(self._rs_progress_upd, pct, f"Отправлено кадров: {i} / {total} ({pct}%)")
                else:
                    self.post(self._rs_log, "Передача завершена успешно.")
            finally:
                ser.close()
        except Exception as e:
            self.post(self._rs_log, f"Ошибка: {e}")
            self.post(messagebox.showerror, "Ошибка", str(e))
        finally:
            self.post(self._rs_reset)

    def _rs_progress_upd(self, pct, text):
        self.rs_progress["value"] = pct
        self.rs_status.configure(text=text)

    def _rs_reset(self):
        self.rs_send_btn.configure(state="normal")
        self.rs_cancel_btn.configure(state="disabled")
        self.rs_status.configure(text="Готово")

    # ---------- выход ----------

    def _on_close(self):
        if self.rs_worker and self.rs_worker.is_alive():
            if not messagebox.askyesno("Выход", "Идёт передача по RS232. Прервать и выйти?"):
                return
            self.rs_cancel.set()
        self.settings["pc_last_dir"] = self.pc_cur_dir or ""
        save_settings(self.settings)
        if self.clients:
            done = threading.Event()
            clients = list(self.clients.values())
            self.clients.clear()

            def close_all():
                for c in clients:
                    c.disconnect()
                done.set()

            self.worker.q.put((close_all, None, None))
            done.wait(3)
        self.destroy()


if __name__ == "__main__":
    app = ProgramTransferTool()
    app.mainloop()
