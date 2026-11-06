using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// What the property card can offer on one square: the level it stands at, the levels the
    /// rules and the player's money allow on this visit, what each costs and what it would earn.
    /// Nothing here touches the scene, so the card's arithmetic is tested without Unity. The
    /// rules themselves stay in <see cref="GameRules"/>; the host checks every command again.
    /// </summary>
    public sealed class PropertyChoice
    {
        private readonly int _baseRent;
        private readonly IReadOnlyList<int> _developedRents;
        private readonly int _fullGroupMultiplier;

        private PropertyChoice(int squareIndex, bool buying, int level, int allowed, int price, int buildingCost, int money, int laps,
            bool developable, bool fullGroup, int baseRent, IReadOnlyList<int> developedRents, int fullGroupMultiplier)
        {
            SquareIndex = squareIndex;
            Buying = buying;
            Level = level;
            Allowed = allowed;
            Price = price;
            BuildingCost = buildingCost;
            Money = money;
            Laps = laps;
            Developable = developable;
            FullGroup = fullGroup;
            _baseRent = baseRent;
            _developedRents = developedRents;
            _fullGroupMultiplier = fullGroupMultiplier;
            int budget = money - price;
            Most = allowed > 0 && buildingCost > 0 && budget >= 0 ? Math.Min(allowed, budget / buildingCost) : 0;
        }

        /// <summary>
        /// The card for <paramref name="player"/> on <paramref name="squareIndex"/>: buying it (the
        /// land plus any houses the rules allow on the same visit) or building on a town they own.
        /// </summary>
        public static PropertyChoice For(BoardDefinition board, RulesConfig rules, GameState state, PlayerState player, int squareIndex, bool buying)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (player == null) throw new ArgumentNullException(nameof(player));
            SquareDefinition square = board[squareIndex];
            int level = state.GetBuildingLevel(squareIndex);
            int allowed = buying
                ? GameRules.BuildableAfterPurchase(board, player, squareIndex)
                : GameRules.BuildableNow(board, state, player, squareIndex);
            if (square.IsResort)
            {
                // A resort's rent is its base times the resorts its owner holds, this one counted when buying it.
                int held = GameRules.ResortsOwned(board, state, player.Id) + (buying && state.GetOwner(squareIndex) != player.Id ? 1 : 0);
                return new PropertyChoice(squareIndex, buying, level, 0, buying ? square.Price : 0, 0, player.Money, player.Laps,
                    false, false, square.BaseRent * Math.Max(1, held), square.DevelopedRents, 1) { Resort = true, ResortStage = Math.Max(1, held) };
            }

            return new PropertyChoice(squareIndex, buying, level, allowed, buying ? square.Price : 0, square.BuildingCost, player.Money, player.Laps,
                square.IsDevelopable, OwnsGroupWith(board, state, player.Id, square, buying ? squareIndex : -1), square.BaseRent, square.DevelopedRents,
                rules?.FullGroupRentMultiplier ?? 1);
        }

        /// <summary>True when the player holds (or, counting <paramref name="asIfOwned"/>, would hold) every property of the square's group.</summary>
        private static bool OwnsGroupWith(BoardDefinition board, GameState state, int playerId, SquareDefinition square, int asIfOwned)
        {
            if (square.Type != SquareType.Property || square.GroupId == 0) return false;
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition other = board[i];
                if (other.Type != SquareType.Property || other.GroupId != square.GroupId) continue;
                if (i != asIfOwned && state.GetOwner(i) != playerId) return false;
            }

            return true;
        }

        public int SquareIndex { get; }

        /// <summary>True on a holiday resort: one tile, no buildings, its rent counting the resorts the player would hold.</summary>
        public bool Resort { get; private set; }

        /// <summary>On a resort, how many resorts the player holds with this one (the beach's stage and the rent's multiple); 0 on a town.</summary>
        public int ResortStage { get; private set; }

        /// <summary>True on the purchase card: the land is not owned yet and its price comes first.</summary>
        public bool Buying { get; }

        /// <summary>The level the square stands at now: 0 bare land, 1-4 houses, 5 the hotel.</summary>
        public int Level { get; }

        /// <summary>How many buildings the rules allow on this visit, money aside.</summary>
        public int Allowed { get; }

        /// <summary>How many of those the player can pay for, after the land if buying.</summary>
        public int Most { get; }

        /// <summary>The land's price when buying, otherwise 0.</summary>
        public int Price { get; }

        public int BuildingCost { get; }
        public int Money { get; }
        public int Laps { get; }

        /// <summary>False for a square that never takes buildings: the card shows the land alone.</summary>
        public bool Developable { get; }

        /// <summary>True when the player would own the whole colour group, which raises the bare land's rent.</summary>
        public bool FullGroup { get; }

        /// <summary>The highest level the player can pick now.</summary>
        public int Top => Level + Most;

        /// <summary>What the card selects when it opens: the land when buying, one more building when it can be afforded.</summary>
        public int DefaultPick => Buying ? Level : Math.Min(Level + 1, Top);

        /// <summary>True when buying is possible at all: the land can be paid for (always true on the building card).</summary>
        public bool AffordsLand => Money >= Price;

        public bool CanPick(int level) => level >= Level && level <= Top;

        public int Clamp(int pick) => Math.Max(Level, Math.Min(Top, pick));

        /// <summary>The whole price of standing at <paramref name="pick"/>: the land if buying, plus each building above the current level.</summary>
        public int CostOf(int pick) => Price + Math.Max(0, pick - Level) * BuildingCost;

        /// <summary>Rent a visitor would pay at <paramref name="level"/>; bare land earns more once the group is complete.</summary>
        public int RentAt(int level)
        {
            if (level > 0 && _developedRents != null && _developedRents.Count > 0)
                return _developedRents[Math.Min(level, _developedRents.Count) - 1];
            return FullGroup ? _baseRent * _fullGroupMultiplier : _baseRent;
        }

        /// <summary>
        /// Why <paramref name="level"/> cannot be picked now, as the stamp across its tile says; null
        /// when it can be (or already stands). The rules come first, money last.
        /// </summary>
        public string Locked(int level)
        {
            if (level <= Top) return null;
            if (level - Level > Allowed)
            {
                if (level >= SquareDefinition.MaxBuildingLevel)
                    return Level == GameRules.MaxHouses ? L("Yeniden gelince") : L("4 evle yeniden gelince");
                return Laps == 0 ? L("İlk turdan sonra") : F($"Bu gelişte en çok {Allowed}");
            }

            return L("Para yetmez");
        }
    }
}
