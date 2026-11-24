using System;
using TactileWars.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Chunky game button: the face sits on a darker lip and presses down onto it with a squash, hover
    /// brightens, release springs back, every click plays a UI sound through AudioManager. Works with timeScale 0.</summary>
    public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public RectTransform face;
        public Image faceImage;
        public Image lipImage;
        public RectTransform content;
        public float lip = 8f;
        public SoundId sound = SoundId.UiClick;
        public bool interactable = true;

        public Action onClick;
        /// <summary>Raised with true when the pointer enters the button and false when it leaves.</summary>
        public Action<bool> onHover;

        Color faceColor = Color.white, lipColor = Color.grey;
        bool pressed, hover;
        float press;       // 0..1 pressed amount
        float squash;      // spring offset
        float squashVel;
        Vector2 faceBase;
        bool baseCaptured;
        bool animating;

        public void SetColors(Color faceCol, Color lipCol)
        {
            faceColor = faceCol;
            lipColor = lipCol;
            ApplyColors();
        }

        public void SetInteractable(bool on)
        {
            interactable = on;
            ApplyColors();
        }

        void ApplyColors()
        {
            Color f = faceColor, l = lipColor;
            if (!interactable)
            {
                float g = f.grayscale * 0.55f + 0.38f;
                f = new Color(g, g, g * 1.04f, f.a);
                float lg = l.grayscale * 0.6f + 0.25f;
                l = new Color(lg, lg, lg * 1.05f, l.a);
            }
            else if (hover && !pressed) f = Color.Lerp(f, Color.white, 0.1f);
            if (faceImage != null) faceImage.color = f;
            if (lipImage != null) lipImage.color = l;
        }

        void Awake() { CaptureBase(); ApplyColors(); }

        void CaptureBase()
        {
            if (baseCaptured || face == null) return;
            faceBase = face.anchoredPosition;
            baseCaptured = true;
        }

        void OnDisable()
        {
            pressed = false; hover = false; press = 0f; squash = 0f; squashVel = 0f;
            if (face != null && baseCaptured) face.anchoredPosition = faceBase;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable) return;
            pressed = true;
            squashVel += 1.2f;
            animating = true;
            ApplyColors();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!pressed) return;
            pressed = false;
            squashVel -= 1.6f;
            animating = true;
            ApplyColors();
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; ApplyColors(); onHover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { hover = false; ApplyColors(); onHover?.Invoke(false); }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            Click();
        }

        /// <summary>Programmatic click (tests, keyboard shortcuts). Respects 'interactable'.</summary>
        public void Click()
        {
            if (!interactable)
            {
                AudioManager.Play(SoundId.UiError, Vector3.zero);
                UIAnim.Shake((RectTransform)transform, 8f, 0.25f);
                return;
            }
            AudioManager.Play(sound, Vector3.zero);
            squashVel -= 0.6f;
            animating = true;
            onClick?.Invoke();
        }

        void Update()
        {
            CaptureBase();
            if (!animating) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            press = Mathf.MoveTowards(press, pressed ? 1f : 0f, dt * 14f);
            // Under damped spring: press squashes, release overshoots, then it settles.
            squashVel += (-220f * squash - 18f * squashVel) * dt;
            squash += squashVel * dt;
            if (face != null) face.anchoredPosition = faceBase + new Vector2(0f, -lip * 0.75f * press);
            float sx = 1f + squash + press * 0.02f;
            float sy = 1f - squash - press * 0.03f;
            transform.localScale = new Vector3(sx, sy, 1f);
            if (!pressed && press <= 0f && Mathf.Abs(squash) < 0.0008f && Mathf.Abs(squashVel) < 0.01f)
            {
                squash = 0f; squashVel = 0f; animating = false;
                transform.localScale = Vector3.one;
                if (face != null) face.anchoredPosition = faceBase;
            }
        }
    }
}
