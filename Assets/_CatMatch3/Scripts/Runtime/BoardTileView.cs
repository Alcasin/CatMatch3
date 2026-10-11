using CatMatch3.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    /// <summary>Temporary tile visual and pointer relay; contains no board rules.</summary>
    public sealed class BoardTileView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        private BoardPrototype owner;
        private BoardPosition position;
        private Image border;
        private Image face;
        private Text label;
        private CanvasGroup visibility;
        private Image stripe;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize(BoardPrototype controller, BoardPosition cell, Image body, Text text)
        {
            owner = controller;
            position = cell;
            border = GetComponent<Image>();
            face = body;
            label = text;
            visibility = gameObject.AddComponent<CanvasGroup>();
            var stripeObject = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            stripeObject.transform.SetParent(face.transform, false);
            stripe = stripeObject.GetComponent<Image>();
            stripe.raycastTarget = false;
            stripe.enabled = false;
        }

        public void Show(CatType type)
        {
            Show(new Tile(type));
        }

        public void Show(Tile tile)
        {
            var type = tile.Type;
            SetVisibility(1f, 1f);
            Color[] colors =
            {
                new Color(1f, 0.58f, 0.22f), new Color(0.17f, 0.16f, 0.23f),
                new Color(0.94f, 0.95f, 0.98f), new Color(0.53f, 0.60f, 0.69f),
                new Color(0.81f, 0.70f, 0.52f)
            };
            face.color = colors[(int)type];
            label.color = type == CatType.Black ? Color.white : new Color(0.10f, 0.12f, 0.17f);
            label.text = type.ToString().ToUpperInvariant() + "\n" + position;
            stripe.enabled = tile.IsSpecial;
            stripe.color = type == CatType.Black ? Color.white : new Color(0.12f, 0.10f, 0.20f);
            var stripeRect = stripe.rectTransform;
            bool horizontal = tile.Stripe == StripeOrientation.Horizontal;
            stripeRect.anchorMin = horizontal ? new Vector2(0f, 0.15f) : new Vector2(0.85f, 0f);
            stripeRect.anchorMax = horizontal ? new Vector2(1f, 0.15f) : new Vector2(0.85f, 1f);
            stripeRect.anchoredPosition = Vector2.zero;
            stripeRect.sizeDelta = horizontal ? new Vector2(-12f, 10f) : new Vector2(10f, -12f);
            SetEmphasis(false, false);
        }

        public void SetVisibility(float alpha, float scale)
        {
            visibility.alpha = alpha;
            Rect.localScale = Vector3.one * scale;
        }

        public void SetEmphasis(bool selected, bool matched)
        {
            border.color = matched ? new Color(0.24f, 1f, 0.68f) :
                selected ? new Color(1f, 0.85f, 0.20f) : new Color(0.25f, 0.30f, 0.39f);
            float inset = selected || matched ? 7f : 3f;
            face.rectTransform.offsetMin = Vector2.one * inset;
            face.rectTransform.offsetMax = Vector2.one * -inset;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
                owner.BeginPointer(position, data.pointerId, data.position);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
                owner.EndPointer(data.pointerId, data.position);
        }

        public void OnDrag(PointerEventData data) { owner.DragPointer(data.pointerId, data.position); }
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
    }
}
