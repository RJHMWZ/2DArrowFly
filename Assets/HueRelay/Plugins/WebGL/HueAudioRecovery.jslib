mergeInto(LibraryManager.library, {
    $HueAudioRecovery__deps: ['$WEBAudio'],
    $HueAudioRecovery: {
        allowed: false,
        waiting: false,
        resuming: false,
        installed: false,
        resume: function (fromGesture) {
            if (!HueAudioRecovery.allowed) return;
            var context = WEBAudio.audioContext;
            if (!context || context.state === 'closed' || context.state === 'running') {
                HueAudioRecovery.waiting = false;
                return;
            }
            HueAudioRecovery.waiting = true;
            // Autoplay restrictions may leave a resume promise pending indefinitely.
            // A real user gesture must be able to retry that request.
            if ((HueAudioRecovery.resuming && !fromGesture) || typeof context.resume !== 'function') return;
            try {
                var result = context.resume();
                if (result && typeof result.then === 'function') {
                    HueAudioRecovery.resuming = true;
                    result.then(function () {
                        HueAudioRecovery.resuming = false;
                        HueAudioRecovery.waiting = context.state !== 'running';
                    }, function () {
                        // Autoplay denial/interruption: retry on the next real touch.
                        HueAudioRecovery.resuming = false;
                    });
                }
            } catch (e) { /* Host still owns the audio session; a later touch retries. */ }
        },
        install: function () {
            if (HueAudioRecovery.installed || typeof window === 'undefined'
                || typeof window.addEventListener !== 'function') return;
            HueAudioRecovery.installed = true;
            var onGesture = function () {
                if (HueAudioRecovery.waiting) HueAudioRecovery.resume(true);
            };
            window.addEventListener('touchstart', onGesture);
            window.addEventListener('mousedown', onGesture);
            if (Module.deinitializers) Module.deinitializers.push(function () {
                window.removeEventListener('touchstart', onGesture);
                window.removeEventListener('mousedown', onGesture);
                HueAudioRecovery.installed = false;
            });
        }
    },
    HueAudio_SetPlaybackAllowed__deps: ['$HueAudioRecovery'],
    HueAudio_SetPlaybackAllowed: function (allowed) {
        HueAudioRecovery.allowed = !!allowed;
        HueAudioRecovery.install();
        if (allowed) HueAudioRecovery.resume();
    },
    HueAudio_Resume__deps: ['$HueAudioRecovery'],
    HueAudio_Resume: function () { HueAudioRecovery.resume(); }
});
