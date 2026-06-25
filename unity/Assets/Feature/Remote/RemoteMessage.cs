#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。
//       spec/code/Remote/protocol.md のエンベロープ/制御メッセージを忠実に写したDTO群。

/// <summary>
/// プロトコル共通エンベロープ。
/// JsonUtility は任意 dictionary を扱えないため、payload は型ごとに具象 [Serializable]
/// クラスを用意し「2パスパース」する (1回目で type を読み、2回目で型付き payload を読む)。
/// </summary>
[Serializable]
public class RemoteEnvelope
{
    /// <summary>プロトコルバージョン (常に 1)。</summary>
    public int v = 1;

    /// <summary>メッセージ種別 (RemoteMessageType の定数)。</summary>
    public string type;

    /// <summary>リクエスト/レスポンス対応付け用の任意 id。</summary>
    public string id;

    /// <summary>送信側の unix ms。</summary>
    public long ts;
}

/// <summary>
/// 型付き payload を載せたエンベロープ。JsonUtility は generic を直接シリアライズ
/// できないが、具象派生型 (RemoteEnvelope&lt;HelloPayload&gt; 等) なら扱える。
/// </summary>
[Serializable]
public class RemoteEnvelope<TPayload> : RemoteEnvelope
{
    public TPayload payload;
}

/// <summary>
/// メッセージ種別の文字列定数 (protocol.md の type 列と一致させる)。
/// </summary>
public static class RemoteMessageType
{
    public const string Hello = "hello";
    public const string Welcome = "welcome";

    public const string CommandListRequest = "command.list.request";
    public const string CommandList = "command.list";
    public const string CommandInvoke = "command.invoke";
    public const string CommandResult = "command.result";

    public const string SceneLoad = "scene.load";
    public const string BlackboardSet = "blackboard.set";
    public const string MasterReload = "master.reload";

    public const string Telemetry = "telemetry";
    public const string Log = "log";

    public const string RtcOffer = "rtc.offer";
    public const string RtcAnswer = "rtc.answer";
    public const string RtcIce = "rtc.ice";

    public const string Error = "error";
}

// ---- payload DTOs (protocol.md の payload 列を写す) ----

[Serializable]
public class HelloPayload
{
    public string role; // "unity" | "panel"
    public string name;
}

[Serializable]
public class WelcomePayload
{
    public string sessionId;
    public bool unityConnected;
}

[Serializable]
public class CommandInfo
{
    public string name;
    public string description;
    public string kind; // "action" | "toggle"
}

[Serializable]
public class CommandListPayload
{
    public CommandInfo[] commands;
}

[Serializable]
public class CommandInvokePayload
{
    public string name;
    // JsonUtility には null 可能 bool が無いので、value 有無は valueProvided で表す。
    // (受信時に hasValue を別途判定するのは難しいため、bridge 側で value を解釈する)
    public bool value;
}

[Serializable]
public class CommandResultPayload
{
    public string name;
    public bool ok;
    public string message;
}

[Serializable]
public class SceneLoadPayload
{
    public string scene;
}

[Serializable]
public class BlackboardSetPayload
{
    public string key;
    public string value; // 値は文字列で受け、bridge 側で型解釈 (TODO参照)
}

[Serializable]
public class MasterReloadPayload
{
    public string sheet;
}

[Serializable]
public class TelemetryPayload
{
    public float fps;
    public string scene;
    public float memoryMB;
    public long time;
}

[Serializable]
public class LogPayload
{
    public string level; // log/warning/error
    public string message;
}

[Serializable]
public class RtcSdpPayload
{
    public string sdp;
}

[Serializable]
public class RtcIcePayload
{
    public string candidate;
}

[Serializable]
public class ErrorPayload
{
    public string code;    // unity_not_connected | bad_message | unknown_type | internal
    public string message;
}

/// <summary>
/// エンベロープの組立て/解析ヘルパ。JsonUtility ベース。
/// </summary>
public static class RemoteMessage
{
    /// <summary>
    /// 型付き payload を JSON 文字列へ。ts は呼び出し時刻 (unix ms) を補完。
    /// </summary>
    public static string Build<TPayload>(string type, TPayload payload, string id = null)
    {
        var envelope = new RemoteEnvelope<TPayload>
        {
            v = 1,
            type = type,
            id = id,
            ts = NowUnixMs(),
            payload = payload,
        };
        return JsonUtility.ToJson(envelope);
    }

    /// <summary>
    /// 1パス目: type/id だけ読む (payload は無視)。
    /// </summary>
    public static RemoteEnvelope ParseHeader(string json)
    {
        try
        {
            return JsonUtility.FromJson<RemoteEnvelope>(json);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Remote] ParseHeader failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 2パス目: type 判定後に型付き payload を読む。
    /// </summary>
    public static RemoteEnvelope<TPayload> Parse<TPayload>(string json)
    {
        try
        {
            return JsonUtility.FromJson<RemoteEnvelope<TPayload>>(json);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Remote] Parse<{typeof(TPayload).Name}> failed: {e.Message}");
            return null;
        }
    }

    public static long NowUnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
#endif
