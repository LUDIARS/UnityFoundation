using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Foundation.Melpomene
{
    /// <summary>
    /// ビルド後のゲーム内で動作するバグ報告オーバーレイ。
    /// IMGUI(OnGUI) で実装しているため Canvas / EventSystem / フォント等の追加配線なしで
    /// プレイヤービルド上でも確実に表示・入力できる。
    /// 入力内容を GitHub Issue として送信する（最小スコープ：ゲーム内バグ報告のみ）。
    /// </summary>
    public class MelpomeneReporter : MonoBehaviour
    {
        MelpomeneRuntimeConfig _config;
        MelpomeneIssueClient _client;

        bool _open;
        bool _sending;
        string _title = "";
        string _description = "";
        string _userName = "";
        MelpomeneRuntimeCategory _category = MelpomeneRuntimeCategory.Bug;
        MelpomeneRuntimePriority _priority = MelpomeneRuntimePriority.Medium;
        string _status = "";
        Vector2 _descScroll;

        Rect _windowRect;
        const int WindowId = 0x4D454C50; // 'MELP'

        static readonly string[] CategoryLabels = { "Bug", "Feature", "Improvement", "Question" };
        static readonly string[] PriorityLabels = { "Low", "Medium", "High", "Critical" };

        /// <summary>外部（Bootstrap）から設定を注入して初期化する。</summary>
        public void Initialize(MelpomeneRuntimeConfig config)
        {
            _config = config;
            _client = new MelpomeneIssueClient(config);
            _userName = SystemInfo.deviceName;
        }

        void Update()
        {
            if (_config == null) return;
            if (_config.toggleKey == Key.None) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[_config.toggleKey].wasPressedThisFrame)
            {
                _open = !_open;
            }
        }

        void OnGUI()
        {
            if (_config == null) return;

            // 解像度に応じて UI を拡大（高解像度・モバイルでも読めるように）。
            float scale = Mathf.Clamp(Screen.height / 720f, 1f, 3f);
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float virtualW = Screen.width / scale;

            if (!_open)
            {
                // 常設の起動ボタン（右上）。
                if (GUI.Button(new Rect(virtualW - 110f, 8f, 100f, 30f), "🐞 Report"))
                {
                    _open = true;
                }
                GUI.matrix = prevMatrix;
                return;
            }

            if (_windowRect.width <= 0f)
            {
                _windowRect = new Rect((virtualW - 420f) * 0.5f, 40f, 420f, 420f);
            }

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Melpomene バグ報告");
            GUI.matrix = prevMatrix;
        }

        void DrawWindow(int id)
        {
            using (new GUILayout.VerticalScope())
            {
                if (!_config.IsValid)
                {
                    GUILayout.Label("⚠ 設定が未完成です（repositoryOwner / repositoryName / accessToken）。\n" +
                                    "Resources/MelpomeneRuntimeConfig を設定してください。");
                }

                GUILayout.Label("報告者");
                _userName = GUILayout.TextField(_userName ?? "");

                GUILayout.Label("タイトル（概要）");
                _title = GUILayout.TextField(_title ?? "");

                GUILayout.Label("詳細");
                using (var sv = new GUILayout.ScrollViewScope(_descScroll, GUILayout.Height(120f)))
                {
                    _descScroll = sv.scrollPosition;
                    _description = GUILayout.TextArea(_description ?? "", GUILayout.ExpandHeight(true));
                }

                GUILayout.Label("カテゴリ");
                _category = (MelpomeneRuntimeCategory)GUILayout.SelectionGrid((int)_category, CategoryLabels, 4);

                GUILayout.Label("優先度");
                _priority = (MelpomeneRuntimePriority)GUILayout.SelectionGrid((int)_priority, PriorityLabels, 4);

                GUILayout.Space(8f);

                using (new GUILayout.HorizontalScope())
                {
                    bool canSend = !_sending && !string.IsNullOrWhiteSpace(_title) && _config.IsValid;
                    using (new GuiDisabledScope(!canSend))
                    {
                        if (GUILayout.Button(_sending ? "送信中..." : "送信"))
                        {
                            Submit().Forget();
                        }
                    }

                    if (GUILayout.Button("閉じる"))
                    {
                        _open = false;
                    }
                }

                if (!string.IsNullOrEmpty(_status))
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(_status);
                }
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        async UniTaskVoid Submit()
        {
            _sending = true;
            _status = "送信中...";

            var ticket = new MelpomeneReportTicket
            {
                userName = _userName,
                title = _title,
                description = _description,
                category = _category,
                priority = _priority,
                sceneName = SceneManager.GetActiveScene().name,
                platform = $"{Application.platform} ({SystemInfo.operatingSystem})",
                appVersion = Application.version,
                screenInfo = _config.captureScreenInfo
                    ? $"{Screen.width}x{Screen.height} dpi:{Screen.dpi:F0}"
                    : "",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            var result = await _client.CreateIssueAsync(ticket);

            _sending = false;
            if (result.Success)
            {
                _status = $"送信完了！ Issue #{result.IssueNumber}";
                _title = "";
                _description = "";
            }
            else
            {
                _status = $"送信失敗: {result.Error}";
            }
        }

        /// <summary>
        /// IMGUI のボタン無効化スコープ。<c>GUI.enabled</c> を一時的に切り替える。
        /// （EditorGUI.DisabledScope のランタイム代替）
        /// </summary>
        readonly struct GuiDisabledScope : IDisposable
        {
            readonly bool _prev;

            public GuiDisabledScope(bool disabled)
            {
                _prev = GUI.enabled;
                GUI.enabled = _prev && !disabled;
            }

            public void Dispose()
            {
                GUI.enabled = _prev;
            }
        }
    }
}
