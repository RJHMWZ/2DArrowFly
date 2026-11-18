// Included by Playworks through unity.scripts.externalJSLibraries.
// Resume suspended browser audio on the actual gesture, before Unity's next frame.
// SDK/host volume and mute controls are deliberately left to Playworks.
(function () {
    if (window.__hueAudioUnlockInstalled) return;
    window.__hueAudioUnlockInstalled = true;
    var contexts = [];
    var activated = false;

    function resume(context) {
        if (context.state !== "suspended" || document.hidden) return;
        try {
            var result = context.resume();
            if (result && result.catch) result.catch(function () {});
        } catch (ignored) {}
    }

    function remember(context) {
        if (contexts.indexOf(context) < 0) contexts.push(context);
    }

    function observe(Type) {
        if (!Type || !Type.prototype) return;
        ["createGain", "createBufferSource", "decodeAudioData"].forEach(function (name) {
            var original = Type.prototype[name];
            if (!original || original.__hueObserved) return;
            function tracked() {
                remember(this);
                return original.apply(this, arguments);
            }
            tracked.__hueObserved = true;
            Type.prototype[name] = tracked;
        });
    }

    observe(window.AudioContext);
    observe(window.webkitAudioContext);
    function onGesture(event) {
        if (!event.isTrusted) return;
        activated = true;
        contexts.forEach(resume);
    }
    ["pointerdown", "touchstart", "mousedown", "keydown"].forEach(function (name) {
        // Listen at window: the playable's mouse/touch adapter can stop the
        // original event before it reaches document or the game canvas.
        window.addEventListener(name, onGesture, { capture: true, passive: true });
    });
    document.addEventListener("visibilitychange", function () {
        if (activated && !document.hidden) contexts.forEach(resume);
    });
})();
