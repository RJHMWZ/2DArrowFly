using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    // One shared shade. Nested cards borrow the existing dimmer instead of
    // multiplying two translucent overlays (for example Pause -> Settings).
    public static class HueModalBackdrop
    {
        public static readonly Color Tint = new Color(.08f, .09f, .12f, .55f);
        private struct Layer { public Image image; public float easing, initialAlpha; }
        private static readonly List<Layer> layers = new List<Layer>(8);

        public static bool Contains(Image image)
        {
            for (int i = 0; i < layers.Count; i++) if (layers[i].image == image) return true;
            return false;
        }

        public static void Show(Image image, float initialAlpha = 0)
        {
            if (!image) return;
            Prune();
            if (Contains(image)) return;
            if (layers.Count > 0) initialAlpha = Mathf.Max(initialAlpha, layers[layers.Count - 1].image.color.a);
            layers.Add(new Layer { image = image, initialAlpha = Mathf.Clamp(initialAlpha, 0, Tint.a) });
            image.material = null;
            Paint();
        }

        public static void Apply(Image image, float easing)
        {
            if (!image) return;
            Show(image);
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer.image != image) continue;
                layer.easing = Mathf.Clamp01(easing); layers[i] = layer;
                break;
            }
            Paint();
        }

        public static void Hide(Image image)
        {
            if (!image) return;
            for (int i = layers.Count - 1; i >= 0; i--)
                if (layers[i].image == image) layers.RemoveAt(i);
            image.color = Tint;
            Prune(); Paint();
        }

        private static void Prune()
        {
            for (int i = layers.Count - 1; i >= 0; i--)
                if (!layers[i].image || !layers[i].image.isActiveAndEnabled) layers.RemoveAt(i);
        }

        private static void Paint()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.image) continue;
                Color color = Tint;
                color.a = i == layers.Count - 1 ? Mathf.Lerp(layer.initialAlpha, Tint.a, layer.easing) : 0;
                layer.image.color = color;
            }
        }
    }
}
