using System.Collections.Generic;
using UnityEngine;

namespace SmallGames
{
    public struct SheepBoardCell
    {
        public int GridX;
        public int GridY;
        public int Layer;
        public int IconId;
    }

    public sealed class SheepBoardPlan
    {
        public int[] LeftIcons = new int[0];
        public int[] RightIcons = new int[0];
        public SheepBoardCell[] Cells = new SheepBoardCell[0];
        public int Total;
        public int TypeNum;
    }

    /// <summary>
    /// 按关卡参数生成叠叠消棋盘：外形底层 + 边缘多层叠加，单格只实例化已有预制体。
    /// </summary>
    public static class SheepBoardBuilder
    {
        public const int GridSize = 24;
        public const int TileSpan = 3;

        public static int Build(SheepGameView view, SheepLevelData config)
        {
            if (view == null || config == null)
                return 0;

            var prefab = view.TilePrefab;
            if (prefab == null)
                prefab = AssetHub.Load<SheepTileView>("Sheep/SheepTile");
            if (prefab == null || view.BoardRoot == null)
                return 0;

            var icons = view.IconSprites;
            var iconCap = icons != null ? icons.Length : 0;
            var plan = Plan(config, iconCap);
            SpawnStacks(view, view.LeftStack, prefab, icons, plan.LeftIcons);
            SpawnStacks(view, view.RightStack, prefab, icons, plan.RightIcons);

            var board = view.BoardRoot;
            Canvas.ForceUpdateCanvases();
            var width = Mathf.Max(240f, board.rect.width);
            var height = Mathf.Max(240f, board.rect.height);
            for (var i = 0; i < plan.Cells.Length; i++)
            {
                var cell = plan.Cells[i];
                var tile = Spawn(view, board, prefab, icons, cell.IconId, cell.Layer);
                var rect = tile != null ? tile.transform as RectTransform : null;
                if (rect == null)
                    continue;

                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width / GridSize * 2.7f, height / GridSize * 2.9f);
                var gx = cell.GridX + 1.5f;
                var gy = cell.GridY + 1.5f;
                if (cell.Layer > 0)
                {
                    var dx = gx - GridSize * 0.5f;
                    var dy = gy - GridSize * 0.5f;
                    var len = Mathf.Max(0.001f, Mathf.Sqrt(dx * dx + dy * dy));
                    var push = cell.Layer * 0.48f;
                    gx += dx / len * push;
                    gy += dy / len * push;
                }

                rect.anchoredPosition = new Vector2(
                    (gx / GridSize - 0.5f) * width,
                    (gy / GridSize - 0.5f) * height);
                rect.SetAsLastSibling();
            }

            return plan.Total;
        }

        /// <summary>按 FixedCells 直接出 plan，不做任何随机与补齐。</summary>
        private static SheepBoardPlan PlanFromFixed(SheepLevelData config)
        {
            var source = config.FixedCells;
            var plan = new SheepBoardPlan
            {
                Cells = new SheepBoardCell[source.Length],
                LeftIcons = new int[0],
                RightIcons = new int[0],
                Total = source.Length,
                TypeNum = Mathf.Max(1, config.TypeNum)
            };

            for (var i = 0; i < source.Length; i++)
            {
                plan.Cells[i] = new SheepBoardCell
                {
                    GridX = source[i].GridX,
                    GridY = source[i].GridY,
                    Layer = source[i].Layer,
                    IconId = source[i].IconId
                };
            }

            return plan;
        }

        public static SheepBoardPlan PlanPreview(SheepLevelData config, int seed, int iconCap = 0)
        {
            var state = Random.state;
            Random.InitState(seed);
            var plan = Plan(config, iconCap);
            Random.state = state;
            return plan;
        }

