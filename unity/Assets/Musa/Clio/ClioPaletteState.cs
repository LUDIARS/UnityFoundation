using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Clio
{
    /// <summary>
    /// 選択中タグパレットの保持
    /// NOTE: ClioWindow で選び、ClioCubeWatcher が新規キューブへ付与する。
    ///       セッション内で保持できれば十分なので SessionState を使う
    /// </summary>
    public static class ClioPaletteState
    {
        private const string SessionKey = "Clio.PaletteState.SelectedTags";

        /// <summary>選択中のタグ (Curare タグ語彙)</summary>
        public static List<string> SelectedTags
        {
            get
            {
                var raw = SessionState.GetString(SessionKey, "");
                return string.IsNullOrEmpty(raw)
                    ? new List<string>()
                    : raw.Split(',').Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            }
            set
            {
                SessionState.SetString(SessionKey, string.Join(",", value ?? new List<string>()));
            }
        }

        public static void Toggle(string tag)
        {
            var tags = SelectedTags;
            if (!tags.Remove(tag))
            {
                tags.Add(tag);
            }
            SelectedTags = tags;
        }

        public static bool IsSelected(string tag)
        {
            return SelectedTags.Contains(tag);
        }

        public static void Clear()
        {
            SelectedTags = new List<string>();
        }
    }
}
