#!/usr/bin/env python3
"""HTTP + UDP server with tkinter GUI — shows edge LED pixels around a rectangle."""

import json
import socket
import struct
import threading
import time
import tkinter as tk
from datetime import datetime, timezone
from http.server import HTTPServer, BaseHTTPRequestHandler
from tkinter import scrolledtext

HTTP_PORT = 9000
UDP_PORT = 9001
DEBUG_PORT = 9002
DISCOVERY_PORT = 9003
DISCOVERY_MAGIC = "AMBILIGHT_DISCOVER"

# Shared state
latest_edges = None    # (hCount, vCount, rgb_bytes)
latest_debug_frame = None  # (width, height, rgb_bytes)
frame_count = 0
frame_times = []
server_fps = 0.0
device_stats = ""
lock = threading.Lock()
gui_ref = None


class Gui:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("TV Capture Server")
        self.root.configure(bg="#1a1a2e")
        self.root.geometry("900x700")

        top = tk.Frame(self.root, bg="#1a1a2e")
        top.pack(fill=tk.X, padx=10, pady=(10, 5))

        self.info_label = tk.Label(top, text="Waiting for frames...",
                                   fg="#e0e0e0", bg="#1a1a2e", font=("Menlo", 12))
        self.info_label.pack(side=tk.LEFT)

        self.fps_label = tk.Label(top, text="FPS: --", fg="#00ff88", bg="#1a1a2e", font=("Menlo", 12))
        self.fps_label.pack(side=tk.RIGHT)

        self.stats_label = tk.Label(self.root, text="", fg="#aaa", bg="#1a1a2e",
                                     font=("Menlo", 10), anchor=tk.W)
        self.stats_label.pack(fill=tk.X, padx=10)

        self.canvas = tk.Canvas(self.root, bg="#111", highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

        self.log = scrolledtext.ScrolledText(self.root, height=8, bg="#0d1117",
                                              fg="#c9d1d9", font=("Menlo", 10),
                                              insertbackground="#c9d1d9", wrap=tk.WORD)
        self.log.pack(fill=tk.X, padx=10, pady=(5, 10))
        self.log.configure(state=tk.DISABLED)

        self._last_frame_id = 0
        self._debug_win = None
        self._debug_canvas = None
        self._debug_photo = None
        self._check_updates()

    def _check_updates(self):
        with lock:
            edges = latest_edges
            fps = server_fps
            stats = device_stats
            fc = frame_count
            debug = latest_debug_frame

        if edges and fc != self._last_frame_id:
            self._last_frame_id = fc
            self._draw_edges(edges)
        if debug:
            self._draw_debug_frame(debug)

        self.fps_label.config(text=f"FPS: {fps:.1f}")
        if stats:
            self.stats_label.config(text=stats)

        self.root.after(16, self._check_updates)

    def _draw_edges(self, edges):
        hc, vc, rgb = edges
        total = (hc + vc) * 2
        if len(rgb) < total * 3:
            return

        cw = self.canvas.winfo_width()
        ch = self.canvas.winfo_height()
        if cw < 10 or ch < 10:
            return

        self.canvas.delete("all")

        # Strip thickness
        s = max(6, min(cw, ch) // 20)

        # TV rect = inner area after strips
        tv_x0, tv_y0 = s, s
        tv_x1, tv_y1 = cw - s, ch - s
        self.canvas.create_rectangle(tv_x0, tv_y0, tv_x1, tv_y1, fill="#222", outline="#333")

        idx = 0

        # Top: hc segments across full width, corners included
        for i in range(hc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x0 = i * cw // hc
            x1 = (i + 1) * cw // hc
            self.canvas.create_rectangle(x0, 0, x1, s, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Right: vc segments, between top and bottom strips
        for i in range(vc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            y0 = s + i * (ch - 2 * s) // vc
            y1 = s + (i + 1) * (ch - 2 * s) // vc
            self.canvas.create_rectangle(cw - s, y0, cw, y1, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Bottom: hc segments R→L, corners included
        for i in range(hc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x0 = (hc - 1 - i) * cw // hc
            x1 = (hc - i) * cw // hc
            self.canvas.create_rectangle(x0, ch - s, x1, ch, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Left: vc segments B→T, between top and bottom strips
        for i in range(vc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            y0 = s + (vc - 1 - i) * (ch - 2 * s) // vc
            y1 = s + (vc - i) * (ch - 2 * s) // vc
            self.canvas.create_rectangle(0, y0, s, y1, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        self.info_label.config(text=f"Frame #{frame_count}  {hc}h+{vc}v = {total} LEDs")

    def _draw_debug_frame(self, frame):
        w, h, rgb = frame
        scale = 8
        dw, dh = w * scale, h * scale

        if self._debug_win is None or not self._debug_win.winfo_exists():
            self._debug_win = tk.Toplevel(self.root)
            self._debug_win.title("Debug Frame")
            self._debug_win.geometry(f"{dw}x{dh}")
            self._debug_canvas = tk.Canvas(self._debug_win, width=dw, height=dh,
                                           bg="#000", highlightthickness=0)
            self._debug_canvas.pack()

        photo = tk.PhotoImage(width=dw, height=dh)
        # Build PPM-style row data for PhotoImage
        for y in range(h):
            row = []
            for x in range(w):
                idx = (y * w + x) * 3
                r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]
                color = f"#{r:02x}{g:02x}{b:02x}"
                row.extend([color] * scale)
            row_str = " ".join(row)
            for sy in range(scale):
                photo.put(f"{{{row_str}}}", to=(0, y * scale + sy))

        self._debug_photo = photo
        self._debug_canvas.delete("all")
        self._debug_canvas.create_image(0, 0, anchor=tk.NW, image=photo)

    def add_log(self, text):
        self.root.after(0, self._append_log, text)

    def _append_log(self, text):
        self.log.configure(state=tk.NORMAL)
        self.log.insert(tk.END, text + "\n")
        self.log.see(tk.END)
        self.log.configure(state=tk.DISABLED)

    def run(self):
        self.root.mainloop()


def update_fps():
    global server_fps, frame_times
    now = time.monotonic()
    frame_times.append(now)
    cutoff = now - 2.0
    frame_times = [t for t in frame_times if t > cutoff]
    if len(frame_times) >= 2:
        span = frame_times[-1] - frame_times[0]
        server_fps = (len(frame_times) - 1) / span if span > 0 else 0
    else:
        server_fps = 0


def udp_listener():
    """Receive edge packets: [1B hCount][1B vCount][RGB * (h+v)*2]."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", UDP_PORT))
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 64 * 1024)
    print(f"UDP listener on port {UDP_PORT}")

    while True:
        try:
            data, addr = sock.recvfrom(65535)
            if len(data) < 3:
                continue

            hc = data[0]
            vc = data[1]
            total = (hc + vc) * 2
            rgb = data[2:]

            if len(rgb) < total * 3:
                continue

            global latest_edges, frame_count
            with lock:
                frame_count += 1
                latest_edges = (hc, vc, rgb[:total * 3])
                update_fps()

        except Exception as e:
            print(f"UDP error: {e}")


def debug_udp_listener():
    """Receive full frame: [2B width][2B height][RGB * w * h]."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", DEBUG_PORT))
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 256 * 1024)
    print(f"Debug UDP listener on port {DEBUG_PORT}")

    while True:
        try:
            data, addr = sock.recvfrom(65535)
            if len(data) < 5:
                continue
            w = (data[0] << 8) | data[1]
            h = (data[2] << 8) | data[3]
            rgb = data[4:]
            if len(rgb) < w * h * 3:
                continue
            global latest_debug_frame
            with lock:
                latest_debug_frame = (w, h, rgb[:w * h * 3])
        except Exception as e:
            print(f"Debug UDP error: {e}")


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(length)

        try:
            event = json.loads(body)
        except json.JSONDecodeError:
            self._reply(400, "bad json")
            return

        global device_stats
        ts = datetime.now(timezone.utc).strftime("%H:%M:%S")
        ev_name = event.get('event', '?')
        data = event.get('data', '')
        data_str = f"  data={data}" if data else ""
        line = f"[{ts}] {ev_name:25s}  {data_str}"
        print(line.strip())

        if ev_name == 'Stats' and data:
            with lock:
                device_stats = f"Device: {data}"

        if gui_ref:
            gui_ref.add_log(line.strip())

        self._reply(200, json.dumps({"status": "ok"}))

    def _reply(self, code, body):
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(body.encode() if isinstance(body, str) else body)

    def log_message(self, format, *args):
        pass


def get_local_ip():
    """Get this machine's LAN IP by connecting to a broadcast address."""
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("255.255.255.255", 1))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"


def discovery_listener():
    """Respond to UDP broadcast discovery probes from TV service."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind(("0.0.0.0", DISCOVERY_PORT))
    local_ip = get_local_ip()
    reply = f"{DISCOVERY_MAGIC}:{local_ip}".encode()
    print(f"Discovery listener on port {DISCOVERY_PORT} (advertising {local_ip})")

    while True:
        try:
            data, addr = sock.recvfrom(1024)
            if data == DISCOVERY_MAGIC.encode():
                sock.sendto(reply, addr)
                msg = f"Discovery: {addr[0]} found us"
                print(msg)
                if gui_ref:
                    gui_ref.add_log(msg)
        except Exception as e:
            print(f"Discovery error: {e}")


def start_http():
    server = HTTPServer(("0.0.0.0", HTTP_PORT), Handler)
    print(f"HTTP server on port {HTTP_PORT}")
    server.serve_forever()


if __name__ == "__main__":
    gui = Gui()
    gui_ref = gui

    threading.Thread(target=start_http, daemon=True).start()
    threading.Thread(target=udp_listener, daemon=True).start()
    threading.Thread(target=debug_udp_listener, daemon=True).start()
    threading.Thread(target=discovery_listener, daemon=True).start()

    local_ip = get_local_ip()
    gui.add_log(f"HTTP:{HTTP_PORT}  UDP:{UDP_PORT}  DEBUG:{DEBUG_PORT}  DISCOVERY:{DISCOVERY_PORT}")
    gui.add_log(f"Local IP: {local_ip} (advertised to TV)")
    gui.run()
