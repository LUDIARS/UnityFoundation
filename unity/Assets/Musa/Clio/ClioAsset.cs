using System;
using System.Collections.Generic;

namespace Clio
{
    /// <summary>
    /// Curare アセット DTO
    /// NOTE: curare-server `assets` テーブル (db/schema.ts) のうち Clio が使う部分
    /// </summary>
    [Serializable]
    public class ClioAsset
    {
        public string id = "";
        public string projectId = "";
        public string name = "";
        public string description = "";
        public List<string> tags = new List<string>();
        public string visibility = "";
    }

    /// <summary>
    /// GET /api/v1/search のレスポンス
    /// </summary>
    [Serializable]
    public class ClioSearchResponse
    {
        public List<ClioAsset> results = new List<ClioAsset>();
        public int page;
        public int limit;
    }

    /// <summary>
    /// PATCH /api/v1/assets/:id のリクエストボディ (タグ更新)
    /// </summary>
    [Serializable]
    public class ClioTagsUpdateRequest
    {
        public List<string> tags = new List<string>();
    }
}
