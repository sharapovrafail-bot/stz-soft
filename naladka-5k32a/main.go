package main

import (
	"embed"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"sync/atomic"
	"time"
)

//go:embed static/*
var content embed.FS

var lastPing atomic.Int64

func jobsDir() string {
	// Папка с деталями: Документы\Зубофреза 5К32А\Детали (доступна на запись и после установки)
	var cands []string
	if h, err := os.UserHomeDir(); err == nil {
		cands = append(cands, filepath.Join(h, "Documents", "Зубофреза 5К32А", "Детали"), filepath.Join(h, "Документы", "Зубофреза 5К32А", "Детали"))
		if st, err := os.Stat(filepath.Join(h, "Documents")); err == nil && st.IsDir() {
			cands = cands[:1]
		} else if st, err := os.Stat(filepath.Join(h, "Документы")); err == nil && st.IsDir() {
			cands = cands[1:2]
		} else {
			cands = cands[:1]
		}
	}
	if exe, err := os.Executable(); err == nil {
		cands = append(cands, filepath.Join(filepath.Dir(exe), "Детали"))
	}
	for _, d := range cands {
		if os.MkdirAll(d, 0o755) == nil {
			return d
		}
	}
	return "."
}

func drawDir() string {
	d := filepath.Join(filepath.Dir(jobsDir()), "Чертежи")
	os.MkdirAll(d, 0o755)
	return d
}

func xferDir() string {
	d := filepath.Join(filepath.Dir(jobsDir()), "Обмен")
	os.MkdirAll(d, 0o755)
	return d
}

func safeName(n string) (string, bool) {
	n = filepath.Base(n)
	if n == "." || n == ".." || n == "" || strings.ContainsAny(n, `\/:*?"<>|`) {
		return "", false
	}
	if !strings.HasSuffix(strings.ToLower(n), ".json") {
		n += ".json"
	}
	return n, true
}

