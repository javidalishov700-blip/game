// A vertical colour gradient on any UI graphic, done by recolouring its vertices rather than
// with a texture — so it works on the rounded, nine-sliced Panel sprite as well as on a plain
// rectangle, and costs nothing beyond the mesh the graphic already builds.
using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    [DisallowMultipleComponent]
    public sealed class UiGradient : BaseMeshEffect
    {
        private Color _top = Color.white;
        private Color _bottom = Color.white;

        public void SetColors(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;

            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null)
            {
                return;
            }

            int count = vh.currentVertCount;

            if (count == 0)
            {
                return;
            }

            UIVertex vertex = default;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
            }

            float span = Mathf.Max(0.0001f, maxY - minY);

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                // Multiplied rather than replaced, so the graphic's own colour still tints and
                // fades the gradient the way it would any other image.
                Color shade = Color.Lerp(_bottom, _top, (vertex.position.y - minY) / span);
                vertex.color = shade * (Color)vertex.color;

                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
