mergeInto(LibraryManager.library, {
    HueTT_GetSafeTopRatio: function () {
        try {
            if (typeof tt === "undefined" || !tt.getSystemInfoSync) return 0;
            var info = tt.getSystemInfoSync();
            if (!(info.screenHeight > 0)) return 0;
            var safeTop = Math.max(0, info.statusBarHeight || 0, info.safeArea ? info.safeArea.top || 0 : 0);
            // Older hosts can omit the capsule API; reserve a toolbar band then.
            var bottom = safeTop + 44;
            if (typeof tt.getMenuButtonLayout === "function") {
                try {
                    var menu = tt.getMenuButtonLayout();
                    if (menu && menu.bottom > 0) bottom = Math.max(safeTop, menu.bottom);
                } catch (error) { /* Retain the safe-area fallback on older hosts. */ }
            }
            return Math.max(0, Math.min(1, bottom / info.screenHeight));
        } catch (error) {
            return 0;
        }
    }
});
