const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('Assets/HueRelay/Plugins/WebGL/HueSafeArea.jslib', 'utf8');
function read(tt) {
    const context = { tt, LibraryManager: { library: {} }, mergeInto: Object.assign };
    vm.runInNewContext(source, context);
    return context.LibraryManager.library.HueTT_GetSafeTopRatio();
}
assert.equal(read(undefined), 0);
assert.equal(read({getSystemInfoSync: () => ({screenHeight: 0})}), 0);
for (const pixelRatio of [1, 2, 3, 4]) {
    const tt = {
        getSystemInfoSync: () => ({screenHeight: 900, pixelRatio, statusBarHeight: 24, safeArea: {top: 30}}),
        getMenuButtonLayout: () => ({top: 32, bottom: 64})
    };
    assert.equal(read(tt), 64 / 900);
    assert.equal(read({...tt, getMenuButtonLayout: undefined}), 74 / 900);
    assert.equal(read({...tt, getMenuButtonLayout: () => {throw new Error('unsupported');}}), 74 / 900);
    assert.equal(read({...tt, getMenuButtonLayout: () => ({bottom: 20})}), 30 / 900);
}
console.log('PASS: TT capsule bounds, density-independent conversion, unavailable API and safe-area fallback.');
