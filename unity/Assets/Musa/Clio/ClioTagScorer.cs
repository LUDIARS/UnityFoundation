using System;
using System.Collections.Generic;
using System.Linq;

namespace Clio
{
    /// <summary>
    /// タグ合致スコア計算 (純ロジック・Unity API 非依存)
    /// NOTE: 「タグ合致が主、名前一致は補助ヒント」の重み付けをここに集約する
    /// </summary>
    public static class ClioTagScorer
    {
        /// <summary>タグ 1 件一致のスコア</summary>
        public const int TagMatchScore = 10;

        /// <summary>名前が完全一致した場合の加点</summary>
        public const int NameExactScore = 5;

        /// <summary>名前トークンが含まれる場合の加点 (トークンごと)</summary>
        public const int NameTokenScore = 2;

        /// <summary>
        /// 候補アセットのスコアを計算する
        /// </summary>
        /// <param name="candidateTags">候補アセットが持つタグ (Curare タグ語彙)</param>
        /// <param name="wantedTags">プレースホルダが求めるタグ</param>
        /// <param name="candidateName">候補アセット名</param>
        /// <param name="nameHint">オブジェクト名などの補助ヒント (null 可)</param>
        public static int Score(
            IReadOnlyCollection<string> candidateTags,
            IReadOnlyCollection<string> wantedTags,
            string candidateName,
            string nameHint)
        {
            var score = 0;

            if (candidateTags != null && wantedTags != null)
            {
                var candidateSet = new HashSet<string>(
                    candidateTags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(Normalize));
                score += wantedTags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Count(t => candidateSet.Contains(Normalize(t))) * TagMatchScore;
            }

            if (!string.IsNullOrWhiteSpace(nameHint) && !string.IsNullOrWhiteSpace(candidateName))
            {
                var name = Normalize(candidateName);
                var hint = Normalize(nameHint);
                if (name == hint)
                {
                    score += NameExactScore;
                }
                else
                {
                    score += Tokenize(hint).Count(token => name.Contains(token)) * NameTokenScore;
                }
            }

            return score;
        }

        /// <summary>
        /// オブジェクト名からヒントトークンを取り出す
        /// NOTE: "Cube (1)" のようなプリミティブ既定名はヒントにしない
        /// </summary>
        public static string ExtractNameHint(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return "";
            var trimmed = System.Text.RegularExpressions.Regex
                .Replace(objectName, @"\s*\(\d+\)\s*$", "")
                .Trim();
            return Normalize(trimmed) == "cube" ? "" : trimmed;
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static IEnumerable<string> Tokenize(string value)
        {
            return value.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2);
        }
    }
}
