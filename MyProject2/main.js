var SERVICE_APP_ID = 'org.tizen.example.Service';
var buttons = [];
var focusIndex = 0;

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
            break;
        case 10009: // RETURN / BACK
            tizen.application.getCurrentApplication().exit();
            break;
    }
});

document.addEventListener('DOMContentLoaded', function() {
    buttons = [
        document.getElementById('btnStart'),
        document.getElementById('btnStop')
    ];
    setFocus(0);
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
