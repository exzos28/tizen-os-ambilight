var SERVICE_APP_ID = 'fCAYP0k0uD.Service';
var buttons = [];
var focusIndex = 0;
var debugEnabled = false;

function hslToRgb(h, s, l) {
    s /= 100; l /= 100;
    var c = (1 - Math.abs(2 * l - 1)) * s;
    var x = c * (1 - Math.abs((h / 60) % 2 - 1));
    var m = l - c / 2;
    var r, g, b;
    if (h < 60)       { r = c; g = x; b = 0; }
    else if (h < 120) { r = x; g = c; b = 0; }
    else if (h < 180) { r = 0; g = c; b = x; }
    else if (h < 240) { r = 0; g = x; b = c; }
    else if (h < 300) { r = x; g = 0; b = c; }
    else               { r = c; g = 0; b = x; }
    return [(r + m) * 255 | 0, (g + m) * 255 | 0, (b + m) * 255 | 0];
}

var gradientAnimId = null;

function drawGradient() {
    var canvas = document.getElementById('gradient');
    var w = 96, h = 54;
    canvas.width = w;
    canvas.height = h;
    var ctx = canvas.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    var img = ctx.createImageData(w, h);
    var offset = 0;

    var cx = w / 2, cy = h / 2;
    var maxR = Math.sqrt(cx * cx + cy * cy);

    function frame() {
        var data = img.data;
        var i = 0;
        for (var y = 0; y < h; y++) {
            var dy = y - cy;
            for (var x = 0; x < w; x++) {
                var dx = x - cx;
                var angle = (Math.atan2(dy, dx) * 180 / Math.PI + 360 + offset) % 360;
                var dist = Math.sqrt(dx * dx + dy * dy);
                var sat = Math.min(dist / maxR, 1) * 100;
                var rgb = hslToRgb(angle, sat, 50);
                data[i++] = rgb[0];
                data[i++] = rgb[1];
                data[i++] = rgb[2];
                data[i++] = 255;
            }
        }
        ctx.putImageData(img, 0, 0);
        offset = (offset + 3) % 360;
        gradientAnimId = requestAnimationFrame(frame);
    }
    frame();
}

function stopGradient() {
    if (gradientAnimId !== null) {
        cancelAnimationFrame(gradientAnimId);
        gradientAnimId = null;
    }
}

function setDebug(enabled) {
    debugEnabled = enabled;
    var canvas = document.getElementById('gradient');
    var app = document.getElementById('app');
    var btn = document.getElementById('btnDebug');

    if (debugEnabled) {
        canvas.classList.add('visible');
        app.classList.add('debug-on');
        btn.textContent = 'Debug: ON';
        btn.classList.add('active');
        drawGradient();
    } else {
        stopGradient();
        canvas.classList.remove('visible');
        app.classList.remove('debug-on');
        btn.textContent = 'Debug: OFF';
        btn.classList.remove('active');
    }

    try { localStorage.setItem('debugEnabled', debugEnabled ? '1' : '0'); } catch (e) {}
}

function toggleDebug() {
    setDebug(!debugEnabled);
    log('Debug ' + (debugEnabled ? 'enabled' : 'disabled'));
}

function log(msg) {
    var logDiv = document.getElementById('log');
    var entry = document.createElement('div');
    entry.className = 'log-entry';
    var time = new Date().toLocaleTimeString();
    entry.textContent = '[' + time + '] ' + msg;
    logDiv.insertBefore(entry, logDiv.firstChild);
}

// ---------------------------------------------------------------- Status panel
//
// The service answers status requests on a local message port (tv/service/StatusPort.cs):
// IPC between the two apps on the TV. Polled every POLL_MS; the reply is JSON with
// capture, esp32, hyperhdr and configWarnings.

var STATUS_PORT = 'ambilight.status';
var UI_PORT = 'ambilight.ui';
var POLL_MS = 500;
var NO_REPLY_MS = 3000;      // port is there but the service doesn't answer
var STALL_MS = 2000;         // capture "running" but no frame for this long

