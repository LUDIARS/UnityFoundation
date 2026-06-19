using Cysharp.Threading.Tasks;

namespace Foundation.Melpomene
{
    /// <summary>
    /// バグ報告チケットの送信先。GitHub 直送 / 任意リレーサーバなど実装を差し替えられる。
    /// </summary>
    public interface IMelpomeneSubmitTarget
    {
        /// <summary>UI に表示する送信先名(例: "GitHub (direct)" / "Relay")。</summary>
        string DisplayName { get; }

        /// <summary>チケットを送信する。失敗時は <see cref="MelpomeneSubmitResult.Success"/> が false。</summary>
        UniTask<MelpomeneSubmitResult> SubmitAsync(MelpomeneReportTicket ticket);
    }
}
