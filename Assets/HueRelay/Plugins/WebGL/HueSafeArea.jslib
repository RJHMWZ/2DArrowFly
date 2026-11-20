mergeInto(LibraryManager.library, {
    // Return the notch/status-bar inset as a fraction of logical screen height.
    // Capsule avoidance belongs to the HUD layout, not this inset.
    HueTT_SafeAreaTopRatio: function () {
        try {
            var choices = [typeof jssdk !== "undefined" ? jssdk : null,
                typeof tt !== "undefined" ? tt : null,
                typeof TTMinis !== "undefined" && TTMinis ? TTMinis.game : null];
            for (var c = 0; c < choices.length; c++) {
                var api = choices[c];
                if (!api || typeof api.getSystemInfoSync !== "function") continue;
                try {
                    var info = api.getSystemInfoSync();
                    var area = info && info.safeArea;
                    if (!area || typeof info.screenHeight !== "number" || !isFinite(info.screenHeight)
                        || !(info.screenHeight > 0) || typeof area.top !== "number" || !isFinite(area.top)
                        || area.top < 0 || area.top > info.screenHeight * .2) continue;
                    return area.top / info.screenHeight;
                } catch (error) { /* Try the next available host namespace. */ }
            }
            return -1;
        } catch (error) {
            return -1;
        }
    }
});
