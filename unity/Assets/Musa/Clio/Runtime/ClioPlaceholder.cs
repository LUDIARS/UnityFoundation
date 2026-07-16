using System.Collections.Generic;
using UnityEngine;

namespace Clio
{
    /// <summary>
    /// 置換時の prefab スケール合わせ方
    /// </summary>
    public enum ClioFitMode
    {
        /// <summary>キューブの各軸サイズへ引き伸ばす</summary>
        StretchToBounds,
        /// <summary>縦横比を保ってキューブに収まる最大サイズへ</summary>
        UniformFit,
        /// <summary>prefab 本来のスケールを維持 (位置/回転のみ合わせる)</summary>
        KeepPrefabScale,
    }

    /// <summary>
    /// Clio プレースホルダ
    /// NOTE: キューブに付与され「どのタグのリソースへ置き換えるか」を保持する。
    ///       置換処理自体は Editor 側 (ClioPlaceholderReplacer) が行う。
    ///       ビルドに残っても副作用が無いよう Runtime アセンブリに置く。
    /// </summary>
    public class ClioPlaceholder : MonoBehaviour
    {
        /// <summary>置き換え先リソースに求めるタグ (Curare タグ語彙)</summary>
        public List<string> tags = new List<string>();

        /// <summary>スケール合わせ方</summary>
        public ClioFitMode fitMode = ClioFitMode.UniformFit;

        /// <summary>このプレースホルダを自動置換の対象にするか</summary>
        public bool autoReplace = true;

        /// <summary>最後に解決された候補 prefab のアセットパス (Editor が書き込む)</summary>
        public string resolvedAssetPath = "";

        /// <summary>最後に解決された候補のスコア (Editor が書き込む)</summary>
        public int resolvedScore;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // NOTE: プレースホルダであることを SceneView 上で識別できるようにする
            Gizmos.color = string.IsNullOrEmpty(resolvedAssetPath)
                ? new Color(1f, 0.6f, 0.1f, 0.9f)   // 未解決: オレンジ
                : new Color(0.2f, 0.9f, 0.4f, 0.9f); // 解決済: グリーン
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
#endif
    }
}
