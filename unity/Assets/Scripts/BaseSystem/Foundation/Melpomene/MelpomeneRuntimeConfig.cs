using UnityEngine;

namespace Foundation.Melpomene
{
    /// <summary>
    /// ランタイム版 Melpomene 設定。
    /// ビルド後のゲーム内バグ報告で使う GitHub 接続情報を保持する。
    /// Editor 専用の <c>Melpomene.MelpomeneConfig</c> とは別物で、こちらは Resources から実行時にロードできる。
    /// NOTE: アクセストークンをビルドへ同梱するのはプレイテスト用途限定。配布ビルドへは含めないこと。
    /// </summary>
    [CreateAssetMenu(fileName = "MelpomeneRuntimeConfig", menuName = "Melpomene/Runtime Config")]
    public class MelpomeneRuntimeConfig : ScriptableObject
    {
        /// <summary>Resources 配下のロードパス（拡張子なし）。</summary>
        public const string ResourcesPath = "MelpomeneRuntimeConfig";

        /// <summary>Issue 本文に埋め込むバージョン表記。</summary>
        public const string Version = "1.0.0-runtime";

        [Header("GitHub Repository")]
        [Tooltip("リポジトリオーナー（組織名またはユーザー名）")]
        public string repositoryOwner = "";

        [Tooltip("リポジトリ名")]
        public string repositoryName = "";

        [Header("GitHub Authentication")]
        [Tooltip("Issues 書き込み権限を持つ Personal Access Token。プレイテストビルドのみ同梱する。")]
        public string accessToken = "";

        [Header("Default Settings")]
        [Tooltip("作成する Issue に必ず付与するラベル")]
        public string[] defaultLabels = new[] { "melpomene", "in-game-report" };

        [Header("Capture")]
        [Tooltip("報告本文に画面解像度などの環境情報を含めるか")]
        public bool captureScreenInfo = true;

        [Header("Availability")]
        [Tooltip("リリースビルドでも報告 UI を有効にするか。false の場合は開発ビルド(Development Build)とエディタでのみ有効。")]
        public bool enableInReleaseBuild = false;

        [Tooltip("報告 UI を開閉するキー（Input System / Keyboard.current 経由）。None で無効。")]
        public UnityEngine.InputSystem.Key toggleKey = UnityEngine.InputSystem.Key.F1;

        /// <summary>GitHub API のリポジトリ基底 URL。</summary>
        public string ApiBaseUrl => $"https://api.github.com/repos/{repositoryOwner}/{repositoryName}";

        /// <summary>Issue 作成に必要な設定が揃っているか。</summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(repositoryOwner) &&
            !string.IsNullOrEmpty(repositoryName) &&
            !string.IsNullOrEmpty(accessToken);

        static MelpomeneRuntimeConfig _cached;

        /// <summary>
        /// Resources から設定をロードする（キャッシュあり）。存在しなければ null を返す。
        /// </summary>
        public static MelpomeneRuntimeConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MelpomeneRuntimeConfig>(ResourcesPath);
            if (_cached == null)
            {
                Debug.LogWarning($"[Melpomene] Runtime config not found at Resources/{ResourcesPath}. In-game reporting disabled.");
            }
            return _cached;
        }
    }
}
