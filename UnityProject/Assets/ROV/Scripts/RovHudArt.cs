using UnityEngine;

namespace ROVVR
{
    // Tiny antialiased masks generated once. Standard uGUI Images handle
    // rendering/batching; no post-processing or per-frame texture work.
    public static class RovHudArt
    {
        public enum Kind { Card, Battery, Depth, Camera, Attitude, Dot }

        public static Sprite Create(Kind kind)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                float d;
                if (kind == Kind.Card)
                {
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * 18f;
                    d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 14f;
                }
                else if (kind == Kind.Dot) d = p.magnitude - 9f;
                else d = IconDistance(kind, p * 0.5f) * 2f - 2f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - d)));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ROV " + kind + " mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), 100f, 0,
                SpriteMeshType.FullRect, kind == Kind.Card ? Vector4.one * 15f : Vector4.zero);
        }

        private static float IconDistance(Kind kind, Vector2 p)
        {
            float d = 100f;
            switch (kind)
            {
                case Kind.Battery:
                    Segment(ref d,p,-10,-6,8,-6); Segment(ref d,p,8,-6,8,6);
                    Segment(ref d,p,8,6,-10,6); Segment(ref d,p,-10,6,-10,-6);
                    Segment(ref d,p,11,-2,11,2); Segment(ref d,p,-5,0,3,0); break;
                case Kind.Depth:
                    Segment(ref d,p,0,10,0,-9); Segment(ref d,p,-5,-4,0,-9); Segment(ref d,p,0,-9,5,-4);
                    Segment(ref d,p,-10,8,-6,8); Segment(ref d,p,6,8,10,8); break;
                case Kind.Camera:
                    Segment(ref d,p,-10,-7,5,-7); Segment(ref d,p,5,-7,5,7); Segment(ref d,p,5,7,-10,7); Segment(ref d,p,-10,7,-10,-7);
                    Segment(ref d,p,5,2,11,6); Segment(ref d,p,11,6,11,-6); Segment(ref d,p,11,-6,5,-2); break;
                case Kind.Attitude:
                    Segment(ref d,p,-12,0,-4,0); Segment(ref d,p,4,0,12,0);
                    Segment(ref d,p,-4,0,0,-4); Segment(ref d,p,0,-4,4,0); Segment(ref d,p,0,6,0,10); break;
            }
            return d;
        }

        private static void Segment(ref float d, Vector2 p, float x1,float y1,float x2,float y2)
        {
            Vector2 a=new Vector2(x1,y1), v=new Vector2(x2-x1,y2-y1);
            d=Mathf.Min(d, (p-a-v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude)).magnitude);
        }
    }
}
