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

# Shared state
latest_edges = None    # (hCount, vCount, rgb_bytes)
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
        self._check_updates()

    def _check_updates(self):
        with lock:
            edges = latest_edges
            fps = server_fps
            stats = device_stats
            fc = frame_count

        if edges and fc != self._last_frame_id:
            self._last_frame_id = fc
            self._draw_edges(edges)

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

        # Draw dark TV rectangle in center
        margin = 40
        tv_x0, tv_y0 = margin, margin
        tv_x1, tv_y1 = cw - margin, ch - margin
        tv_w = tv_x1 - tv_x0
        tv_h = tv_y1 - tv_y0
        self.canvas.create_rectangle(tv_x0, tv_y0, tv_x1, tv_y1, fill="#222", outline="#333")

        idx = 0
        sq = max(4, min(margin - 4, tv_w // hc, tv_h // vc))

        # Top: left to right
        for i in range(hc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x = tv_x0 + i * tv_w // hc + (tv_w // hc - sq) // 2
            y = tv_y0 - sq - 2
            self.canvas.create_rectangle(x, y, x + sq, y + sq, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Right: top to bottom
        for i in range(vc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x = tv_x1 + 2
            y = tv_y0 + i * tv_h // vc + (tv_h // vc - sq) // 2
            self.canvas.create_rectangle(x, y, x + sq, y + sq, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Bottom: right to left
        for i in range(hc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x = tv_x0 + (hc - 1 - i) * tv_w // hc + (tv_w // hc - sq) // 2
            y = tv_y1 + 2
            self.canvas.create_rectangle(x, y, x + sq, y + sq, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        # Left: bottom to top
        for i in range(vc):
            r, g, b = rgb[idx], rgb[idx+1], rgb[idx+2]; idx += 3
            x = tv_x0 - sq - 2
            y = tv_y0 + (vc - 1 - i) * tv_h // vc + (tv_h // vc - sq) // 2
            self.canvas.create_rectangle(x, y, x + sq, y + sq, fill=f"#{r:02x}{g:02x}{b:02x}", outline="")

        self.info_label.config(text=f"Frame #{frame_count}  {hc}h+{vc}v = {total} LEDs")

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


def start_http():
    server = HTTPServer(("0.0.0.0", HTTP_PORT), Handler)
    print(f"HTTP server on port {HTTP_PORT}")
    server.serve_forever()


if __name__ == "__main__":
    gui = Gui()
    gui_ref = gui

    threading.Thread(target=start_http, daemon=True).start()
    threading.Thread(target=udp_listener, daemon=True).start()

    gui.add_log(f"HTTP:{HTTP_PORT}  UDP:{UDP_PORT}")
    gui.run()
