using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The pieces that stand on a property: a timber house with a tiled roof, and the "konak", a
    /// two-storey mansion with a jutting upper floor, that stands in for the hotel. Each is a parent
    /// object (base at y = 0, one unit wide and deep) holding coloured parts, so a board can scale it.
    /// </summary>
    public static class BuildingParts
    {
        public static readonly Color Timber = new Color(0.76f, 0.58f, 0.38f);
        public static readonly Color Tile = new Color(0.72f, 0.30f, 0.22f);
        public static readonly Color Whitewash = new Color(0.94f, 0.91f, 0.84f);
        public static readonly Color DarkTimber = new Color(0.45f, 0.30f, 0.18f);

        public static GameObject House(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Walls", MeshFactory.RoundedBox(Vector3.one, 0.05f, 3), Timber, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f));
            Part(root, "Roof", MeshFactory.Prism(), Tile, new Vector3(0f, 0.58f, 0f), new Vector3(1.05f, 0.42f, 1.05f));
            return root;
        }

        public static GameObject Mansion(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Ground floor", MeshFactory.RoundedBox(Vector3.one, 0.05f, 3), Whitewash, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.8f));
            Part(root, "Upper floor", MeshFactory.RoundedBox(Vector3.one, 0.05f, 3), Timber, new Vector3(0f, 0.66f, 0f), new Vector3(1f, 0.34f, 0.95f));
            Part(root, "Roof", MeshFactory.Cone(4), Tile, new Vector3(0f, 0.82f, 0f), new Vector3(0.78f, 0.3f, 0.78f), Quaternion.Euler(0f, 45f, 0f));
            Part(root, "Chimney", MeshFactory.Cylinder(8), DarkTimber, new Vector3(0.3f, 0.85f, 0.2f), new Vector3(0.06f, 0.25f, 0.06f));
            return root;
        }

        private static GameObject Root(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void Part(GameObject root, string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            GameObject part = ModelParts.Create(name, root.transform, mesh, color, 0.2f);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        }
    }
}
