using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Clio
{
    /// <summary>
    /// 新規キューブ検出 → リソース自動選択
    /// NOTE: 「対象物をキューブにするだけでリソースを自動選択する」の本体。
    ///       hierarchyChanged で素のプリミティブキューブを見つけ、
    ///       ClioPlaceholder を付与 → 解決 → (設定次第で) 置換まで行う
    /// </summary>
    [InitializeOnLoad]
    public static class ClioCubeWatcher
    {
        /// <summary>処理済みオブジェクトの instanceID (再処理防止)</summary>
        private static readonly HashSet<int> SeenInstanceIds = new HashSet<int>();

        private static bool _processScheduled;

        static ClioCubeWatcher()
        {
            // NOTE: 起動時点で存在するキューブは「既存レイアウト」として対象外にする
            MarkExistingCubesAsSeen();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        /// <summary>
        /// シーン内の未処理キューブを今すぐ走査する (ClioWindow の手動ボタン用)
        /// </summary>
        public static int RescanNow()
        {
            return ProcessNewCubes(force: true);
        }

        #region Private Methods

        private static void OnHierarchyChanged()
        {
            if (!ClioConfig.AutoSelectEnabled) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            if (_processScheduled) return;

            // NOTE: 置換で hierarchyChanged が再発火するため delayCall で 1 回にまとめる
            _processScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _processScheduled = false;
                ProcessNewCubes(force: false);
            };
        }

        private static int ProcessNewCubes(bool force)
        {
            var processed = 0;
            foreach (var meshFilter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var go = meshFilter.gameObject;
                if (!force && SeenInstanceIds.Contains(go.GetInstanceID())) continue;
                SeenInstanceIds.Add(go.GetInstanceID());

                if (!IsBareCube(meshFilter)) continue;

                var placeholder = go.GetComponent<ClioPlaceholder>();
                if (placeholder == null)
                {
                    placeholder = Undo.AddComponent<ClioPlaceholder>(go);
                    placeholder.tags = new List<string>(ClioPaletteState.SelectedTags);
                }

                var best = ClioResourceResolver.ResolveInto(placeholder);
                if (best == null)
                {
                    Debug.Log($"[Clio] 候補なし: {go.name} (tags: {string.Join(",", placeholder.tags)})");
                    processed++;
                    continue;
                }

                Debug.Log($"[Clio] 自動選択: {go.name} → {best.assetPath} (score {best.score})");
                if (ClioConfig.AutoReplaceEnabled && placeholder.autoReplace)
                {
                    ClioPlaceholderReplacer.Replace(placeholder);
                }
                processed++;
            }
            return processed;
        }

        /// <summary>
        /// 素のプリミティブキューブか (Cube メッシュ + 基本コンポーネントのみ)
        /// </summary>
        private static bool IsBareCube(MeshFilter meshFilter)
        {
            if (meshFilter.sharedMesh == null || meshFilter.sharedMesh.name != "Cube") return false;

            // NOTE: ゲームロジック持ちのオブジェクトを誤って対象にしないためのガード。
            //       Transform/MeshFilter/MeshRenderer/Collider/ClioPlaceholder 以外があれば対象外
            return meshFilter.gameObject.GetComponents<Component>().All(c =>
                c is Transform ||
                c is MeshFilter ||
                c is MeshRenderer ||
                c is Collider ||
                c is ClioPlaceholder);
        }

        private static void MarkExistingCubesAsSeen()
        {
            foreach (var meshFilter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                SeenInstanceIds.Add(meshFilter.gameObject.GetInstanceID());
            }
        }

        #endregion
    }
}
