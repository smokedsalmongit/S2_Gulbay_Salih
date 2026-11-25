using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// Turns the laundry into real clothes: a T-shirt, a sweater, shorts, trousers and a towel, each a soft, slightly
    /// crumpled mesh with a woven fabric texture. Dirty clothes get coffee / grease / mud stains; wet clothes are darker
    /// and shiny. The meshes are built with "height" along local x so they hang upright on the drying rack.
    /// </summary>
    public class ClothesMaker : MonoBehaviour
    {
        private enum Kind { TShirt, Sweater, Shorts, Trousers, Towel }

        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "Lavender", Kind.TShirt }, { "Teal", Kind.Shorts }, { "Yellow", Kind.Sweater },
            { "Coral", Kind.Trousers }, { "Sky", Kind.Towel },
        };

        private void Start()
        {
            int seed = 1;
            foreach (var g in FindObjectsByType<Garment>(FindObjectsInactive.Include))
            {
                string colour = g.name.Replace("[Grab] Clothes ", "");
                if (!Kinds.TryGetValue(colour, out var kind)) continue;
                var baseColor = g.DryColor;

                var mesh = BuildMesh(Outline(kind), seed);
                g.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mc = g.GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = mesh;

                var rng = new System.Random(seed * 31);
                var fabric = FabricTexture(baseColor, kind == Kind.Towel, rng);
                var dirty = DirtyTexture(fabric, rng);
                var wet = WetTexture(fabric, rng);
                var template = g.GetComponent<Renderer>().sharedMaterial;
                g.Setup(Mat(template, dirty, 0.05f), Mat(template, wet, 0.82f), Mat(template, fabric, 0.12f));
                seed++;
            }
        }

        private static Material Mat(Material template, Texture2D tex, float smoothness)
        {
            var m = new Material(template) { color = Color.white, mainTexture = tex };
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        // ---------- shapes (u = across, v = along the garment, metres) ----------

        private static Vector2[] Outline(Kind k)
        {
            switch (k)
            {
                case Kind.TShirt:
                    return new[]
                    {
                        V(0, 0.145f), V(-0.06f, 0.165f), V(-0.11f, 0.16f), V(-0.17f, 0.115f), V(-0.145f, 0.055f), V(-0.105f, 0.075f),
                        V(-0.115f, -0.16f), V(0.115f, -0.16f), V(0.105f, 0.075f), V(0.145f, 0.055f), V(0.17f, 0.115f), V(0.11f, 0.16f), V(0.06f, 0.165f),
                    };
                case Kind.Sweater:
                    return new[]
                    {
                        V(0, 0.145f), V(-0.06f, 0.165f), V(-0.11f, 0.16f), V(-0.185f, -0.07f), V(-0.15f, -0.095f), V(-0.105f, 0.04f),
                        V(-0.11f, -0.165f), V(0.11f, -0.165f), V(0.105f, 0.04f), V(0.15f, -0.095f), V(0.185f, -0.07f), V(0.11f, 0.16f), V(0.06f, 0.165f),
                    };
                case Kind.Shorts:
                    return new[] { V(-0.14f, 0.13f), V(0.14f, 0.13f), V(0.155f, -0.13f), V(0.02f, -0.13f), V(0, -0.03f), V(-0.02f, -0.13f), V(-0.155f, -0.13f) };
                case Kind.Trousers:
                    return new[] { V(-0.12f, 0.24f), V(0.12f, 0.24f), V(0.13f, -0.24f), V(0.025f, -0.24f), V(0, 0.03f), V(-0.025f, -0.24f), V(-0.13f, -0.24f) };
                default:
                    return new[] { V(-0.2f, -0.14f), V(0.2f, -0.14f), V(0.2f, 0.14f), V(-0.2f, 0.14f) };
            }
        }

        private static Vector2 V(float u, float v) => new Vector2(u, v);

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>A soft cloth slab: the outline on a fine grid, puffed in the middle and gently wrinkled.</summary>
        private static Mesh BuildMesh(Vector2[] poly, int seed)
        {
            const float step = 0.01f, thick = 0.012f;
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var p in poly) { minU = Mathf.Min(minU, p.x); maxU = Mathf.Max(maxU, p.x); minV = Mathf.Min(minV, p.y); maxV = Mathf.Max(maxV, p.y); }
            int nu = Mathf.CeilToInt((maxU - minU) / step), nv = Mathf.CeilToInt((maxV - minV) / step);
            var cell = new bool[nu, nv];
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
                cell[i, j] = Inside(poly, new Vector2(minU + (i + 0.5f) * step, minV + (j + 0.5f) * step));

            bool In(int i, int j) => i >= 0 && j >= 0 && i < nu && j < nv && cell[i, j];

            // distance (in cells) from each node to the outside, for the puffy middle
            var dist = new float[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nv; j++)
            {
                float best = 6;
                for (int a = -6; a <= 6; a++)
                for (int b = -6; b <= 6; b++)
                    if (!In(i + a, j + b) && !In(i + a - 1, j + b - 1)) best = Mathf.Min(best, Mathf.Sqrt(a * a + b * b));
                dist[i, j] = best;
            }

            float ox = seed * 3.7f;
            float Height(int i, int j)
            {
                float u = i * step, v = j * step;
                float puff = Mathf.SmoothStep(0, 1, dist[i, j] / 6f) * 0.012f;
                float wrinkle = (Mathf.PerlinNoise(ox + u * 9f, v * 9f) - 0.5f) * 0.016f + Mathf.Sin((u + v * 0.6f) * 38f + ox) * 0.0025f;
                return thick * 0.5f + puff + wrinkle * Mathf.Clamp01(dist[i, j] / 2f);
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var topIndex = new Dictionary<int, int>();
            var botIndex = new Dictionary<int, int>();
            float cu = (minU + maxU) / 2, cv = (minV + maxV) / 2;

            // local x = along (v), local z = across (u), y = thickness
            Vector3 P(int i, int j, float y) => new Vector3(minV + j * step - cv, y, minU + i * step - cu);
            Vector2 UV(int i, int j) => new Vector2(i / (float)nu, j / (float)nv);

            int Node(Dictionary<int, int> map, int i, int j, bool top)
            {
                int key = i * 10000 + j;
                if (map.TryGetValue(key, out var idx)) return idx;
                idx = verts.Count;
                verts.Add(P(i, j, top ? Height(i, j) : -thick * 0.5f));
                uvs.Add(UV(i, j));
                map[key] = idx;
                return idx;
            }

            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                if (!cell[i, j]) continue;
                int a = Node(topIndex, i, j, true), b = Node(topIndex, i + 1, j, true), c = Node(topIndex, i, j + 1, true), d = Node(topIndex, i + 1, j + 1, true);
                tris.AddRange(new[] { a, b, c, b, d, c }); // top faces up
                int e = Node(botIndex, i, j, false), f = Node(botIndex, i + 1, j, false), g = Node(botIndex, i, j + 1, false), h = Node(botIndex, i + 1, j + 1, false);
                tris.AddRange(new[] { e, g, f, f, g, h }); // bottom faces down

                // side walls where the next cell is outside
                if (!In(i - 1, j)) Side(i, j, i, j + 1);
                if (!In(i + 1, j)) Side(i + 1, j + 1, i + 1, j);
                if (!In(i, j - 1)) Side(i + 1, j, i, j);
                if (!In(i, j + 1)) Side(i, j + 1, i + 1, j + 1);
            }

            void Side(int i0, int j0, int i1, int j1)
            {
                int s = verts.Count;
                verts.Add(P(i0, j0, Height(i0, j0))); verts.Add(P(i1, j1, Height(i1, j1)));
                verts.Add(P(i0, j0, -thick * 0.5f)); verts.Add(P(i1, j1, -thick * 0.5f));
                uvs.Add(UV(i0, j0)); uvs.Add(UV(i1, j1)); uvs.Add(UV(i0, j0)); uvs.Add(UV(i1, j1));
                tris.AddRange(new[] { s, s + 1, s + 2, s + 1, s + 3, s + 2 }); // facing outwards
            }

            var mesh = new Mesh { name = "Garment" };
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- textures ----------

        private const int Size = 128;

        private static Texture2D NewTex() =>
            new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        private static Texture2D FabricTexture(Color c, bool towel, System.Random rng)
        {
            var tex = NewTex();
            float o = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float weave = towel ? 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.9f, y * 0.9f)
                                    : ((x + y) % 2 == 0 ? 1f : 0.94f);
                float cloud = 0.92f + 0.08f * Mathf.PerlinNoise(o + x * 0.05f, y * 0.05f);
                var col = c * weave * cloud;
                if (towel && (y < 6 || y > Size - 7)) col *= 0.85f; // woven border
                col.a = 1;
                tex.SetPixel(x, y, col);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Coffee, grease and mud stains with darker rims, and a dull grey cast over the whole piece.</summary>
        private static Texture2D DirtyTexture(Texture2D fabric, System.Random rng)
        {
            var tex = NewTex();
            var px = fabric.GetPixels();
            var dull = new Color(0.55f, 0.52f, 0.47f);
            for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(px[i], dull * (px[i].grayscale + 0.4f), 0.22f);

            Color[] stainColors = { new Color(0.4f, 0.27f, 0.15f), new Color(0.58f, 0.47f, 0.24f), new Color(0.33f, 0.32f, 0.22f), new Color(0.28f, 0.22f, 0.18f) };
            int count = 4 + rng.Next(3);
            for (int s = 0; s < count; s++)
            {
                float cx = 15 + (float)rng.NextDouble() * (Size - 30), cy = 15 + (float)rng.NextDouble() * (Size - 30);
                float r = 6 + (float)rng.NextDouble() * 14f;
                var sc = stainColors[rng.Next(stainColors.Length)];
                float o = (float)rng.NextDouble() * 50f;
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float wobble = (Mathf.PerlinNoise(o + x * 0.12f, o + y * 0.12f) - 0.5f) * r * 0.9f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + wobble;
                    if (d > r) continue;
                    float inside = Mathf.SmoothStep(0, 1, (r - d) / 3f);
                    float rim = Mathf.Exp(-Mathf.Pow((r - d) / 1.6f, 2)) * 0.5f; // tide mark at the edge
                    int k = y * Size + x;
                    var stained = Color.Lerp(px[k], px[k] * sc * 1.6f, 0.55f) * (1 - rim * 0.35f);
                    px[k] = Color.Lerp(px[k], stained, Mathf.Clamp01(inside * 0.8f + rim));
                }
            }
            for (int i = 0; i < px.Length; i++) px[i].a = 1;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Wet fabric: clearly darker and a little more saturated, with even darker soaked patches.</summary>
        private static Texture2D WetTexture(Texture2D fabric, System.Random rng)
        {
            var tex = NewTex();
            var px = fabric.GetPixels();
            float o = (float)rng.NextDouble() * 50f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int k = y * Size + x;
                var c = px[k];
                Color.RGBToHSV(c, out var h, out var s, out var v);
                float soak = Mathf.PerlinNoise(o + x * 0.06f, y * 0.06f);
                v *= Mathf.Lerp(0.62f, 0.45f, Mathf.SmoothStep(0.4f, 0.8f, soak));
                s = Mathf.Clamp01(s * 1.15f);
                var w = Color.HSVToRGB(h, s, v);
                w.a = 1;
                px[k] = w;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
