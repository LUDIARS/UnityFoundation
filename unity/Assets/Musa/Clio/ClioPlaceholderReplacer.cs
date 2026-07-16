using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Clio
{
    /// <summary>
    /// プレースホルダキューブ → prefab インスタンスの置換
    /// NOTE: transform (位置/回転/親/兄弟順) を保持し、Undo 1 回で戻せる
    /// </summary>
    public static class ClioPlaceholderReplacer
    {
        /// <summary>
        /// プレースホルダを解決済み候補 (resolvedAssetPath) で置換する
        /// </summary>
        /// <returns>生成した prefab インスタンス (失敗時 null)</returns>
        public static GameObject Replace(ClioPlaceholder placeholder)
        {
            if (placeholder == null) return null;
            if (string.IsNullOrEmpty(placeholder.resolvedAssetPath))
            {
                Debug.LogWarning($"[Clio] 置換候補が未解決です: {placeholder.gameObject.name}");
                return null;
            }
            return Replace(placeholder, placeholder.resolvedAssetPath);
        }

        /// <summary>
        /// プレースホルダを指定 prefab で置換する
        /// </summary>
        public static GameObject Replace(ClioPlaceholder placeholder, string prefabAssetPath)
        {
            if (placeholder == null) return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefab == null)
            {
                Debug.LogError($"[Clio] prefab が見つかりません: {prefabAssetPath}");
                return null;
            }

            var source = placeholder.gameObject;
            var sourceTransform = source.transform;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clio Replace Placeholder");
            var undoGroup = Undo.GetCurrentGroup();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, source.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Clio Replace Placeholder");

            var instanceTransform = instance.transform;
            instanceTransform.SetParent(sourceTransform.parent, false);
            instanceTransform.SetSiblingIndex(sourceTransform.GetSiblingIndex());
            instanceTransform.position = sourceTransform.position;
            instanceTransform.rotation = sourceTransform.rotation;
            ApplyFit(instanceTransform, sourceTransform, placeholder.fitMode);

            // NOTE: ユーザが名前を付けていた場合は引き継ぐ (既定名 "Cube" は prefab 名を採用)
            if (!string.IsNullOrEmpty(ClioTagScorer.ExtractNameHint(source.name)))
            {
                instance.name = source.name;
            }

            Debug.Log($"[Clio] 置換: {source.name} → {prefabAssetPath}");
            Undo.DestroyObjectImmediate(source);
            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = instance;
            return instance;
        }

        #region Private Methods

        private static void ApplyFit(Transform instance, Transform source, ClioFitMode fitMode)
        {
            if (fitMode == ClioFitMode.KeepPrefabScale) return;

            var bounds = CalculateLocalBounds(instance.gameObject);
            if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
            {
                // NOTE: Renderer を持たない prefab はサイズ合わせ不能なのでそのまま
                return;
            }

            // NOTE: プリミティブキューブは lossyScale = ワールドサイズ
            var targetSize = source.lossyScale;
            var currentSize = bounds.size;

            if (fitMode == ClioFitMode.StretchToBounds)
            {
                instance.localScale = Vector3.Scale(instance.localScale, new Vector3(
                    SafeRatio(targetSize.x, currentSize.x),
                    SafeRatio(targetSize.y, currentSize.y),
                    SafeRatio(targetSize.z, currentSize.z)));
            }
            else // UniformFit
            {
                var ratio = Mathf.Min(
                    SafeRatio(targetSize.x, currentSize.x),
                    SafeRatio(targetSize.y, currentSize.y),
                    SafeRatio(targetSize.z, currentSize.z));
                instance.localScale *= ratio;
            }
        }

        /// <summary>
        /// インスタンス直下の Renderer 合成バウンディング (ワールド) を求める
        /// </summary>
        private static Bounds CalculateLocalBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>()
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                .ToArray();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static float SafeRatio(float target, float current)
        {
            return current <= Mathf.Epsilon ? 1f : target / current;
        }

        #endregion
    }
}
