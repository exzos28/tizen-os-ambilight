#!/usr/bin/env python3
"""
led_tester.py — Send test ambilight frames to the ESP32 over UDP.

Usage:
    python led_tester.py --host 192.168.1.42 --mode rainbow
    python led_tester.py --host 192.168.1.42 --mode solid --color 255 0 0
    python led_tester.py --host 192.168.1.42 --mode sweep --fps 30
    python led_tester.py --host 192.168.1.42 --mode sides
    python led_tester.py --host 192.168.1.42 --mode off

Packet format (same as TV capture service):
    [hCount: 1B][vCount: 1B]
    [top:    hCount * 3 bytes  RGB, left→right ]
    [right:  vCount * 3 bytes  RGB, top→bottom ]
    [bottom: hCount * 3 bytes  RGB, right→left ]
    [left:   vCount * 3 bytes  RGB, bottom→top ]

Modes:
    solid   — fill all sides with one color (default white)
    rainbow — rotating hue around the perimeter
    sweep   — single bright dot chasing around the perimeter
    sides   — each side a different fixed color (mirrors calibration preview)
    off     — all LEDs off
"""

import argparse
import colorsys
import math
import socket
import sys
import time

UDP_PORT = 9001


# ---------------------------------------------------------------------------
# Frame building
# ---------------------------------------------------------------------------

def make_packet(h_count: int, v_count: int,
                top: list, right: list, bottom: list, left: list) -> bytes:
    """Build a raw UDP packet from per-side RGB lists.

    Each list contains `count` entries of (r, g, b).
    """
    buf = bytearray([h_count, v_count])
    for samples in (top, right, bottom, left):
        for r, g, b in samples:
            buf += bytes([r & 0xFF, g & 0xFF, b & 0xFF])
    return bytes(buf)


def hsv_to_rgb(h: float, s: float = 1.0, v: float = 1.0):
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, s, v)
    return int(r * 255), int(g * 255), int(b * 255)


# ---------------------------------------------------------------------------
# Generators — each yields (top, right, bottom, left) sample lists
# ---------------------------------------------------------------------------

def gen_solid(h_count, v_count, r, g, b):
    color = [(r, g, b)]
    while True:
        yield (color * h_count, color * v_count, color * h_count, color * v_count)


def gen_off(h_count, v_count):
    yield from gen_solid(h_count, v_count, 0, 0, 0)


def gen_rainbow(h_count, v_count, fps):
    """Rotating hue: the hue shifts uniformly around the entire perimeter."""
    total = (h_count + v_count) * 2
    t = 0.0
    dt = 1.0 / fps
    while True:
        # Assign each sample point a position [0, total) along the perimeter.
        # Perimeter order: top (l→r), right (t→b), bottom (r→l), left (b→t)
        def side_colors(offset: int, count: int):
            return [hsv_to_rgb((offset + i) / total + t) for i in range(count)]

        off_top    = 0
        off_right  = h_count
        off_bottom = h_count + v_count
        off_left   = h_count + v_count + h_count

        top    = side_colors(off_top,    h_count)
        right  = side_colors(off_right,  v_count)
        bottom = side_colors(off_bottom, h_count)
        left   = side_colors(off_left,   v_count)

        yield (top, right, bottom, left)
        t += dt * 0.3   # hue shift speed


def gen_sweep(h_count, v_count, fps):
    """A single white dot travels around the perimeter."""
    total = (h_count + v_count) * 2
    pos = 0.0
    dt = 1.0 / fps
    tail = 4   # number of dimmer trailing LEDs

    while True:
        frame: list[tuple[int,int,int]] = [(0, 0, 0)] * total
        for t in range(tail + 1):
            idx = int(pos - t) % total
            brightness = 1.0 - t / (tail + 1)
            v = int(255 * brightness)
            frame[idx] = (v, v, v)

        top    = frame[0            : h_count]
        right  = frame[h_count      : h_count + v_count]
        bottom = frame[h_count + v_count : h_count + v_count + h_count]
        left   = frame[h_count + v_count + h_count :]

        yield (top, right, bottom, left)
        pos = (pos + fps * dt * 0.8) % total   # speed: ~0.8 perimeter/sec


def gen_sides(h_count, v_count):
    """Each side a distinct color — mirrors the calibration preview on the ESP32."""
    colors = {
        'top':    (249, 115, 22),   # orange
        'right':  ( 34, 197, 94),   # green
        'bottom': ( 14, 165, 233),  # blue
        'left':   (168,  85, 247),  # purple
    }
    top    = [colors['top']]    * h_count
    right  = [colors['right']]  * v_count
    bottom = [colors['bottom']] * h_count
    left   = [colors['left']]   * v_count
    while True:
        yield (top, right, bottom, left)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description='ESP32 ambilight LED tester')
    parser.add_argument('--host',  required=True,  help='ESP32 IP address')
    parser.add_argument('--port',  type=int, default=UDP_PORT, help=f'UDP port (default {UDP_PORT})')
    parser.add_argument('--mode',  default='rainbow',
                        choices=['solid', 'rainbow', 'sweep', 'sides', 'off'],
                        help='Test pattern')
    parser.add_argument('--fps',   type=float, default=30,  help='Frames per second')
    parser.add_argument('--h',     type=int,   default=6,   help='Horizontal sample count (default 6)')
    parser.add_argument('--v',     type=int,   default=4,   help='Vertical sample count (default 4)')
    parser.add_argument('--color', type=int,   nargs=3, default=[255, 255, 255],
                        metavar=('R', 'G', 'B'), help='Color for solid mode (default 255 255 255)')
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    interval = 1.0 / args.fps

    print(f'Sending {args.mode!r} to {args.host}:{args.port}  '
          f'({args.h}h + {args.v}v samples, {args.fps} fps)')
    print('Press Ctrl-C to stop.')

    if args.mode == 'solid':
        gen = gen_solid(args.h, args.v, *args.color)
    elif args.mode == 'rainbow':
        gen = gen_rainbow(args.h, args.v, args.fps)
    elif args.mode == 'sweep':
        gen = gen_sweep(args.h, args.v, args.fps)
    elif args.mode == 'sides':
        gen = gen_sides(args.h, args.v)
    else:  # off
        gen = gen_off(args.h, args.v)

    try:
        frame = 0
        t0 = time.monotonic()
        for top, right, bottom, left in gen:
            pkt = make_packet(args.h, args.v, top, right, bottom, left)
            sock.sendto(pkt, (args.host, args.port))
            frame += 1

            # Rate limiting: sleep until next frame deadline.
            deadline = t0 + frame * interval
            wait = deadline - time.monotonic()
            if wait > 0:
                time.sleep(wait)

            # Print FPS every second.
            elapsed = time.monotonic() - t0
            if frame % max(1, int(args.fps)) == 0:
                actual_fps = frame / elapsed if elapsed > 0 else 0
                print(f'\r{actual_fps:.1f} fps  frame {frame}', end='', flush=True)

    except KeyboardInterrupt:
        print('\nStopped.')
    finally:
        # Send one black frame to turn off LEDs.
        black = [(0, 0, 0)]
        pkt = make_packet(args.h, args.v,
                          black * args.h, black * args.v,
                          black * args.h, black * args.v)
        sock.sendto(pkt, (args.host, args.port))
        sock.close()


if __name__ == '__main__':
    main()
