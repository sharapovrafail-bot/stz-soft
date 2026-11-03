//go:build windows

package main

import (
	"os"
	"path/filepath"
	"strings"
	"syscall"
	"unsafe"

	"github.com/jchv/go-webview2"
)

const appVersion = "0.7.0"

func openWindow(url string) (ok bool) {
	defer func() {
		if recover() != nil {
			ok = false
		}
	}()
	data := filepath.Join(os.Getenv("LOCALAPPDATA"), "Zuborez525", "webview")
	os.MkdirAll(data, 0o755)
	w := webview2.NewWithOptions(webview2.WebViewOptions{
		AutoFocus: true,
		DataPath:  data,
		WindowOptions: webview2.WindowOptions{
			Title:  "Зуборез 525 — наладка станка для конических колёс · версия " + appVersion + " · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru",
			Width:  1366,
			Height: 860,
			Center: true,
			IconId: 1,
		},
	})
	if w == nil {
		return false
	}
	defer w.Destroy()
	w.Navigate(url)
	w.Run()
	return true
}

var (
	user32   = syscall.NewLazyDLL("user32.dll")
	kernel32 = syscall.NewLazyDLL("kernel32.dll")
)

// alreadyRunning: если программа уже запущена — выводит её окно вперёд и возвращает true.
func alreadyRunning() bool {
	name, _ := syscall.UTF16PtrFromString(`Local\Zuborez525_SpecTehZakaz`)
	h, _, err := kernel32.NewProc("CreateMutexW").Call(0, 0, uintptr(unsafe.Pointer(name)))
	if h == 0 {
		return false
	}
	if en, ok := err.(syscall.Errno); !ok || en != 183 {
		return false
	}
	var found uintptr
	cb := syscall.NewCallback(func(hwnd uintptr, _ uintptr) uintptr {
		buf := make([]uint16, 256)
		user32.NewProc("GetWindowTextW").Call(hwnd, uintptr(unsafe.Pointer(&buf[0])), 256)
		if strings.HasPrefix(syscall.UTF16ToString(buf), "Зуборез 525 — наладка") {
			found = hwnd
			return 0
		}
		return 1
	})
	user32.NewProc("EnumWindows").Call(cb, 0)
	if found == 0 {
		return false
	}
	user32.NewProc("ShowWindow").Call(found, 9)
	user32.NewProc("SetForegroundWindow").Call(found)
	return true
}
