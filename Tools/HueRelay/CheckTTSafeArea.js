const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('Assets/HueRelay/Plugins/WebGL/HueSafeArea.jslib', 'utf8');
function read(hosts = {}) {
    const context = { ...hosts, LibraryManager: { library: {} }, mergeInto: Object.assign };
    vm.runInNewContext(source, context);
    return context.LibraryManager.library.HueTT_SafeAreaTopRatio();
}
function api(top, extra = {}) {
    return { getSystemInfoSync: () => ({screenHeight: 900, safeArea: {top}, ...extra}) };
}
assert.equal(read(), -1);
for (const bad of [null, {}, {screenHeight: 0}, {screenHeight: Infinity, safeArea: {top: 30}},
    {screenHeight: 900, safeArea: {top: -1}}, {screenHeight: 900, safeArea: {top: 181}},
    {screenHeight: 900, safeArea: {top: '30'}}, {screenHeight: 900, safeArea: {top: NaN}}]) {
    assert.equal(read({tt: {getSystemInfoSync: () => bad}}), -1);
}
for (const pixelRatio of [1, 2, 3, 4]) {
    const host = api(30, {pixelRatio});
    host.getMenuButtonLayout = () => {throw new Error('Capsule must not be used as the notch inset');};
    assert.equal(read({tt: host}), 30 / 900);
    assert.equal(read({jssdk: host}), 30 / 900);
    assert.equal(read({TTMinis: {game: host}}), 30 / 900);
}
assert.equal(read({tt: api(0, {statusBarHeight: 24})}), 0);
assert.equal(read({tt: api(180)}), .2);
assert.equal(read({jssdk: {getSystemInfoSync: () => {throw new Error('unsupported');}}, tt: api(44)}), 44 / 900);
assert.equal(read({jssdk: api(-1), tt: api(30)}), 30 / 900);
assert.equal(read({jssdk: {}, tt: null, TTMinis: {game: api(30)}}), 30 / 900);
assert.equal(read({jssdk: api(44), tt: api(30)}), 44 / 900);
assert.equal(read({TTMinis: null}), -1);
console.log('PASS: TT notch inset; jssdk/tt/TTMinis host fallback; invalid data; zero-inset devices; density-independent ratios; no capsule padding.');
