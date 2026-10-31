using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using UnityEngine;

namespace Lazpoly.Presentation.Data
{
    /// <summary>Everything a game needs from data: the rules, the board and how groups look.</summary>
    public sealed class BoardData
    {
        private readonly Dictionary<int, Color> _groupColors;

        private readonly Dictionary<int, string> _groupNames;

        public BoardData(BoardDefinition board, RulesConfig rules, Dictionary<int, Color> groupColors,
            Dictionary<int, string> groupNames = null)
        {
            _groupNames = groupNames ?? new Dictionary<int, string>();
            Board = board;
            Rules = rules;
            _groupColors = groupColors;
            Checksum = BoardChecksum.Compute(board, rules);
        }

        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }

        /// <summary>Digest of board and rules; online players must agree on it.</summary>
        public ulong Checksum { get; }

        /// <summary>The group's name from the board file, or null when it has none.</summary>
        public string GroupName(int groupId) => _groupNames.TryGetValue(groupId, out string name) ? name : null;

        public Color GroupColor(int groupId) =>
            _groupColors.TryGetValue(groupId, out Color color) ? color : new Color(0.85f, 0.85f, 0.85f);
    }
}
