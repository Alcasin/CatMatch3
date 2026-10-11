using System.Collections;
using System.Collections.Generic;
using System.Text;
using CatMatch3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class BoardView : MonoBehaviour
    {
        private readonly BoardTileView[,] tiles = new BoardTileView[Board.Width, Board.Height];
        private BoardPrototype owner;
        private RectTransform boardRect;
        private RectTransform canvasRect;
        private Text status;
        private Font font;
        private Vector2 lastSize;
        private float step;
        private CanvasGroup boardVisibility;
        private readonly Image[,] pawMarks = new Image[Board.Width, Board.Height];
        private Text objectiveStatus;
        private Image stripeEffect;

        public float CellSize => step;

        public void Build(BoardPrototype controller)
        {
            if (boardRect != null) return;
            owner = controller;
            canvasRect = (RectTransform)transform;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = CreateText("Title", transform, "CAT MATCH 3", 44);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(800f, 70f));
            var instructions = CreateText("Instructions", transform,
                "Tap two neighbors, or drag onto a neighbor.\nGold = selected | Mint = matched | Cyan cell = paw", 25);
            Place(instructions.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(820f, 90f));
            status = CreateText("Status", transform, "", 26);
            Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(820f, 70f));

            boardRect = CreateRect("Board", transform);
            boardVisibility = boardRect.gameObject.AddComponent<CanvasGroup>();
            boardRect.gameObject.AddComponent<RectMask2D>();
            Place(boardRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            // Cell frames are behind all movable tile roots and never move during gravity or swaps.
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var marker = CreateRect("Paw cell " + new BoardPosition(x, y), boardRect).gameObject.AddComponent<Image>();
                    marker.color = new Color(0.10f, 0.88f, 1f);
                    marker.raycastTarget = false;
                    marker.enabled = false;
                    pawMarks[x, y] = marker;
                }
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var position = new BoardPosition(x, y);
                    var tileRect = CreateRect("Tile " + position, boardRect);
                    tileRect.gameObject.AddComponent<Image>();
                    var faceRect = CreateRect("Face", tileRect);
                    Stretch(faceRect);
                    var face = faceRect.gameObject.AddComponent<Image>();
                    face.raycastTarget = false;
                    var label = CreateText("Cat", faceRect, "", 21);
                    Stretch(label.rectTransform);
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = 23;
                    var tile = tileRect.gameObject.AddComponent<BoardTileView>();
                    tile.Initialize(owner, position, face, label);
                    tiles[x, y] = tile;
                }
            }

            stripeEffect = CreateRect("Stripe clear effect", boardRect).gameObject.AddComponent<Image>();
            stripeEffect.raycastTarget = false;
            stripeEffect.enabled = false;

            var resetRect = CreateRect("Reset Board", transform);
            Place(resetRect, new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(420f, 96f));
            var resetImage = resetRect.gameObject.AddComponent<Image>();
            resetImage.color = new Color(0.25f, 0.78f, 0.64f);
            var reset = resetRect.gameObject.AddComponent<Button>();
            reset.targetGraphic = resetImage;
            reset.navigation = new Navigation { mode = Navigation.Mode.None };
            reset.onClick.AddListener(owner.ResetBoard);
            var resetLabel = CreateText("Label", resetRect, "RESET BOARD", 30);
            resetLabel.color = new Color(0.07f, 0.14f, 0.17f);
            Stretch(resetLabel.rectTransform);
            objectiveStatus = CreateText("Development objectives", transform, "", 23);
            Place(objectiveStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(820f, 90f));
            Canvas.ForceUpdateCanvases();
            Relayout();
        }

        private void LateUpdate()
        {
            if (boardRect != null && canvasRect.rect.size != lastSize)
            {
                // A resize cancels a gesture/animation instead of changing its coordinate basis mid-swap.
                Relayout();
                owner.ResetBoard();
            }
        }

        private void Relayout()
        {
            lastSize = canvasRect.rect.size;
            float side = Mathf.Max(7f, Mathf.Min(lastSize.x - 64f, lastSize.y - 540f));
            boardRect.sizeDelta = new Vector2(side, side);
            step = side / Board.Width;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    Place(pawMarks[x, y].rectTransform, new Vector2(0.5f, 0.5f),
                        CellCenter(new BoardPosition(x, y)), Vector2.one * (step - 1f));
                    Place(tiles[x, y].Rect, new Vector2(0.5f, 0.5f),
                        CellCenter(new BoardPosition(x, y)), Vector2.one * (step - 6f));
                }
        }

        public void ShowBoard(Board board)
        {
            ShowSnapshot(board.Capture());
        }

        public void ShowSnapshot(BoardSnapshot snapshot)
        {
            boardVisibility.alpha = 1f;
            boardRect.localScale = Vector3.one;
            stripeEffect.enabled = false;
            stripeEffect.rectTransform.localScale = Vector3.one;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var position = new BoardPosition(x, y);
                    tiles[x, y].Rect.anchoredPosition = CellCenter(position);
                    var tile = snapshot.GetTile(position);
                    if (tile.HasValue) tiles[x, y].Show(tile.Value);
                    else tiles[x, y].SetVisibility(0f, 1f);
                }
        }

        public void SetSelection(BoardPosition? selected)
        {
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                    tiles[x, y].SetEmphasis(selected.HasValue && selected.Value == new BoardPosition(x, y), false);
        }

        public void Highlight(MatchResult matches)
        {
            foreach (var position in matches.Cells)
                tiles[position.X, position.Y].SetEmphasis(false, true);
        }

        public void SetStatus(string message) { status.text = message; }

        public void ShowObjectives(ObjectiveSnapshot progress)
        {
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++) pawMarks[x, y].enabled = false;
            if (progress == null) { objectiveStatus.text = ""; return; }
            foreach (var cell in progress.RemainingMarks) pawMarks[cell.X, cell.Y].enabled = true;
            var text = new StringBuilder("DEV OBJECTIVES  |  ");
            foreach (var collection in progress.Collections)
                text.Append($"{collection.Type}: {collection.Collected}/{collection.Target}  ");
            text.Append($"\nPaws: {progress.ClearedPaws}/{progress.TotalPaws}");
            if (progress.IsComplete) text.Append("  |  Fulfilled - keep playing");
            objectiveStatus.text = text.ToString();
        }

        public bool TryLocalPoint(Vector2 screenPoint, out Vector2 local)
        {
            // Overlay Canvas: no camera is involved. Local axes follow the Core convention.
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPoint, null, out local);
        }

        public bool TryCell(Vector2 screenPoint, out BoardPosition position)
        {
            position = default;
            if (!TryLocalPoint(screenPoint, out var local) || !boardRect.rect.Contains(local)) return false;
            position = new BoardPosition(Mathf.FloorToInt((local.x - boardRect.rect.xMin) / step),
                Mathf.FloorToInt((local.y - boardRect.rect.yMin) / step));
            return Board.IsValidPosition(position);
        }

        public IEnumerator AnimateExchange(BoardPosition first, BoardPosition second, bool returning)
        {
            var a = tiles[first.X, first.Y].Rect;
            var b = tiles[second.X, second.Y].Rect;
            var fromA = a.anchoredPosition;
            var fromB = b.anchoredPosition;
            var toA = CellCenter(returning ? first : second);
            var toB = CellCenter(returning ? second : first);
            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                a.anchoredPosition = Vector2.Lerp(fromA, toA, fraction);
                b.anchoredPosition = Vector2.Lerp(fromB, toB, fraction);
                yield return null;
            }
            a.anchoredPosition = toA;
            b.anchoredPosition = toB;
        }

        public IEnumerator AnimateClear(IReadOnlyList<TileRemoval> removals)
        {
            float elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / 0.22f);
                foreach (var removal in removals)
                    tiles[removal.Position.X, removal.Position.Y].SetVisibility(1f - fraction, 1f - 0.7f * fraction);
                yield return null;
            }
        }

        public IEnumerator AnimateActivation(SpecialActivation activation)
        {
            foreach (var cell in activation.Cells) tiles[cell.X, cell.Y].SetEmphasis(false, true);
            bool horizontal = activation.Tile.Stripe == StripeOrientation.Horizontal;
            var center = CellCenter(activation.Position);
            Place(stripeEffect.rectTransform, new Vector2(0.5f, 0.5f),
                horizontal ? new Vector2(0f, center.y) : new Vector2(center.x, 0f),
                horizontal ? new Vector2(boardRect.rect.width, step * 0.36f) : new Vector2(step * 0.36f, boardRect.rect.height));
            stripeEffect.enabled = true;
            float elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / 0.22f);
                stripeEffect.color = new Color(0.24f, 1f, 0.68f, 0.65f * (1f - fraction));
                stripeEffect.rectTransform.localScale = horizontal
                    ? new Vector3(fraction, 1f, 1f) : new Vector3(1f, fraction, 1f);
                yield return null;
            }
            stripeEffect.enabled = false;
        }

        public IEnumerator AnimateCreation(IReadOnlyList<SpecialCreation> creations)
        {
            if (creations.Count == 0) yield break;
            float elapsed = 0f;
            while (elapsed < 0.30f)
            {
                elapsed += Time.unscaledDeltaTime;
                float scale = 1f + 0.2f * Mathf.Sin(Mathf.Clamp01(elapsed / 0.30f) * Mathf.PI);
                foreach (var creation in creations)
                {
                    var tile = tiles[creation.Position.X, creation.Position.Y];
                    tile.SetEmphasis(true, false);
                    tile.SetVisibility(1f, scale);
                }
                yield return null;
            }
            foreach (var creation in creations)
            {
                tiles[creation.Position.X, creation.Position.Y].SetVisibility(1f, 1f);
                tiles[creation.Position.X, creation.Position.Y].SetEmphasis(false, false);
            }
        }

        public IEnumerator AnimateGravity(IReadOnlyList<TileMovement> movements)
        {
            if (movements.Count == 0) yield break;
            float elapsed = 0f;
            while (elapsed < 0.30f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.30f));
                foreach (var movement in movements)
                    tiles[movement.From.X, movement.From.Y].Rect.anchoredPosition =
                        Vector2.Lerp(CellCenter(movement.From), CellCenter(movement.To), fraction);
                yield return null;
            }
        }

        public IEnumerator AnimateRefill(IReadOnlyList<TileSpawn> spawns)
        {
            foreach (var spawn in spawns) tiles[spawn.Position.X, spawn.Position.Y].Show(spawn.Tile);
            float elapsed = 0f;
            while (elapsed < 0.38f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.38f));
                foreach (var spawn in spawns)
                    tiles[spawn.Position.X, spawn.Position.Y].Rect.anchoredPosition =
                        Vector2.Lerp(CellCenter(spawn.EntryPosition), CellCenter(spawn.Position), fraction);
                yield return null;
            }
        }

        public IEnumerator AnimateReshuffle(ReshuffleResult result)
        {
            ShowSnapshot(result.Before);
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / 0.25f);
                boardVisibility.alpha = 1f - fraction;
                boardRect.localScale = Vector3.one * (1f - 0.12f * fraction);
                yield return null;
            }
            ShowSnapshot(result.After);
            // Change layout while hidden, then bring the verified arrangement back into view.
            boardVisibility.alpha = 0f;
            elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / 0.25f);
                boardVisibility.alpha = fraction;
                boardRect.localScale = Vector3.one * (0.88f + 0.12f * fraction);
                yield return null;
            }
        }

        private Vector2 CellCenter(BoardPosition position)
        {
            return new Vector2((position.X + 0.5f) * step - boardRect.rect.width * 0.5f,
                (position.Y + 0.5f) * step - boardRect.rect.height * 0.5f);
        }

        private Text CreateText(string name, Transform parent, string value, int size)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = new Color(0.94f, 0.95f, 0.98f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