var PREVIEW_W = 320, PREVIEW_H = 180, PREVIEW_EDGE = 28;

var localPort = null;
var waitingSince = 0;        // first unanswered request, 0 when the last one was answered
var prevStatus = null;
var logged = {};             // last logged text per key, to log changes only

function logChange(key, text) {
    if (logged[key] === text) return;
    logged[key] = text;
    log(text);
}

function setRow(id, text, cls) {
    var el = document.getElementById(id);
    el.textContent = text;
    el.className = 'value ' + cls;
}

function setVerdict(text, cls) {
    var el = document.getElementById('verdict');
    el.textContent = text;
    el.className = cls;
    logChange('verdict', text);
}

function setDetail(lines) {
    document.getElementById('detail').textContent = lines.join('\n');
}

function hwSummary(hw) {
    if (!hw) return '';
    return hw.fullW + '×' + hw.fullH + ', ' + hw.blocks + (hw.blocks === 1 ? ' slot' : ' slots') + ', ' + hw.delayMs + ' ms';
}

// -> { text, cls, verdict, verdictCls, detail }
function captureView(c) {
    var retry = c.retryInMs != null ? ' · retry in ' + Math.ceil(c.retryInMs / 1000) + ' s' : '';
    switch (c.state) {
        case 'waiting':
            return { text: 'Starting…', cls: 'muted', verdict: 'Checking your TV…', verdictCls: 'muted' };
        case 'starting':
            return {
                text: c.hw ? 'Hardware found (' + hwSummary(c.hw) + '), waiting for the first frame' : 'Initialising…',
                cls: 'warn', verdict: 'Checking your TV…', verdictCls: 'muted'
            };
        case 'failed':
            if (c.failure === 'library' || c.failure === 'symbols') {
                return {
                    text: 'Not available on this TV' + retry, cls: 'bad',
                    verdict: '✗ This TV does not expose the capture API', verdictCls: 'bad', detail: c.error
                };
            }
            return {
                text: 'Hardware not ready' + retry, cls: 'bad',
                verdict: '✗ Capture does not work on this TV right now', verdictCls: 'bad', detail: c.error
            };
        case 'running':
            if (c.lastFrameAgoMs > STALL_MS) {
                return {
                    text: 'No new frames for ' + Math.round(c.lastFrameAgoMs / 1000) + ' s', cls: 'bad',
                    verdict: '✗ Capture stalled', verdictCls: 'bad'
                };
            }
            if (c.badReads >= c.reads) {
                return {
                    text: 'The hardware returns errors (ret=' + c.lastBadRet + ')', cls: 'bad',
                    verdict: '✗ Capture does not work on this TV', verdictCls: 'bad'
                };
            }
            var text = 'Working · ' + (c.frameMs > 0 ? Math.round(1000 / c.frameMs) : 0) + ' fps · ' + hwSummary(c.hw);
            var ok = { verdict: '✓ Your TV supports screen capture', verdictCls: 'ok' };
            if (c.badReads > 0) {
                ok.text = text + ' · ' + c.badReads + ' of ' + c.reads + ' reads failed (ret=' + c.lastBadRet + ')';
                ok.cls = 'warn';
            } else {
                ok.text = text;
                ok.cls = 'ok';
            }
            ok.detail = 'Turn on Debug: the captured colors should follow the color wheel.';
            return ok;
    }
    return { text: 'Unknown state: ' + c.state, cls: 'muted', verdict: 'Checking your TV…', verdictCls: 'muted' };
}

// UDP has no replies: "sending" means the service hands frames to the network.
function esp32View(e, c, prev) {
    if (!e.enabled) return ['Off (ambilight.json)', 'muted'];
    if (!e.hosts.length) return ['Searching for controllers on the network…', 'warn'];
    var to = e.hosts.join(', ');
    var p = prev && prev.esp32 && prev.esp32.enabled ? prev.esp32 : { framesSent: 0, sendErrors: 0 };
    if (e.sendErrors > p.sendErrors) return ['Sending fails: ' + e.lastError, 'bad'];
    if (e.framesSent > p.framesSent) return ['Sending to ' + to, 'ok'];
    if (c.state !== 'running') return ['Found ' + to + ' — waiting for capture', 'warn'];
    return ['Found ' + to + ' — no frames sent', 'warn'];
}

