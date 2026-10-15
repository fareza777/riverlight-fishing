using System.Collections;
using System.IO;
using CozyFishing.Core;
using CozyFishing.Story;
using CozyFishing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Opens every menu page (pause tabs, market, title, character select, settings) in English and Indonesian and
    /// screenshots each, to check the layout on the current screen size. Runs with "-selftest-ui" or, in the editor,
    /// when Temp/ui_shots.flag exists. Shots go to Temp/Shots (editor) and are logged as "[UiShots]".
    /// </summary>
    public sealed class UiShots : MonoBehaviour
    {
        int n;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            bool flag = Application.isEditor && File.Exists("Temp/ui_shots.flag");
            if (!DevArgs.Has("-selftest-ui") && !flag) return;
            if (flag) File.Delete("Temp/ui_shots.flag");
            var go = new GameObject("UiShots");
            DontDestroyOnLoad(go);
            go.AddComponent<UiShots>();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory("Temp/Shots");
            GameState.Instance.Data.seenCutscenes.Add("intro");
            if (SceneManager.GetActiveScene().name != "River") { SceneManager.LoadScene("River"); yield return Wait(3f); }
            CutscenePlayer.SkipAll();
            yield return Wait(2f);
            Progression.RefreshOrders();
            foreach (var lang in new[] { Language.English, Language.Indonesian })
            {
                Loc.Preview(lang);
                string l = lang == Language.English ? "en" : "id";
                yield return Shot($"{l}_hud");
                foreach (var tab in new[] { PauseMenu.Tab.Journal, PauseMenu.Tab.Bag, PauseMenu.Tab.Map, PauseMenu.Tab.Settings })
                {
                    PauseMenu.Open(tab);
                    yield return Shot($"{l}_{tab}");
                }
                PauseMenu.Close();
                ShopUI.Open();
                yield return Shot($"{l}_shop");
                var shop = FindAnyObjectByType<ShopUI>();
                if (shop != null) shop.SendMessage("Close");
            }
            Loc.EndPreview();
            SceneManager.LoadScene("Title");
            yield return Wait(7f);
            yield return Shot("title_main");
            yield return Click(Loc.T("new_game"));
            yield return Shot("title_select");
            Debug.Log("[UiShots] done");
        }

        /// <summary>Presses the first visible button whose label matches (like a tap).</summary>
        static IEnumerator Click(string label)
        {
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t != null && t.text == label && b.isActiveAndEnabled) { b.onClick.Invoke(); break; }
            }
            yield return Wait(1f);
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator Shot(string label)
        {
            yield return Wait(0.7f);
            yield return new WaitForEndOfFrame();
            if (Application.isEditor) ScreenCapture.CaptureScreenshot($"Temp/Shots/ui_{n:00}_{label}.png");
            Debug.Log($"[UiShots] shot {label}");
            n++;
            yield return Wait(1.6f);   // time for an external screen grab (adb screencap) on a phone
        }
    }
}
