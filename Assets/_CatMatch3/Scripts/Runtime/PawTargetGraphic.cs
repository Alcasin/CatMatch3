using UnityEngine;
using UnityEngine.UI;

namespace CatMatch3.Runtime
{
    /// <summary>Fixed-cell honey target and paw motif, drawn behind movable cat tiles.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PawTargetGraphic : MaskableGraphic
    {
        private static readonly Color32 Border = new Color32(0xAD, 0x78, 0x3E, 255);
        private static readonly Color32 Honey = new Color32(0xF1, 0xCC, 0x83, 255);
        private static readonly Color32 Paw = new Color32(0x80, 0x53, 0x2A, 255);

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            Populate(vertices, GetPixelAdjustedRect());
        }

        private static void Populate(VertexHelper vertices, Rect bounds)
        {
            vertices.Clear();
            float side = Mathf.Min(bounds.width, bounds.height);
            if (side <= 0f) return;
            float stroke = side * 0.022f;
            RoundedRect(vertices, bounds, side * 0.08f, Border);
            var fill = new Rect(bounds.xMin + stroke, bounds.yMin + stroke,
                bounds.width - 2f * stroke, bounds.height - 2f * stroke);
            RoundedRect(vertices, fill, side * 0.06f, Honey);

            // A small corner paw remains in the cat sprite's transparent lower-right margin.
            // Four toe pads and one broad palm are part of this fixed cell, never the tile.
            var origin = new Vector2(bounds.xMin, bounds.yMin);
            Ellipse(vertices, origin + new Vector2(0.87f, 0.095f) * side, new Vector2(0.060f, 0.047f) * side, Paw);
            Ellipse(vertices, origin + new Vector2(0.785f, 0.155f) * side, new Vector2(0.027f, 0.034f) * side, Paw);
            Ellipse(vertices, origin + new Vector2(0.835f, 0.205f) * side, new Vector2(0.028f, 0.038f) * side, Paw);
            Ellipse(vertices, origin + new Vector2(0.900f, 0.205f) * side, new Vector2(0.028f, 0.038f) * side, Paw);
            Ellipse(vertices, origin + new Vector2(0.950f, 0.155f) * side, new Vector2(0.027f, 0.034f) * side, Paw);
        }

        private static void RoundedRect(VertexHelper vertices, Rect bounds, float radius, Color32 tint)
        {
            const int segments = 5;
            int start = vertices.currentVertCount;
            vertices.AddVert(bounds.center, tint, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? bounds.xMax - radius : bounds.xMin + radius,
                    corner < 2 ? bounds.yMax - radius : bounds.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                }
            }
            Fan(vertices, start, 4 * (segments + 1));
        }

        private static void Ellipse(VertexHelper vertices, Vector2 center, Vector2 radius, Color32 tint)
        {
            const int segments = 20;
            int start = vertices.currentVertCount;
            vertices.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), tint, Vector2.zero);
            }
            Fan(vertices, start, segments);
        }

        private static void Fan(VertexHelper vertices, int start, int count)
        {
            for (int i = 0; i < count; i++)
                vertices.AddTriangle(start, start + i + 1, start + (i + 1) % count + 1);
        }
    }
}
