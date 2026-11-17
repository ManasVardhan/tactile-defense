using System.Collections;
using TactileWars.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Paint wipe between scenes: a team colored splat grows to cover the screen, the next scene loads,
    /// then the splat shrinks away. Persistent overlay canvas (never part of captures).</summary>
    public class ScreenTransition : MonoBehaviour
    {
        static ScreenTransition instance;
        Image splat, splat2;
        Canvas canvas;
        public static bool Busy { get; private set; }

        static ScreenTransition Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenTransition", typeof(RectTransform));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenTransition>();
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            instance.canvas = c;
            var skin = UIFactory.Skin;
            instance.splat2 = UIFactory.Img(go.transform, "Splat2", skin != null ? skin.splat : null, Color.white);
            ((RectTransform)instance.splat2.transform).At(0.5f, 0.5f, 0f, 0f, 1400f, 1400f);
            instance.splat = UIFactory.Img(go.transform, "Splat", skin != null ? skin.splat : null, Color.white);
            ((RectTransform)instance.splat.transform).At(0.5f, 0.5f, 0f, 0f, 1400f, 1400f);
            instance.splat.raycastTarget = true;
            go.SetActive(false);
            return instance;
        }

        /// <summary>Loads a scene behind a paint wipe in the player's color.</summary>
        public static void Load(string scene, TeamColorId color)
        {
            if (Busy) return;
            var t = Ensure();
            t.gameObject.SetActive(true);
            t.StartCoroutine(t.Run(scene, UIColors.Team(color), UIColors.TeamDark(color)));
        }

        IEnumerator Run(string scene, Color c, Color dark)
        {
            Busy = true;
            splat.color = c;
            splat2.color = dark;
            var a = (RectTransform)splat.transform;
            var b = (RectTransform)splat2.transform;
            float t = 0f;
            const float inTime = 0.38f;
            while (t < inTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / inTime);
                float s = Mathf.Lerp(0.02f, 2.4f, UIAnim.InCubic(k) * 0.7f + k * 0.3f);
                a.localScale = new Vector3(s, s, 1f);
                a.localRotation = Quaternion.Euler(0f, 0f, k * 40f);
                float s2 = Mathf.Lerp(0.02f, 2.6f, Mathf.Clamp01(k * 1.15f));
                b.localScale = new Vector3(s2, s2, 1f);
                b.localRotation = Quaternion.Euler(0f, 0f, -k * 25f);
                yield return null;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            t = 0f;
            const float outTime = 0.42f;
            while (t < outTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / outTime);
                float s = Mathf.Lerp(2.4f, 0f, UIAnim.InCubic(k));
                a.localScale = new Vector3(s, s, 1f);
                float s2 = Mathf.Lerp(2.6f, 0f, UIAnim.InCubic(Mathf.Clamp01(k * 1.2f)));
                b.localScale = new Vector3(s2, s2, 1f);
                yield return null;
            }
            gameObject.SetActive(false);
            Busy = false;
        }
    }
}
