using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Clio
{
    /// <summary>
    /// Clio 設定
    /// NOTE: 2 つの JSON ファイルから読み込む (Melpomene と同じ分離方針)
    ///   - musa/clio/clio_config.json  (プロジェクト共通設定、Git 管理)
    ///   - musa/clio/clio_local.json   (認証情報など個人設定、gitignore 対象)
    /// </summary>
    public static class ClioConfig
    {
        #region File Paths

        // NOTE: Application.dataPath = "unity/Assets" → "../.." でリポジトリルート
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string ConfigDir => Path.Combine(RepoRoot, "musa", "clio");
        private static string ConfigPath => Path.Combine(ConfigDir, "clio_config.json");
        private static string LocalPath => Path.Combine(ConfigDir, "clio_local.json");

        #endregion

        #region Data Classes

        [Serializable]
        public class TagMapping
        {
            public string unityLabel = "";
            public string curareTag = "";
        }

        [Serializable]
        private class ProjectConfig
        {
            public string curareBaseUrl = "http://localhost:8090";
            public string curareProjectId = "";
            public int searchLimit = 20;
            public bool autoSelectEnabled = true;
            public bool autoReplaceEnabled = false;
            public List<TagMapping> tagMappings = new List<TagMapping>();
        }

        [Serializable]
        private class LocalConfig
        {
            public string authToken = "";
            public string devUserId = "";
        }

        #endregion

        #region Fields

        private static ProjectConfig _project;
        private static LocalConfig _local;

        #endregion

        #region Properties

        public static string CurareBaseUrl
        {
            get { EnsureLoaded(); return _project.curareBaseUrl; }
            set { EnsureLoaded(); _project.curareBaseUrl = value; }
        }

        public static string CurareProjectId
        {
            get { EnsureLoaded(); return _project.curareProjectId; }
            set { EnsureLoaded(); _project.curareProjectId = value; }
        }

        public static int SearchLimit
        {
            get { EnsureLoaded(); return Mathf.Clamp(_project.searchLimit, 1, 100); }
            set { EnsureLoaded(); _project.searchLimit = Mathf.Clamp(value, 1, 100); }
        }

        /// <summary>新規キューブの自動選択 (ClioCubeWatcher) を有効にするか</summary>
        public static bool AutoSelectEnabled
        {
            get { EnsureLoaded(); return _project.autoSelectEnabled; }
            set { EnsureLoaded(); _project.autoSelectEnabled = value; }
        }

        /// <summary>解決後に確認なしで置換まで行うか</summary>
        public static bool AutoReplaceEnabled
        {
            get { EnsureLoaded(); return _project.autoReplaceEnabled; }
            set { EnsureLoaded(); _project.autoReplaceEnabled = value; }
        }

        public static List<TagMapping> TagMappings
        {
            get { EnsureLoaded(); return _project.tagMappings; }
        }

        /// <summary>Cernere JWT (空なら dev ヘッダ認証にフォールバック)</summary>
        public static string AuthToken
        {
            get { EnsureLoaded(); return _local.authToken; }
            set { EnsureLoaded(); _local.authToken = value; }
        }

        /// <summary>dev モードの X-User-Id 値</summary>
        public static string DevUserId
        {
            get { EnsureLoaded(); return _local.devUserId; }
            set { EnsureLoaded(); _local.devUserId = value; }
        }

        #endregion

        #region Public Methods

        public static void Load()
        {
            _project = LoadFile<ProjectConfig>(ConfigPath) ?? new ProjectConfig();
            _local = LoadFile<LocalConfig>(LocalPath) ?? new LocalConfig();
        }

        public static void Save()
        {
            EnsureLoaded();
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(_project, true));
                File.WriteAllText(LocalPath, JsonUtility.ToJson(_local, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Clio] 設定の保存に失敗: {ex.Message}");
            }
        }

        public static void Reload()
        {
            _project = null;
            _local = null;
            Load();
        }

        #endregion

        #region Private Methods

        private static void EnsureLoaded()
        {
            if (_project == null || _local == null)
            {
                Load();
            }
        }

        private static T LoadFile<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Clio] 設定ファイルの読込に失敗 ({path}): {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
