using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.UI;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The game clock on the lawn (the owner's wish, Faz 28, from their reference picture): a dark digital board on two legs
    /// near the top edge of the green, showing the game time left as "29:59". It stands upright and turns with the camera;
    /// the last five minutes show in amber, the last one in red. <see cref="BoardView.ClockSpot"/> says where it stands.
    /// </summary>
    public sealed class GameClockSign : MonoBehaviour
    {
        /// <summary>The board's size relative to the lawn's inner width: wide enough for "60:00", low enough to hide little.</summary>
        public const float WidthShare = 0.12f;
        public const float HeightShare = 0.05f;
        public const float LegShare = 0.03f;

        /// <summary>Under this many seconds left the digits turn amber, under <see cref="RedSeconds"/> red.</summary>
        public const int AmberSeconds = 5 * 60;
        public const int RedSeconds = 60;

        private static readonly Color CaseColor = new Color(0.2f, 0.21f, 0.23f);
        private static readonly Color ScreenColor = new Color(0.06f, 0.065f, 0.075f);
        private static readonly Color LegColor = new Color(0.32f, 0.33f, 0.35f);
        private static readonly Color DigitColor = new Color(0.93f, 0.96f, 1f);
        private static readonly Color AmberColor = new Color(1f, 0.74f, 0.25f);
        private static readonly Color RedColor = new Color(1f, 0.36f, 0.3f);

        private TextMesh _digits;
        private int _shownSeconds = -1;

        /// <summary>What the board says with <paramref name="secondsLeft"/> left: minutes and seconds, rounded up, never below "00:00".</summary>
        public static string Text(double secondsLeft)
        {
            int seconds = secondsLeft <= 0 ? 0 : (int)System.Math.Ceiling(secondsLeft - 1e-6);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        /// <summary>The digits' colour with <paramref name="secondsLeft"/> left.</summary>
        public static Color ColorFor(double secondsLeft) =>
            secondsLeft <= RedSeconds ? RedColor : secondsLeft <= AmberSeconds ? AmberColor : DigitColor;

        /// <summary>Builds the board under <paramref name="parent"/> (the board view), sized for a lawn <paramref name="inner"/> wide.</summary>
        public static GameClockSign Create(Transform parent, float inner)
        {
            var root = new GameObject("Game Clock");
            root.transform.SetParent(parent, false);
            var sign = root.AddComponent<GameClockSign>();

            float width = inner * WidthShare, height = inner * HeightShare, leg = inner * LegShare, depth = height * 0.22f;
            GameObject body = ModelParts.Create("Case", root.transform, MeshFactory.RoundedBox(new Vector3(width, height, depth), depth * 0.3f, 2), CaseColor, 0.35f);
            body.transform.localPosition = new Vector3(0f, leg + height / 2f, 0f);
            GameObject screen = ModelParts.Create("Screen", root.transform,
                MeshFactory.RoundedBox(new Vector3(width * 0.9f, height * 0.78f, depth * 0.3f), depth * 0.1f, 1), ScreenColor, 0.6f);
            screen.transform.localPosition = new Vector3(0f, leg + height / 2f, -depth * 0.45f);
            foreach (float side in new[] { -0.32f, 0.32f })
            {
                GameObject post = ModelParts.Create("Leg", root.transform, MeshFactory.Cylinder(10), LegColor, 0.4f);
                post.transform.localScale = new Vector3(depth * 0.28f, leg + height * 0.3f, depth * 0.28f);
                post.transform.localPosition = new Vector3(width * side, 0f, 0f);
            }

            var digits = new GameObject("Digits");
            digits.transform.SetParent(root.transform, false);
            digits.transform.localPosition = new Vector3(0f, leg + height / 2f, -depth * 0.62f);
            sign._digits = digits.AddComponent<TextMesh>();
            sign._digits.anchor = TextAnchor.MiddleCenter;
            sign._digits.alignment = TextAlignment.Center;
            sign._digits.fontSize = 160;
            sign._digits.font = UiTheme.BoldFont;
            sign._digits.characterSize = height * 0.0375f; // A TextMesh em is fontSize × characterSize / 10: digits about 0.6 of the board high.
            sign._digits.color = DigitColor;
            sign._digits.text = Text(0);
            digits.GetComponent<MeshRenderer>().sharedMaterial = PrintMaterial.For(sign._digits.font);
            return sign;
        }

        /// <summary>Stands the board at <paramref name="localSpot"/> on the lawn, its face to a camera turned by <paramref name="yaw"/>.</summary>
        public void Place(Vector3 localSpot, float yaw)
        {
            transform.localPosition = localSpot;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Shows <paramref name="secondsLeft"/>; the text changes only when the shown second does.</summary>
        public void Show(double secondsLeft)
        {
            int seconds = secondsLeft <= 0 ? 0 : (int)System.Math.Ceiling(secondsLeft - 1e-6);
            if (seconds == _shownSeconds || _digits == null) return;
            _shownSeconds = seconds;
            _digits.text = Text(seconds);
            _digits.color = ColorFor(seconds);
        }
    }
}
