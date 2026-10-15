using CozyFishing.Core;
using CozyFishing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using static CozyFishing.EditorTools.SceneKit;

namespace CozyFishing.EditorTools
{
    /// <summary>The Title scene: splash, main menu, character select (all built at runtime by <see cref="TitleScreen"/>).</summary>
    public static class TitleSceneBuilder
    {
        [MenuItem("CozyFishing/Scenes/Build Title")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.05f);
            new GameObject("Systems").AddComponent<GameInput>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var title = new GameObject("Title").AddComponent<TitleScreen>();
            Set(title, "background", Sprite("Assets/Art/UI/Title/title_bg.png", 2048));
            Set(title, "logo", Sprite("Assets/Art/UI/Title/logo_mark.png", 512));
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Title.unity");
            Debug.Log("[TitleSceneBuilder] Title built");
        }
    }
}
