var SERVICE_APP_ID = 'org.tizen.example.Service';
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

function setStatus(text, isRunning) {
    var el = document.getElementById('statusText');
    el.textContent = text;
    el.className = isRunning ? 'running' : '';
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
                log('Service launched successfully');
                setStatus('Running', true);
            },
            function(err) {
                log('Launch failed: ' + err.message);
                setStatus('Error', false);
            }
        );
    } catch (e) {
        log('Exception: ' + e.message);
        setStatus('Error', false);
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
                log('Stop command sent to service');
                setStatus('Not running', false);
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

    // Restore debug state from storage
    try {
        if (localStorage.getItem('debugEnabled') === '1') {
            setDebug(true);
        }
    } catch (e) {}

    log('UI application started');

    try {
        tizen.application.getAppsContext(function(contexts) {
            for (var i = 0; i < contexts.length; i++) {
                if (contexts[i].appId === SERVICE_APP_ID) {
                    setStatus('Running', true);
                    log('Service is already running');
                    return;
                }
            }
            log('Service is not running');
        });
    } catch (e) {
        log('Could not check service status');
    }
});
