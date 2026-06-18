using UnityEngine;

namespace Foundation.Melpomene
{
    /// <summary>
    /// ゲーム起動時にバグ報告レポーターを常駐させるブートストラップ。
    /// シーンへの手動配置不要で、ビルド後のプレイヤーでも自動的に有効になる。
    /// 可用性は設定とビルド種別でゲートする（既定は開発ビルド/エディタのみ）。
    /// </summary>
    public static class MelpomeneRuntimeBootstrap
    {
        static GameObject _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;

            var config = MelpomeneRuntimeConfig.Load();
            if (config == null) return;

            // リリースビルドでは既定で無効。開発ビルド・エディタ・明示フラグ時のみ有効化。
            bool enabled = Application.isEditor || Debug.isDebugBuild || config.enableInReleaseBuild;
            if (!enabled) return;

            _instance = new GameObject("[MelpomeneReporter]");
            Object.DontDestroyOnLoad(_instance);
            var reporter = _instance.AddComponent<MelpomeneReporter>();
            reporter.Initialize(config);

            Debug.Log("[Melpomene] Runtime reporter started.");
        }
    }
}
