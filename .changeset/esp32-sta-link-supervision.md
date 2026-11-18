---
"tizen-os-ambilight": patch
---

ESP32: the station link is supervised again after a Wi-Fi dropout. The board now re-begins the saved network every 60 s and restarts itself if it stays down for 90 s, instead of sitting silent with the strip blanked until it is power-cycled. `GET /metrics` also reports `reset_reason` and `wifi_status`, so a report can say why the board restarted.