        public static SheepBoardPlan Plan(SheepLevelData config, int iconCap)
        {
            if (config == null)
                return new SheepBoardPlan();

            config.EnsureInitialized();
            // 固定排布（教学关）：棋盘完全由关卡说了算，不再走随机生成
            if (config.HasFixedCells)
                return PlanFromFixed(config);
            var typeNum = Mathf.Max(1, config.TypeNum);
            if (iconCap > 0)
                typeNum = Mathf.Min(typeNum, iconCap);

            var compose = Mathf.Max(1, config.ComposeNum);
            var leftCount = Mathf.Max(0, config.LeftStack);
            var rightCount = Mathf.Max(0, config.RightStack);
            var minCount = config.LevelNum * config.LevelBlockNum + leftCount + rightCount;
            var unit = compose * typeNum;
            var total = minCount;
            if (unit > 0 && total % unit != 0)
                total = (total / unit + 1) * unit;

            var bag = BuildBag(total, typeNum, compose);
            var pos = 0;
            var left = Take(bag, ref pos, leftCount);
            var right = Take(bag, ref pos, rightCount);
            if (left.Length > 0 && left.Length == right.Length)
            {
                for (var i = 0; i < right.Length / 2; i++)
                {
                    var j = right.Length - 1 - i;
                    var tmp = right[i];
                    right[i] = right[j];
                    right[j] = tmp;
                }
            }

            var motif = SheepMotifs.Resolve(config);
            var mask = SheepMotifs.BuildMask(motif);
            var boardNeed = bag.Count - pos;
            var slots = BuildStackSlots(mask, boardNeed, config.LevelNum, config.BorderStep);
            var cells = new List<SheepBoardCell>(boardNeed);
            for (var i = 0; i < slots.Count && pos < bag.Count; i++)
            {
                var slot = slots[i];
                cells.Add(new SheepBoardCell
                {
                    GridX = slot.x,
                    GridY = slot.y,
                    Layer = slot.z,
                    IconId = bag[pos]
                });
                pos++;
            }

            while (pos < bag.Count)
            {
                var fall = slots.Count > 0 ? slots[slots.Count - 1] : new Vector3Int(10, 10, config.LevelNum);
                fall.z++;
                cells.Add(new SheepBoardCell
                {
                    GridX = fall.x,
                    GridY = fall.y,
                    Layer = fall.z,
                    IconId = bag[pos]
                });
                pos++;
            }

            return new SheepBoardPlan
            {
                LeftIcons = left,
                RightIcons = right,
                Cells = cells.ToArray(),
                Total = left.Length + right.Length + cells.Count,
                TypeNum = typeNum
            };
        }

