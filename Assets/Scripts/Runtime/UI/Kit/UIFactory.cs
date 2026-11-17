using TactileWars.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    public enum TextStyle { Heading, HeadingThin, HeadingDark, Body, BodyLight, BodyOutline, BodyMuted }
    public enum ButtonColor { Green, Slate, Red, Gold, White, Team, Blue, Grey }

    /// <summary>Builds every widget from the UISkin so the whole UI shares one look: chunky rounded pills with a
    /// darker lip, cream cards with a rim and soft shadow, outlined display font. Sizes are canvas units at the
    /// 1920x1080 reference.</summary>
    public static class UIFactory
    {
        public static UISkin Skin;
        public static TeamColorId PlayerTeam = TeamColorId.Red;
        public const int UILayer = 5;

        // Corner radius in pixels of the source sprites (see UIArtBuilder).
        const float R16 = 16f, R32 = 40f, PillH = 128f, TabR = 48f;

        // ------------------------------------------------------------------ rects

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor at (ax, ay) of the parent, pivot at the same point unless given, position and size.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h, float px = -1f, float py = -1f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(px < 0f ? ax : px, py < 0f ? ay : py);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Removes all children right away from layout (deferred destroy would leave them in layout groups
        /// for one frame).</summary>
        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i);
                c.gameObject.SetActive(false);
                c.SetParent(null, false);
                Object.Destroy(c.gameObject);
            }
        }

        // ------------------------------------------------------------------ images and text

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float ppuMult = 1f, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppuMult; }
            else img.preserveAspect = sprite != null && Skin != null && sprite != Skin.white && sprite != Skin.gradientV && sprite != Skin.dash && sprite != Skin.glow;
            return img;
        }

        public static Image Icon(Transform parent, UIIcon icon, float size, string name = null)
        {
            var img = Img(parent, name ?? ("Icon_" + icon), Skin != null ? Skin.Icon(icon) : null, Color.white);
            img.preserveAspect = true;
            ((RectTransform)img.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        public static float Round16(float radius) => R16 / Mathf.Max(1f, radius);
        public static float Round32(float radius) => R32 / Mathf.Max(1f, radius);
        public static float PillMult(float height) => PillH / Mathf.Max(1f, height);

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextStyle style, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            bool heading = style == TextStyle.Heading || style == TextStyle.HeadingThin || style == TextStyle.HeadingDark;
            if (Skin != null)
            {
                t.font = heading ? Skin.headingFont : Skin.bodyFont;
                if (style == TextStyle.Heading && Skin.headingOutline != null) t.fontSharedMaterial = Skin.headingOutline;
                else if (style == TextStyle.HeadingThin && Skin.headingOutlineThin != null) t.fontSharedMaterial = Skin.headingOutlineThin;
                else if (style == TextStyle.BodyOutline && Skin.bodyOutline != null) t.fontSharedMaterial = Skin.bodyOutline;
            }
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = style switch
            {
                TextStyle.HeadingDark => UIColors.Ink,
                TextStyle.Body => UIColors.Ink,
                TextStyle.BodyMuted => UIColors.Muted,
                _ => Color.white,
            };
            // Lilita One has a narrow space that the outline swallows: widen word gaps for headings.
            if (heading) { t.characterSpacing = 1.5f; t.wordSpacing = style == TextStyle.HeadingDark ? 14f : 24f; }
            return t;
        }

        /// <summary>Wrapped paragraph text (body font).</summary>
        public static TextMeshProUGUI Paragraph(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Text(parent, name, text, size, TextStyle.Body, align);
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.lineSpacing = -6f;
            return t;
        }

        // ------------------------------------------------------------------ buttons

        public static void Colors(ButtonColor c, out Color face, out Color lip)
        {
            switch (c)
            {
                case ButtonColor.Green: face = UIColors.Green; lip = UIColors.GreenDark; break;
                case ButtonColor.Red: face = UIColors.Red; lip = UIColors.RedDark; break;
                case ButtonColor.Gold: face = UIColors.Gold; lip = UIColors.GoldDark; break;
                case ButtonColor.White: face = UIColors.Card; lip = UIColors.CardRim; break;
                case ButtonColor.Blue: face = UIColors.Blue; lip = UIColors.BlueDark; break;
                case ButtonColor.Grey: face = UIColors.Grey; lip = UIColors.GreyDark; break;
                case ButtonColor.Team: face = UIColors.Team(PlayerTeam); lip = UIColors.TeamDark(PlayerTeam); break;
                default: face = UIColors.Hex(0x4A5165); lip = UIColors.Hex(0x2A2E3B); break;
            }
        }

        /// <summary>Pill button: lip, face, shine, then an icon and or label centered on the face.</summary>
        public static UIButton Button(Transform parent, string name, string label, ButtonColor color, float w, float h, UIIcon? icon = null, float fontSize = 0f)
        {
            float lip = Mathf.Round(Mathf.Clamp(h * 0.09f, 5f, 10f));
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var sprite = Skin != null ? Skin.pill : null;
            float mult = PillMult(h - lip);
            var lipImg = Img(root, "Lip", sprite, Color.grey, mult);
            ((RectTransform)lipImg.transform).Stretch();
            var faceImg = Img(root, "Face", sprite, Color.white, mult);
            var face = ((RectTransform)faceImg.transform).Stretch(0f, 0f, 0f, lip);
            var shine = Img(face, "Shine", Skin != null ? Skin.pillShine : null, new Color(1f, 1f, 1f, 0.32f), mult);
            ((RectTransform)shine.transform).Stretch(0f, 0f, 0f, 0f);

            var content = Rect(face, "Content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = new Vector2(0f, 1f);
            var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = h * 0.12f;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (icon.HasValue)
            {
                var ic = Icon(content, icon.Value, (h - lip) * 0.62f);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = (h - lip) * 0.62f;
            }
            if (!string.IsNullOrEmpty(label))
            {
                bool dark = color == ButtonColor.White;
                var t = Text(content, "Label", label, fontSize > 0f ? fontSize : (h - lip) * 0.46f, dark ? TextStyle.HeadingDark : TextStyle.Heading);
                t.overflowMode = TextOverflowModes.Overflow;
            }

            var b = root.gameObject.AddComponent<UIButton>();
            b.face = face; b.faceImage = faceImg; b.lipImage = lipImg; b.content = content; b.lip = lip;
            Colors(color, out var f, out var l);
            b.SetColors(f, l);
            return b;
        }

        public static TextMeshProUGUI Label(this UIButton b)
        {
            var t = b.content != null ? b.content.Find("Label") : null;
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        public static void SetColor(this UIButton b, ButtonColor color)
        {
            Colors(color, out var f, out var l);
            b.SetColors(f, l);
        }

        /// <summary>Round button with a centered icon.</summary>
        public static UIButton IconButton(Transform parent, string name, UIIcon icon, float d, ButtonColor color, float iconScale = 0.58f)
        {
            float lip = Mathf.Round(Mathf.Clamp(d * 0.07f, 4f, 9f));
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(d, d);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var sprite = Skin != null ? Skin.circle : null;
            var lipImg = Img(root, "Lip", sprite, Color.grey);
            ((RectTransform)lipImg.transform).Stretch();
            var faceImg = Img(root, "Face", sprite, Color.white);
            var face = ((RectTransform)faceImg.transform).Stretch(0f, 0f, 0f, lip);
            var shine = Img(face, "Shine", Skin != null ? Skin.circleShine : null, new Color(1f, 1f, 1f, 0.35f));
            ((RectTransform)shine.transform).Stretch();
            var content = Rect(face, "Content").At(0.5f, 0.5f, 0f, 0f, d, d);
            Icon(content, icon, (d - lip) * iconScale);
            var b = root.gameObject.AddComponent<UIButton>();
            b.face = face; b.faceImage = faceImg; b.lipImage = lipImg; b.content = content; b.lip = lip;
            Colors(color, out var f, out var l);
            b.SetColors(f, l);
            return b;
        }

        // ------------------------------------------------------------------ panels

        /// <summary>Cream card with rim, lip and soft drop shadow. Returns the face (content) rect; the root is its parent.</summary>
        public static RectTransform Card(Transform parent, string name, float w, float h, float radius = 36f, Color? face = null, Color? rim = null)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var sh = Img(root, "Shadow", Skin != null ? Skin.softShadow : null, UIColors.Shadow, 1f);
            ((RectTransform)sh.transform).Stretch(-26f, -14f, -26f, -40f);
            var rimImg = Img(root, "Rim", Skin != null ? Skin.rounded32 : null, rim ?? UIColors.CardRim, Round32(radius));
            ((RectTransform)rimImg.transform).Stretch();
            var f = Img(root, "Face", Skin != null ? Skin.rounded32 : null, face ?? UIColors.Card, Round32(radius - 5f));
            return ((RectTransform)f.transform).Stretch(5f, 5f, 5f, 12f);
        }

        /// <summary>Flat rounded rectangle (inner strips, chips, list rows).</summary>
        public static Image Panel(Transform parent, string name, Color color, float radius = 20f)
        {
            return Img(parent, name, Skin != null ? Skin.rounded32 : null, color, Round32(radius));
        }

        public static Image Dim(Transform parent, float alpha = 0.62f)
        {
            var d = Img(parent, "Dim", Skin != null ? Skin.white : null, UIColors.WithAlpha(UIColors.Dim, alpha), 1f, true);
            ((RectTransform)d.transform).Stretch();
            return d;
        }

        /// <summary>Banner title ribbon with folded tails.</summary>
        public static RectTransform Ribbon(Transform parent, string text, Color color, float w, float h, float fontSize = 0f)
        {
            var img = Img(parent, "Ribbon", Skin != null ? Skin.ribbon : null, color, 128f / h);
            var rt = (RectTransform)img.transform;
            rt.sizeDelta = new Vector2(w, h);
            var t = Text(rt, "Title", text, fontSize > 0f ? fontSize : h * 0.5f, TextStyle.Heading);
            ((RectTransform)t.transform).Stretch(h * 0.7f, 0f, h * 0.7f, h * 0.2f);
            return rt;
        }

        /// <summary>Currency chip: dark pill with a big icon overlapping its left end and a number.</summary>
        public static TextMeshProUGUI Chip(Transform parent, string name, UIIcon icon, float w, float h, out RectTransform root)
        {
            root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var bg = Img(root, "Bg", Skin != null ? Skin.pill : null, UIColors.WithAlpha(UIColors.Ink, 0.82f), PillMult(h));
            ((RectTransform)bg.transform).Stretch(h * 0.35f, 0f, 0f, 0f);
            var ic = Icon(root, icon, h * 1.25f);
            ((RectTransform)ic.transform).At(0f, 0.5f, h * 0.45f, 0f, h * 1.25f, h * 1.25f, 0.5f, 0.5f);
            var t = Text(root, "Value", "0", h * 0.6f, TextStyle.Heading, TextAlignmentOptions.MidlineRight);
            ((RectTransform)t.transform).Stretch(h * 1.15f, 0f, h * 0.42f, 0f);
            return t;
        }

        /// <summary>Small icon + value pair used on cards ("coin 120").</summary>
        public static TextMeshProUGUI IconValue(Transform parent, string name, UIIcon icon, string value, float size, TextStyle style, out RectTransform root)
        {
            root = Rect(parent, name);
            var hl = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = size * 0.2f;
            var csf = root.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var ic = Icon(root, icon, size * 1.15f);
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = size * 1.15f;
            var t = Text(root, "Value", value, size, style, TextAlignmentOptions.MidlineLeft);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static UIProgressBar Bar(Transform parent, string name, float w, float h, Color fill, Color? bg = null)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var back = Img(root, "Back", Skin != null ? Skin.pill : null, bg ?? UIColors.WithAlpha(UIColors.Ink, 0.75f), PillMult(h));
            ((RectTransform)back.transform).Stretch();
            var f = Img(root, "Fill", Skin != null ? Skin.pill : null, fill, PillMult(h - 6f));
            var frt = (RectTransform)f.transform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(3f, 3f); frt.offsetMax = new Vector2(3f + h, -3f);
            var sh = Img(f.transform, "Shine", Skin != null ? Skin.pillShine : null, new Color(1, 1, 1, 0.35f), PillMult(h - 6f));
            ((RectTransform)sh.transform).Stretch();
            var bar = root.gameObject.AddComponent<UIProgressBar>();
            bar.fill = frt;
            bar.fillImage = f;
            return bar;
        }

        /// <summary>Slider with a pill track, colored fill and a round white knob with a lip.</summary>
        public static Slider Slider(Transform parent, string name, float w, Color fill)
        {
            float h = 30f;
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, 64f);
            var track = Img(root, "Track", Skin != null ? Skin.pill : null, UIColors.WithAlpha(UIColors.Ink, 0.2f), PillMult(h));
            ((RectTransform)track.transform).At(0.5f, 0.5f, 0f, 0f, w, h);
            var fillArea = Rect(root, "FillArea").At(0.5f, 0.5f, 0f, 0f, w - h, h);
            var f = Img(fillArea, "Fill", Skin != null ? Skin.pill : null, fill, PillMult(h));
            var frt = (RectTransform)f.transform;
            frt.sizeDelta = Vector2.zero;
            var handleArea = Rect(root, "HandleArea").At(0.5f, 0.5f, 0f, 0f, w - h, 64f);
            var knobRoot = Rect(handleArea, "Knob");
            knobRoot.sizeDelta = new Vector2(58f, 0f);
            var knobLip = Img(knobRoot, "Lip", Skin != null ? Skin.circle : null, UIColors.CardRim);
            ((RectTransform)knobLip.transform).At(0.5f, 0.5f, 0f, -3f, 54f, 54f);
            var knob = Img(knobRoot, "Face", Skin != null ? Skin.circle : null, Color.white, 1f, true);
            ((RectTransform)knob.transform).At(0.5f, 0.5f, 0f, 1f, 54f, 54f);
            var dot = Img(knob.transform, "Dot", Skin != null ? Skin.circle : null, fill);
            ((RectTransform)dot.transform).At(0.5f, 0.5f, 0f, 0f, 20f, 20f);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = frt;
            s.handleRect = knobRoot;
            s.targetGraphic = knob;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.transition = Selectable.Transition.None;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            // Wider hit area for fingers.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0f);
            hit.raycastTarget = true;
            return s;
        }

        /// <summary>Horizontal scroll list with a masked viewport; children of 'content' are laid out left to right.</summary>
        public static ScrollRect HScroll(Transform parent, string name, float spacing, float padding, out RectTransform content)
        {
            var root = Rect(parent, name);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0f);
            hit.raycastTarget = true;
            var viewport = Rect(root, "Viewport").Stretch();
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(24, 0);
            content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.padding = new RectOffset((int)padding, (int)padding, 0, 0);
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;
            sr.inertia = true;
            return sr;
        }

        /// <summary>Text input on a white rounded field with a pencil icon.</summary>
        public static TMP_InputField Input(Transform parent, string name, float w, float h, string value, int maxChars = 16)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var bg = Img(root, "Bg", Skin != null ? Skin.pill : null, Color.white, PillMult(h), true);
            ((RectTransform)bg.transform).Stretch();
            var rim = Img(root, "Rim", Skin != null ? Skin.pill : null, UIColors.CardRim, PillMult(h));
            ((RectTransform)rim.transform).Stretch();
            rim.transform.SetAsFirstSibling();
            ((RectTransform)bg.transform).Stretch(3f, 3f, 3f, 6f);
            var area = Rect(root, "TextArea").Stretch(h * 0.45f, 4f, h * 1.1f, 6f);
            area.gameObject.AddComponent<RectMask2D>();
            var txt = Text(area, "Text", value, h * 0.46f, TextStyle.Body, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)txt.transform).Stretch();
            txt.overflowMode = TextOverflowModes.Overflow;
            var ph = Text(area, "Placeholder", "Your name", h * 0.46f, TextStyle.BodyMuted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)ph.transform).Stretch();
            var pen = Icon(root, UIIcon.Pencil, h * 0.6f);
            ((RectTransform)pen.transform).At(1f, 0.5f, -h * 0.55f, 1f, h * 0.6f, h * 0.6f, 0.5f, 0.5f);
            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.characterLimit = maxChars;
            input.targetGraphic = bg;
            input.transition = Selectable.Transition.None;
            input.text = value;
            input.caretColor = UIColors.Ink;
            input.customCaretColor = true;
            input.caretWidth = 3;
            input.selectionColor = UIColors.WithAlpha(UIColors.Gold, 0.45f);
            if (Skin != null) input.fontAsset = Skin.bodyFont;
            return input;
        }

        /// <summary>Row of small level pips (filled up to 'value').</summary>
        public static Image[] Pips(Transform parent, string name, int count, float size, out RectTransform root)
        {
            root = Rect(parent, name);
            var hl = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = size * 0.35f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var arr = new Image[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = Img(root, "Pip" + i, Skin != null ? Skin.circle : null, UIColors.CardLine);
                ((RectTransform)arr[i].transform).sizeDelta = new Vector2(size, size);
            }
            return arr;
        }

        /// <summary>Small rounded tag such as "OFFLINE" or "SIMULATED".</summary>
        public static RectTransform Tag(Transform parent, string name, string text, Color bg, float h = 34f, float fontSize = 0f)
        {
            var img = Img(parent, name, Skin != null ? Skin.pill : null, bg, PillMult(h));
            var rt = (RectTransform)img.transform;
            var t = Text(rt, "Text", text, fontSize > 0f ? fontSize : h * 0.56f, TextStyle.HeadingThin);
            t.overflowMode = TextOverflowModes.Overflow;
            float tw = t.GetPreferredValues(text).x;
            rt.sizeDelta = new Vector2(tw + h * 0.9f, h);
            ((RectTransform)t.transform).Stretch(0f, 0f, 0f, 2f);
            return rt;
        }
    }
}
