using System;

namespace Foundation.Melpomene
{
    /// <summary>チケットの優先度。</summary>
    public enum MelpomeneRuntimePriority
    {
        Low,
        Medium,
        High,
        Critical,
    }

    /// <summary>チケットのカテゴリ。</summary>
    public enum MelpomeneRuntimeCategory
    {
        Bug,
        Feature,
        Improvement,
        Question,
    }

    /// <summary>
    /// ゲーム内バグ報告チケット。GitHub Issue のタイトル/本文を生成する。
    /// </summary>
    [Serializable]
    public class MelpomeneReportTicket
    {
        public string userName;
        public string title;
        public string description;
        public string sceneName;
        public MelpomeneRuntimePriority priority = MelpomeneRuntimePriority.Medium;
        public MelpomeneRuntimeCategory category = MelpomeneRuntimeCategory.Bug;
        public string platform;
        public string appVersion;
        public string screenInfo;
        public string timestamp;

        /// <summary>GitHub Issue タイトルを生成する。</summary>
        public string GenerateIssueTitle()
        {
            return $"[Melpomene] {title}";
        }

        /// <summary>GitHub Issue 本文を生成する。</summary>
        public string GenerateIssueBody()
        {
            return $@"## 報告者
{(string.IsNullOrEmpty(userName) ? "(匿名)" : userName)}

## 発生状況
- **シーン**: {sceneName}
- **プラットフォーム**: {platform}
- **アプリバージョン**: {appVersion}
- **画面**: {screenInfo}

## 説明
{description}

## メタデータ
- **優先度**: {priority}
- **カテゴリ**: {category}
- **報告日時**: {timestamp}

---
*この Issue はゲーム内 Melpomene レポーターによって自動生成されました*
<sub>Melpomene Runtime v{MelpomeneRuntimeConfig.Version}</sub>";
        }
    }
}
