using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public class LianliankanLevelEditorWindow : EditorWindow
    {
        private const string Folder = "Assets/Content/Lianliankan/Levels";
        private const float Cell = 36f;

        private LianliankanLevelData _level;
        private LianliankanLayoutKind _template = LianliankanLayoutKind.Ring32;
        private LianliankanCellKind _brush = LianliankanCellKind.Tile;
        private Vector2 _scroll;
        private int _paintButton = -1;
        private Sprite[] _icons;

        [MenuItem("SmallGames/对对消/关卡编辑器", false, 0)]
        public static void Open()
        {
            var window = GetWindow<LianliankanLevelEditorWindow>("对对消关卡编辑器");
            window.minSize = new Vector2(760f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_level == null)
                TrySelectFirst();
        }

        private void OnGUI()
        {
            if (_icons == null)
                _icons = LianliankanSprites.LoadAll().Icons;

            DrawToolbar();
            if (_level == null)
            {
                EditorGUILayout.HelpBox("还没有关卡。点「新建关卡」或先执行 SmallGames/对对消/生成示例关卡。", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawSettings();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_level, "Edit Lianliankan Level");
                _level.EnsureInitialized();
                EditorUtility.SetDirty(_level);
            }

            EditorGUILayout.Space(8);
            DrawTemplates();
            EditorGUILayout.Space(6);
            DrawBrush();
            EditorGUILayout.Space(6);
            DrawStats();
            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawBoard();
            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    CycleLevel(-1);
            }

            var next = (LianliankanLevelData)EditorGUILayout.ObjectField(_level, typeof(LianliankanLevelData), false);
            if (next != _level)
                _level = next;

            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    CycleLevel(1);
            }

            if (GUILayout.Button("新建关卡", EditorStyles.toolbarButton, GUILayout.Width(80)))
                CreateLevel();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(56)))
                Save();
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("试玩此关", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    PlayCurrent();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettings()
        {
            _level.EnsureInitialized();
            EditorGUILayout.LabelField("关卡设置", EditorStyles.boldLabel);
            _level.LevelId = EditorGUILayout.IntField("关卡编号", _level.LevelId);
            _level.Title = EditorGUILayout.TextField("关卡名", _level.Title);
            _level.TypeCount = EditorGUILayout.IntSlider("图案种类", _level.TypeCount, LianliankanLevelData.MinTypes, LianliankanLevelData.MaxTypes);
            _level.TimeLimit = EditorGUILayout.Slider("限时（秒）", _level.TimeLimit, 30f, 300f);
        }

        private void DrawTemplates()
        {
            EditorGUILayout.LabelField("棋盘形状", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前", LianliankanBoardLayout.DisplayName(_level.Layout), GUILayout.Width(160f));
            _template = (LianliankanLayoutKind)EditorGUILayout.EnumPopup(_template);
            if (GUILayout.Button("套用模板", GUILayout.Width(80)))
                ApplyTemplate(_template);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            TemplateButton(LianliankanLayoutKind.Ring32);
            TemplateButton(LianliankanLayoutKind.Center36);
            TemplateButton(LianliankanLayoutKind.Wide48);
            TemplateButton(LianliankanLayoutKind.Full64);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            TemplateButton(LianliankanLayoutKind.Twin40);
            TemplateButton(LianliankanLayoutKind.Hollow48);
            TemplateButton(LianliankanLayoutKind.Columns42);
            TemplateButton(LianliankanLayoutKind.UShape44);
            EditorGUILayout.EndHorizontal();
        }

        private void TemplateButton(LianliankanLayoutKind kind)
        {
            var label = LianliankanBoardLayout.DisplayName(kind);
            if (GUILayout.Button(_level.Layout == kind ? $"[{label}]" : label, GUILayout.Height(24)))
            {
                _template = kind;
                ApplyTemplate(kind);
            }
        }

        private void ApplyTemplate(LianliankanLayoutKind kind)
        {
            if (kind == LianliankanLayoutKind.Custom)
                return;
            Undo.RecordObject(_level, "Apply Lianliankan Template");
            _level.ApplyTemplate(kind);
            EditorUtility.SetDirty(_level);
            GUI.FocusControl(null);
        }

        private void DrawStats()
        {
            var occupied = _level.CountOccupied();
            var blockers = _level.CountBlockers();
            var even = occupied % 2 == 0;
            var text = $"占格 {occupied}　{(even ? "偶数，可配对" : "奇数，开局会丢掉 1 格")}　障碍 {blockers}　种类 {_level.TypeCount}　限时 {_level.TimeLimit:0} 秒";
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }

        private void DrawBrush()
        {
            EditorGUILayout.LabelField("画笔（左键绘制，右键挖空，拖动连涂）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            BrushButton("占格", LianliankanCellKind.Tile);
            BrushButton("障碍", LianliankanCellKind.Blocker);
            BrushButton("挖空", LianliankanCellKind.Empty);
            EditorGUILayout.EndHorizontal();
        }

        private void BrushButton(string label, LianliankanCellKind kind)
        {
            var selected = _brush == kind;
            var old = GUI.backgroundColor;
            if (kind == LianliankanCellKind.Blocker)
                GUI.backgroundColor = new Color(0.82f, 0.62f, 0.38f);
            else if (kind == LianliankanCellKind.Empty)
                GUI.backgroundColor = new Color(0.25f, 0.28f, 0.32f);
            else
                GUI.backgroundColor = new Color(0.78f, 0.86f, 0.62f);
            var style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(selected ? $"[{label}]" : label, style, GUILayout.Height(24)))
                _brush = kind;
            GUI.backgroundColor = old;
        }

        private void DrawBoard()
        {
            var boardW = LianliankanLevelData.Cols * Cell + 8f;
            var boardH = LianliankanLevelData.Rows * Cell + 8f;
            var rect = GUILayoutUtility.GetRect(boardW, boardH, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.18f));

            var e = Event.current;
            if (e.type == EventType.MouseUp)
                _paintButton = -1;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && (e.button == 0 || e.button == 1))
                _paintButton = e.button;

            for (var row = 0; row < LianliankanLevelData.Rows; row++)
            {
                for (var col = 0; col < LianliankanLevelData.Cols; col++)
                {
                    var cell = new Rect(rect.x + 4f + col * Cell, rect.y + 4f + row * Cell, Cell - 3f, Cell - 3f);
                    var kind = _level.GetCell(row, col);
                    DrawCell(cell, kind, (row + col) % Mathf.Max(1, _level.TypeCount));

                    if ((_paintButton == 0 || _paintButton == 1) && cell.Contains(e.mousePosition))
                    {
                        var next = _paintButton == 1 ? LianliankanCellKind.Empty : _brush;
                        if (kind != next)
                        {
                            Undo.RecordObject(_level, "Paint Lianliankan Cell");
                            _level.SetCell(row, col, next);
                            EditorUtility.SetDirty(_level);
                            Repaint();
                        }
                    }
                }
            }
        }

        private void DrawCell(Rect cell, LianliankanCellKind kind, int iconId)
        {
            if (kind == LianliankanCellKind.Empty)
            {
                EditorGUI.DrawRect(cell, UIStyle.SlotEmpty);
                return;
            }

            if (kind == LianliankanCellKind.Blocker)
            {
                EditorGUI.DrawRect(cell, new Color(0.62f, 0.48f, 0.28f));
                var inner = new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, cell.height - 8f);
                var blocker = IconOf(LianliankanLevelData.BlockerIconIndex);
                if (blocker != null)
                    DrawSprite(inner, blocker, Color.white);
                return;
            }

            EditorGUI.DrawRect(cell, new Color(0.78f, 0.86f, 0.62f));
            var tileInner = new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, cell.height - 8f);
            var sprite = IconOf(LianliankanLevelData.FirstPlayableIconIndex + iconId);
            if (sprite != null)
                DrawSprite(tileInner, sprite, Color.white);
        }

        private Sprite IconOf(int iconId)
        {
            if (_icons == null || _icons.Length == 0)
                return null;
            return _icons[Mathf.Abs(iconId) % _icons.Length];
        }

        private static void DrawSprite(Rect rect, Sprite sprite, Color color)
        {
            if (sprite == null)
                return;
            var tex = sprite.texture;
            if (tex == null)
                return;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
            GUI.color = prev;
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清空"))
            {
                Undo.RecordObject(_level, "Clear Lianliankan Board");
                _level.ClearAll();
                EditorUtility.SetDirty(_level);
            }

            if (GUILayout.Button("满盘 8×8"))
                ApplyTemplate(LianliankanLayoutKind.Full64);

            if (GUILayout.Button("校验"))
                EditorUtility.DisplayDialog("校验", ValidateMessage(_level, IconCap()), "确定");
            if (GUILayout.Button("平衡表", GUILayout.Width(72)))
                GameBalanceBuilder.PingOrCreate();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "占格必须是偶数，图案才会两两配对。障碍用图标 1，不可消除，连线不能穿过。\n" +
                "模板只改占格形状，不会清掉你后刷的障碍。试玩会按当前资源进游戏。",
                MessageType.None);
        }

        private void CreateLevel()
        {
            Directory.CreateDirectory(Folder);
            var id = NextLevelId();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Level_{id:00}.asset");
            var asset = LianliankanLevelData.CreateDefault();
            asset.LevelId = id;
            asset.Title = $"第{id}关";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _level = asset;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void Save()
        {
            if (_level == null)
                return;
            _level.EnsureInitialized();
            EditorUtility.SetDirty(_level);
            AssetDatabase.SaveAssets();
        }

        private void PlayCurrent()
        {
            if (_level == null)
                return;
            Save();
            EditorPrefs.SetString(LianliankanLevelCatalog.EditorPlayKey, AssetDatabase.GetAssetPath(_level));
            if (EditorApplication.isPlaying)
            {
                LianliankanLevelCatalog.SetOverride(_level);
                if (GameManager.Instance != null)
                    GameManager.Instance.StartGame(GameModeType.Lianliankan);
                return;
            }

            EditorApplication.isPlaying = true;
        }

        private void CycleLevel(int delta)
        {
            var all = LoadLevels();
            if (all.Count == 0)
                return;
            var index = 0;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == _level)
                    index = i;
            }

            _level = all[(index + delta + all.Count) % all.Count];
        }

        private static List<LianliankanLevelData> LoadLevels()
        {
            var list = new List<LianliankanLevelData>();
            var guids = AssetDatabase.FindAssets("t:LianliankanLevelData");
            for (var i = 0; i < (guids?.Length ?? 0); i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LianliankanLevelData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (level != null)
                    list.Add(level);
            }

            list.Sort((a, b) =>
            {
                var id = a.LevelId.CompareTo(b.LevelId);
                return id != 0 ? id : string.CompareOrdinal(a.name, b.name);
            });
            return list;
        }

        private void TrySelectFirst()
        {
            if (Selection.activeObject is LianliankanLevelData selected)
            {
                _level = selected;
                return;
            }

            var all = LoadLevels();
            if (all.Count > 0)
                _level = all[0];
        }

        private static int NextLevelId()
        {
            var max = 0;
            var all = LoadLevels();
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].LevelId > max)
                    max = all[i].LevelId;
            }

            return max + 1;
        }

        private int IconCap()
        {
            var total = _icons != null && _icons.Length > 0 ? _icons.Length : LianliankanLevelData.MaxTypes + 1;
            return LianliankanLevelData.PlayableIconCap(total);
        }

        public static string ValidateMessage(LianliankanLevelData level, int iconCap)
        {
            if (level == null)
                return "没有关卡。";
            level.EnsureInitialized();
            var occupied = level.CountOccupied();
            var blockers = level.CountBlockers();
            if (occupied < 2)
                return $"占格太少（{occupied}），至少要 2 格。";
            if (occupied % 2 != 0)
                return $"占格必须是偶数（当前 {occupied}），开局会丢掉最后一张。";
            if (iconCap > 0 && level.TypeCount > iconCap)
                return $"图案种类 {level.TypeCount} 超过可用图标数 {iconCap}（图标 1 留给障碍）。";
            return $"通过。{LianliankanLevelData.Cols}×{LianliankanLevelData.Rows}，占格 {occupied}，障碍 {blockers}，种类 {level.TypeCount}，限时 {level.TimeLimit:0} 秒。";
        }
    }

    [CustomEditor(typeof(LianliankanLevelData))]
    public class LianliankanLevelDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("打开对对消关卡编辑器", GUILayout.Height(32)))
                LianliankanLevelEditorWindow.Open();
        }
    }
}
