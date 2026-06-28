namespace Foundation.Melpomene
{
    /// <summary>
    /// 送信先(<see cref="IMelpomeneSubmitTarget"/>)からの送信結果。
    /// GitHub 直送・リレー送信のどちらでも共通で使う。
    /// </summary>
    public struct MelpomeneSubmitResult
    {
        public bool Success;
        public int IssueNumber;
        public string IssueUrl;
        public string Error;

        /// <summary>失敗結果を生成する。</summary>
        public static MelpomeneSubmitResult Fail(string error)
        {
            return new MelpomeneSubmitResult { Success = false, Error = error };
        }

        /// <summary>成功結果を生成する。</summary>
        public static MelpomeneSubmitResult Ok(int number, string url)
        {
            return new MelpomeneSubmitResult { Success = true, IssueNumber = number, IssueUrl = url };
        }
    }
}
