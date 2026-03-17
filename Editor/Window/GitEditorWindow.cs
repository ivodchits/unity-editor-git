using System;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class GitEditorWindow : EditorWindow
    {
        // Section drawers
        PullPushSectionDrawer _pullPush;
        CommitSectionDrawer _commit;
        StagingSectionDrawer _staging;
        HistorySectionDrawer _history;
        BranchesSectionDrawer _branches;
        InfoSectionDrawer _info;
        ConsoleSectionDrawer _console;

        // Layout state — horizontal split
        float _splitRatio;
        bool _showHistory = true;
        Vector2 _rightScrollPos;
        string _currentBranch = "";
        bool _initialized;

        // Vertical section heights (scroll view portion of each section)
        float _stagingHeight;
        float _historyHeight;
        float _consoleHeight;

        // Cached pixel bottom of fixed sections (pullpush + commit + separators), measured each Repaint
        float _fixedSectionBottom = 140f;

        // Splitter control IDs
        static readonly int HorizSplitterId    = "HorizSplitter".GetHashCode();
        static readonly int StagingHistorySplitterId = "StagingHistorySplit".GetHashCode();
        static readonly int HistoryConsoleSplitterId  = "HistoryConsoleSplit".GetHashCode();

        [MenuItem("Window/Git")]
        public static void ShowWindow()
        {
            var window = GetWindow<GitEditorWindow>("Git");
            window.minSize = new Vector2(600, 400);
        }

        void OnEnable()
        {
            GitStyles.ResetStyles();
            _initialized = false;
            Initialize();
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            _console?.Dispose();
        }

        void Initialize()
        {
            if (_initialized) return;

            _splitRatio    = GitSettings.SplitRatio;
            _stagingHeight = GitSettings.StagingHeight;
            _historyHeight = GitSettings.HistoryHeight;
            _consoleHeight = GitSettings.ConsoleHeight;

            _pullPush  = new PullPushSectionDrawer();
            _commit    = new CommitSectionDrawer();
            _staging   = new StagingSectionDrawer();
            _history   = new HistorySectionDrawer();
            _branches  = new BranchesSectionDrawer();
            _info      = new InfoSectionDrawer();
            _console   = new ConsoleSectionDrawer();

            _pullPush.OnCompleted  = RefreshAll;
            _commit.OnCommitted    = RefreshAll;
            _staging.OnChanged     = () =>
            {
                var sel = _staging.SelectedChange;
                if (sel != null) _info.ShowWorkingChange(sel);
                Repaint();
            };
            _history.OnCommitSelected = commit => { _info.ShowCommit(commit); Repaint(); };
            _history.OnRefreshNeeded  = RefreshAll;
            _branches.OnChanged       = RefreshAll;

            _console.Initialize();
            _initialized = true;

            RefreshAll();
        }

        void OnUpdate()
        {
            if (_pullPush != null && _pullPush.IsRunning)
            {
                _pullPush.Update();
                Repaint();
            }
        }

        void RefreshAll()
        {
            _currentBranch = GitBranchService.GetCurrentBranch();
            _staging.Refresh();
            _history.Refresh();
            _branches.Refresh();
            Repaint();
        }

        void OnGUI()
        {
            if (!_initialized || _pullPush == null)
                Initialize();

            try
            {
                DrawToolbar();

                EditorGUILayout.BeginHorizontal();

                // Left pane — no outer scroll; each section manages its own scroll view
                float leftWidth = position.width * _splitRatio;
                EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth));
                DrawLeftPane();
                EditorGUILayout.EndVertical();

                // Horizontal splitter
                DrawHorizSplitter();

                // Right pane
                EditorGUILayout.BeginVertical();
                _rightScrollPos = EditorGUILayout.BeginScrollView(_rightScrollPos);
                _info.Draw();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                GitStyles.ResetStyles();
                GUIUtility.ExitGUI();
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(GitIcons.Refresh, EditorStyles.toolbarButton, GUILayout.Width(28)))
                RefreshAll();

            GUILayout.Space(8);
            EditorGUILayout.LabelField($"Branch: {_currentBranch}", EditorStyles.toolbarButton, GUILayout.Width(200));
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Repo:", GUILayout.Width(35));
            string root = GitCommandRunner.RepoRoot;
            EditorGUILayout.SelectableLabel(root, EditorStyles.toolbarTextField, GUILayout.MinWidth(150), GUILayout.Height(18));

            if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Git Repository", root, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    GitCommandRunner.RepoRoot = selected.Replace('/', '\\');
                    GitSettings.RepoRoot = GitCommandRunner.RepoRoot;
                    RefreshAll();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawLeftPane()
        {
            // Fixed-height sections at the top
            _pullPush.Draw();
            DrawSeparator();
            _commit.Draw();
            DrawSeparator();

            // Measure the bottom of fixed sections during Repaint so we know
            // exactly how much vertical space is left for the resizable sections.
            if (Event.current.type == EventType.Repaint)
                _fixedSectionBottom = GUILayoutUtility.GetLastRect().yMax;

            // Available height for staging + history/branches + console
            // (subtract toggle row height and two splitter heights)
            const float toggleRowHeight = 20f;
            const float splittersHeight = 10f; // 2 × 5 px
            const float bottomPadding   = 72f; // extra breathing room so nothing clips at window edge
            float available = Mathf.Max(180f,
                position.height - _fixedSectionBottom - toggleRowHeight - splittersHeight - bottomPadding);

            float total = _stagingHeight + _historyHeight + _consoleHeight;
            float scale = total > available ? available / total : 1f;

            // ── Staging area ─────────────────────────────────────
            _staging.Draw(_stagingHeight * scale);

            // Splitter: Staging / History
            DrawVerticalSplitter(StagingHistorySplitterId, ref _stagingHeight, ref _historyHeight,
                onDragEnd: () => { GitSettings.StagingHeight = _stagingHeight; GitSettings.HistoryHeight = _historyHeight; });

            // ── History / Branches ────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_showHistory,  "History",  EditorStyles.toolbarButton)) _showHistory = true;
            if (GUILayout.Toggle(!_showHistory, "Branches", EditorStyles.toolbarButton)) _showHistory = false;
            EditorGUILayout.EndHorizontal();

            if (_showHistory) _history.Draw(_historyHeight * scale);
            else              _branches.Draw(_historyHeight * scale);

            // Splitter: History / Console
            DrawVerticalSplitter(HistoryConsoleSplitterId, ref _historyHeight, ref _consoleHeight,
                onDragEnd: () => { GitSettings.HistoryHeight = _historyHeight; GitSettings.ConsoleHeight = _consoleHeight; });

            // ── Console ───────────────────────────────────────────
            _console.Draw(_consoleHeight * scale);
        }

        // ── Separator (thin line between fixed sections) ──────────────────────────
        void DrawSeparator()
        {
            GUILayout.Space(2);
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(2);
        }

        // ── Vertical drag splitter (between two stacked sections) ─────────────────
        void DrawVerticalSplitter(int controlHash, ref float above, ref float below, Action onDragEnd = null)
        {
            const float minHeight = 40f;

            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(5), GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));

            int id = GUIUtility.GetControlID(controlHash, FocusType.Passive, rect);

            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        Event.current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    above = Mathf.Max(minHeight, above + Event.current.delta.y);
                    below = Mathf.Max(minHeight, below - Event.current.delta.y);
                    Repaint();
                    Event.current.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    onDragEnd?.Invoke();
                    Event.current.Use();
                    break;
            }
        }

        // ── Horizontal drag splitter (left/right panes) ───────────────────────────
        void DrawHorizSplitter()
        {
            var rect = GUILayoutUtility.GetRect(0, 0, GUILayout.Width(4), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            int id = GUIUtility.GetControlID(HorizSplitterId, FocusType.Passive, rect);

            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        Event.current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    _splitRatio = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.3f, 0.7f);
                    Repaint();
                    Event.current.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    GitSettings.SplitRatio = _splitRatio;
                    Event.current.Use();
                    break;
            }
        }
    }
}
