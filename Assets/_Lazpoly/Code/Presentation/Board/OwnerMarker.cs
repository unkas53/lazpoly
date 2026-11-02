using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The piece that shows who owns a town before any house stands on it: the owner's tent
    /// (<see cref="TentParts"/>), or the flag when the tent model is not in the build. The board
    /// places, scales, raises and lowers it the same way either way.
    /// </summary>
    public sealed class OwnerMarker : MonoBehaviour
    {
        /// <summary>The tent's length along the building row, in tile widths.</summary>
        public const float TentLength = 0.30f;

        /// <summary>Where the tent's centre sits, inward from the square's centre, in tile depths: on the building row, behind the pawns.</summary>
        public const float TentOffset = 0.36f;

        private GameObject _pennant;

        /// <summary>True when the marker is the tent; false when it is the flag.</summary>
        public bool IsTent { get; private set; }

        /// <summary>Makes the tent, or the flag when the model is missing; <paramref name="wavePhase"/> staggers the flags' flutter.</summary>
        public static OwnerMarker Create(string name, Transform parent, float wavePhase)
        {
            GameObject root = TentParts.Tent(name, parent);
            bool tent = root != null;
            GameObject pennant = null;
            if (!tent)
            {
                root = BuildingParts.Flag(name, parent);
                pennant = root.transform.Find(BuildingParts.PennantName).gameObject;
                pennant.AddComponent<FlagWave>().Rest(pennant.transform.localRotation, wavePhase);
            }

            var marker = root.AddComponent<OwnerMarker>();
            marker.IsTent = tent;
            marker._pennant = pennant;
            return marker;
        }

        /// <summary>The marker's size on a square <paramref name="width"/> wide, in local scale.</summary>
        public Vector3 RestScale(float width) => IsTent ? Vector3.one * (width * TentLength) : new Vector3(width * 0.5f, width * 0.62f, width * 0.5f);

        /// <summary>
        /// Where the marker stands and which way it turns on a square whose outer edge faces <paramref name="outward"/>;
        /// <paramref name="size"/> is the square's width along the edge and its depth from inner edge to outer.
        /// </summary>
        public void Place(Vector3 squareCenter, Vector3 outward, Vector2 size, float surfaceY, Vector3 buildingRowCenter)
        {
            if (IsTent)
            {
                // The tent lies along the row, its door towards the next square, just inside the tile's inner edge.
                transform.localPosition = squareCenter - outward * (size.y * TentOffset) + Vector3.up * surfaceY;
                transform.localRotation = Quaternion.LookRotation(Vector3.Cross(outward, Vector3.up));
            }
            else
            {
                transform.localPosition = buildingRowCenter;
                transform.localRotation = Quaternion.LookRotation(-outward); // Its +X, the pennant, flies along the row.
            }
        }

        /// <summary>Colours the marker for its owner: the canvas and seams, or the pennant.</summary>
        public void Paint(Color color)
        {
            if (IsTent) TentParts.Paint(gameObject, color);
            else if (_pennant != null) ModelParts.Paint(_pennant, color);
        }
    }
}
