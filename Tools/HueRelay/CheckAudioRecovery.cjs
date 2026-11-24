// Run from the project root: node Tools/HueRelay/CheckAudioRecovery.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

async function run() {
    const library = {}, events = {}, module = { deinitializers: [] };
    const sandbox = {
        LibraryManager: { library }, mergeInto: Object.assign,
        Module: module, WEBAudio: { audioContext: null },
        window: {
            addEventListener: (name, callback) => { assert(!events[name], 'duplicate input hook'); events[name] = callback; },
            removeEventListener: (name, callback) => { assert.equal(events[name], callback); delete events[name]; }
        }
    };
    vm.createContext(sandbox);
    vm.runInContext(fs.readFileSync('Assets/HueRelay/Plugins/WebGL/HueAudioRecovery.jslib', 'utf8'), sandbox);
    sandbox.HueAudioRecovery = library.$HueAudioRecovery;
    const allow = library.HueAudio_SetPlaybackAllowed, resume = library.HueAudio_Resume;
    const flush = () => new Promise(resolve => setImmediate(resolve));
    let calls = 0;
    const context = { state: 'suspended', resume() { calls++; this.state = 'running'; return Promise.resolve(); } };
    sandbox.WEBAudio.audioContext = context;
    allow(1); await flush();
    assert.equal(context.state, 'running'); assert.equal(calls, 1);
    for (let i = 0; i < 100; i++) resume();
    assert.equal(calls, 1, 'running audio must not restart');
    for (const state of ['suspended', 'interrupted']) {
        context.state = state; resume(); await flush(); assert.equal(context.state, 'running');
    }
    allow(0); context.state = 'suspended';
    const before = calls;
    resume(); events.touchstart(); events.mousedown();
    assert.equal(calls, before, 'video/background must not recover game audio');
    allow(1); await flush(); assert.equal(context.state, 'running');
    context.state = 'suspended';
    context.resume = () => { calls++; return Promise.reject(new Error('audio session occupied')); };
    resume(); await flush();
    assert.equal(sandbox.HueAudioRecovery.waiting, true);
    context.resume = function () { calls++; this.state = 'running'; return Promise.resolve(); };
    events.touchstart(); await flush(); assert.equal(context.state, 'running');
    // Some browsers leave a non-gesture resume pending rather than rejecting it.
    context.state = 'suspended'; context.resume = () => new Promise(() => {});
    resume(); assert.equal(sandbox.HueAudioRecovery.resuming, true);
    context.resume = function () { calls++; this.state = 'running'; return Promise.resolve(); };
    events.touchstart(); await flush(); assert.equal(context.state, 'running');
    context.state = 'interrupted'; context.resume = () => { throw new Error('host still interrupted'); };
    assert.doesNotThrow(resume);
    context.state = 'closed'; const closedCalls = calls;
    context.resume = () => { calls++; throw new Error('must not resume closed context'); };
    resume(); assert.equal(calls, closedCalls);
    sandbox.WEBAudio.audioContext = null; assert.doesNotThrow(resume);
    // Repeated ad exits must reuse hooks, and Unity shutdown must remove them.
    allow(1); allow(0); allow(1); assert.equal(module.deinitializers.length, 1);
    module.deinitializers.forEach(cleanup => cleanup()); assert.equal(Object.keys(events).length, 0);
    console.log('PASS: suspended/interrupted recovery; active-video/background suppression; gesture retry after rejection/pending promise; closed/missing contexts; bounded hooks and shutdown cleanup.');
}
run().catch(error => { console.error(error); process.exitCode = 1; });
