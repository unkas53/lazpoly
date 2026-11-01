using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>The small pictures the menus use beside their titles; each is drawn from a few shapes, so no art files are needed.</summary>
    public enum UiIcon
    {
        /// <summary>A die: playing alone against the computer.</summary>
        Dice,
        /// <summary>Two people: playing together at one screen.</summary>
        People,
        /// <summary>Three linked dots: playing online.</summary>
        Network,
        /// <summary>An open book: how to play.</summary>
        Book,
        /// <summary>Three sliders: settings.</summary>
        Sliders,
        /// <summary>A door: leaving.</summary>
        Door,
        /// <summary>A list: the table rules.</summary>
        List,
        /// <summary>A house: the home view of the board.</summary>
        Home,
        /// <summary>A flag: ownership.</summary>
        Flag,
        /// <summary>A bookmark: the saved game.</summary>
        Bookmark,
        /// <summary>Two bars: the game is held still.</summary>
        Pause,
    }

    /// <summary>Draws a <see cref="UiIcon"/> as a square of shapes inside a layout.</summary>
    public static class UiIcons
    {
        private static readonly Color Ink = new Color(0.12f, 0.08f, 0.03f, 0.85f);

        /// <summary>Adds the icon as a fixed-size square child of <paramref name="parent"/>, drawn in <paramref name="color"/>.</summary>
        public static RectTransform Create(RectTransform parent, UiIcon icon, float size, Color color)
        {
            RectTransform root = UiRoot.Rect(parent, $"Icon {icon}", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(root.gameObject, size, size);
            Draw(root, icon, color);
            return root;
        }

        private static void Draw(RectTransform root, UiIcon icon, Color color)
        {
            Color dark = Color.Lerp(color, Color.black, 0.45f);
            switch (icon)
            {
                case UiIcon.Dice:
                    Shape(root, 0.06f, 0.06f, 0.88f, 0.88f, color);
                    foreach (Vector2 pip in new[] { new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.28f), new Vector2(0.5f, 0.5f), new Vector2(0.28f, 0.72f), new Vector2(0.72f, 0.72f) })
                        Shape(root, pip.x - 0.08f, pip.y - 0.08f, 0.16f, 0.16f, Ink, round: true);
                    break;

                case UiIcon.People:
                    Shape(root, 0.16f, 0.56f, 0.28f, 0.28f, color, round: true);
                    Shape(root, 0.04f, 0.1f, 0.52f, 0.42f, color);
                    Shape(root, 0.6f, 0.5f, 0.26f, 0.26f, dark, round: true);
                    Shape(root, 0.5f, 0.1f, 0.46f, 0.36f, dark);
                    break;

                case UiIcon.Network:
                    Shape(root, 0.46f, 0.28f, 0.08f, 0.5f, dark);
                    Shape(root, 0.2f, 0.26f, 0.6f, 0.08f, dark);
                    Shape(root, 0.36f, 0.66f, 0.28f, 0.28f, color, round: true);
                    Shape(root, 0.06f, 0.06f, 0.28f, 0.28f, color, round: true);
                    Shape(root, 0.66f, 0.06f, 0.28f, 0.28f, color, round: true);
                    break;

                case UiIcon.Book:
                    Shape(root, 0.08f, 0.14f, 0.4f, 0.72f, color);
                    Shape(root, 0.52f, 0.14f, 0.4f, 0.72f, color);
                    Shape(root, 0.46f, 0.1f, 0.08f, 0.8f, dark);
                    break;

                case UiIcon.Sliders:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.16f + i * 0.28f;
                        Shape(root, 0.08f, y + 0.06f, 0.84f, 0.1f, dark);
                        float knob = i == 0 ? 0.62f : i == 1 ? 0.24f : 0.46f;
                        Shape(root, knob, y, 0.22f, 0.22f, color, round: true);
                    }

                    break;

                case UiIcon.Door:
                    Shape(root, 0.22f, 0.06f, 0.56f, 0.88f, color);
                    Shape(root, 0.6f, 0.44f, 0.12f, 0.12f, Ink, round: true);
                    break;

                case UiIcon.List:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.14f + i * 0.28f;
                        Shape(root, 0.08f, y, 0.18f, 0.18f, color, round: true);
                        Shape(root, 0.34f, y + 0.04f, 0.58f, 0.1f, dark);
                    }

                    break;

                case UiIcon.Home:
                    Shape(root, 0.2f, 0.06f, 0.6f, 0.46f, color);
                    Shape(root, 0.3f, 0.36f, 0.4f, 0.4f, color).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Shape(root, 0.44f, 0.06f, 0.12f, 0.22f, Ink);
                    break;

                case UiIcon.Flag:
                    Shape(root, 0.18f, 0.06f, 0.1f, 0.88f, dark);
                    Shape(root, 0.3f, 0.52f, 0.56f, 0.36f, color);
                    break;

                case UiIcon.Bookmark:
                    Shape(root, 0.26f, 0.06f, 0.48f, 0.88f, color);
                    Shape(root, 0.26f, 0.06f, 0.48f, 0.36f, dark).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;

                case UiIcon.Pause:
                    Shape(root, 0.16f, 0.08f, 0.26f, 0.84f, color);
                    Shape(root, 0.58f, 0.08f, 0.26f, 0.84f, color);
                    break;
            }
        }

        /// <summary>A shape at (x, y) with size (w, h), all as fractions of the icon square.</summary>
        private static Image Shape(RectTransform root, float x, float y, float w, float h, Color color, bool round = false)
        {
            RectTransform rect = UiRoot.Rect(root, round ? "Dot" : "Block", new Vector2(x, y), new Vector2(x + w, y + h), Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = round ? UiSprites.Circle : UiSprites.Rounded;
            image.type = round ? Image.Type.Simple : Image.Type.Sliced;
            return image;
        }
    }
}
