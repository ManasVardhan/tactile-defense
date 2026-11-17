using System;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Modal yes / no card.</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        UIPanelAnim anim;
        TextMeshProUGUI title, message;
        UIButton ok, cancel;
        Action onOk;

        public static ConfirmDialog Create(Transform parent)
        {
            var rt = UIFactory.Rect(parent, "ConfirmDialog").Stretch();
            var d = rt.gameObject.AddComponent<ConfirmDialog>();
            UIFactory.Dim(rt, 0.6f);
            var cardRoot = UIFactory.Rect(rt, "CardRoot").At(0.5f, 0.5f, 0f, -10f, 860f, 440f);
            var face = UIFactory.Card(cardRoot, "Card", 860f, 440f);
            ((RectTransform)face.parent).Stretch();
            d.title = UIFactory.Text(face, "Title", "Are you sure?", 54f, TextStyle.HeadingDark);
            ((RectTransform)d.title.transform).At(0.5f, 1f, 0f, -36f, 780f, 70f, 0.5f, 1f);
            d.message = UIFactory.Paragraph(face, "Message", "", 30f, UIColors.Muted, TextAlignmentOptions.Top);
            ((RectTransform)d.message.transform).At(0.5f, 1f, 0f, -120f, 720f, 150f, 0.5f, 1f);
            d.cancel = UIFactory.Button(face, "Cancel", "CANCEL", ButtonColor.Slate, 300f, 100f);
            ((RectTransform)d.cancel.transform).At(0.5f, 0f, -170f, 34f, 300f, 100f, 0.5f, 0f);
            d.cancel.sound = Audio.SoundId.UiBack;
            d.cancel.onClick = () => d.Close();
            d.ok = UIFactory.Button(face, "Confirm", "OK", ButtonColor.Red, 300f, 100f);
            ((RectTransform)d.ok.transform).At(0.5f, 0f, 170f, 34f, 300f, 100f, 0.5f, 0f);
            d.ok.onClick = () => { var a = d.onOk; d.Close(); a?.Invoke(); };
            rt.gameObject.AddComponent<CanvasGroup>();
            d.anim = rt.gameObject.AddComponent<UIPanelAnim>();
            d.anim.scaleTarget = cardRoot;
            rt.gameObject.SetActive(false);
            return d;
        }

        public void Show(string titleText, string messageText, string okText, ButtonColor okColor, Action confirm)
        {
            title.text = titleText;
            message.text = messageText;
            var l = ok.Label();
            if (l != null) l.text = okText;
            ok.SetColor(okColor);
            onOk = confirm;
            transform.SetAsLastSibling();
            anim.Show();
        }

        public void Close() => anim.Hide();
        public UIButton ConfirmButton => ok;
    }
}