        private static void SpawnStacks(SheepGameView view, RectTransform parent, SheepTileView prefab, Sprite[] icons, int[] bag)
        {
            if (parent == null || bag == null)
                return;

            for (var i = 0; i < bag.Length; i++)
            {
                var tile = Spawn(view, parent, prefab, icons, bag[i], i);
                var rect = tile != null ? tile.transform as RectTransform : null;
                if (rect == null)
                    continue;

                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -20f - i * 28f);
                rect.SetAsLastSibling();
            }
        }

        private static List<Vector3Int> BuildStackSlots(bool[,] mask, int need, int levelNum, int borderStep)
        {
            var slots = new List<Vector3Int>(Mathf.Max(0, need));
            if (need <= 0)
                return slots;

            var leftOrigins = SheepMotifs.CollectLeftOrigins(mask);
            if (leftOrigins.Count == 0)
                leftOrigins.Add(new Vector2Int(10, 10));
            Shuffle(leftOrigins);

            var maxEdge = Mathf.Max(1, levelNum + Mathf.Max(0, borderStep));
            var maxInner = Mathf.Max(1, levelNum - 1);
            var uniqueTarget = Mathf.Clamp(need * 5 / 9, 4, need);
            uniqueTarget = Mathf.Min(uniqueTarget, leftOrigins.Count * 2);

            var selected = new List<Vector2Int>(uniqueTarget);
            var selectedSet = new HashSet<int>();
            for (var i = 0; i < leftOrigins.Count && selected.Count < uniqueTarget; i++)
            {
                var p = leftOrigins[i];
                AddSelected(selected, selectedSet, p.x, p.y);
                var mx = SheepMotifs.MirrorX(p.x);
                if (mx != p.x)
                    AddSelected(selected, selectedSet, mx, p.y);
            }

            if (selected.Count == 0)
                AddSelected(selected, selectedSet, 10, 10);

            var rim = new List<Vector2Int>();
            var inner = new List<Vector2Int>();
            SplitRim(selected, selectedSet, mask, rim, inner);
            Shuffle(rim);
            rim.Sort((a, b) => EmptyOf(selectedSet, mask, b).CompareTo(EmptyOf(selectedSet, mask, a)));
            Shuffle(inner);

            for (var i = 0; i < selected.Count && slots.Count < need; i++)
                slots.Add(new Vector3Int(selected[i].x, selected[i].y, 0));

            var layer = 1;
            while (slots.Count < need && layer < maxEdge)
            {
                var before = slots.Count;
                AppendLayer(slots, rim, layer, need);
                if (layer < maxInner)
                    AppendLayer(slots, inner, layer, need);
                if (slots.Count == before)
                    break;
                layer++;
            }

            while (slots.Count < need)
            {
                var halo = HaloOf(selectedSet, mask);
                if (halo.Count == 0)
                    break;
                Shuffle(halo);
                var take = Mathf.Min(halo.Count, need - slots.Count);
                for (var i = 0; i < take; i++)
                {
                    var p = halo[i];
                    AddSelected(selected, selectedSet, p.x, p.y);
                    slots.Add(new Vector3Int(p.x, p.y, 0));
                    var mx = SheepMotifs.MirrorX(p.x);
                    if (slots.Count < need && mx != p.x)
                    {
                        AddSelected(selected, selectedSet, mx, p.y);
                        slots.Add(new Vector3Int(mx, p.y, 0));
                    }
                }

                SplitRim(selected, selectedSet, mask, rim, inner);
                layer = 1;
                while (slots.Count < need && layer < maxEdge)
                {
                    AppendLayer(slots, rim, layer, need);
                    layer++;
                }
            }

            while (slots.Count < need && selected.Count > 0)
            {
                var p = selected[slots.Count % selected.Count];
                slots.Add(new Vector3Int(p.x, p.y, maxEdge + slots.Count / selected.Count));
            }

            if (slots.Count > need)
                slots.RemoveRange(need, slots.Count - need);
            slots.Sort((a, b) =>
            {
                var l = a.z.CompareTo(b.z);
                return l != 0 ? l : a.x.CompareTo(b.x);
            });
            return slots;
        }

        private static void AddSelected(List<Vector2Int> selected, HashSet<int> keys, int x, int y)
        {
            var key = SheepMotifs.Key(x, y);
            if (!keys.Add(key))
                return;
            selected.Add(new Vector2Int(x, y));
        }

        private static void SplitRim(
            List<Vector2Int> selected,
            HashSet<int> keys,
            bool[,] mask,
            List<Vector2Int> rim,
            List<Vector2Int> inner)
        {
            rim.Clear();
            inner.Clear();
            for (var i = 0; i < selected.Count; i++)
            {
                var p = selected[i];
                if (p.x > SheepMotifs.MirrorX(p.x))
                    continue;
                if (EmptyOf(keys, mask, p) > 0)
                    rim.Add(p);
                else
                    inner.Add(p);
            }
        }

        private static int EmptyOf(HashSet<int> keys, bool[,] mask, Vector2Int p)
        {
            var n = 0;
            if (!keys.Contains(SheepMotifs.Key(p.x - 1, p.y))) n++;
            if (!keys.Contains(SheepMotifs.Key(p.x + 1, p.y))) n++;
            if (!keys.Contains(SheepMotifs.Key(p.x, p.y - 1))) n++;
            if (!keys.Contains(SheepMotifs.Key(p.x, p.y + 1))) n++;
            if (SheepMotifs.IsRim(mask, p.x, p.y))
                n += 2;
            return n;
        }

        private static List<Vector2Int> HaloOf(HashSet<int> keys, bool[,] mask)
        {
            var halo = new List<Vector2Int>();
            var seen = new HashSet<int>();
            var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
            foreach (var key in keys)
            {
                var x = key / 100;
                var y = key % 100;
                for (var d = 0; d < dirs.Length; d++)
                {
                    var nx = x + dirs[d].x;
                    var ny = y + dirs[d].y;
                    if (nx < 0 || ny < 0 || nx > SheepMotifs.OriginMax || ny > SheepMotifs.OriginMax)
                        continue;
                    if (nx > SheepMotifs.MirrorX(nx))
                        continue;
                    var nk = SheepMotifs.Key(nx, ny);
                    if (keys.Contains(nk) || !seen.Add(nk))
                        continue;
                    if (mask != null && !SheepMotifs.On(mask, nx, ny) && !SheepMotifs.On(mask, x, y))
                        continue;
                    halo.Add(new Vector2Int(nx, ny));
                }
            }

            return halo;
        }

        private static void AppendLayer(List<Vector3Int> slots, List<Vector2Int> points, int layer, int need)
        {
            for (var i = 0; i < points.Count && slots.Count < need; i++)
            {
                var p = points[i];
                slots.Add(new Vector3Int(p.x, p.y, layer));
                var mx = SheepMotifs.MirrorX(p.x);
                if (slots.Count < need && mx != p.x)
                    slots.Add(new Vector3Int(mx, p.y, layer));
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static SheepTileView Spawn(SheepGameView view, Transform parent, SheepTileView prefab, Sprite[] icons, int iconId, int layer)
        {
            // 优先从对象池取，避免每次开局都 Instantiate 上百个棋子
            SheepTileView tile = null;
            if (view != null)
                tile = view.TakeTile(parent);
            if (tile == null)
            {
                if (prefab == null)
                    return null;
                tile = Object.Instantiate(prefab, parent, false);
            }

            tile.name = $"Tile_L{layer}_{iconId}";
            tile.Setup(iconId, IconOf(icons, iconId), layer, false);
            tile.RememberHome();
            return tile;
        }

        private static int[] Take(List<int> bag, ref int pos, int count)
        {
            count = Mathf.Min(Mathf.Max(0, count), Mathf.Max(0, bag.Count - pos));
            var result = new int[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = bag[pos];
                pos++;
            }

            return result;
        }

        private static List<int> BuildBag(int total, int typeNum, int compose)
        {
            var bag = new List<int>(total);
            var groups = total / compose;
            for (var i = 0; i < groups; i++)
            {
                var id = i % typeNum;
                for (var k = 0; k < compose; k++)
                    bag.Add(id);
            }

            for (var i = bag.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }

            return bag;
        }

        private static Sprite IconOf(Sprite[] icons, int iconId)
        {
            if (icons == null || iconId < 0 || iconId >= icons.Length)
                return null;
            return icons[iconId];
        }
    }
}
