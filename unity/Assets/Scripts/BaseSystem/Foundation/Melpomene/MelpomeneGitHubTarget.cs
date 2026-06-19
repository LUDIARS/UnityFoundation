using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Foundation.Melpomene
{
    /// <summary>
    /// GitHub Issues API へ直接 Issue を作成する送信先(PAT 同梱)。
    /// UnityWebRequest + UniTask で実装しており、ビルド後のプレイヤーでも動作する。
    /// 旧 <c>MelpomeneIssueClient</c> をリネームし、<see cref="IMelpomeneSubmitTarget"/> 実装にしたもの。
    /// </summary>
    public class MelpomeneGitHubTarget : IMelpomeneSubmitTarget
    {
        readonly MelpomeneRuntimeConfig _config;

        public MelpomeneGitHubTarget(MelpomeneRuntimeConfig config)
        {
            _config = config;
        }

        public string DisplayName => "GitHub (direct)";

        public async UniTask<MelpomeneSubmitResult> SubmitAsync(MelpomeneReportTicket ticket)
        {
            if (_config == null || !_config.IsValid)
            {
                return MelpomeneSubmitResult.Fail("Melpomene config is missing or invalid.");
            }

            var url = $"{_config.ApiBaseUrl}/issues";

            var requestBody = new GitHubIssueRequest
            {
                title = ticket.GenerateIssueTitle(),
                body = ticket.GenerateIssueBody(),
                labels = MelpomeneLabelBuilder.Build(_config, ticket),
            };

            var json = JsonUtility.ToJson(requestBody);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {_config.accessToken}");
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("User-Agent", "Melpomene-Unity-Runtime");

                try
                {
                    // UniTask: 非成功(4xx/5xx, 通信失敗)は例外を送出するため catch で拾う。
                    await request.SendWebRequest();
                }
                catch (UnityWebRequestException e)
                {
                    var err = $"{e.Error} / {e.Text}";
                    Debug.LogError($"[Melpomene] Failed to create issue: {err}");
                    return MelpomeneSubmitResult.Fail(err);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Melpomene] Exception creating issue: {e.Message}");
                    return MelpomeneSubmitResult.Fail(e.Message);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<GitHubIssueResponse>(request.downloadHandler.text);
                    Debug.Log($"[Melpomene] Issue created: #{response.number} - {response.html_url}");
                    return MelpomeneSubmitResult.Ok(response.number, response.html_url);
                }

                var fallbackErr = $"{request.error} / {request.downloadHandler.text}";
                Debug.LogError($"[Melpomene] Failed to create issue: {fallbackErr}");
                return MelpomeneSubmitResult.Fail(fallbackErr);
            }
        }

        [Serializable]
        class GitHubIssueRequest
        {
            public string title;
            public string body;
            public string[] labels;
        }

        [Serializable]
        class GitHubIssueResponse
        {
            public int number;
            public string html_url;
            public string state;
        }
    }
}
