using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// The order Tab walks through the elements on screen: row by row from the top, left to right
    /// within a row. Two elements share a row when their centres are closer vertically than half
    /// the taller one's height, so the buttons of one row stay together whatever their exact heights.
    /// </summary>
    public static class FocusOrder
    {
        /// <summary>Sorts the rectangles (screen space, y up) into reading order; returns the indices into <paramref name="rects"/>.</summary>
        public static List<int> Sort(IReadOnlyList<Rect> rects)
        {
            var order = new List<int>(rects.Count);
            for (int i = 0; i < rects.Count; i++) order.Add(i);
            order.Sort((a, b) => Compare(rects[a], rects[b]));
            return order;
        }

        public static int Compare(Rect a, Rect b)
        {
            float tolerance = Mathf.Max(a.height, b.height) * 0.5f;
            float dy = a.center.y - b.center.y;
            if (Mathf.Abs(dy) > tolerance) return dy > 0f ? -1 : 1; // higher on the screen comes first
            if (!Mathf.Approximately(a.center.x, b.center.x)) return a.center.x < b.center.x ? -1 : 1;
            return 0;
        }

        /// <summary>The index after <paramref name="current"/> in <paramref name="order"/> (before it with <paramref name="backwards"/>), wrapping; the first (or last) when current is not in the list.</summary>
        public static int Step(IReadOnlyList<int> order, int current, bool backwards)
        {
            if (order.Count == 0) return -1;
            int at = -1;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == current) at = i;
            }

            if (at < 0) return backwards ? order[order.Count - 1] : order[0];
            int next = (at + (backwards ? -1 : 1) + order.Count) % order.Count;
            return order[next];
        }
    }
}
