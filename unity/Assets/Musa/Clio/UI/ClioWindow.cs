using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Clio
{
    /// <summary>
    /// Clio EditorWindow — アセット検索/タグ配置 UI
    /// NOTE: 単独ウィンドウとしても、MusaWindow への埋め込み (DrawContent 委譲) でも動く
    /// </summary>
    public class ClioWindow : EditorWindow
    {
        public static readonly string[] SubTabNames = { "検索/配置", "プレースホルダ", "設定" };

        /// <summary>表示中サブタブ (MusaWindow のサイドバーからも切替される)</summary>
        public int subTab;

        private const int PaletteColumns = 4;

        // 検索/配置タブ
        private string searchQuery = "";
        private List<string> vocabulary = new List<string>();
        private List<ClioCandidate> localResults = new List<ClioCandidate>();
        private List<ClioAsset> curareResults;
        private readonly Dictionary<string, ClioCandidate> curareLocalMatches =
            new Dictionary<string, ClioCandidate>();
        private bool isSearching;
        private string statusMessage = "";
        private Vector2 searchScroll;

        // プレースホルダタブ
        private Vector2 placeholderScroll;

        // 設定タブ
        private string connectionStatus = "";

        [MenuItem("Musa/Clio アセット配置")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClioWindow>("Clio");
            window.minSize = new Vector2(480, 360);
        }

        /// <summary>MusaWindow 埋め込み用の初期化</summary>
        public void InitializeForMusa()
        {
            RefreshVocabulary();
        }

        private void OnEnable()
        {
            RefreshVocabulary();
        }

        private void OnGUI()
        {
            subTab = GUILayout.Toolbar(subTab, SubTabNames, GUILayout.Height(26));
            EditorGUILayout.Space(4);
            DrawContent();
        }

        /// <summary>
        /// 現在のサブタブの内容を描画する (MusaWindow から委譲される)
        /// </summary>
        public void DrawContent()
        {
            switch (subTab)
            {
                case 0: DrawSearchTab(); break;
                case 1: DrawPlaceholderTab(); break;
                case 2: DrawSettingsTab(); break;
            }
        }

        // =====================================================================
        // 検索/配置タブ
        // =====================================================================
        #region Search Tab

        private void DrawSearchTab()
        {
            DrawTagPalette();
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            searchQuery = EditorGUILayout.TextField("キーワード", searchQuery);
            if (GUILayout.Button("ローカル検索", GUILayout.Width(90)))
            {
                RunLocalSearch();
            }
            EditorGUI.BeginDisabledGroup(isSearching);
            if (GUILayout.Button(isSearching ? "検索中..." : "Curare 検索", GUILayout.Width(90)))
            {
                RunCurareSearchAsync().Forget();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(ClioPaletteState.SelectedTags.Count == 0);
            if (GUILayout.Button("選択タグでキューブ配置", GUILayout.Height(26)))
            {
                PlacePlaceholderCube();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }

            searchScroll = EditorGUILayout.BeginScrollView(searchScroll);
            DrawLocalResults();
            DrawCurareResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTagPalette()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("タグパレット", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("語彙更新", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                RefreshVocabulary();
            }
            if (GUILayout.Button("選択解除", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                ClioPaletteState.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (vocabulary.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "タグ語彙がありません。prefab に AssetLabel を付けるか、Curare 検索で取得してください。",
                    MessageType.Info);
            }
            else
            {
                for (var i = 0; i < vocabulary.Count; i += PaletteColumns)
                {
                    EditorGUILayout.BeginHorizontal();
                    foreach (var tag in vocabulary.Skip(i).Take(PaletteColumns))
                    {
                        var selected = ClioPaletteState.IsSelected(tag);
                        var next = GUILayout.Toggle(selected, tag, EditorStyles.miniButton);
                        if (next != selected)
                        {
                            ClioPaletteState.Toggle(tag);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            var selectedTags = ClioPaletteState.SelectedTags;
            if (selectedTags.Count > 0)
            {
                EditorGUILayout.LabelField($"選択中: {string.Join(", ", selectedTags)}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLocalResults()
        {
            if (localResults.Count == 0) return;

            EditorGUILayout.LabelField($"ローカル候補 ({localResults.Count})", EditorStyles.boldLabel);
            foreach (var candidate in localResults)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{candidate.assetName}  (score {candidate.score})");
                EditorGUILayout.LabelField(
                    $"tags: {string.Join(", ", candidate.curareTags)}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(candidate.assetPath));
                }
                if (GUILayout.Button("配置", GUILayout.Width(50)))
                {
                    PlacePrefab(candidate.assetPath);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCurareResults()
        {
            if (curareResults == null) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Curare 検索結果 ({curareResults.Count})", EditorStyles.boldLabel);
            foreach (var asset in curareResults)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(asset.name);
                EditorGUILayout.LabelField($"tags: {string.Join(", ", asset.tags)}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // NOTE: ローカル有無は検索完了時のキャッシュを参照 (OnGUI 毎の解決を避ける)
                curareLocalMatches.TryGetValue(asset.id, out var local);
                if (local != null)
                {
                    if (GUILayout.Button("配置", GUILayout.Width(50)))
                    {
                        PlacePrefab(local.assetPath);
                    }
                }
                else
                {
                    GUILayout.Label("ローカル無し", EditorStyles.miniLabel, GUILayout.Width(70));
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RunLocalSearch()
        {
            localResults = ClioResourceResolver.ResolveCandidates(
                ClioPaletteState.SelectedTags, searchQuery, ClioConfig.SearchLimit);
            statusMessage = localResults.Count == 0 ? "ローカル候補が見つかりません" : "";
        }

        private async UniTaskVoid RunCurareSearchAsync()
        {
            isSearching = true;
            statusMessage = "Curare を検索中...";
            try
            {
                var results = await ClioClient.FromConfig().SearchAssetsAsync(
                    searchQuery, ClioPaletteState.SelectedTags, 1, ClioConfig.SearchLimit);
                curareResults = results;
                statusMessage = results == null
                    ? "Curare 検索に失敗しました (Console 参照)"
                    : $"Curare: {results.Count} 件";

                if (results != null)
                {
                    // NOTE: 検索結果のタグを語彙へ取り込む
                    var merged = new HashSet<string>(vocabulary);
                    foreach (var tag in results.SelectMany(r => r.tags))
                    {
                        merged.Add(tag);
                    }
                    vocabulary = merged.OrderBy(t => t).ToList();

                    // NOTE: ローカル有無の突き合わせは検索完了時に 1 回だけ行う
                    curareLocalMatches.Clear();
                    foreach (var asset in results)
                    {
                        var local = ClioResourceResolver.ResolveBest(asset.tags, asset.name);
                        if (local != null)
                        {
                            curareLocalMatches[asset.id] = local;
                        }
                    }
                }
            }
            finally
            {
                isSearching = false;
                Repaint();
            }
        }

        private void RefreshVocabulary()
        {
            var merged = new HashSet<string>(ClioTagMapper.CollectLocalVocabulary());
            foreach (var mapping in ClioConfig.TagMappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.curareTag))
                {
                    merged.Add(mapping.curareTag);
                }
            }
            vocabulary = merged.OrderBy(t => t).ToList();
        }

        private void PlacePlaceholderCube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Clio Place Cube");
            cube.transform.position = GetSceneViewPivot();

            var placeholder = Undo.AddComponent<ClioPlaceholder>(cube);
            placeholder.tags = new List<string>(ClioPaletteState.SelectedTags);

            var best = ClioResourceResolver.ResolveInto(placeholder);
            if (best != null && ClioConfig.AutoReplaceEnabled)
            {
                ClioPlaceholderReplacer.Replace(placeholder);
            }
            else
            {
                Selection.activeGameObject = cube;
            }
        }

        private void PlacePrefab(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[Clio] prefab が見つかりません: {assetPath}");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Clio Place Prefab");
            instance.transform.position = GetSceneViewPivot();
            Selection.activeGameObject = instance;
            Debug.Log($"[Clio] 配置: {assetPath}");
        }

        private static Vector3 GetSceneViewPivot()
        {
            var sceneView = SceneView.lastActiveSceneView;
            return sceneView != null ? sceneView.pivot : Vector3.zero;
        }

        #endregion

        // =====================================================================
        // プレースホルダタブ
        // =====================================================================
        #region Placeholder Tab

        private void DrawPlaceholderTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            var autoSelect = EditorGUILayout.ToggleLeft(
                "自動選択 (新規キューブを検出してリソースを解決)", ClioConfig.AutoSelectEnabled);
            var autoReplace = EditorGUILayout.ToggleLeft(
                "自動置換 (解決できたら確認なしで置き換える)", ClioConfig.AutoReplaceEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                ClioConfig.AutoSelectEnabled = autoSelect;
                ClioConfig.AutoReplaceEnabled = autoReplace;
                ClioConfig.Save();
            }
            EditorGUILayout.EndVertical();

            var placeholders = FindObjectsByType<ClioPlaceholder>(FindObjectsSortMode.None);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"プレースホルダ ({placeholders.Length})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("シーン再走査", GUILayout.Width(100)))
            {
                var count = ClioCubeWatcher.RescanNow();
                Debug.Log($"[Clio] 再走査: {count} 件処理");
            }
            EditorGUI.BeginDisabledGroup(placeholders.All(p => string.IsNullOrEmpty(p.resolvedAssetPath)));
            if (GUILayout.Button("解決済みを全て置換", GUILayout.Width(130)))
            {
                foreach (var p in placeholders.Where(p => !string.IsNullOrEmpty(p.resolvedAssetPath)))
                {
                    ClioPlaceholderReplacer.Replace(p);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            placeholderScroll = EditorGUILayout.BeginScrollView(placeholderScroll);
            foreach (var placeholder in placeholders)
            {
                DrawPlaceholderRow(placeholder);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPlaceholderRow(ClioPlaceholder placeholder)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(placeholder.gameObject.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"tags: {string.Join(", ", placeholder.tags)}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(placeholder.resolvedAssetPath)
                    ? "候補: 未解決"
                    : $"候補: {placeholder.resolvedAssetPath} (score {placeholder.resolvedScore})",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("選択", GUILayout.Width(50)))
            {
                Selection.activeGameObject = placeholder.gameObject;
                EditorGUIUtility.PingObject(placeholder.gameObject);
            }
            if (GUILayout.Button("再解決", GUILayout.Width(60)))
            {
                ClioResourceResolver.ResolveInto(placeholder);
            }
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(placeholder.resolvedAssetPath));
            if (GUILayout.Button("置換", GUILayout.Width(50)))
            {
                ClioPlaceholderReplacer.Replace(placeholder);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        // =====================================================================
        // 設定タブ
        // =====================================================================
        #region Settings Tab

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Curare 接続", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var baseUrl = EditorGUILayout.TextField("Base URL", ClioConfig.CurareBaseUrl);
            var projectId = EditorGUILayout.TextField("Project ID", ClioConfig.CurareProjectId);
            var searchLimit = EditorGUILayout.IntSlider("検索件数", ClioConfig.SearchLimit, 1, 100);
            var devUserId = EditorGUILayout.TextField("Dev User ID", ClioConfig.DevUserId);
            var authToken = EditorGUILayout.PasswordField("Auth Token (JWT)", ClioConfig.AuthToken);
            if (EditorGUI.EndChangeCheck())
            {
                ClioConfig.CurareBaseUrl = baseUrl;
                ClioConfig.CurareProjectId = projectId;
                ClioConfig.SearchLimit = searchLimit;
                ClioConfig.DevUserId = devUserId;
                ClioConfig.AuthToken = authToken;
                ClioConfig.Save();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("接続テスト", GUILayout.Width(90)))
            {
                TestConnectionAsync().Forget();
            }
            if (!string.IsNullOrEmpty(connectionStatus))
            {
                EditorGUILayout.LabelField(connectionStatus);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            DrawTagMappings();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("設定を再読込", GUILayout.Width(120)))
            {
                ClioConfig.Reload();
                RefreshVocabulary();
            }
        }

        private void DrawTagMappings()
        {
            EditorGUILayout.LabelField("タグマッピング (AssetLabel ↔ Curare タグ)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "表に無い語は同名で相互変換されます (identity)", EditorStyles.miniLabel);

            var mappings = ClioConfig.TagMappings;
            var removeIndex = -1;
            for (var i = 0; i < mappings.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var label = EditorGUILayout.TextField(mappings[i].unityLabel);
                EditorGUILayout.LabelField("↔", GUILayout.Width(20));
                var tag = EditorGUILayout.TextField(mappings[i].curareTag);
                if (EditorGUI.EndChangeCheck())
                {
                    mappings[i].unityLabel = label;
                    mappings[i].curareTag = tag;
                    ClioConfig.Save();
                }
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
            {
                mappings.RemoveAt(removeIndex);
                ClioConfig.Save();
            }
            if (GUILayout.Button("マッピング追加", GUILayout.Width(120)))
            {
                mappings.Add(new ClioConfig.TagMapping());
                ClioConfig.Save();
            }
        }

        private async UniTaskVoid TestConnectionAsync()
        {
            connectionStatus = "確認中...";
            Repaint();
            var ok = await ClioClient.FromConfig().CheckHealthAsync();
            connectionStatus = ok ? "接続 OK" : "接続失敗 (Console 参照)";
            Repaint();
        }

        #endregion
    }
}
