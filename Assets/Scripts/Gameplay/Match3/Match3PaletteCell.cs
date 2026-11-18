using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>自定义调色格：一颗方块、一个障碍，或「随机图标」。</summary>
    public class Match3PaletteCell : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public Image Icon;
        public Text Label;
    }
}
