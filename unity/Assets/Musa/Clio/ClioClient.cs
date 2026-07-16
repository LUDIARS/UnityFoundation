using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Clio
{
    /// <summary>
    /// Curare REST API クライアント
    /// NOTE: curare-server の実装 (routes/search.ts, routes/assets.ts) に対応する。
    ///   - GET   /api/v1/search?q=&projectId=&tags=a,b&page=&limit=
    ///   - GET   /api/v1/assets/:id
    ///   - PATCH /api/v1/assets/:id  (タグ更新)
    ///   - GET   /api/health
    /// 認証: Bearer (Cernere JWT)。未設定なら dev フォールバック (X-User-Id / X-User-Role)。
    /// </summary>
    public class ClioClient
    {
        private const int TimeoutSeconds = 10;

        private readonly string _baseUrl;
        private readonly string _authToken;
        private readonly string _devUserId;

        public ClioClient(string baseUrl, string authToken, string devUserId)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _authToken = authToken ?? "";
            _devUserId = devUserId ?? "";
        }

        /// <summary>ClioConfig の現在値からクライアントを作る</summary>
        public static ClioClient FromConfig()
        {
            return new ClioClient(ClioConfig.CurareBaseUrl, ClioConfig.AuthToken, ClioConfig.DevUserId);
        }

        /// <summary>
        /// タグ/キーワードでアセットを横断検索
        /// </summary>
        public async UniTask<List<ClioAsset>> SearchAssetsAsync(
            string query, IReadOnlyCollection<string> tags, int page = 1, int limit = 20)
        {
            var qs = new List<string> { $"page={page}", $"limit={limit}" };
            if (!string.IsNullOrEmpty(query))
            {
                qs.Add($"q={UnityWebRequest.EscapeURL(query)}");
            }
            if (tags != null && tags.Count > 0)
            {
                qs.Add($"tags={UnityWebRequest.EscapeURL(string.Join(",", tags))}");
            }
            if (!string.IsNullOrEmpty(ClioConfig.CurareProjectId))
            {
                qs.Add($"projectId={UnityWebRequest.EscapeURL(ClioConfig.CurareProjectId)}");
            }

            var json = await GetAsync($"/api/v1/search?{string.Join("&", qs)}");
            if (json == null) return null;

            var response = JsonUtility.FromJson<ClioSearchResponse>(json);
            return response?.results;
        }

        /// <summary>
        /// アセット詳細を取得
        /// </summary>
        public async UniTask<ClioAsset> GetAssetAsync(string assetId)
        {
            var json = await GetAsync($"/api/v1/assets/{assetId}");
            return json == null ? null : JsonUtility.FromJson<ClioAsset>(json);
        }

        /// <summary>
        /// アセットのタグを更新 (AssetLabel → Curare タグ書き戻し用)
        /// </summary>
        public async UniTask<bool> UpdateTagsAsync(string assetId, IEnumerable<string> tags)
        {
            var body = new ClioTagsUpdateRequest { tags = tags.ToList() };
            using (var request = new UnityWebRequest($"{_baseUrl}/api/v1/assets/{assetId}", "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyAuthHeaders(request);
                request.timeout = TimeoutSeconds;

                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Clio] タグ更新に失敗 ({assetId}): {ex.Message}");
                    return false;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Clio] タグ更新に失敗 ({assetId}): {request.error} {request.downloadHandler.text}");
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 疎通確認 (GET /api/health)
        /// </summary>
        public async UniTask<bool> CheckHealthAsync()
        {
            using (var request = UnityWebRequest.Get($"{_baseUrl}/api/health"))
            {
                request.timeout = TimeoutSeconds;
                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Clio] Curare へ到達できません ({_baseUrl}): {ex.Message}");
                    return false;
                }
                return request.result == UnityWebRequest.Result.Success;
            }
        }

        #region Private Methods

        private async UniTask<string> GetAsync(string pathAndQuery)
        {
            using (var request = UnityWebRequest.Get($"{_baseUrl}{pathAndQuery}"))
            {
                ApplyAuthHeaders(request);
                request.timeout = TimeoutSeconds;

                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Clio] Curare リクエスト失敗 ({pathAndQuery}): {ex.Message}");
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Clio] Curare リクエスト失敗 ({pathAndQuery}): {request.error} {request.downloadHandler.text}");
                    return null;
                }
                return request.downloadHandler.text;
            }
        }

        private void ApplyAuthHeaders(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }
            else if (!string.IsNullOrEmpty(_devUserId))
            {
                // NOTE: curare-server dev モードのフォールバック認証 (auth/middleware.ts)
                request.SetRequestHeader("X-User-Id", _devUserId);
                request.SetRequestHeader("X-User-Role", "general");
            }
        }

        #endregion
    }
}
