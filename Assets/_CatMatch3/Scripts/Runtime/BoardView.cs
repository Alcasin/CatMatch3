using System;
using System.Collections;
using System.Collections.Generic;
using CatMatch3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private Sprite orangeCatSprite;
        [SerializeField] private Sprite blackCatSprite;
        [SerializeField] private Sprite whiteCatSprite;
        [SerializeField] private Sprite grayCatSprite;
        [SerializeField] private Sprite siameseCatSprite;

        private readonly BoardTileView[,] tiles = new BoardTileView[Board.Width, Board.Height];
        private BoardPrototype owner;
        private RectTransform boardRect;
        private RectTransform canvasRect;
        private Text status;
        private Font font;
        private Vector2 lastSize;
        private float step;
        private CanvasGroup boardVisibility;
        private readonly PawTargetGraphic[,] pawMarks = new PawTargetGraphic[Board.Width, Board.Height];
        private Image stripeEffect;
        private Text comboFeedback;
        private Text levelNumber;
        private Text levelTitle;
        private Text movesLabel;
        private Text moves;
        private Text tutorial;
        private GameObject resultPanel;
        private Text resultTitle;
        private Text resultMessage;
        private Text resultButtonLabel;
        private RectTransform resultSafeArea;
        private RectTransform resultCard;
        private RectTransform resultBadge;
        private RectTransform resultAction;
        private Image resultCat;
        private Button resultButton;
        private Rect lastSafeArea;
        private RectTransform header;
        private RectTransform movesPanel;
        private RectTransform goalsRoot;
        private Text goalsLabel;
        private RectTransform boardFrame;
        private RectTransform restartRect;
        private Text restartLabel;
        private readonly List<ObjectiveCard> objectiveCards = new List<ObjectiveCard>();
        private int activeObjectives;
        private bool diagnosticHud;
        private string lastStatus = "";
        private Texture2D panelTexture;
        private Sprite panelSprite;
        private static readonly Color Ink = new Color32(0x42, 0x5B, 0x6D, 255);
        private static readonly Color Cream = new Color32(0xFF, 0xF8, 0xEE, 255);
        private static readonly Color Blue = new Color32(0xBB, 0xD3, 0xDC, 255);

        private sealed class ObjectiveCard
        {
            public RectTransform Root, IconWell;
            public Image Icon;
            public Text Symbol, Caption, Count;
        }

        // Pure geometry shared by relayout and standalone checks; values are in safe-area UI units.
        private struct HudLayout
        {
            public float Scale, Margin, Width, HeaderHeight, CardsTop, CardHeight, GoalsTop,
                TutorialTop, BoardSide, BoardY, RestartY, StatusY;
            public int Columns, Rows;
        }

        private struct ResultContent
        {
            public string Title, Message, Action;
            public bool Success;
        }

        private static ResultContent GetResultContent(GameplayState state, GameplayFailure failure, bool finalLevel)
        {
            bool win = state == GameplayState.Win;
            return new ResultContent
            {
                Success = win,
                Title = win ? (finalLevel ? "Game Complete!" : "Level Complete!") : "Try Again!",
                Message = win ? "Purrfect!" : failure == GameplayFailure.NoLegalMoves
                    ? "No more matches available" : "Out of Moves",
                Action = win ? (finalLevel ? "PLAY AGAIN" : "NEXT LEVEL") : "RETRY"
            };
        }

        private struct ResultLayout
        {
            public float Scale;
            public Rect Panel, Badge, Title, Message, Action;
        }

        private static ResultLayout CalculateResultLayout(float width, float height)
        {
            float s = Mathf.Min(width / 900f, height / 1200f);
            return new ResultLayout
            {
                Scale = s,
                Panel = new Rect(-360f * s, -320f * s, 720f * s, 640f * s),
                Badge = new Rect(-60f * s, 146f * s, 120f * s, 120f * s),
                Title = new Rect(-312f * s, 18f * s, 624f * s, 104f * s),
                Message = new Rect(-312f * s, -72f * s, 624f * s, 80f * s),
                Action = new Rect(-312f * s, -260f * s, 624f * s, 128f * s)
            };
        }

        private static HudLayout CalculateLayout(float width, float height, int objectiveCount)
        {
            float scale = Mathf.Min(width / 900f, height / 1400f);
            float margin = 27f * scale;
            int columns = Mathf.Clamp(objectiveCount, 1, 3);
            int rows = Mathf.Max(1, (objectiveCount + columns - 1) / columns);
            float goalsTop = (24f + 144f + 18f) * scale;
            float cardsTop = goalsTop + 32f * scale;
            float tutorialTop = cardsTop + (rows * 104f + (rows - 1) * 12f + 12f) * scale;
            float boardTop = tutorialTop + 52f * scale;
            float footerHeight = 244f * scale;
            float frameSide = Mathf.Max(1f, Mathf.Min(width - 2f * margin, height - boardTop - footerHeight));
            return new HudLayout
            {
                Scale = scale, Margin = margin, Width = width - 2f * margin, HeaderHeight = 144f * scale,
                Columns = columns, Rows = rows, GoalsTop = goalsTop, CardsTop = cardsTop,
                CardHeight = 104f * scale, TutorialTop = tutorialTop,
                BoardSide = Mathf.Max(1f, frameSide - 16f * scale),
                BoardY = (footerHeight - boardTop) * 0.5f,
                RestartY = (24f + 64f) * scale, StatusY = (24f + 128f + 16f + 28f) * scale
            };
        }

        public float CellSize => step;

        public Sprite GetCatSprite(CatType type)
        {
            Sprite sprite;
            switch (type)
            {
                case CatType.Orange: sprite = orangeCatSprite; break;
                case CatType.Black: sprite = blackCatSprite; break;
                case CatType.White: sprite = whiteCatSprite; break;
                case CatType.Gray: sprite = grayCatSprite; break;
                case CatType.Siamese: sprite = siameseCatSprite; break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown cat visual.");
            }
            if (sprite == null)
                throw new InvalidOperationException($"Missing {type} cat sprite. Run CatMatch3 > Setup > Apply M5-A Cat Sprites.");
            return sprite;
        }

        public void ValidateCatSprites()
        {
            foreach (CatType type in Enum.GetValues(typeof(CatType))) GetCatSprite(type);
        }

        public void Build(BoardPrototype controller)
        {
            if (boardRect != null) return;
            ValidateCatSprites();
            owner = controller;
            BuildPanelSprite();
            var background = CreateRect("Mint background", transform);
            Stretch(background);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color32(0xE7, 0xF1, 0xEC, 255);
            backgroundImage.raycastTarget = false;
            canvasRect = CreateRect("Safe area", transform);
            ApplySafeArea();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            header = CreatePanel("Level header", canvasRect, Cream, true);
            levelNumber = CreateHudText("Level number", header, "", 26, true);
            levelNumber.alignment = TextAnchor.MiddleLeft;
            levelTitle = CreateHudText("Level title", header, "", 44, true);
            levelTitle.alignment = TextAnchor.MiddleLeft;
            movesPanel = CreatePanel("Moves panel", header, Blue);
            movesLabel = CreateHudText("Moves label", movesPanel, "MOVES", 24, true);
            moves = CreateHudText("Moves remaining", movesPanel, "", 64, true);
            goalsLabel = CreateHudText("Goals label", canvasRect, "GOALS", 24, true);
            goalsLabel.alignment = TextAnchor.MiddleLeft;
            goalsRoot = CreateRect("Objective cards", canvasRect);
            tutorial = CreateHudText("Tutorial", canvasRect, "", 26);
            status = CreateHudText("Diagnostic status", canvasRect, "", 24);
            status.gameObject.SetActive(false);

            boardFrame = CreatePanel("Board surround", canvasRect, Blue, true);
            var frameFill = CreatePanel("Cream inset", boardFrame, Cream);
            Stretch(frameFill);
            frameFill.offsetMin = Vector2.one * 3f;
            frameFill.offsetMax = Vector2.one * -3f;

            boardRect = CreateRect("Board", canvasRect);
            boardVisibility = boardRect.gameObject.AddComponent<CanvasGroup>();
            boardRect.gameObject.AddComponent<RectMask2D>();
            Place(boardRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), Vector2.one);
            // Cell frames are behind all movable tile roots and never move during gravity or swaps.
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var marker = CreateRect("Paw cell " + new BoardPosition(x, y), boardRect).gameObject.AddComponent<PawTargetGraphic>();
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
                    // Proportional padding keeps the complete sprite inside the square hitbox.
                    faceRect.anchorMin = Vector2.one * 0.06f;
                    faceRect.anchorMax = Vector2.one * 0.94f;
                    faceRect.offsetMin = faceRect.offsetMax = Vector2.zero;
                    var face = faceRect.gameObject.AddComponent<Image>();
                    face.raycastTarget = false;
                    face.type = Image.Type.Simple;
                    face.preserveAspect = true;
                    face.useSpriteMesh = false;
                    face.color = Color.white;
                    var tile = tileRect.gameObject.AddComponent<BoardTileView>();
                    tile.Initialize(owner, position, face, this);
                    tiles[x, y] = tile;
                }
            }

            stripeEffect = CreateRect("Stripe clear effect", boardRect).gameObject.AddComponent<Image>();
            stripeEffect.raycastTarget = false;
            stripeEffect.enabled = false;

            comboFeedback = CreateText("Cascade feedback", boardRect, "Combo!", 28);
            comboFeedback.fontStyle = FontStyle.Bold;
            comboFeedback.color = Cream;
            var comboOutline = comboFeedback.gameObject.AddComponent<Outline>();
            comboOutline.effectColor = new Color(0.26f, 0.36f, 0.43f, 0.6f);
            comboOutline.effectDistance = new Vector2(1f, -1f);
            comboFeedback.enabled = false;

            restartRect = CreatePanel("Restart current level", canvasRect, new Color32(0xE8, 0xAD, 0x67, 255));
            var resetImage = restartRect.GetComponent<Image>();
            resetImage.raycastTarget = true;
            var reset = restartRect.gameObject.AddComponent<Button>();
            reset.targetGraphic = resetImage;
            reset.navigation = new Navigation { mode = Navigation.Mode.None };
            var buttonColors = reset.colors;
            buttonColors.highlightedColor = new Color(1f, 0.97f, 0.92f);
            buttonColors.selectedColor = buttonColors.highlightedColor;
            buttonColors.pressedColor = new Color(0.84f, 0.82f, 0.78f);
            buttonColors.fadeDuration = 0.1f;
            reset.colors = buttonColors;
            reset.onClick.AddListener(owner.ResetBoard);
            restartLabel = CreateHudText("Label", restartRect, "RESTART", 34, true);
            Stretch(restartLabel.rectTransform);
            BuildResultPanel();
            Canvas.ForceUpdateCanvases();
            Relayout();
        }

        private void LateUpdate()
        {
            if (boardRect != null && (canvasRect.rect.size != lastSize || Screen.safeArea != lastSafeArea))
            {
                // A resize cancels a gesture/animation instead of changing its coordinate basis mid-swap.
                ApplySafeArea();
                Canvas.ForceUpdateCanvases();
                Relayout();
                owner.ResetBoard();
            }
        }

        private void Relayout()
        {
            lastSize = canvasRect.rect.size;
            var layout = CalculateLayout(lastSize.x, lastSize.y, activeObjectives);
            float s = layout.Scale;
            PlaceFromTop(header, layout.Margin, 24f * s, layout.Width, layout.HeaderHeight);
            float movesWidth = 156f * s;
            float textWidth = layout.Width - movesWidth - 72f * s;
            Place(levelNumber.rectTransform, new Vector2(0f, 1f), new Vector2(24f * s + textWidth / 2f, -36f * s),
                new Vector2(textWidth, 32f * s));
            Place(levelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(24f * s + textWidth / 2f, -89f * s),
                new Vector2(textWidth, 66f * s));
            Place(movesPanel, new Vector2(1f, 0.5f), new Vector2(-18f * s - movesWidth / 2f, 0f),
                new Vector2(movesWidth, 112f * s));
            Place(movesLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -23f * s), new Vector2(140f * s, 28f * s));
            Place(moves.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -73f * s), new Vector2(140f * s, 70f * s));
            PlaceFromTop(goalsLabel.rectTransform, layout.Margin + 4f * s, layout.GoalsTop, layout.Width - 8f * s, 24f * s);
            PlaceFromTop(goalsRoot, layout.Margin, layout.CardsTop, layout.Width,
                layout.Rows * layout.CardHeight + (layout.Rows - 1) * 12f * s);
            float cardWidth = (layout.Width - (layout.Columns - 1) * 12f * s) / Mathf.Max(2, layout.Columns);
            for (int i = 0; i < activeObjectives; i++)
            {
                var card = objectiveCards[i];
                int row = i / layout.Columns;
                int rowCount = Mathf.Min(layout.Columns, activeObjectives - row * layout.Columns);
                float rowWidth = rowCount * cardWidth + (rowCount - 1) * 12f * s;
                float x = (layout.Width - rowWidth) / 2f + (i % layout.Columns) * (cardWidth + 12f * s);
                PlaceFromTop(card.Root, x, row * (layout.CardHeight + 12f * s), cardWidth, layout.CardHeight);
                Place(card.IconWell, new Vector2(0f, 0.5f), new Vector2(52f * s, 0f), Vector2.one * 72f * s);
                float countWidth = cardWidth - 116f * s;
                Place(card.Caption.rectTransform, new Vector2(0f, 1f), new Vector2(100f * s + countWidth / 2f, -28f * s),
                    new Vector2(countWidth, 26f * s));
                Place(card.Count.rectTransform, new Vector2(0f, 1f), new Vector2(100f * s + countWidth / 2f, -67f * s),
                    new Vector2(countWidth, 48f * s));
                FitHudText(card.Caption, 22, s);
                FitHudText(card.Count, 40, s);
                FitHudText(card.Symbol, 20, s);
            }
            PlaceFromTop(tutorial.rectTransform, layout.Margin, layout.TutorialTop, layout.Width, 32f * s);
            Place(restartRect, new Vector2(0.5f, 0f), new Vector2(0f, layout.RestartY), new Vector2(320f, 128f) * s);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, layout.StatusY), new Vector2(layout.Width, 56f * s));
            FitHudText(levelNumber, 26, s);
            FitHudText(levelTitle, 44, s);
            FitHudText(movesLabel, 24, s);
            FitHudText(moves, 64, s);
            FitHudText(goalsLabel, 24, s);
            FitHudText(tutorial, 26, s);
            FitHudText(status, 24, s);
            FitHudText(restartLabel, 34, s);
            float side = layout.BoardSide;
            Place(boardRect, new Vector2(0.5f, 0.5f), new Vector2(0f, layout.BoardY), Vector2.one * side);
            Place(boardFrame, new Vector2(0.5f, 0.5f), new Vector2(0f, layout.BoardY), Vector2.one * (side + 16f * s));
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
            RelayoutResult();
        }

        public void ShowBoard(Board board)
        {
            ShowSnapshot(board.Capture());
        }

        public void ShowSnapshot(BoardSnapshot snapshot)
        {
            boardVisibility.alpha = 1f;
            boardRect.localScale = Vector3.one;
            ClearTransientFeedback();
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

        public void SetStatus(string message)
        {
            lastStatus = message;
            status.text = diagnosticHud ? message : message.StartsWith("ERROR", StringComparison.Ordinal)
                ? "Unable to continue. Please restart." : "";
            status.gameObject.SetActive(status.text.Length > 0);
        }

        public void ShowObjectives(ObjectiveSnapshot progress)
        {
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++) pawMarks[x, y].enabled = false;
            int count = progress == null ? 0 : progress.Collections.Count + (progress.TotalPaws > 0 ? 1 : 0);
            while (objectiveCards.Count < count) objectiveCards.Add(BuildObjectiveCard());
            for (int i = 0; i < objectiveCards.Count; i++) objectiveCards[i].Root.gameObject.SetActive(i < count);
            if (progress != null)
            {
                foreach (var cell in progress.RemainingMarks) pawMarks[cell.X, cell.Y].enabled = true;
                for (int i = 0; i < progress.Collections.Count; i++)
                {
                    var collection = progress.Collections[i];
                    var card = objectiveCards[i];
                    card.Icon.sprite = GetCatSprite(collection.Type);
                    card.Icon.enabled = true;
                    card.Symbol.gameObject.SetActive(false);
                    card.Caption.text = collection.Type.ToString().ToUpperInvariant();
                    card.Count.text = $"{Mathf.Min(collection.Collected, collection.Target)} / {collection.Target}";
                }
                if (progress.TotalPaws > 0)
                {
                    var card = objectiveCards[count - 1];
                    card.Icon.enabled = false;
                    card.Symbol.gameObject.SetActive(true);
                    card.Caption.text = "PAWS";
                    card.Count.text = $"{progress.ClearedPaws} / {progress.TotalPaws}";
                }
            }
            // Reveal the fixed target background through the moving tile's otherwise opaque frame.
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++) tiles[x, y].SetPawTarget(pawMarks[x, y].enabled);
            if (activeObjectives != count)
            {
                activeObjectives = count;
                Relayout();
            }
        }

        public void ShowLevel(int number, string title, int? remainingMoves, bool showTutorial, bool diagnostic)
        {
            diagnosticHud = diagnostic;
            levelNumber.text = diagnostic ? "DIAGNOSTIC" : $"LEVEL {number:00}";
            levelTitle.text = title;
            moves.text = remainingMoves.HasValue ? remainingMoves.Value.ToString() : "∞";
            tutorial.text = showTutorial ? "Swap adjacent cats to match 3!" : "";
            SetStatus(lastStatus);
        }

        private ObjectiveCard BuildObjectiveCard()
        {
            var card = new ObjectiveCard { Root = CreatePanel("Objective card", goalsRoot, Cream) };
            card.IconWell = CreatePanel("Icon background", card.Root, Blue);
            var iconRect = CreateRect("Cat icon", card.IconWell);
            Stretch(iconRect);
            iconRect.offsetMin = Vector2.one * 4f;
            iconRect.offsetMax = Vector2.one * -4f;
            card.Icon = iconRect.gameObject.AddComponent<Image>();
            card.Icon.preserveAspect = true;
            card.Icon.raycastTarget = false;
            card.Symbol = CreateHudText("Temporary paw label", card.IconWell, "PAW", 20, true);
            Stretch(card.Symbol.rectTransform);
            card.Caption = CreateHudText("Objective name", card.Root, "", 22, true);
            card.Caption.alignment = TextAnchor.MiddleLeft;
            card.Count = CreateHudText("Progress", card.Root, "", 40, true);
            card.Count.alignment = TextAnchor.MiddleLeft;
            return card;
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color, bool shadow = false)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = panelSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            if (shadow)
            {
                var depth = rect.gameObject.AddComponent<Shadow>();
                depth.effectColor = new Color(0.26f, 0.36f, 0.43f, 0.12f);
                depth.effectDistance = new Vector2(0f, -4f);
            }
            return rect;
        }

        private Text CreateHudText(string name, Transform parent, string value, int size, bool bold = false)
        {
            var text = CreateText(name, parent, value, size);
            text.color = Ink;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.resizeTextForBestFit = true;
            return text;
        }

        private static void FitHudText(Text text, int size, float scale)
        {
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale));
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(size * scale * 0.6f));
        }

        private static void PlaceFromTop(RectTransform rect, float x, float y, float width, float height)
        {
            Place(rect, new Vector2(0f, 1f), new Vector2(x + width / 2f, -y - height / 2f), new Vector2(width, height));
        }

        private void BuildPanelSprite()
        {
            const int size = 64;
            const float radius = 16f;
            panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "HUD rounded panel", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            panelTexture.SetPixels(pixels);
            panelTexture.Apply(false, true);
            panelSprite = Sprite.Create(panelTexture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect, Vector4.one * radius);
            panelSprite.name = "HUD rounded panel";
            panelSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDestroy()
        {
            if (panelSprite != null) Destroy(panelSprite);
            if (panelTexture != null) Destroy(panelTexture);
        }

        public void HideResult() { resultPanel.SetActive(false); }
        public void ShowResult(GameplayState state, GameplayFailure failure, bool finalLevel)
        {
            ClearTransientFeedback();
            var content = GetResultContent(state, failure, finalLevel);
            resultTitle.text = content.Title;
            resultMessage.text = content.Message;
            resultButtonLabel.text = content.Action;
            resultBadge.GetComponent<Image>().color = content.Success ? (Color)new Color32(0xF7, 0xE1, 0xC3, 255) : Blue;
            resultCat.sprite = GetCatSprite(content.Success ? CatType.Orange : CatType.Gray);
            resultButton.interactable = true;
            resultPanel.transform.SetAsLastSibling();
            RelayoutResult();
            resultPanel.SetActive(true);
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            canvasRect.anchorMin = new Vector2(lastSafeArea.xMin / Mathf.Max(1, Screen.width), lastSafeArea.yMin / Mathf.Max(1, Screen.height));
            canvasRect.anchorMax = new Vector2(lastSafeArea.xMax / Mathf.Max(1, Screen.width), lastSafeArea.yMax / Mathf.Max(1, Screen.height));
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
        }

        private void BuildResultPanel()
        {
            // Full-canvas scrim blocks background raycasts, including outside the safe area.
            var overlay = CreateRect("Result overlay", transform);
            Stretch(overlay);
            resultPanel = overlay.gameObject;
            var scrim = overlay.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.16f, 0.23f, 0.28f, 0.42f);
            scrim.raycastTarget = true;
            resultSafeArea = CreateRect("Result safe area", overlay);
            resultCard = CreatePanel("Cream result card", resultSafeArea, Cream, true);
            resultBadge = CreatePanel("Cat portrait background", resultCard, Blue);
            var catRect = CreateRect("Cat portrait", resultBadge);
            Stretch(catRect);
            catRect.offsetMin = Vector2.one * 8f;
            catRect.offsetMax = Vector2.one * -8f;
            resultCat = catRect.gameObject.AddComponent<Image>();
            resultCat.preserveAspect = true;
            resultCat.raycastTarget = false;
            resultTitle = CreateHudText("Result title", resultCard, "", 56, true);
            resultMessage = CreateHudText("Result message", resultCard, "", 32);
            resultMessage.color = new Color32(0x71, 0x86, 0x96, 255);
            resultAction = CreatePanel("Result action", resultCard, new Color32(0xE8, 0xAD, 0x67, 255));
            var image = resultAction.GetComponent<Image>();
            image.raycastTarget = true;
            resultButton = resultAction.gameObject.AddComponent<Button>();
            resultButton.targetGraphic = image;
            resultButton.navigation = new Navigation { mode = Navigation.Mode.None };
            resultButton.colors = restartRect.GetComponent<Button>().colors;
            resultButton.onClick.AddListener(HandleResultAction);
            resultButtonLabel = CreateHudText("Action label", resultAction, "", 36, true);
            Stretch(resultButtonLabel.rectTransform);
            resultPanel.SetActive(false);
        }

        private void HandleResultAction()
        {
            if (!resultPanel.activeSelf || !resultButton.interactable) return;
            resultButton.interactable = false;
            owner.ResultAction();
        }

        private void RelayoutResult()
        {
            resultSafeArea.anchorMin = canvasRect.anchorMin;
            resultSafeArea.anchorMax = canvasRect.anchorMax;
            resultSafeArea.offsetMin = resultSafeArea.offsetMax = Vector2.zero;
            var layout = CalculateResultLayout(canvasRect.rect.width, canvasRect.rect.height);
            PlaceResultPart(resultCard, layout.Panel);
            PlaceResultPart(resultBadge, layout.Badge);
            PlaceResultPart(resultTitle.rectTransform, layout.Title);
            PlaceResultPart(resultMessage.rectTransform, layout.Message);
            PlaceResultPart(resultAction, layout.Action);
            resultCat.rectTransform.offsetMin = Vector2.one * (8f * layout.Scale);
            resultCat.rectTransform.offsetMax = Vector2.one * (-8f * layout.Scale);
            FitHudText(resultTitle, 56, layout.Scale);
            FitHudText(resultMessage, 32, layout.Scale);
            FitHudText(resultButtonLabel, 36, layout.Scale);
        }

        private static void PlaceResultPart(RectTransform rect, Rect bounds)
        {
            Place(rect, Vector2.one * 0.5f, bounds.center, bounds.size);
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

        public void ClearTransientFeedback()
        {
            if (stripeEffect != null)
            {
                stripeEffect.enabled = false;
                stripeEffect.rectTransform.localScale = Vector3.one;
            }
            if (comboFeedback != null) comboFeedback.enabled = false;
        }

        private static float DropEase(float fraction)
        {
            float remaining = 1f - Mathf.Clamp01(fraction);
            return 1f - remaining * remaining * remaining;
        }

        // Alpha and scale share the existing clear slot: quick small pop, then soft disappearance.
        private static Vector2 ClearPose(float fraction, bool cascade)
        {
            float t = Mathf.Clamp01(fraction);
            float peak = cascade ? 1.08f : 1.05f;
            if (t < 0.25f) return new Vector2(1f, Mathf.Lerp(1f, peak, Mathf.SmoothStep(0f, 1f, t / 0.25f)));
            float fade = Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.75f);
            return new Vector2(1f - fade, Mathf.Lerp(peak, 0.82f, fade));
        }

        private static Rect ActivationBounds(SpecialActivation activation, float side, float cellSize, float reach)
        {
            var origin = new Vector2((activation.Position.X + 0.5f) * cellSize - side * 0.5f,
                (activation.Position.Y + 0.5f) * cellSize - side * 0.5f);
            bool horizontal = activation.Tile.Stripe == StripeOrientation.Horizontal;
            float from = Mathf.Lerp(horizontal ? origin.x : origin.y, -side * 0.5f, reach);
            float to = Mathf.Lerp(horizontal ? origin.x : origin.y, side * 0.5f, reach);
            float thickness = cellSize * 0.18f;
            return horizontal ? new Rect(from, origin.y - thickness * 0.5f, to - from, thickness)
                : new Rect(origin.x - thickness * 0.5f, from, thickness, to - from);
        }

        public IEnumerator AnimateClear(IReadOnlyList<TileRemoval> removals, bool cascade = false)
        {
            bool showCombo = cascade && removals.Count > 0;
            Vector2 comboOrigin = showCombo ? CellCenter(removals[0].Position) : Vector2.zero;
            if (showCombo)
            {
                Place(comboFeedback.rectTransform, Vector2.one * 0.5f, comboOrigin, new Vector2(step, step * 0.32f));
                comboFeedback.fontSize = Mathf.Max(1, Mathf.RoundToInt(step * 0.24f));
                comboFeedback.enabled = true;
            }
            float elapsed = 0f;
            try
            {
                while (elapsed < 0.22f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float fraction = Mathf.Clamp01(elapsed / 0.22f);
                    var pose = ClearPose(fraction, cascade);
                    for (int i = 0; i < removals.Count; i++)
                    {
                        var cell = removals[i].Position;
                        tiles[cell.X, cell.Y].SetVisibility(pose.x, pose.y);
                    }
                    if (showCombo)
                    {
                        var tint = Cream;
                        tint.a = Mathf.Sin(fraction * Mathf.PI);
                        comboFeedback.color = tint;
                        comboFeedback.rectTransform.anchoredPosition = comboOrigin + Vector2.up * (step * (0.12f + 0.12f * fraction));
                    }
                    yield return null;
                }
            }
            finally { if (comboFeedback != null) comboFeedback.enabled = false; }
        }

        public IEnumerator AnimateActivation(SpecialActivation activation)
        {
            foreach (var cell in activation.Cells) tiles[cell.X, cell.Y].SetEmphasis(false, true);
            var bounds = ActivationBounds(activation, boardRect.rect.width, step, 0f);
            Place(stripeEffect.rectTransform, Vector2.one * 0.5f, bounds.center, bounds.size);
            stripeEffect.rectTransform.localScale = Vector3.one;
            stripeEffect.enabled = true;
            float elapsed = 0f;
            try
            {
                while (elapsed < 0.22f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float fraction = Mathf.Clamp01(elapsed / 0.22f);
                    bounds = ActivationBounds(activation, boardRect.rect.width, step, DropEase(fraction / 0.7f));
                    stripeEffect.rectTransform.anchoredPosition = bounds.center;
                    stripeEffect.rectTransform.sizeDelta = bounds.size;
                    stripeEffect.color = new Color(0.45f, 0.85f, 0.86f, 0.32f * Mathf.Sin(fraction * Mathf.PI));
                    yield return null;
                }
            }
            finally { if (stripeEffect != null) stripeEffect.enabled = false; }
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
                    // Creation keeps its scale pulse without showing a player selection outline.
                    tile.SetEmphasis(false, false);
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
                float fraction = DropEase(elapsed / 0.30f);
                for (int i = 0; i < movements.Count; i++)
                {
                    var movement = movements[i];
                    tiles[movement.From.X, movement.From.Y].Rect.anchoredPosition =
                        Vector2.Lerp(CellCenter(movement.From), CellCenter(movement.To), fraction);
                }
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
                float fraction = DropEase(elapsed / 0.38f);
                for (int i = 0; i < spawns.Count; i++)
                {
                    var spawn = spawns[i];
                    tiles[spawn.Position.X, spawn.Position.Y].Rect.anchoredPosition =
                        Vector2.Lerp(CellCenter(spawn.EntryPosition), CellCenter(spawn.Position), fraction);
                }
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
