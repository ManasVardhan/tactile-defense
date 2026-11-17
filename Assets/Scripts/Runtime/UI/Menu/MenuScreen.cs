using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Base for menu screens: a dimmed backdrop, a big card with a ribbon title and a back button.
    /// Subclasses fill 'Body' in Build and update it in Refresh (called on open and when the profile changes).</summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        public MenuRoot Menu { get; private set; }
        public RectTransform Body { get; private set; }
        public RectTransform CardRect { get; private set; }
        public UIPanelAnim Anim { get; private set; }
        public UIButton BackButton { get; private set; }
        protected TextMeshProUGUI titleText;

        public abstract string Title { get; }
        protected virtual Vector2 CardSize => new Vector2(1600f, 790f);
        protected virtual float CardY => -58f;

        public void Init(MenuRoot menu)
        {
            Menu = menu;
            var rt = (RectTransform)transform;
            rt.Stretch();
            var dim = UIFactory.Dim(rt, 0.45f);
            dim.raycastTarget = true;
            var cardRoot = UIFactory.Rect(rt, "CardRoot").At(0.5f, 0.5f, 0f, CardY, CardSize.x, CardSize.y);
            var face = UIFactory.Card(cardRoot, "Card", CardSize.x, CardSize.y);
            ((RectTransform)face.parent).Stretch();
            CardRect = cardRoot;
            Body = UIFactory.Rect(face, "Body").Stretch(36f, 100f, 36f, 30f);
            var ribbon = UIFactory.Ribbon(cardRoot, Title, UIColors.Team(UIFactory.PlayerTeam), Mathf.Max(520f, Title.Length * 42f + 220f), 112f, 58f);
            ribbon.At(0.5f, 1f, 0f, 44f, ribbon.sizeDelta.x, 112f, 0.5f, 1f);
            titleText = ribbon.GetComponentInChildren<TextMeshProUGUI>();
            BackButton = UIFactory.IconButton(face, "Back", UIIcon.ArrowLeft, 84f, ButtonColor.Slate);
            ((RectTransform)BackButton.transform).At(0f, 1f, 22f, -12f, 84f, 84f, 0f, 1f);
            BackButton.sound = Audio.SoundId.UiBack;
            BackButton.onClick = () => Menu.CloseScreen();
            var cg = gameObject.AddComponent<CanvasGroup>();
            Anim = gameObject.AddComponent<UIPanelAnim>();
            Anim.scaleTarget = cardRoot;
            Build();
            gameObject.SetActive(false);
        }

        protected abstract void Build();
        public virtual void Refresh() { }
        public virtual void OnOpened() { }

        /// <summary>Re-tints the ribbon after a team change.</summary>
        public void RetintRibbon()
        {
            if (titleText == null) return;
            var img = titleText.transform.parent.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = UIColors.Team(UIFactory.PlayerTeam);
        }
    }
}
