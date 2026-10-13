using CatMatch3.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    /// <summary>Cat sprite, special indicator and pointer relay; contains no board rules.</summary>
    public sealed class BoardTileView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        private BoardPrototype owner;
        private BoardPosition position;
        private Image border;
        private Image face;
        private BoardView spriteSource;
        private CanvasGroup visibility;
        private Image firstStripeBand;
        private Image secondStripeBand;
        private TileSelectionGraphic selectionOutline;
        private bool pawTarget;
        private bool selected;
        private bool matched;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize(BoardPrototype controller, BoardPosition cell, Image body, BoardView visuals)
        {
            owner = controller;
            position = cell;
            border = GetComponent<Image>();
            face = body;
            spriteSource = visuals;
            visibility = gameObject.AddComponent<CanvasGroup>();
            firstStripeBand = CreateStripeBand("Stripe edge 1");
            secondStripeBand = CreateStripeBand("Stripe edge 2");
            var selectionObject = new GameObject("Selection outline", typeof(RectTransform), typeof(TileSelectionGraphic));
            selectionObject.transform.SetParent(transform, false);
            // Above the cell background, below the cat and stripe bands so artwork stays clear.
            selectionObject.transform.SetAsFirstSibling();
            selectionOutline = selectionObject.GetComponent<TileSelectionGraphic>();
            selectionOutline.rectTransform.anchorMin = Vector2.zero;
            selectionOutline.rectTransform.anchorMax = Vector2.one;
            selectionOutline.rectTransform.offsetMin = selectionOutline.rectTransform.offsetMax = Vector2.zero;
            selectionOutline.raycastTarget = false;
            selectionOutline.enabled = false;
        }

        private Image CreateStripeBand(string name)
        {
            var bandObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            // Siblings of the padded face: bands move/fade with the tile, outside its artwork.
            bandObject.transform.SetParent(transform, false);
            var band = bandObject.GetComponent<Image>();
            band.color = new Color(1f, 0.96f, 0.65f);
            band.raycastTarget = false;
            band.enabled = false;
            var outline = bandObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.10f, 0.15f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return band;
        }

        private static void PlaceStripeBand(Image band, Vector2 min, Vector2 max)
        {
            band.rectTransform.anchorMin = min;
            band.rectTransform.anchorMax = max;
            band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
        }

        public void Show(CatType type)
        {
            Show(new Tile(type));
        }

        public void Show(Tile tile)
        {
            var type = tile.Type;
            SetVisibility(1f, 1f);
            face.sprite = spriteSource.GetCatSprite(type);
            face.color = Color.white;
            firstStripeBand.enabled = secondStripeBand.enabled = tile.IsSpecial;
            // 2.5%-thick edge bands stay outside the face's 6% padding; leave the corners clear.
            if (tile.Stripe == StripeOrientation.Vertical)
            {
                PlaceStripeBand(firstStripeBand, new Vector2(0.10f, 0.015f), new Vector2(0.90f, 0.04f));
                PlaceStripeBand(secondStripeBand, new Vector2(0.10f, 0.96f), new Vector2(0.90f, 0.985f));
            }
            else if (tile.Stripe == StripeOrientation.Horizontal)
            {
                PlaceStripeBand(firstStripeBand, new Vector2(0.015f, 0.10f), new Vector2(0.04f, 0.90f));
                PlaceStripeBand(secondStripeBand, new Vector2(0.96f, 0.10f), new Vector2(0.985f, 0.90f));
            }
            SetEmphasis(false, false);
        }

        public void SetVisibility(float alpha, float scale)
        {
            visibility.alpha = alpha;
            Rect.localScale = Vector3.one * scale;
        }

        public void SetEmphasis(bool selected, bool matched)
        {
            this.selected = selected;
            this.matched = matched;
            UpdateBorder();
        }

        public void SetPawTarget(bool marked)
        {
            if (pawTarget == marked) return;
            pawTarget = marked;
            UpdateBorder();
        }

        private void UpdateBorder()
        {
            selectionOutline.enabled = selected && !matched;
            border.color = matched ? new Color(0.24f, 1f, 0.68f) :
                new Color(0.25f, 0.30f, 0.39f);
            if (pawTarget)
            {
                var tint = border.color;
                tint.a = matched ? 0.25f : 0f;
                border.color = tint;
            }
            // Image alpha does not alter the existing rectangular hitbox (alpha test stays zero).
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
