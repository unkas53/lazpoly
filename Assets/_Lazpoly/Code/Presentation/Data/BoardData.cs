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

        public BoardData(BoardDefinition board, RulesConfig rules, Dictionary<int, Color> groupColors)
        {
            Board = board;
            Rules = rules;
            _groupColors = groupColors;
        }

        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }

        public Color GroupColor(int groupId) =>
            _groupColors.TryGetValue(groupId, out Color color) ? color : new Color(0.85f, 0.85f, 0.85f);
    }
}
