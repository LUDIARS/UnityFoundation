#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Cysharp.Threading.Tasks;
using DataManagement;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// scene.load / blackboard.set / master.reload を既存システムへ橋渡しする。
/// メインスレッドで呼ばれる前提 (RemoteDebugClient.DrainInbound 経由)。
/// </summary>
public sealed class RemoteSceneBridge
{
    public bool Handle(string json, RemoteEnvelope header)
    {
        switch (header.type)
        {
            case RemoteMessageType.SceneLoad:
                HandleSceneLoad(json);
                return true;

            case RemoteMessageType.BlackboardSet:
                HandleBlackboardSet(json);
                return true;

            case RemoteMessageType.MasterReload:
                HandleMasterReload(json);
                return true;

            default:
                return false;
        }
    }

    private void HandleSceneLoad(string json)
    {
        var env = RemoteMessage.Parse<SceneLoadPayload>(json);
        var scene = env?.payload?.scene;
        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogWarning("[Remote] scene.load: empty scene name");
            return;
        }

        // 既存のシーン読み込み経路を使用する。
        // SceneLoader.LoadScene は SceneDependencies(Addressables) を解決して
        // ベース+追加シーンを読み込む正規ルート。
        SceneLoader.LoadScene(scene);
    }

    private void HandleBlackboardSet(string json)
    {
        var env = RemoteMessage.Parse<BlackboardSetPayload>(json);
        var payload = env?.payload;
        if (payload == null || string.IsNullOrEmpty(payload.key))
        {
            Debug.LogWarning("[Remote] blackboard.set: missing key");
            return;
        }

        // TODO(verify): Blackboard は ReactiveProperty<T> ベースの強い型付き API
        //   (Register/Subscribe<T>) しか持たず、文字列キー→任意値の汎用 Set が無い。
        //   partial 自動生成 (BlackboardAttribute / BlackboardCodeBuilder) 側に
        //   キー名→プロパティの動的セッタを生やすか、専用の SetByKey(string,string) を
        //   追加する必要がある。現状は受信ログのみ。
        Debug.Log($"[Remote] blackboard.set requested key={payload.key} value={payload.value} " +
                  "(no generic string setter on Blackboard — see TODO)");
    }

    private void HandleMasterReload(string json)
    {
        var env = RemoteMessage.Parse<MasterReloadPayload>(json);
        var sheet = env?.payload?.sheet;
        Debug.Log($"[Remote] master.reload sheet={sheet}");

        // TODO(verify): MasterData には「指定シートのみ」を再取得する public API が無い。
        //   LoadMasterData<T>(sheetName) は具象型 T が必要で、sheet 名だけでは呼べない。
        //   MasterDataLoad() は private。最も近い公開 API は MasterData.Instance.Setup()
        //   で、これは全マスタを再ロードする (Data Studio publish の反映には十分)。
        //   per-sheet 反映が要るなら MasterData に Reload(string sheet) を追加するのが筋。
        ReloadAll().Forget();
    }

    private static async UniTaskVoid ReloadAll()
    {
        // _versionInfos を再評価させたいが Setup は前回値があるとスキップしうる。
        // ここでは全体 Setup を呼ぶ (詳細な per-sheet 制御は MasterData 側拡張が必要)。
        await MasterData.Instance.Setup();
    }
}
#endif
