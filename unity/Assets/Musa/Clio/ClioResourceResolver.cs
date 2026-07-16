using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Clio
{
    /// <summary>
    /// 解決候補 1 件
    /// </summary>
    public class ClioCandidate
    {
        public string assetPath;
        public string assetName;
        public List<string> curareTags = new List<string>();
        public int score;
    }

    /// <summary>
    /// タグ → ローカル prefab 候補の解決
    /// NOTE: AssetLabel (ClioTagMapper) で候補を集め、ClioTagScorer で順位付けする。
    ///       ネットワークに出ない同期処理のみ (Curare 横断検索は ClioWindow 側)
    /// </summary>
    public static class ClioResourceResolver
    {
        /// <summary>
        /// 求めるタグ + 名前ヒントから候補を列挙する (スコア降順)
        /// </summary>
        public static List<ClioCandidate> ResolveCandidates(
            IReadOnlyCollection<string> wantedCurareTags, string nameHint, int maxCandidates = 10)
        {
            var wanted = (wantedCurareTags ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            var guids = new List<string>();
            if (wanted.Count > 0)
            {
                var labels = ClioTagMapper.CurareTagsToUnityLabels(wanted);
                guids.AddRange(ClioTagMapper.FindLocalPrefabGuidsByLabels(labels));
            }

            // NOTE: 名前ヒントでの補完検索 (タグ未設定のキューブでも名前から解決できるように)
            if (!string.IsNullOrWhiteSpace(nameHint))
            {
                foreach (var guid in AssetDatabase.FindAssets($"t:Prefab {nameHint}"))
                {
                    if (!guids.Contains(guid))
                    {
                        guids.Add(guid);
                    }
                }
            }

            var candidates = new List<ClioCandidate>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var tags = ClioTagMapper.GetCurareTagsOfAsset(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var score = ClioTagScorer.Score(tags, wanted, name, nameHint);
                if (score <= 0) continue;

                candidates.Add(new ClioCandidate
                {
                    assetPath = path,
                    assetName = name,
                    curareTags = tags,
                    score = score,
                });
            }

            return candidates
                .OrderByDescending(c => c.score)
                .ThenBy(c => c.assetName)
                .Take(maxCandidates)
                .ToList();
        }

        /// <summary>
        /// 最良候補を返す (無ければ null)
        /// </summary>
        public static ClioCandidate ResolveBest(
            IReadOnlyCollection<string> wantedCurareTags, string nameHint)
        {
            return ResolveCandidates(wantedCurareTags, nameHint, 1).FirstOrDefault();
        }

        /// <summary>
        /// プレースホルダの解決結果を書き込む
        /// </summary>
        public static ClioCandidate ResolveInto(ClioPlaceholder placeholder)
        {
            var hint = ClioTagScorer.ExtractNameHint(placeholder.gameObject.name);
            var best = ResolveBest(placeholder.tags, hint);
            placeholder.resolvedAssetPath = best?.assetPath ?? "";
            placeholder.resolvedScore = best?.score ?? 0;
            EditorUtility.SetDirty(placeholder);
            return best;
        }
    }
}
