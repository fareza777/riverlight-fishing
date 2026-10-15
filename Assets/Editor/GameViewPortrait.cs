using System;
using System.Reflection;
using UnityEditor;

namespace CozyFishing.EditorTools
{
    /// <summary>Sets the Game view to a 1080x1920 portrait phone resolution (menu: CozyFishing/Game View Portrait).</summary>
    public static class GameViewPortrait
    {
        [MenuItem("CozyFishing/Game View Portrait")]
        public static void Apply() => Apply(1080, 1920);

        /// <summary>A tall 20:9 phone (e.g. Redmi Note 11) to check UI on long screens.</summary>
        [MenuItem("CozyFishing/Game View Tall Phone 1080x2400")]
        public static void ApplyTall() => Apply(1080, 2400);

        /// <summary>A 4:3 tablet in portrait to check UI on wide screens.</summary>
        [MenuItem("CozyFishing/Game View Tablet 1536x2048")]
        public static void ApplyTablet() => Apply(1536, 2048);

        public static void Apply(int width, int height)
        {
            string Label = $"Phone Portrait {width}x{height}";
            var asm = typeof(Editor).Assembly;
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            var groupType = group.GetType();

            int index = -1;
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText").GetValue(size) == Label) { index = i; break; }
            }
            if (index < 0)
            {
                var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                object newSize = ctor.Invoke(new object[] { Enum.ToObject(sizeKind, 1), width, height, Label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }

            var window = EditorWindow.GetWindow(gameViewType);
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            select.Invoke(window, new object[] { index, null });
        }
    }
}
