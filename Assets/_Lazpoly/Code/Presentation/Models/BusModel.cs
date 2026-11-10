using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The bus of the bus station, on its own (the owner's cartoon diorama, Meshy AI; Tools/Art/make_station.py cuts it
    /// from the stop so it can drive off with a player): wheels on y = 0, centred on its footprint, its nose towards
    /// +Z, in the station's units (the stop is one unit wide). The numbers below are printed by that script.
    /// </summary>
    public static class BusModel
    {
        /// <summary>Where the bus stands in the stop's frame, and its turn about y there: its nose towards the stop's -X.</summary>
        public static readonly Vector3 ParkedAt = new Vector3(0.0753f, 0.0504f, 0.1680f);
        public const float ParkedTurn = -90f;

        /// <summary>The bus's size, in station widths: across, its roof over the ground, and nose to tail.</summary>
        public const float Width = 0.3694f;
        public const float Height = 0.3998f;
        public const float Length = 0.6413f;

        /// <summary>Whether the bus's files are in the build and readable.</summary>
        public static bool IsAvailable => FigureModels.IsAvailable(FigureModels.BusId);

        /// <summary>The bus as a parent object with its body, or null when its files are missing.</summary>
        public static GameObject Create(string name, Transform parent) =>
            IsAvailable ? FigureModels.Create(FigureModels.BusId, name, parent) : null;
    }
}
