using UnityEngine;
using UnityEngine.EventSystems;

namespace TactileWars.UI
{
    /// <summary>Tray card press: starts a drag (or a tap to arm) in the base editor.</summary>
    public class BaseEditorCard : MonoBehaviour, IPointerDownHandler
    {
        BaseEditorRoot root;
        string id;
        public string Id => id;

        public void Init(BaseEditorRoot r, string defenseId) { root = r; id = defenseId; }

        public void OnPointerDown(PointerEventData e)
        {
            if (root != null) root.OnCardPressed(id);
        }
    }
}