function hyperHdrView(h) {
    if (!h.enabled) return ['Off (ambilight.json)', 'muted'];
    if (h.connected && h.replies > 0) return ['Connected to ' + h.endpoint, 'ok'];
    if (h.connected) return ['Connecting to ' + h.endpoint + '…', 'warn'];
    return ['Not connected to ' + h.endpoint + (h.lastError ? ': ' + h.lastError : ''), 'bad'];
}

function renderStatus(s) {
    setRow('rowService', 'Running', 'ok');
    logChange('service', 'Service is running');

    var cap = captureView(s.capture);
    setRow('rowCapture', cap.text, cap.cls);
    setVerdict(cap.verdict, cap.verdictCls);
    // Row texts tick (retry countdown, stall seconds): log only the error itself.
    if (cap.cls === 'bad' && cap.detail) logChange('captureError', 'Capture: ' + cap.detail);

    var esp = esp32View(s.esp32, s.capture, prevStatus);
    setRow('rowEsp32', esp[0], esp[1]);
    var hh = hyperHdrView(s.hyperhdr);
    setRow('rowHyperHdr', hh[0], hh[1]);

    var detail = [];
    if (cap.detail) detail.push(cap.detail);
    for (var i = 0; i < s.configWarnings.length; i++) detail.push('Config: ' + s.configWarnings[i]);
    setDetail(detail);

    renderPreview(s.capture, s.capture.state === 'running' && s.capture.lastFrameAgoMs <= STALL_MS);
    prevStatus = s;
}

function renderServiceDown(text) {
    setRow('rowService', text, 'bad');
    logChange('service', 'Service: ' + text);
    setRow('rowCapture', '—', 'muted');
    setRow('rowEsp32', '—', 'muted');
    setRow('rowHyperHdr', '—', 'muted');
    setVerdict('Press Start Service to check your TV', 'muted');
    setDetail([]);
    renderPreview(null, false);
    prevStatus = null;
}

function onStatusMessage(data) {
    for (var i = 0; i < data.length; i++) {
        if (data[i].key !== 'status') continue;
        var s;
        try { s = JSON.parse(data[i].value); } catch (e) { log('Bad status reply: ' + e.message); return; }
        waitingSince = 0;
        renderStatus(s);
    }
}

function pollStatus() {
    if (!localPort) return;
    var remote;
    try {
        // Throws NotFoundError while the service (or its port) isn't up.
        remote = tizen.messageport.requestRemoteMessagePort(SERVICE_APP_ID, STATUS_PORT);
    } catch (e) {
        waitingSince = 0;
        renderServiceDown('Not running — press Start Service');
        return;
    }
    try {
        remote.sendMessage([{ key: 'request', value: 'status' }], localPort);
    } catch (e) {
        renderServiceDown('Not responding (' + e.message + ')');
        return;
    }
    var now = Date.now();
    if (!waitingSince) waitingSince = now;
    else if (now - waitingSince > NO_REPLY_MS) renderServiceDown('Not responding');
}

function initStatus() {
    try {
        localPort = tizen.messageport.requestLocalMessagePort(UI_PORT);
        localPort.addMessagePortListener(onStatusMessage);
    } catch (e) {
        log('Status unavailable: ' + e.message);
        setVerdict('Status unavailable: ' + e.message, 'bad');
        return;
    }
    pollStatus();
    setInterval(pollStatus, POLL_MS);
}

