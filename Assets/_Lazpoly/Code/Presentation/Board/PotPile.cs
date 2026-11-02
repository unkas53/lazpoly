using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The tea money seen on the lawn: a glass jar that is always there, with coins stacking up round
    /// its foot as the pot grows and going again when it is paid out. One coin stands for
    /// <see cref="CoinValue"/>; past the last stack the pile stops growing.
    /// </summary>
    public sealed class PotPile
    {
        public const int CoinValue = 50;
        public const float JarScale = 0.5f;

        private readonly List<GameObject> _coins = new List<GameObject>();

        public PotPile(Transform parent)
        {
            var root = new GameObject("Pot pile");
            root.transform.SetParent(parent, false);
            Root = root.transform;
            LawnParts.Jar("Jar", Root).transform.localScale = Vector3.one * JarScale;
        }

        public Transform Root { get; }

        /// <summary>How many coins lie round the jar now.</summary>
        public int Coins => _coins.Count;

        /// <summary>Shows a pot of <paramref name="amount"/>: coins are added or taken away to match.</summary>
        public void Show(int amount)
        {
            int wanted = LawnParts.CoinsFor(amount, CoinValue);
            while (_coins.Count > wanted)
            {
                GameObject coin = _coins[_coins.Count - 1];
                _coins.RemoveAt(_coins.Count - 1);
                if (Application.isPlaying) Object.Destroy(coin);
                else Object.DestroyImmediate(coin);
            }

            while (_coins.Count < wanted)
            {
                GameObject coin = LawnParts.CoinPiece("Coin", Root);
                coin.transform.localPosition = LawnParts.CoinSpot(_coins.Count, JarScale * 0.36f);
                coin.transform.localRotation = Quaternion.Euler(0f, _coins.Count * 23f, 0f);
                _coins.Add(coin);
            }
        }
    }
}
