using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Foundation.Melpomene
{
    /// <summary>
    /// ランタイムから GitHub Issues API へ Issue を作成するクライアント。
    /// UnityWebRequest + UniTask で実装しており、ビルド後のプレイヤーでも動作する。
    /// </summary>
    public class MelpomeneIssueClient
    {
        readonly MelpomeneRuntimeConfig _config;

        public MelpomeneIssueClient(MelpomeneRuntimeConfig config)
        {
            _config = config;
        }

        /// <summary>Issue 作成結果。</summary>
        public struct Result
        {
            public bool Success;
            public int IssueNumber;
            public string IssueUrl;
            public string Error;
        }

        /// <summary>
        /// Issue を作成する。設定が無効な場合や通信失敗時は <see cref="Result.Success"/> が false になる。
        /// </summary>
        public async UniTask<Result> CreateIssueAsync(MelpomeneReportTicket ticket)
        {
            if (_config == null || !_config.IsValid)
            {
                return new Result { Success = false, Error = "Melpomene config is missing or invalid." };
            }

            var url = $"{_config.ApiBaseUrl}/issues";

            var labels = new List<string>();
            if (_config.defaultLabels != null)
            {
                labels.AddRange(_config.defaultLabels);
            }
            labels.Add(ticket.priority.ToString().ToLower());
            labels.Add(ticket.category.ToString().ToLower());

            var requestBody = new GitHubIssueRequest
            {
                title = ticket.GenerateIssueTitle(),
                body = ticket.GenerateIssueBody(),
                labels = labels.ToArray(),
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
                    return new Result { Success = false, Error = err };
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Melpomene] Exception creating issue: {e.Message}");
                    return new Result { Success = false, Error = e.Message };
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<GitHubIssueResponse>(request.downloadHandler.text);
                    Debug.Log($"[Melpomene] Issue created: #{response.number} - {response.html_url}");
                    return new Result
                    {
                        Success = true,
                        IssueNumber = response.number,
                        IssueUrl = response.html_url,
                    };
                }

                var fallbackErr = $"{request.error} / {request.downloadHandler.text}";
                Debug.LogError($"[Melpomene] Failed to create issue: {fallbackErr}");
                return new Result { Success = false, Error = fallbackErr };
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
