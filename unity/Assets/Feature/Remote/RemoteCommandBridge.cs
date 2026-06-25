#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// DebugPrompt のコマンド (IDebugCommand) と Remote 制御チャネルの橋渡し。
/// - command.list.request -> command.list で一覧返答
/// - command.invoke -> Execute()。toggle は payload.value があれば IsEnabled へ反映
/// - 実行結果を command.result で返す
///
/// 送信は client.Enqueue を使う (SRP: WS の所有はしない)。
/// メインスレッドで呼ばれる前提 (RemoteDebugClient.DrainInbound 経由)。
/// </summary>
public sealed class RemoteCommandBridge
{
    private readonly RemoteDebugClient _client;
    private readonly IReadOnlyList<IDebugCommand> _commands;

    public RemoteCommandBridge(RemoteDebugClient client, IReadOnlyList<IDebugCommand> commands)
    {
        _client = client;
        _commands = commands;
    }

    /// <summary>
    /// RemoteDebugClient.OnMessage から受信 JSON とヘッダを受け取り、対象 type のみ処理する。
    /// </summary>
    /// <returns>このブリッジが処理した場合 true。</returns>
    public bool Handle(string json, RemoteEnvelope header)
    {
        switch (header.type)
        {
            case RemoteMessageType.CommandListRequest:
                HandleListRequest(header.id);
                return true;

            case RemoteMessageType.CommandInvoke:
                HandleInvoke(json, header.id);
                return true;

            default:
                return false;
        }
    }

    private void HandleListRequest(string requestId)
    {
        var infos = new List<CommandInfo>(_commands.Count);
        foreach (var cmd in _commands)
        {
            infos.Add(new CommandInfo
            {
                name = cmd.Name,
                description = cmd.Description,
                kind = (cmd is DebugToggleCommandBase) ? "toggle" : "action",
            });
        }

        var payload = new CommandListPayload { commands = infos.ToArray() };
        _client.Enqueue(RemoteMessage.Build(RemoteMessageType.CommandList, payload, requestId));
    }

    private void HandleInvoke(string json, string requestId)
    {
        var env = RemoteMessage.Parse<CommandInvokePayload>(json);
        var payload = env?.payload;
        if (payload == null || string.IsNullOrEmpty(payload.name))
        {
            Reply(requestId, "", false, "missing command name");
            return;
        }

        var cmd = Find(payload.name);
        if (cmd == null)
        {
            Reply(requestId, payload.name, false, "command not found");
            return;
        }

        try
        {
            if (cmd is DebugToggleCommandBase toggle)
            {
                // protocol.md: toggle は value:boolean で状態を指定する。
                // JsonUtility は null 不可 bool のため、payload.value をそのまま採用する
                // (panel 側は toggle 実行時に必ず value を入れる契約)。
                toggle.IsEnabled = payload.value;
            }
            else
            {
                cmd.Execute();
            }

            Reply(requestId, payload.name, true, null);
        }
        catch (Exception e)
        {
            Reply(requestId, payload.name, false, e.Message);
        }
    }

    private IDebugCommand Find(string name)
    {
        foreach (var cmd in _commands)
        {
            if (cmd.Name == name) return cmd;
        }
        return null;
    }

    private void Reply(string requestId, string name, bool ok, string message)
    {
        var payload = new CommandResultPayload { name = name, ok = ok, message = message };
        _client.Enqueue(RemoteMessage.Build(RemoteMessageType.CommandResult, payload, requestId));
    }
}
#endif
