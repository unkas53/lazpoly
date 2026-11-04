using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The piece that shows who owns a town before any house stands on it: the plot with its sign
    /// (<see cref="BuildingModels"/>, level 0) in the owner's colour, standing where the buildings will
    /// stand and at their size, or the flag when the models are not in the build. The board places,
    /// scales, raises and lowers it the same way either way; the first house takes its place.
    /// </summary>
    public sealed class OwnerMarker : MonoBehaviour
    {
        private GameObject _pennant;

        /// <summary>True when the marker is the plot; false when it is the flag.</summary>
        public bool IsPlot { get; private set; }

        /// <summary>Makes the plot, or the flag when the models are missing; <paramref name="wavePhase"/> staggers the flags' flutter.</summary>
        public static OwnerMarker Create(string name, Transform parent, float wavePhase)
        {
            GameObject root = BuildingModels.Create(0, name, parent);
            bool plot = root != null;
            GameObject pennant = null;
            if (!plot)
            {
                root = BuildingParts.Flag(name, parent);
                pennant = root.transform.Find(BuildingParts.PennantName).gameObject;
                pennant.AddComponent<FlagWave>().Rest(pennant.transform.localRotation, wavePhase);
            }

            var marker = root.AddComponent<OwnerMarker>();
            marker.IsPlot = plot;
            marker._pennant = pennant;
            return marker;
        }

        /// <summary>The marker's size on a square <paramref name="width"/> wide, in local scale: the plot as big as the buildings.</summary>
        public Vector3 RestScale(float width) => IsPlot ? Vector3.one * (width * BoardView.BuildingScale) : new Vector3(width * 0.5f, width * 0.62f, width * 0.5f);

        /// <summary>
        /// Where the marker stands and which way it turns on a square whose outer edge faces <paramref name="outward"/>:
        /// on the buildings' spot, the plot's sign facing out like the buildings' fronts.
        /// </summary>
        public void Place(Vector3 outward, Vector3 buildingSpot)
        {
            transform.localPosition = buildingSpot;
            transform.localRotation = Quaternion.LookRotation(IsPlot ? outward : -outward); // The flag's +X, the pennant, flies along the row.
        }

        /// <summary>Colours the marker for its owner: the plot's paving and sign face (lightened like the buildings' walls), or the pennant.</summary>
        public void Paint(Color color)
        {
            if (IsPlot)
            {
                BuildingModels.PaintOwner(gameObject, color);
            }
            else if (_pennant != null)
            {
                ModelParts.Paint(_pennant, color);
            }
        }
    }
}
