using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ClassicUs.FreezerMod
{
    internal static class FreezeAssets
    {
        private static Sprite _freezeSprite;
        private static Sprite _ringSprite;

        public static Sprite LoadFreezeSprite(Bounds matchWorldBounds)
        {
            if (_freezeSprite != null) return _freezeSprite;

            try
            {
                byte[] bytes;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("freeze.png"))
                {
                    if (stream == null)
                    {
                        FreezerPlugin.Log.LogError("freeze.png embedded resource not found.");
                        return null;
                    }
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(texture, bytes);

                float targetWidth = matchWorldBounds.size.x > 0f ? matchWorldBounds.size.x : 0.6f;
                float pixelsPerUnit = texture.width / targetWidth;

                _freezeSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);

                return _freezeSprite;
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("LoadFreezeSprite failed: " + e);
                return null;
            }
        }

        public static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 1.6f);
                    pixels[y * size + x] = new Color32(140, 215, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _ringSprite;
        }
    }
}
