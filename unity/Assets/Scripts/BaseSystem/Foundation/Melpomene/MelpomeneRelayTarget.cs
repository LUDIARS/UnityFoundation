using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Foundation.Melpomene
{
    /// <summary>
    /// 任意のリレーサーバ(<c>config.relayUrl</c>)へ報告を POST する送信先。
    /// GitHub トークンはサーバ側に置きクライアントへ載せない。AWS に限定されない。
    /// 契約は spec/code/Debug/MelpomeneDestination.md 準拠。
    /// </summary>
    public class MelpomeneRelayTarget : IMelpomeneSubmitTarget
    {
        readonly MelpomeneRuntimeConfig _config;

        public MelpomeneRelayTarget(MelpomeneRuntimeConfig config)
        {
            _config = config;
        }

        public string DisplayName => "Relay";

        public async UniTask<MelpomeneSubmitResult> SubmitAsync(MelpomeneReportTicket ticket)
        {
            if (_config == null || !_config.IsValid)
            {
                return MelpomeneSubmitResult.Fail("Melpomene config is missing or invalid.");
            }

            var url = _config.relayUrl;

            var requestBody = new MelpomeneRelayRequest
            {
                title = ticket.GenerateIssueTitle(),
                body = ticket.GenerateIssueBody(),
                labels = MelpomeneLabelBuilder.Build(_config, ticket),
                category = ticket.category.ToString(),
                priority = ticket.priority.ToString(),
                userName = ticket.userName,
                sceneName = ticket.sceneName,
                platform = ticket.platform,
                appVersion = ticket.appVersion,
                screenInfo = ticket.screenInfo,
                timestamp = ticket.timestamp,
                source = "melpomene-runtime",
                clientVersion = MelpomeneRuntimeConfig.Version,
            };

            var json = JsonUtility.ToJson(requestBody);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("User-Agent", "Melpomene-Unity-Runtime");
                if (!string.IsNullOrEmpty(_config.relayAuthHeader))
                {
                    request.SetRequestHeader("Authorization", _config.relayAuthHeader);
                }

                try
                {
                    // UniTask: 非成功(4xx/5xx, 通信失敗)は例外を送出するため catch で拾う。
                    await request.SendWebRequest();
                }
                catch (UnityWebRequestException e)
                {
                    var err = $"{e.Error} / {e.Text}";
                    Debug.LogError($"[Melpomene] Failed to relay report: {err}");
                    return MelpomeneSubmitResult.Fail(err);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Melpomene] Exception relaying report: {e.Message}");
                    return MelpomeneSubmitResult.Fail(e.Message);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<MelpomeneRelayResponse>(request.downloadHandler.text);
                    if (response != null && response.success)
                    {
                        Debug.Log($"[Melpomene] Report relayed: #{response.issueNumber} - {response.issueUrl}");
                        return MelpomeneSubmitResult.Ok(response.issueNumber, response.issueUrl);
                    }

                    var serverErr = response != null && !string.IsNullOrEmpty(response.error)
                        ? response.error
                        : "relay returned success:false";
                    Debug.LogError($"[Melpomene] Relay rejected report: {serverErr}");
                    return MelpomeneSubmitResult.Fail(serverErr);
                }

                var fallbackErr = $"{request.error} / {request.downloadHandler.text}";
                Debug.LogError($"[Melpomene] Failed to relay report: {fallbackErr}");
                return MelpomeneSubmitResult.Fail(fallbackErr);
            }
        }

        [Serializable]
        class MelpomeneRelayRequest
        {
            public string title;
            public string body;
            public string[] labels;
            public string category;
            public string priority;
            public string userName;
            public string sceneName;
            public string platform;
            public string appVersion;
            public string screenInfo;
            public string timestamp;
            public string source;
            public string clientVersion;
        }

        [Serializable]
        class MelpomeneRelayResponse
        {
            public bool success;
            public int issueNumber;
            public string issueUrl;
            public string error;
        }
    }
}
