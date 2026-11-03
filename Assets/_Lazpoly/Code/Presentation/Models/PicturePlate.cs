using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// A picture lying flat in its parent's plane: a quad in the parent's XY, readable from the -Z side
    /// like the board's printed text, drawn with the <see cref="ShaderResource"/> shader so pieces
    /// standing on it hide it. The lawn's logo is one.
    /// </summary>
    public static class PicturePlate
    {
        public const string ShaderResource = "Lazpoly/Shaders/Picture";

        private static Shader _shader;
        private static bool _reportedMissing;

        /// <summary>Whether the picture shader is in the build; without it callers keep their lettering.</summary>
        public static bool IsAvailable => Shader() != null;

        /// <summary>A quad of <paramref name="size"/> (width, height) showing <paramref name="texture"/>, centred on the parent's origin.</summary>
        public static GameObject Create(string name, Transform parent, Texture texture, Vector2 size)
        {
            var plate = new GameObject(name);
            plate.transform.SetParent(parent, false);
            plate.AddComponent<MeshFilter>().sharedMesh = Quad(size);
            var renderer = plate.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = new Material(Shader()) { name = $"Picture {name}", mainTexture = texture, color = Color.white };
            return plate;
        }

        /// <summary>A quad in the XY plane facing -Z, its picture's top towards +Y.</summary>
        public static Mesh Quad(Vector2 size)
        {
            float w = size.x * 0.5f;
            float h = size.y * 0.5f;
            var mesh = new Mesh { name = "Picture Quad" };
            mesh.vertices = new[] { new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            // Wound to face -Z, the side the board's text is read from.
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shader Shader()
        {
            if (_shader != null) return _shader;
            _shader = Resources.Load<Shader>(ShaderResource);
            if (_shader == null && !_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogWarning($"[Lazpoly] Picture shader '{ShaderResource}' is missing; the lawn keeps its lettering.");
            }

            return _shader;
        }
    }
}
