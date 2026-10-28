using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public class SheepLevelEditorWindow : EditorWindow
    {
        private const string Folder = "Assets/Content/Sheep/Levels";
        private const float GridCell = 14f;
        private const float StackCell = 22f;

        private SheepLevelData _level;
        private SheepBoardPlan _plan;
        private int _seed = 1;
        private Vector2 _scroll;
        private Sprite[] _icons;

        [MenuItem("SmallGames/叠叠消/关卡编辑器", false, 1)]
        public static void Open()
        {
            var window = GetWindow<SheepLevelEditorWindow>("叠叠消关卡编辑器");
            window.minSize = new Vector2(820f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_level == null)
                TrySelectFirst();
            RebuildPreview();
        }

        private void OnGUI()
        {
            if (_icons == null)
                _icons = LianliankanSprites.LoadAll().Icons;

            DrawToolbar();
            if (_level == null)
            {
                EditorGUILayout.HelpBox("还没有关卡。点「新建关卡」或先执行 SmallGames/叠叠消/生成示例关卡。", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawSettings();
            var changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                Undo.RecordObject(_level, "Edit Sheep Level");
                _level.EnsureInitialized();
                EditorUtility.SetDirty(_level);
                RebuildPreview();
            }

            EditorGUILayout.Space(8);
            DrawPreviewBar();
            EditorGUILayout.Space(6);
            DrawStats();
            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPreview();
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

            var next = (SheepLevelData)EditorGUILayout.ObjectField(_level, typeof(SheepLevelData), false);
            if (next != _level)
            {
                _level = next;
                RebuildPreview();
            }

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
            DrawMotifPopup();
            _level.TypeNum = EditorGUILayout.IntSlider("图案种类", _level.TypeNum, SheepLevelData.MinTypes, SheepLevelData.MaxTypes);
            _level.LevelNum = EditorGUILayout.IntSlider("层数", _level.LevelNum, SheepLevelData.MinLayers, SheepLevelData.MaxLayers);
            _level.LevelBlockNum = EditorGUILayout.IntSlider("每层块数", _level.LevelBlockNum, 1, SheepLevelData.MaxBlocks);
            _level.BorderStep = EditorGUILayout.IntSlider("边缘加层", _level.BorderStep, 0, 4);
            EditorGUILayout.BeginHorizontal();
            _level.LeftStack = EditorGUILayout.IntSlider("左侧堆", _level.LeftStack, 0, SheepLevelData.MaxStack);
            _level.RightStack = EditorGUILayout.IntSlider("右侧堆", _level.RightStack, 0, SheepLevelData.MaxStack);
            EditorGUILayout.EndHorizontal();
            _level.ComposeNum = EditorGUILayout.IntSlider("消除个数", _level.ComposeNum, 2, 5);
        }

        private void DrawMotifPopup()
        {
            var kinds = (SheepMotifKind[])System.Enum.GetValues(typeof(SheepMotifKind));
            var labels = new string[kinds.Length];
            var values = new int[kinds.Length];
            for (var i = 0; i < kinds.Length; i++)
            {
                labels[i] = SheepMotifs.DisplayName(kinds[i]);
                values[i] = (int)kinds[i];
            }

            _level.Motif = (SheepMotifKind)EditorGUILayout.IntPopup("外形", (int)_level.Motif, labels, values);
        }

        private void DrawPreviewBar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("预览种子", GUILayout.Width(64f));
            var seed = EditorGUILayout.IntField(_seed, GUILayout.Width(80f));
            if (seed != _seed)
            {
                _seed = seed;
                RebuildPreview();
            }

            if (GUILayout.Button("换一局预览", GUILayout.Width(96f)))
            {
                _seed++;
                RebuildPreview();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            if (_plan == null)
                RebuildPreview();
            var min = _level.PlannedMinCount;
            var total = _plan != null ? _plan.Total : _level.ResolvedTotal(IconCap());
            var pad = Mathf.Max(0, total - min);
            var board = _plan != null ? _plan.Cells.Length : 0;
            var left = _plan != null ? _plan.LeftIcons.Length : _level.LeftStack;
            var right = _plan != null ? _plan.RightIcons.Length : _level.RightStack;
            var motif = SheepMotifs.DisplayName(SheepMotifs.Resolve(_level));
            EditorGUILayout.LabelField(
                $"外形 {motif}　最少 {min}　实际上牌 {total}（补齐 {pad}）　棋盘 {board}　左堆 {left}　右堆 {right}　种类 {_plan?.TypeNum ?? _level.TypeNum}",
                EditorStyles.miniLabel);
        }

        private void DrawPreview()
        {
            if (_plan == null)
                RebuildPreview();
            if (_plan == null)
                return;

            var grid = SheepBoardBuilder.GridSize * GridCell;
            var stackW = StackCell + 12f;
            var width = stackW + grid + stackW + 16f;
            var height = grid + 16f;
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.18f));

            var board = new Rect(rect.x + stackW + 8f, rect.y + 8f, grid, grid);
            EditorGUI.DrawRect(board, new Color(0.18f, 0.22f, 0.18f));

            DrawStack(new Rect(rect.x + 8f, rect.y + 8f, StackCell, grid), _plan.LeftIcons);
            DrawStack(new Rect(board.xMax + 8f, rect.y + 8f, StackCell, grid), _plan.RightIcons);

            var span = SheepBoardBuilder.TileSpan * GridCell - 2f;
            var cells = _plan.Cells;
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var tile = new Rect(
                    board.x + cell.GridX * GridCell,
                    board.yMax - (cell.GridY + SheepBoardBuilder.TileSpan) * GridCell,
                    span,
                    span);
                var t = _level.LevelNum <= 1 ? 0f : cell.Layer / (float)(_level.LevelNum - 1);
                var tint = Color.Lerp(new Color(0.72f, 0.88f, 0.52f), new Color(0.28f, 0.46f, 0.24f), t);
                EditorGUI.DrawRect(tile, tint);
                var sprite = IconOf(cell.IconId);
                if (sprite != null)
                {
                    var inner = new Rect(tile.x + 3f, tile.y + 3f, tile.width - 6f, tile.height - 6f);
                    DrawSprite(inner, sprite, Color.white);
                }
            }
        }

        private void DrawStack(Rect rect, int[] icons)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.18f, 0.16f));
            if (icons == null)
                return;
            for (var i = 0; i < icons.Length; i++)
            {
                var tile = new Rect(rect.x + 2f, rect.y + 4f + i * (StackCell - 4f), rect.width - 4f, StackCell - 6f);
                EditorGUI.DrawRect(tile, new Color(0.55f, 0.72f, 0.42f));
                var sprite = IconOf(icons[i]);
                if (sprite != null)
                    DrawSprite(tile, sprite, Color.white);
            }
        }

        private Sprite IconOf(int iconId)
        {
            if (_icons == null || iconId < 0 || iconId >= _icons.Length)
                return null;
            return _icons[iconId];
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
            if (GUILayout.Button("按当前参数预览"))
                RebuildPreview();
            if (GUILayout.Button("校验"))
                EditorUtility.DisplayDialog("校验", ValidateMessage(_level, IconCap()), "确定");
            if (GUILayout.Button("平衡表", GUILayout.Width(72)))
                GameBalanceBuilder.PingOrCreate();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "棋盘左右对称，底层铺出外形，空出来的边缘会多叠几层，越往外越高。\n" +
                "「边缘加层」越大，轮廓上堆得越厚。开局仍会再随机一次，总数补齐到「种类 × 消除个数」的倍数。",
                MessageType.None);
        }

        private void RebuildPreview()
        {
            if (_level == null)
            {
                _plan = null;
                return;
            }

            _level.EnsureInitialized();
            _plan = SheepBoardBuilder.PlanPreview(_level, _seed, IconCap());
            Repaint();
        }

        private void CreateLevel()
        {
            Directory.CreateDirectory(Folder);
            var id = NextLevelId();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Level_{id:00}.asset");
            var asset = SheepLevelData.CreateDefault();
            asset.LevelId = id;
            asset.Title = $"第{id}关";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _level = asset;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            RebuildPreview();
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
            EditorPrefs.SetString(SheepLevelCatalog.EditorPlayKey, AssetDatabase.GetAssetPath(_level));
            if (EditorApplication.isPlaying)
            {
                SheepLevelCatalog.SetOverride(_level);
                if (GameManager.Instance != null)
                    GameManager.Instance.StartGame(GameModeType.Sheep);
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
            RebuildPreview();
        }

        private static List<SheepLevelData> LoadLevels()
        {
            var list = new List<SheepLevelData>();
            var guids = AssetDatabase.FindAssets("t:SheepLevelData");
            for (var i = 0; i < (guids?.Length ?? 0); i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<SheepLevelData>(AssetDatabase.GUIDToAssetPath(guids[i]));
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
            if (Selection.activeObject is SheepLevelData selected)
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
            return _icons != null && _icons.Length > 0 ? _icons.Length : SheepLevelData.MaxTypes;
        }

        public static string ValidateMessage(SheepLevelData level, int iconCap)
        {
            if (level == null)
                return "没有关卡。";
            level.EnsureInitialized();
            var types = level.TypeNum;
            if (iconCap > 0)
                types = Mathf.Min(types, iconCap);
            var unit = level.ComposeNum * Mathf.Max(1, types);
            var total = level.ResolvedTotal(iconCap);
            if (total < unit)
                return $"牌数太少（{total}），至少要 {unit} 张才能消完。";
            if (unit > 0 && total % unit != 0)
                return $"牌数 {total} 不能被 {unit}（种类×{level.ComposeNum}）整除。";
            if (iconCap > 0 && level.TypeNum > iconCap)
                return $"图案种类 {level.TypeNum} 超过图标数 {iconCap}。";
            return $"通过。层 {level.LevelNum}，每层 {level.LevelBlockNum}，侧堆 {level.LeftStack}+{level.RightStack}，实际上牌 {total}。";
        }
    }

    [CustomEditor(typeof(SheepLevelData))]
    public class SheepLevelDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("打开叠叠消关卡编辑器", GUILayout.Height(32)))
                SheepLevelEditorWindow.Open();
        }
    }
}
