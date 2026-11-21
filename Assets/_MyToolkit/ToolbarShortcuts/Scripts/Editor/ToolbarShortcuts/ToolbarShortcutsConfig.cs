using System.Collections.Generic;
using UnityEngine;

namespace MyToolkit.ToolbarShortcuts.Editor
{
    [CreateAssetMenu(fileName = "ToolbarShortcutsConfig", menuName = "ToolbarShortcuts/Toolbar Shortcuts Config")]
    public class ToolbarShortcutsConfig : ScriptableObject
    {
        [Min(0)] public int leftMargin;
        [Min(0)] public int rightMargin;

        public ToolbarShortcutsWindowColumns windowColumns = ToolbarShortcutsWindowColumns.One;

        public List<ToolbarShortcutEntry> shortcuts = new();
    }
}
