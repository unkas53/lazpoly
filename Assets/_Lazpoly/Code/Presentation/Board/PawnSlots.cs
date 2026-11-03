using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Where pawns stand on a square so they never cover each other: a 2x2 grid of slots for up to
    /// four players, 3x3 for more. Slots are numbered row by row from the far left; the offsets are
    /// relative to the square's pawn spot.
    /// </summary>
    public static class PawnSlots
    {
        /// <summary>Slot pitch as a fraction of the square spacing.</summary>
        public const float Pitch = 0.19f;

        /// <summary>How many slots a square has for a game with <paramref name="playerCount"/> players.</summary>
        public static int Capacity(int playerCount) => playerCount <= 4 ? 4 : 9;

        /// <summary>Columns (and rows) of the slot grid that holds <paramref name="capacity"/> slots.</summary>
        public static int Columns(int capacity) => capacity <= 4 ? 2 : 3;

        public static Vector3 Offset(int slot, int capacity, float spacing)
        {
            if (slot < 0 || slot >= capacity) throw new ArgumentOutOfRangeException(nameof(slot));
            int columns = Columns(capacity);
            int row = slot / columns;
            int column = slot % columns;
            float step = spacing * Pitch;
            return new Vector3((column - (columns - 1) / 2f) * step, 0f, (row - (columns - 1) / 2f) * step);
        }

        /// <summary>
        /// Where a standing figure goes: side by side across the screen instead of in a grid, because a tall
        /// figure further from the camera hides behind the one in front of it (the square's own edge runs
        /// diagonally on screen, so a line along it hides them too). The first figure stands near the middle,
        /// the next ones alternate left and right; every other one stands a little further back, so neighbours
        /// overlap like a queue, not a wall. The offset is in the camera's frame: x along the screen's right,
        /// z along the screen's up (away from the camera); turn it by the camera's yaw to place it.
        /// </summary>
        public static Vector3 Line(int slot, int capacity, float spacing)
        {
            if (slot < 0 || slot >= capacity) throw new ArgumentOutOfRangeException(nameof(slot));
            float step = spacing * (capacity <= 4 ? LineStep : CrowdedLineStep);
            float depth = slot % 2 == 1 ? spacing * LineStagger : 0f;
            return new Vector3(LineAcross(slot) * step, 0f, depth);
        }

        /// <summary>Gap between neighbours across the screen, as a fraction of the square's width (four players, and more).</summary>
        public const float LineStep = 0.26f;
        public const float CrowdedLineStep = 0.17f;

        /// <summary>How much further back every other figure stands, as a fraction of the square's width.</summary>
        public const float LineStagger = 0.06f;

        // Places in steps from the middle: -0.5, +0.5, -1.5, +1.5, ...
        private static float LineAcross(int slot) => (slot / 2 + 0.5f) * (slot % 2 == 0 ? -1f : 1f);
    }

    /// <summary>
    /// Which pawn stands in which slot of which square. A pawn that arrives takes the lowest free
    /// slot; a pawn that leaves frees its slot. The same square never hands out one slot twice.
    /// </summary>
    public sealed class PawnCrowd
    {
        private readonly int _capacity;
        private readonly Dictionary<int, (int Square, int Slot)> _places = new Dictionary<int, (int, int)>();

        public PawnCrowd(int playerCount)
        {
            if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));
            _capacity = PawnSlots.Capacity(playerCount);
        }

        public int Capacity => _capacity;

        /// <summary>Puts a pawn on a square and says which slot it got; staying on its square keeps its slot.</summary>
        public int Place(int playerId, int square)
        {
            if (_places.TryGetValue(playerId, out (int Square, int Slot) current) && current.Square == square) return current.Slot;

            _places.Remove(playerId);
            int slot = 0;
            while (slot < _capacity - 1 && IsTaken(square, slot)) slot++;
            _places[playerId] = (square, slot);
            return slot;
        }

        /// <summary>Takes a pawn off the board (bankrupt), freeing its slot.</summary>
        public void Remove(int playerId) => _places.Remove(playerId);

        /// <summary>The slot a pawn stands in, or -1 when it is not on the board.</summary>
        public int SlotOf(int playerId) => _places.TryGetValue(playerId, out (int Square, int Slot) place) ? place.Slot : -1;

        /// <summary>The square a pawn stands on, or -1 when it is not on the board.</summary>
        public int SquareOf(int playerId) => _places.TryGetValue(playerId, out (int Square, int Slot) place) ? place.Square : -1;

        public int CountOn(int square)
        {
            int count = 0;
            foreach ((int Square, int Slot) place in _places.Values) if (place.Square == square) count++;
            return count;
        }

        private bool IsTaken(int square, int slot)
        {
            foreach ((int Square, int Slot) place in _places.Values) if (place.Square == square && place.Slot == slot) return true;
            return false;
        }
    }
}
