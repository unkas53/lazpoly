using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using Lazpoly.Presentation.UI;
using UnityEngine;

namespace Lazpoly.Presentation.Data
{
    /// <summary>Everything a game needs from data: the rules, the board and how groups look.</summary>
    public sealed class BoardData
    {
        private readonly Dictionary<int, Color> _groupColors;
        private readonly List<int> _groupOrder;

        private readonly Dictionary<int, string> _groupNames;

        public BoardData(BoardDefinition board, RulesConfig rules, Dictionary<int, Color> groupColors,
            Dictionary<int, string> groupNames = null)
        {
            _groupNames = groupNames ?? new Dictionary<int, string>();
            Board = board;
            Rules = rules;
            _groupColors = groupColors;
            _groupOrder = new List<int>(groupColors.Keys);
            _groupOrder.Sort();
            Checksum = BoardChecksum.Compute(board, rules);
        }

        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }

        /// <summary>Digest of board and rules; online players must agree on it.</summary>
        public ulong Checksum { get; }

        /// <summary>The group's name from the board file, or null when it has none.</summary>
        public string GroupName(int groupId) => _groupNames.TryGetValue(groupId, out string name) ? name : null;

        /// <summary>The group's colour: the one from the board file, or the colour-blind friendly one standing in for it (<see cref="ColorVision"/>).</summary>
        public Color GroupColor(int groupId)
        {
            Color normal = _groupColors.TryGetValue(groupId, out Color color) ? color : new Color(0.85f, 0.85f, 0.85f);
            int ordinal = GroupOrdinal(groupId);
            return ordinal < 0 ? normal : ColorVision.Group(ordinal, normal);
        }

        /// <summary>The letter shown on the group's marks when colours alone must not carry the meaning; null otherwise.</summary>
        public string GroupMark(int groupId) => ColorVision.GroupMark(GroupOrdinal(groupId));

        /// <summary>The group's position among the groups of the board file (by id), or -1 for an unknown group.</summary>
        public int GroupOrdinal(int groupId) => _groupOrder.IndexOf(groupId);
    }
}
