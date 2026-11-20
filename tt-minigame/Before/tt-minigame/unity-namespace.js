const unityNamespace = {
      unityVersion: '2022.3.62f2', 
      assetBundleFSEnabled: true,
      assetBundleBufferCapacity: 128,
      assetBundleBufferTTL: 5,
      optimizeWebGLMemoryInBackground: true,
      regressionAutomationEnabled: false,
      bootConfig: 'player-connection-ip=10.80.30.25',
	  UnityModule: null,
}
GameGlobal.unityNamespace = GameGlobal.unityNamespace || unityNamespace;
module.exports = unityNamespace;
