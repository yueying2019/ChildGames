using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public class Match3LevelEditorWindow : EditorWindow
    {
        private const string Folder = "Assets/Content/Match3/Levels";
        private const float Cell = 36f;

        private enum PaintLayer
        {
            Tile = 0,
            Overlay = 1
        }

        private Match3LevelData _level;
        private PaintLayer _layer = PaintLayer.Tile;
        private Match3CellKind _brushKind = Match3CellKind.Playable;
        private int _brushColor;
        private int _brushLayers = 1;
        private Vector2 _scroll;
        private int _paintButton = -1;
        private GUIStyle _cellLabel;
        private GUIStyle _cornerLabel;
        private Sprite[] _icons;

        [MenuItem("SmallGames/三消/关卡编辑器", false, 2)]
        public static void Open()
        {
            var window = GetWindow<Match3LevelEditorWindow>("三消关卡编辑器");
            window.minSize = new Vector2(880f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_level == null)
                TrySelectFirst();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_icons == null)
                _icons = LianliankanSprites.LoadAll().Icons;
            DrawToolbar();
            if (_level == null)
            {
                EditorGUILayout.HelpBox("还没有关卡。点「新建关卡」或先执行 SmallGames/三消/生成示例关卡。", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawSettings();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_level, "Edit Match3 Level");
                _level.EnsureInitialized();
                EditorUtility.SetDirty(_level);
            }

            EditorGUILayout.Space(8);
            DrawBrush();
            EditorGUILayout.Space(6);
            DrawStats();
            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawBoard();
            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void EnsureStyles()
        {
            if (_cellLabel == null)
            {
                _cellLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11
                };
                _cellLabel.normal.textColor = Color.white;
            }

            if (_cornerLabel == null)
            {
                _cornerLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperRight,
                    fontSize = 9
                };
                _cornerLabel.normal.textColor = Color.white;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(_level == null))
            {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    CycleLevel(-1);
            }

            var next = (Match3LevelData)EditorGUILayout.ObjectField(_level, typeof(Match3LevelData), false);
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
            _level.DisplayName = EditorGUILayout.TextField("关卡名", _level.DisplayName);

            EditorGUILayout.BeginHorizontal();
            var width = EditorGUILayout.IntSlider("宽", _level.Width, Match3LevelData.MinSize, Match3LevelData.MaxSize);
            var height = EditorGUILayout.IntSlider("高", _level.Height, Match3LevelData.MinSize, Match3LevelData.MaxSize);
            EditorGUILayout.EndHorizontal();
            if (width != _level.Width || height != _level.Height)
            {
                Undo.RecordObject(_level, "Resize Match3 Board");
                _level.Resize(width, height);
                EditorUtility.SetDirty(_level);
            }

            _level.ColorCount = EditorGUILayout.IntSlider("颜色数", _level.ColorCount, 3, Match3LevelData.MaxColor);
            _level.MoveLimit = EditorGUILayout.IntField("步数", _level.MoveLimit);
            _level.GoalType = (Match3GoalType)EditorGUILayout.EnumPopup("过关条件", _level.GoalType);
            if (_level.GoalType == Match3GoalType.Score)
            {
                _level.TargetScore = EditorGUILayout.IntField("目标分数", _level.TargetScore);
            }
            else if (_level.GoalType == Match3GoalType.ClearCells)
            {
                EditorGUILayout.HelpBox($"清掉棋盘上全部障碍即过关，当前 {_level.CountOverlays()} 格。", MessageType.None);
            }
            else
            {
                if (!_level.HasCollectTarget())
                    EditorGUILayout.HelpBox("收集目标全是 0，本关无法过关。请填写数量或点「从棋盘同步」。", MessageType.Error);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("收集数量（0 表示不要求该图标）", EditorStyles.miniLabel);
                if (GUILayout.Button("从棋盘同步", GUILayout.Width(88)))
                    SyncCollect();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                for (var color = 1; color <= _level.ColorCount; color++)
                {
                    _level.CollectCounts[color] = EditorGUILayout.IntField(Match3Palette.Names[color], _level.CollectCounts[color], GUILayout.Width(108));
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBrush()
        {
            EditorGUILayout.LabelField("画笔（左键绘制，右键擦除当前层，拖动连涂）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            LayerButton("方块", PaintLayer.Tile);
            LayerButton("障碍", PaintLayer.Overlay);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (_layer == PaintLayer.Tile)
            {
                BrushButton("挖空", Match3CellKind.Hole, 0);
                BrushButton("随机色", Match3CellKind.Playable, 0);
                for (var color = 1; color <= _level.ColorCount; color++)
                    BrushButton(Match3Palette.Names[color], Match3CellKind.Playable, color);
            }
            else
            {
                BrushButton("无", Match3CellKind.Playable, _brushColor);
                BrushButton("冰层", Match3CellKind.Ice, _brushColor);
                BrushButton("石头", Match3CellKind.Stone, 0);
                BrushButton("果冻", Match3CellKind.Jelly, _brushColor);
                BrushButton("牢笼", Match3CellKind.Cage, _brushColor);
            }

            EditorGUILayout.EndHorizontal();
            if (_layer == PaintLayer.Overlay && Match3CellUtil.IsOverlay(_brushKind))
                _brushLayers = EditorGUILayout.IntSlider("层数", _brushLayers, 1, 3);
        }

        private void LayerButton(string label, PaintLayer layer)
        {
            var selected = _layer == layer;
            var style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(selected ? $"[{label}]" : label, style, GUILayout.Height(24)))
            {
                _layer = layer;
                if (layer == PaintLayer.Tile)
                    _brushKind = Match3CellKind.Playable;
                else if (!Match3CellUtil.IsOverlay(_brushKind) && _brushKind != Match3CellKind.Playable)
                    _brushKind = Match3CellKind.Ice;
            }
        }

        private void BrushButton(string label, Match3CellKind kind, int color)
        {
            var selected = _brushKind == kind && (_layer == PaintLayer.Overlay || _brushColor == color);
            if (_layer == PaintLayer.Overlay)
                selected = _brushKind == kind;
            var old = GUI.backgroundColor;
            if (kind == Match3CellKind.Hole)
                GUI.backgroundColor = new Color(0.25f, 0.28f, 0.32f);
            else if (Match3CellUtil.IsOverlay(kind))
                GUI.backgroundColor = Match3CellUtil.OverlayColor(kind);
            else
                GUI.backgroundColor = new Color(0.85f, 0.85f, 0.88f);
            var style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(selected ? $"[{label}]" : label, style, GUILayout.Height(24)))
            {
                _brushKind = kind;
                if (_layer == PaintLayer.Tile)
                    _brushColor = color;
            }

            GUI.backgroundColor = old;
        }

        private void DrawStats()
        {
            var ice = _level.CountKind(Match3CellKind.Ice);
            var stone = _level.CountKind(Match3CellKind.Stone);
            var jelly = _level.CountKind(Match3CellKind.Jelly);
            var cage = _level.CountKind(Match3CellKind.Cage);
            var text = $"可用 {_level.CountPlayable()}　冰 {ice}　石 {stone}　冻 {jelly}　锁 {cage}";
            var colors = string.Empty;
            for (var color = 1; color <= _level.ColorCount; color++)
            {
                var n = _level.CountFixedColor(color);
                if (n > 0)
                    colors += $"　{Match3Palette.Names[color]}{n}";
            }

            EditorGUILayout.LabelField(colors.Length > 0 ? text + "　固定" + colors : text + "　固定图标 0", EditorStyles.miniLabel);
        }

        private void DrawBoard()
        {
            var boardW = _level.Width * Cell + 8f;
            var boardH = _level.Height * Cell + 8f;
            var rect = GUILayoutUtility.GetRect(boardW, boardH, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.18f));

            var e = Event.current;
            if (e.type == EventType.MouseUp)
                _paintButton = -1;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && (e.button == 0 || e.button == 1))
                _paintButton = e.button;

            for (var y = 0; y < _level.Height; y++)
            {
                for (var x = 0; x < _level.Width; x++)
                {
                    var cell = new Rect(rect.x + 4f + x * Cell, rect.y + 4f + y * Cell, Cell - 3f, Cell - 3f);
                    var spec = _level.GetCell(x, y);
                    DrawCell(cell, spec);

                    if ((_paintButton == 0 || _paintButton == 1) && cell.Contains(e.mousePosition))
                    {
                        var next = NextSpec(spec, _paintButton == 1);
                        if (!spec.Equals(next))
                        {
                            Undo.RecordObject(_level, "Paint Match3 Cell");
                            _level.SetCell(x, y, next);
                            EditorUtility.SetDirty(_level);
                            Repaint();
                        }
                    }
                }
            }
        }

        private Match3CellSpec NextSpec(Match3CellSpec spec, bool erase)
        {
            if (erase)
            {
                if (_layer == PaintLayer.Tile)
                    return Match3CellSpec.Empty;
                return spec.ClearOverlay();
            }

            if (_layer == PaintLayer.Tile)
            {
                if (_brushKind == Match3CellKind.Hole)
                    return Match3CellSpec.Empty;
                return spec.PaintTile(_brushColor);
            }

            return spec.PaintOverlay(_brushKind, _brushLayers);
        }

        private void DrawCell(Rect cell, Match3CellSpec spec)
        {
            EditorGUI.DrawRect(cell, UIStyle.SlotEmpty);
            if (spec.Kind == Match3CellKind.Hole)
                return;

            var inner = new Rect(cell.x + 3f, cell.y + 3f, cell.width - 6f, cell.height - 6f);
            if (spec.Kind == Match3CellKind.Stone)
            {
                DrawSprite(inner, Match3Icons.OverlayOf(_icons, Match3CellKind.Stone), new Color(1f, 1f, 1f, Match3Icons.OverlayAlpha(spec.ResolvedLayers)));
                return;
            }

            var tile = Match3Icons.TileOf(_icons, spec.Color);
            if (tile != null)
                DrawSprite(inner, tile, Color.white);
            else
                GUI.Label(inner, "R", _cellLabel);

            if (Match3CellUtil.IsOverlay(spec.Kind))
            {
                var overlay = Match3Icons.OverlayOf(_icons, spec.Kind);
                DrawSprite(inner, overlay, new Color(1f, 1f, 1f, Match3Icons.OverlayAlpha(spec.ResolvedLayers)));
            }
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
            if (GUILayout.Button("随机图标（保留障碍）"))
            {
                Undo.RecordObject(_level, "Fill Match3 Colors");
                _level.FillColorsKeepOverlays(0);
                EditorUtility.SetDirty(_level);
            }

            if (GUILayout.Button("清除障碍"))
            {
                Undo.RecordObject(_level, "Clear Match3 Overlays");
                _level.ClearOverlaysKeepTiles();
                EditorUtility.SetDirty(_level);
            }

            if (GUILayout.Button("清空为挖空"))
            {
                Undo.RecordObject(_level, "Clear Match3 Board");
                _level.EnsureInitialized();
                for (var i = 0; i < _level.Cells.Length; i++)
                    _level.Cells[i] = Match3CellSpec.Empty;
                EditorUtility.SetDirty(_level);
            }

            if (GUILayout.Button("校验"))
                EditorUtility.DisplayDialog("校验", ValidateMessage(_level), "确定");
            if (GUILayout.Button("平衡表", GUILayout.Width(72)))
                GameBalanceBuilder.PingOrCreate();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "方块层改图标/挖空，障碍层改冰石冻锁，两层互不影响。R = 开局随机图标。\n" +
                "障碍图标半透明盖在棋子上，最多 3 层，层数越多越不透明。\n" +
                "收集模式可用「从棋盘同步」按固定图标数量填写。4 连出直线，L/T 出炸弹，5 连出色球。",
                MessageType.None);
        }

        private void SyncCollect()
        {
            Undo.RecordObject(_level, "Sync Match3 Collect");
            if (!_level.SyncCollectFromBoard())
            {
                EditorUtility.DisplayDialog("从棋盘同步", "棋盘上没有固定图标。先在方块层刷甜甜圈/曲奇等，随机不会算进收集。", "确定");
                return;
            }

            EditorUtility.SetDirty(_level);
        }

        private void CreateLevel()
        {
            Directory.CreateDirectory(Folder);
            var id = NextLevelId();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Level_{id:00}.asset");
            var asset = ScriptableObject.CreateInstance<Match3LevelData>();
            asset.LevelId = id;
            asset.DisplayName = $"第{id}关";
            asset.FillPlayable();
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
            EditorPrefs.SetString(Match3LevelCatalog.EditorPlayKey, AssetDatabase.GetAssetPath(_level));
            if (EditorApplication.isPlaying)
            {
                Match3LevelCatalog.SetOverride(_level);
                if (GameManager.Instance != null)
                    GameManager.Instance.StartGame(GameModeType.Match3);
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

        private static List<Match3LevelData> LoadLevels()
        {
            var list = new List<Match3LevelData>();
            var guids = AssetDatabase.FindAssets("t:Match3LevelData");
            for (var i = 0; i < (guids?.Length ?? 0); i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<Match3LevelData>(AssetDatabase.GUIDToAssetPath(guids[i]));
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
            if (Selection.activeObject is Match3LevelData selected)
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

        public static string ValidateMessage(Match3LevelData level)
        {
            if (level == null)
                return "没有关卡。";
            level.EnsureInitialized();
            var playable = level.CountPlayable();
            if (playable < 8)
                return $"可用格太少（{playable}），至少需要 8 格。";
            if (level.GoalType == Match3GoalType.ClearCells)
            {
                var overlays = level.CountOverlays();
                if (overlays == 0)
                    return "清障碍模式至少要放一格冰层/石头/果冻/牢笼。";
            }

            if (level.GoalType == Match3GoalType.Collect && !level.HasCollectTarget())
                return "收集模式至少要给一种图标填写数量。可用「从棋盘同步」。";

            return $"通过。棋盘 {level.Width}×{level.Height}，可用格 {playable}，障碍 {level.CountOverlays()}，步数 {level.MoveLimit}。";
        }
    }

    [CustomEditor(typeof(Match3LevelData))]
    public class Match3LevelDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("打开三消关卡编辑器", GUILayout.Height(32)))
                Match3LevelEditorWindow.Open();
        }
    }
}
