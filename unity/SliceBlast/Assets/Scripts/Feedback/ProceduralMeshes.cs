using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>Meshes the game builds for itself instead of shipping art assets.</summary>
    public static class ProceduralMeshes
    {
        /// <summary>Flat ring on the XZ plane, used for the blast shockwave.</summary>
        public static Mesh Ring(float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(8, segments);

            Vector3[] vertices = new Vector3[segments * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);

                int next = (i + 1) % segments;
                int t = i * 6;

                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2 + 1;

                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = next * 2;
            }

            Mesh mesh = new Mesh { name = "ShockwaveRing" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
