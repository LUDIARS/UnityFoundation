using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Clio
{
    /// <summary>
    /// Unity AssetLabel ↔ Curare タグ体系の双方向マッピング
    /// NOTE: マッピング表 (clio_config.json の tagMappings) に無い語は
    ///       同名タグ/ラベルとしてそのまま通す (identity マッピング)
    /// </summary>
    public static class ClioTagMapper
    {
        /// <summary>Curare タグ → Unity AssetLabel</summary>
        public static List<string> CurareTagsToUnityLabels(IEnumerable<string> curareTags)
        {
            var mappings = ClioConfig.TagMappings;
            return curareTags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(tag =>
                {
                    var hit = mappings.FirstOrDefault(m =>
                        string.Equals(m.curareTag, tag, System.StringComparison.OrdinalIgnoreCase));
                    return string.IsNullOrEmpty(hit?.unityLabel) ? tag : hit.unityLabel;
                })
                .Distinct()
                .ToList();
        }

        /// <summary>Unity AssetLabel → Curare タグ</summary>
        public static List<string> UnityLabelsToCurareTags(IEnumerable<string> unityLabels)
        {
            var mappings = ClioConfig.TagMappings;
            return unityLabels
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(label =>
                {
                    var hit = mappings.FirstOrDefault(m =>
                        string.Equals(m.unityLabel, label, System.StringComparison.OrdinalIgnoreCase));
                    return string.IsNullOrEmpty(hit?.curareTag) ? label : hit.curareTag;
                })
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// ラベル群のいずれかを持つローカル prefab の GUID を列挙する (OR 検索)
        /// </summary>
        public static List<string> FindLocalPrefabGuidsByLabels(IEnumerable<string> unityLabels)
        {
            var guids = new List<string>();
            var seen = new HashSet<string>();
            foreach (var label in unityLabels.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                foreach (var guid in AssetDatabase.FindAssets($"t:Prefab l:{label}"))
                {
                    if (seen.Add(guid))
                    {
                        guids.Add(guid);
                    }
                }
            }
            return guids;
        }

        /// <summary>
        /// prefab アセットの AssetLabel を Curare タグ語彙で返す
        /// </summary>
        public static List<string> GetCurareTagsOfAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return new List<string>();
            return UnityLabelsToCurareTags(AssetDatabase.GetLabels(asset));
        }

        /// <summary>
        /// タグパレット用の語彙を集める (ローカル prefab の全ラベル → Curare タグ語彙)
        /// </summary>
        public static List<string> CollectLocalVocabulary()
        {
            var labels = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                foreach (var label in AssetDatabase.GetLabels(asset))
                {
                    labels.Add(label);
                }
            }
            return UnityLabelsToCurareTags(labels).OrderBy(t => t).ToList();
        }
    }
}
