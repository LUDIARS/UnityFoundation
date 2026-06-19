namespace Foundation.Melpomene
{
    /// <summary>バグ報告の送信モード。</summary>
    public enum MelpomeneSubmitMode
    {
        /// <summary>GitHub Issues API へ直接 POST(PAT 同梱)。プレイテスト限定。</summary>
        GitHubDirect,

        /// <summary>任意のリレーサーバへ POST。トークンはサーバ側に置く。</summary>
        Relay,
    }
}
