using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Creates scene objects from <see cref="MeshFactory"/> meshes. They start as Unity primitives so they
    /// carry the render pipeline's default material (which builds always include), then get the new mesh.
    /// </summary>
    public static class ModelParts
    {
        public static GameObject Create(string name, Transform parent, Mesh mesh, Color color, float smoothness = 0.25f)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            Object.Destroy(part.GetComponent<Collider>());
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            Paint(part, color, smoothness);
            return part;
        }

        public static void Paint(GameObject part, Color color, float smoothness = 0.25f)
        {
            Material material = part.GetComponent<Renderer>().material;
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }
    }
}
