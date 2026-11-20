module.exports = {
	originalWasmMd5: "12590f08f1ae89dcfd98c8772fd0f166",
    wasmTableSize: 213193,
    wasmSplitVersion: 1795160990809,
    subJsUrl: "",
    usingWasmH5: true, 
    enableWasmCollect: true,
    enableWasmSplit: true,
    globalVarList: [{"name":"global0","type":"i32","mutable":true}],
    wasmCollectAPIVer: "v4",

    ANDROID_CODE_FILE_MD5: "96054c39daeeacab22eb6641e0058f75",
    ANDROID_SUB_CODE_FILE_MD5: "7738845a003821c167c15c166c61a9e5", 
    ARCHIVE_CODE_FILE_MD5: "d63f55f8885c06d8fdeb6ec64ea932bc", 
    IOS_CODE_FILE_MD5: "96054c39daeeacab22eb6641e0058f75",
    enableArchiveMode: true,
    // Compatibility default. The local split CLI rewrites this literal to
    // true only after a successful archive split with a non-empty boot closure;
    // it permits an earlier parallel fetch, while main still waits for archive.
    archiveBootClosureReady: false
}