// Mini screen with the captured samples in EdgeFrame order: top L→R, right T→B,
// bottom R→L, left B→T.
function buildPreview(box, h, v) {
    box.innerHTML = '';
    var W = PREVIEW_W, H = PREVIEW_H, T = PREVIEW_EDGE;
    var topW = W / h, sideH = (H - 2 * T) / v;
    function cell(x, y, w, hh) {
        var d = document.createElement('div');
        d.className = 'swatch';
        d.style.left = x + 'px';
        d.style.top = y + 'px';
        d.style.width = w + 'px';
        d.style.height = hh + 'px';
        box.appendChild(d);
    }
    var i;
    for (i = 0; i < h; i++) cell(i * topW, 0, topW, T);
    for (i = 0; i < v; i++) cell(W - T, T + i * sideH, T, sideH);
    for (i = 0; i < h; i++) cell((h - 1 - i) * topW, H - T, topW, T);
    for (i = 0; i < v; i++) cell(0, T + (v - 1 - i) * sideH, T, sideH);
    box.setAttribute('data-layout', h + 'x' + v);
}

function renderPreview(c, live) {
    var box = document.getElementById('preview');
    box.className = live ? '' : 'stale';
    if (!c || !c.rgb) return;
    if (box.getAttribute('data-layout') !== c.h + 'x' + c.v) buildPreview(box, c.h, c.v);
    var cells = box.childNodes;
    for (var i = 0; i < cells.length; i++) cells[i].style.backgroundColor = '#' + c.rgb.substr(i * 6, 6);
}

function setFocus(index) {
    if (index < 0) index = 0;
    if (index >= buttons.length) index = buttons.length - 1;
    focusIndex = index;
    for (var i = 0; i < buttons.length; i++) {
        buttons[i].classList.remove('focused');
    }
    buttons[focusIndex].classList.add('focused');
    buttons[focusIndex].focus();
}

function launchService() {
    log('Launching service...');
    try {
        var appControl = new tizen.ApplicationControl(
            'http://tizen.org/appcontrol/operation/default',
            null, null, null,
            [new tizen.ApplicationControlData('command', ['start'])]
        );
        tizen.application.launchAppControl(
            appControl,
            SERVICE_APP_ID,
            function() {
                log('Service launched');
                pollStatus();
            },
            function(err) {
                log('Launch failed: ' + err.message);
            }
        );
    } catch (e) {
        log('Exception: ' + e.message);
    }
}

function stopService() {
    log('Stopping service...');
    try {
        var appControl = new tizen.ApplicationControl(
            'http://tizen.org/appcontrol/operation/default',
            null, null, null,
            [new tizen.ApplicationControlData('command', ['stop'])]
        );
        tizen.application.launchAppControl(
            appControl,
            SERVICE_APP_ID,
            function() {
                log('Stop command sent');
            },
            function(err) {
                log('Stop failed: ' + err.message);
            }
        );
    } catch (e) {
        log('Stop exception: ' + e.message);
    }
}

document.addEventListener('keydown', function(e) {
    switch (e.keyCode) {
        case 37: // LEFT
            setFocus(focusIndex - 1);
            break;
        case 39: // RIGHT
            setFocus(focusIndex + 1);
            break;
        case 13: // OK / Enter
            if (focusIndex === 0) launchService();
            if (focusIndex === 1) stopService();
            if (focusIndex === 2) toggleDebug();
            break;
        case 10009: // RETURN / BACK
            tizen.application.getCurrentApplication().exit();
            break;
    }
});

document.addEventListener('DOMContentLoaded', function() {
    buttons = [
        document.getElementById('btnStart'),
        document.getElementById('btnStop'),
        document.getElementById('btnDebug')
    ];
    setFocus(0);

    try {
        if (localStorage.getItem('debugEnabled') === '1') {
            setDebug(true);
        }
    } catch (e) {}

    var version = '';
    try { version = tizen.application.getCurrentApplication().appInfo.version; } catch (e) {}
    if (version) document.getElementById('version').textContent = 'v' + version;
    log('UI application started' + (version ? ' (v' + version + ')' : ''));
    initStatus();
});
