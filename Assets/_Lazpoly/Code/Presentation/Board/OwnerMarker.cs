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
    /// On a holiday resort the marker is the owner's beach instead (<see cref="ResortParts"/>), at the stage
    /// that matches how many resorts the owner holds (<see cref="ShowStage"/>).
    /// </summary>
    public sealed class OwnerMarker : MonoBehaviour
    {
        private GameObject _pennant;
        private GameObject[] _stages;

        /// <summary>True when the marker is the plot; false when it is the flag or a beach.</summary>
        public bool IsPlot { get; private set; }

        /// <summary>True when the marker is a resort's beach.</summary>
        public bool IsResort => _stages != null;

        /// <summary>The beach stage shown, 1 to <see cref="ResortParts.Stages"/>; 0 on a town's marker.</summary>
        public int Stage { get; private set; }

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

        /// <summary>
        /// A resort's beach: all <see cref="ResortParts.Stages"/> stages are made at once and one stands at a time, so
        /// a new resort bought elsewhere only swaps which one shows.
        /// </summary>
        public static OwnerMarker CreateResort(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var stages = new GameObject[ResortParts.Stages];
            for (int i = 0; i < stages.Length; i++) stages[i] = ResortParts.Stage(i + 1, $"Beach {i + 1}", root.transform);
            var marker = root.AddComponent<OwnerMarker>();
            marker._stages = stages;
            marker.ShowStage(1);
            return marker;
        }

        /// <summary>Shows the beach for an owner holding <paramref name="held"/> resorts (clamped to the stages); nothing on a town's marker.</summary>
        public void ShowStage(int held)
        {
            if (_stages == null) return;
            Stage = Mathf.Clamp(held, 1, _stages.Length);
            for (int i = 0; i < _stages.Length; i++) _stages[i].SetActive(i == Stage - 1);
        }

        /// <summary>The marker's size on a square <paramref name="width"/> wide, in local scale: the plot and the beach as big as the buildings.</summary>
        public Vector3 RestScale(float width) => IsPlot || IsResort ? Vector3.one * (width * BoardView.BuildingScale) : new Vector3(width * 0.5f, width * 0.62f, width * 0.5f);

        /// <summary>
        /// Where the marker stands and which way it turns on a square whose outer edge faces <paramref name="outward"/>:
        /// on the buildings' spot, the plot's sign facing a camera at <paramref name="yaw"/> like the buildings' fronts.
        /// </summary>
        public void Place(Vector3 outward, Vector3 buildingSpot, float yaw)
        {
            transform.localPosition = buildingSpot;
            Turn(outward, yaw);
        }

        /// <summary>Turns the plot's sign towards a camera at <paramref name="yaw"/> (<see cref="BoardView.BuildingFront"/>); the flag's pennant flies along the row.</summary>
        public void Turn(Vector3 outward, float yaw) =>
            transform.localRotation = Quaternion.LookRotation(IsPlot || IsResort ? BoardView.BuildingFront(outward, yaw) : -outward);

        /// <summary>Colours the marker for its owner: the plot's paving and sign face (lightened like the buildings' walls), or the pennant.</summary>
        public void Paint(Color color)
        {
            if (IsResort)
            {
                foreach (GameObject stage in _stages) ResortParts.PaintOwner(stage, color);
            }
            else if (IsPlot)
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
