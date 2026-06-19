namespace Foundation.Melpomene
{
    /// <summary>
    /// <c>config.submitMode</c> から送信先実装を選ぶファクトリ。
    /// 純ロジック(副作用なし・決定的)なので単体テストの主対象。
    /// </summary>
    public static class MelpomeneSubmitTargetFactory
    {
        public static IMelpomeneSubmitTarget Create(MelpomeneRuntimeConfig config)
        {
            switch (config.submitMode)
            {
                case MelpomeneSubmitMode.Relay:
                    return new MelpomeneRelayTarget(config);
                case MelpomeneSubmitMode.GitHubDirect:
                default:
                    return new MelpomeneGitHubTarget(config);
            }
        }
    }
}
