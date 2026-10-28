using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames.Editor
{
    /// <summary>
    /// 给商店面板填「道具 -> 图标」表，并把行模板的 IconImage 指到 Item 实例里的 Icon 图片。
    /// 图标命名规则：Assets/UI/&lt;玩法&gt;/&lt;ShopItemId&gt;.png，例如 Assets/UI/Match3/Match3Hint.png。
    /// 新增道具后按同名放图，再跑一次这个菜单即可；缺图会在弹窗里列出来。
    /// </summary>
    public static class ShopIconBinder
    {
        private const string ShopPrefabPath = "Assets/Content/UI/Shop.prefab";
        private const string IconRoot = "Assets/UI";
        private const string IconNodeName = "Icon";

        [MenuItem("SmallGames/绑定商店道具图标")]
        public static void BindFromMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[商店图标] 找不到 {ShopPrefabPath}");
                return;
            }

            var panel = prefab.GetComponent<ShopPanel>();
            if (panel == null)
            {
                Debug.LogError("[商店图标] Shop 预制体根节点上没有 ShopPanel。");
                return;
            }

            var so = new SerializedObject(panel);
            var iconsProp = so.FindProperty("Icons");
            if (iconsProp == null)
            {
                Debug.LogError("[商店图标] ShopPanel 上找不到 Icons 字段。");
                return;
            }

            var items = ShopCatalog.All;
            var ids = (ShopItemId[])Enum.GetValues(typeof(ShopItemId));
            var missing = new StringBuilder();
            iconsProp.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++)
            {
                var id = items[i].Id;
                var path = $"{IconRoot}/{items[i].Mode}/{id}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    missing.AppendLine(path);

                var element = iconsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").enumValueIndex = Array.IndexOf(ids, id);
                element.FindPropertyRelative("Icon").objectReferenceValue = sprite;
            }

            var bound = BindTemplateIcon(panel);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            var message = $"写入 {items.Length} 条道具图标。";
            message += bound ? "行模板 IconImage 已绑定。" : "行模板里没找到名为 Icon 的图片，运行时会按名字兜底查找。";
            if (missing.Length > 0)
                message += "\n\n缺少图标：\n" + missing;
            Debug.Log("[商店图标] " + message);
            EditorUtility.DisplayDialog("商店图标", message, "确定");
        }

        /// <summary>把行模板的 IconImage 指向 Item 预制体实例里名为 Icon 的图片。</summary>
        private static bool BindTemplateIcon(ShopPanel panel)
        {
            var view = panel.ItemTemplate;
            if (view == null)
                return false;

            var images = view.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i].gameObject.name != IconNodeName)
                    continue;

                var so = new SerializedObject(view);
                var prop = so.FindProperty("IconImage");
                if (prop == null)
                    return false;
                prop.objectReferenceValue = images[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
                return true;
            }

            return false;
        }
    }
}
