using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Where the lawn's decorations stand: trees and bushes in the corners of the park outside the
    /// path, bunting on posts along the two near sides of the path. Positions are fractions of the
    /// lawn's inner width, measured from the board's centre. The camera looks down at 35 degrees, so a
    /// piece's height falls up the screen over the ground behind it: every tree stands where its crown
    /// still falls on the lawn, never on the squares' names, and the far corner (the top of the screen,
    /// where the title lies) has only low bushes.
    /// </summary>
    public static class LawnDressing
    {
        /// <summary>Half the grass's width, as a fraction of the inner width (<see cref="BoardView"/> makes the grass 0.9 of it).</summary>
        public const float LawnHalf = 0.45f;

        /// <summary>Half the path's width, as a fraction of the inner width (the path is 0.44 of it).</summary>
        public const float PathHalf = 0.22f;

        /// <summary>Where the bunting posts stand: just outside the path's corners.</summary>
        public const float PostInset = 0.245f;

        /// <summary>A bush's height, for the room it needs up the screen.</summary>
        public const float BushHeight = 0.2f;

        public const int PennantsPerString = 9;

        /// <summary>Trees as (x, z) fractions of the inner width, with the kind of each. The two of a side corner stand apart across the screen, not one over the other.</summary>
        public static readonly Vector2[] TreeSpots =
        {
            new Vector2(-0.33f, -0.26f), new Vector2(-0.27f, -0.42f),
            new Vector2(0.42f, 0.27f), new Vector2(0.26f, 0.33f),
            new Vector2(0.36f, -0.36f),
        };

        public static readonly int[] TreeKinds = { 0, 1, 2, 0, 1 };

        /// <summary>Bushes as (x, z) fractions of the inner width; the far corner has nothing but these.</summary>
        public static readonly Vector2[] BushSpots =
        {
            new Vector2(-0.41f, -0.40f), new Vector2(-0.28f, -0.30f),
            new Vector2(0.41f, 0.41f), new Vector2(0.29f, 0.27f),
            new Vector2(0.42f, -0.29f), new Vector2(0.29f, -0.42f),
            new Vector2(-0.38f, 0.38f), new Vector2(-0.41f, 0.30f), new Vector2(-0.30f, 0.40f),
        };

        /// <summary>The bunting posts in order along the string: the left corner of the path, the near corner, the right corner.</summary>
        public static readonly Vector2[] PostSpots =
        {
            new Vector2(-PostInset, -PostInset), new Vector2(PostInset, -PostInset), new Vector2(PostInset, PostInset),
        };

        /// <summary>Builds everything on the lawn under <paramref name="root"/>, on a lawn <paramref name="inner"/> wide whose grass top is at <paramref name="surfaceY"/>.</summary>
        public static void Dress(Transform root, float inner, float surfaceY)
        {
            for (int i = 0; i < TreeSpots.Length; i++)
            {
                GameObject tree = LawnParts.Tree("Tree", root, TreeKinds[i]);
                tree.transform.localPosition = Ground(TreeSpots[i], inner, surfaceY);
                // Each tree turns a little so the double crowns do not all lean the same way.
                tree.transform.localRotation = Quaternion.Euler(0f, i * 70f, 0f);
            }

            for (int i = 0; i < BushSpots.Length; i++)
            {
                GameObject bush = LawnParts.Bush("Bush", root);
                bush.transform.localPosition = Ground(BushSpots[i], inner, surfaceY);
                bush.transform.localRotation = Quaternion.Euler(0f, i * 50f, 0f);
            }

            var ties = new Vector3[PostSpots.Length];
            for (int i = 0; i < PostSpots.Length; i++)
            {
                GameObject post = LawnParts.BuntingPost("Bunting post", root);
                post.transform.localPosition = Ground(PostSpots[i], inner, surfaceY);
                ties[i] = post.transform.localPosition + Vector3.up * LawnParts.StringHeight;
            }

            for (int i = 1; i < ties.Length; i++) LawnParts.Bunting("Bunting", root, ties[i - 1], ties[i], PennantsPerString);
        }

        /// <summary>A spot's place on the lawn: the fraction scaled to the lawn, on the grass's top.</summary>
        public static Vector3 Ground(Vector2 spot, float inner, float surfaceY) => new Vector3(spot.x * inner, surfaceY, spot.y * inner);

        /// <summary>The ground a piece's top appears over, for a camera turned by <paramref name="yaw"/>: the piece's height falls up the screen.</summary>
        public static Vector3 GroundUnderTop(Vector3 basePoint, float height, float yaw) =>
            basePoint + BoardView.ScreenUp(yaw) * (height / Mathf.Tan(BoardCameraRig.Pitch * Mathf.Deg2Rad));
    }
}
