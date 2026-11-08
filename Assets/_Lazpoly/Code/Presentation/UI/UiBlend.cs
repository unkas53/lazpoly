using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// See-through interface colours in linear colour space. The interface's veils, dimmed layers and shadows were tuned
    /// in gamma colour space, where the screen mixes a layer with what lies under it as the stored values; linear mixes
    /// their linear values, so the same dark layer came out much paler (the settings window's dimmed menu turned from
    /// 546C74 to 8E908F). Here the layer's opacity is chosen so that over the light table and menu the mix lands where
    /// gamma put it; the colour itself stays.
    /// </summary>
    public static class UiBlend
    {
        /// <summary>What the layers usually lie over: the table's sky, close to the menu's light picture as well.</summary>
        public static readonly Color TypicalBackdrop = new Color(0xA2 / 255f, 0xD7 / 255f, 0xF7 / 255f);

        /// <summary>Whether the screen mixes see-through layers in linear colour space.</summary>
        public static bool MixesLinear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>
        /// A colour for a mesh's vertices drawn by a shader that takes them as they are (Unity's built-in text shader):
        /// in linear colour space it must be stored linear, or a dark ink shows as a pale grey.
        /// </summary>
        public static Color ForVertices(Color color) => MixesLinear ? color.linear : color;

        /// <summary><paramref name="color"/> with the opacity that looks as it did in gamma, when the screen mixes in linear.</summary>
        public static Color AsTuned(Color color) => MixesLinear ? ForLinear(color, TypicalBackdrop) : color;

        /// <summary>
        /// A dimming layer over the whole screen as it looked in gamma, when the screen mixes in linear: opacity alone can
        /// match the brightness but not the hue (the dimmed table came out greyer), so the layer's colour is solved too.
        /// </summary>
        public static Color AsTunedLayer(Color color) => MixesLinear ? LayerForLinear(color, TypicalBackdrop) : color;

        /// <summary>
        /// The colour and opacity that, mixed in linear over <paramref name="backdrop"/>, give channel by channel what gamma's
        /// mix of <paramref name="color"/> gave there: the opacity is raised just enough that no channel needs a colour
        /// darker than black, and the colour fills in the rest.
        /// </summary>
        public static Color LayerForLinear(Color color, Color backdrop)
        {
            float a = color.a;
            if (a <= 0f || a >= 1f) return color;
            Color wanted = Color.Lerp(backdrop, color, a).linear;
            Color under = backdrop.linear;
            float opacity = a;
            for (int i = 0; i < 3; i++)
                if (under[i] > 0.0001f) opacity = Mathf.Max(opacity, 1f - wanted[i] / under[i] + LayerMargin);
            opacity = Mathf.Min(opacity, 1f);
            var layer = new Color(0f, 0f, 0f, opacity);
            for (int i = 0; i < 3; i++) layer[i] = Mathf.Clamp01((wanted[i] - (1f - opacity) * under[i]) / opacity);
            Color result = layer.gamma;
            result.a = opacity;
            return result;
        }

        /// <summary>How far above the least opacity a layer goes, so its darkest channel keeps a little colour.</summary>
        private const float LayerMargin = 0.01f;

        /// <summary>
        /// The opacity that, mixed in linear over <paramref name="backdrop"/>, gives the brightness gamma's mix of
        /// <paramref name="color"/> gave there. A layer as bright as the backdrop keeps its opacity: it changes nothing either way.
        /// </summary>
        public static Color ForLinear(Color color, Color backdrop)
        {
            float a = color.a;
            if (a <= 0f || a >= 1f) return color;
            Color gammaMix = Color.Lerp(backdrop, color, a);
            float under = Luma(backdrop.linear);
            float layer = Luma(color.linear);
            float wanted = Luma(gammaMix.linear);
            if (Mathf.Abs(under - layer) < 0.001f) return color;
            color.a = Mathf.Clamp01((under - wanted) / (under - layer));
            return color;
        }

        private static float Luma(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
    }
}