func main() {
	if alreadyRunning() {
		return
	}
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
	addr := ln.Addr().String()
	url := fmt.Sprintf("http://%s/", addr)
	fmt.Println(url)
	mux := http.NewServeMux()
	guard := func(h http.HandlerFunc) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			if r.Host != addr || r.Header.Get("X-App") != "1" {
				http.Error(w, "forbidden", 403)
				return
			}
			if !strings.HasPrefix(r.URL.Path, "/api/lic") && !LicOK() {
				http.Error(w, "license", 402)
				return
			}
			h(w, r)
		}
	}
	mux.HandleFunc("/ping", func(w http.ResponseWriter, r *http.Request) {
		lastPing.Store(time.Now().Unix())
		w.WriteHeader(204)
	})
	mux.HandleFunc("/quit", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(204)
		go func() { time.Sleep(200 * time.Millisecond); os.Exit(0) }()
	})
	mux.HandleFunc("/api/lic", guard(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(LicCheck())
	}))
	mux.HandleFunc("/api/lic/activate", guard(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != "POST" {
			http.Error(w, "method", 405)
			return
		}
		b, _ := io.ReadAll(io.LimitReader(r.Body, 1024))
		ok := LicActivate(string(b))
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(map[string]any{"ok": ok, "status": LicCheck()})
	}))
	mux.HandleFunc("/api/dirs", guard(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(map[string]string{"jobs": jobsDir(), "drawings": drawDir()})
	}))
	mux.HandleFunc("/api/drawings", guard(func(w http.ResponseWriter, r *http.Request) {
		type item struct {
			File string `json:"file"`
			Size int64  `json:"size"`
			Time int64  `json:"time"`
		}
		out := []item{}
		es, _ := os.ReadDir(drawDir())
		for _, e := range es {
			if e.IsDir() {
				continue
			}
			st, err := e.Info()
			if err != nil {
				continue
			}
			out = append(out, item{e.Name(), st.Size(), st.ModTime().Unix()})
		}
		sort.Slice(out, func(i, j int) bool { return out[i].Time > out[j].Time })
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(out)
	}))
	mux.HandleFunc("/api/drawing", guard(func(w http.ResponseWriter, r *http.Request) {
		n := filepath.Base(r.URL.Query().Get("file"))
		if n == "." || n == ".." || n == "" || strings.ContainsAny(n, `\/:*?"<>|`) {
			http.Error(w, "name", 400)
			return
		}
		p := filepath.Join(drawDir(), n)
		switch r.Method {
		case "POST":
			f, err := os.Create(p)
			if err != nil {
				http.Error(w, err.Error(), 500)
				return
			}
			_, err = io.Copy(f, io.LimitReader(r.Body, 60<<20))
			f.Close()
			if err != nil {
				http.Error(w, err.Error(), 500)
				return
			}
			w.WriteHeader(204)
		case "GET":
			http.ServeFile(w, r, p)
		case "DELETE":
			os.Remove(p)
			w.WriteHeader(204)
		default:
			http.Error(w, "method", 405)
		}
	}))
	mux.HandleFunc("/api/xfer", guard(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != "POST" {
			http.Error(w, "method", 405)
			return
		}
		n, ok := safeName(r.URL.Query().Get("file"))
		if !ok {
			http.Error(w, "name", 400)
			return
		}
		b, _ := io.ReadAll(io.LimitReader(r.Body, 1<<20))
		if !json.Valid(b) {
			http.Error(w, "json", 400)
			return
		}
		if err := os.WriteFile(filepath.Join(xferDir(), n), b, 0o644); err != nil {
			http.Error(w, err.Error(), 500)
			return
		}
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(map[string]string{"path": filepath.Join(xferDir(), n)})
	}))
	mux.HandleFunc("/api/jobs", guard(func(w http.ResponseWriter, r *http.Request) {
		type item struct {
			File string          `json:"file"`
			Data json.RawMessage `json:"data"`
			Time int64           `json:"time"`
		}
		out := []item{}
		files, _ := filepath.Glob(filepath.Join(jobsDir(), "*.json"))
		for _, f := range files {
			b, err := os.ReadFile(f)
			if err != nil || !json.Valid(b) {
				continue
			}
			st, _ := os.Stat(f)
			out = append(out, item{filepath.Base(f), b, st.ModTime().Unix()})
		}
		sort.Slice(out, func(i, j int) bool { return out[i].Time > out[j].Time })
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		json.NewEncoder(w).Encode(out)
	}))
	mux.HandleFunc("/api/job", guard(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != "POST" {
			http.Error(w, "method", 405)
			return
		}
		n, ok := safeName(r.URL.Query().Get("file"))
		if !ok {
			http.Error(w, "name", 400)
			return
		}
		b, _ := io.ReadAll(io.LimitReader(r.Body, 1<<20))
		if !json.Valid(b) {
			http.Error(w, "json", 400)
			return
		}
		if err := os.WriteFile(filepath.Join(jobsDir(), n), b, 0o644); err != nil {
			http.Error(w, err.Error(), 500)
			return
		}
		w.WriteHeader(204)
	}))
	mux.HandleFunc("/api/job/delete", guard(func(w http.ResponseWriter, r *http.Request) {
		n, ok := safeName(r.URL.Query().Get("file"))
		if !ok || r.Method != "POST" {
			http.Error(w, "name", 400)
			return
		}
		os.Remove(filepath.Join(jobsDir(), n))
		w.WriteHeader(204)
	}))
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		p := r.URL.Path
		if p == "/" {
			p = "/index.html"
		}
		b, err := content.ReadFile("static" + p)
		if err != nil {
			http.NotFound(w, r)
			return
		}
		switch {
		case strings.HasSuffix(p, ".html"):
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
		case strings.HasSuffix(p, ".jpg"):
			w.Header().Set("Content-Type", "image/jpeg")
		}
		w.Write(b)
	})
	lastPing.Store(time.Now().Unix())
	go http.Serve(ln, mux)
	// Основной режим — собственное окно программы. Закрыли окно — программа завершилась.
	if openWindow(url) {
		return
	}
	// Запасной режим (нет WebView2 / не Windows): браузер + выход при закрытии вкладки
	go func() {
		for {
			time.Sleep(5 * time.Second)
			if time.Now().Unix()-lastPing.Load() > 20 {
				os.Exit(0)
			}
		}
	}()
	// Встроенный режим (macOS): окно программы открывает STZ, браузер не нужен
	if os.Getenv("STZ_EMBED") == "1" {
		select {}
	}
	switch runtime.GOOS {
	case "windows":
		exec.Command("rundll32", "url.dll,FileProtocolHandler", url).Start()
	case "darwin":
		exec.Command("open", url).Start()
	default:
		exec.Command("xdg-open", url).Start()
	}
	select {}
}
