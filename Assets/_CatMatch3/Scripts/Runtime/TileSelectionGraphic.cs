using UnityEngine;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    /// <summary>Transparent rounded selection frame in the existing cell gutter.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TileSelectionGraphic : MaskableGraphic
    {
        private static readonly Color32 Crimson = new Color32(0x8B, 0x1E, 0x2D, 255);

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            Populate(vertices, GetPixelAdjustedRect());
        }

        private static void Populate(VertexHelper vertices, Rect bounds)
        {
            vertices.Clear();
            float side = Mathf.Min(bounds.width, bounds.height);
            if (side <= 0f) return;
            // Roughly 2.5 reference pixels at the production cell size; scales with the board.
            float stroke = Mathf.Min(3f, side * 0.023f);
            // Tile roots are step - 6 and fixed paw cells are step - 1: this 2.5-unit
            // expansion aligns the selection with the target rim without changing either rect.
            const float inset = -2.5f;
            float radius = side * 0.075f;
            var outer = new Rect(bounds.xMin + inset, bounds.yMin + inset,
                bounds.width - 2f * inset, bounds.height - 2f * inset);
            const int segments = 6;
            const int points = 4 * (segments + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? outer.xMax - radius : outer.xMin + radius,
                    corner < 2 ? outer.yMax - radius : outer.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertices.AddVert(center + direction * radius, Crimson, Vector2.zero);
                    vertices.AddVert(center + direction * (radius - stroke), Crimson, Vector2.zero);
                }
            }
            // Connect only the perimeter; the interior contains no fill geometry.
            for (int i = 0; i < points; i++)
            {
                int current = 2 * i;
                int next = 2 * ((i + 1) % points);
                vertices.AddTriangle(current, next, next + 1);
                vertices.AddTriangle(current, next + 1, current + 1);
            }
        }
    }
}
