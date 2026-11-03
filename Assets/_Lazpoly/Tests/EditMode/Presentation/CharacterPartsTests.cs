using System.IO;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class CharacterPartsTests
    {
        private static void AssertColor(Color expected, Color actual) =>
            Assert.That(Vector4.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");

        [Test]
        public void TheCharacterAssetIsOneUnitTallOnItsFeetAndFacesForward()
        {
            string path = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "business_man.txt");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_character.py");
            ObjModel character = ObjModel.Parse(File.ReadAllText(path));

            Assert.That(character.Groups.Count, Is.InRange(25, 35), "one group per material of the palette");
            var materials = new System.Collections.Generic.HashSet<string>();
            foreach (ObjGroup group in character.Groups)
            {
                Assert.IsTrue(materials.Add(group.Material), $"material {group.Material} is split over several groups");
                Assert.AreEqual(group.Vertices.Length, group.Normals.Length, $"{group.Material}: every face has its flat normal");
                Assert.AreNotEqual(Color.magenta, CharacterParts.ColorFor(group.Material), $"{group.Material} is not in the palette");
            }

            foreach (string painted in CharacterParts.PaintedParts) Assert.IsNotNull(character.Find(painted), $"no '{painted}' part to paint");
            Assert.That(character.TriangleCount, Is.InRange(13000, 14000));

            Bounds bounds = character.Bounds;
            Assert.That(bounds.size.y, Is.EqualTo(1f).Within(0.01f), "the character is one unit tall");
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), "the soles stand on the tile");
            Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(bounds.size.x, Is.InRange(0.3f, 0.5f), "shoulders and elbows, about two fifths of the height");
            Assert.That(character.Find("shoe").Bounds.center.z, Is.EqualTo(0f).Within(0.05f), "the feet are centred, not the tablet");
            Assert.Greater(character.Find("tablet").Bounds.center.z, 0.05f, "the tablet is held out in front, so the figure faces +Z");
            Assert.Greater(character.Find("hair").Bounds.center.y, 0.85f, "the hair is on top");
        }

        [Test]
        public void TheCharacterIsPaintedOnTheJacketOnly()
        {
            var root = new GameObject("Character Test");
            try
            {
                Assert.IsTrue(CharacterParts.IsAvailable, "the character model is not in Resources");
                GameObject character = CharacterParts.Character("Model", root.transform);
                Assert.IsNotNull(character);
                Assert.AreEqual(CharacterParts.Load().Groups.Count, character.transform.childCount, "one part per material");

                var player = new Color(0.9f, 0.22f, 0.21f);
                CharacterParts.Paint(character, player);
                Color jacket = PartColor(character, CharacterParts.JacketPart);
                Color light = PartColor(character, CharacterParts.JacketLightPart);
                Color dark = PartColor(character, CharacterParts.JacketDarkPart);
                Color lapel = PartColor(character, CharacterParts.LapelPart);
                AssertColor(player, jacket);
                AssertColor(CharacterParts.Light(player), light);
                AssertColor(CharacterParts.Dark(player), dark);
                AssertColor(CharacterParts.Lapel(player), lapel);
                Assert.Greater(light.grayscale, jacket.grayscale, "sleeves and folds are lighter than the jacket");
                Assert.Less(dark.grayscale, lapel.grayscale, "pockets are darker than the lapels");
                Assert.Less(lapel.grayscale, jacket.grayscale, "lapels are darker than the jacket");
                AssertColor(CharacterParts.ColorFor("skin"), PartColor(character, "skin"));
                AssertColor(CharacterParts.ColorFor("jeans"), PartColor(character, "jeans"));
                AssertColor(CharacterParts.ColorFor("shirt"), PartColor(character, "shirt"));
                Assert.IsTrue(CharacterParts.IsPainted(CharacterParts.LapelPart));
                Assert.IsFalse(CharacterParts.IsPainted("skin"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(BoardCameraRig.BaseYaw)]
        [TestCase(BoardCameraRig.BaseYaw + 90f)]
        [TestCase(BoardCameraRig.BaseYaw + 180f)]
        public void ThePawnFacesTheCameraFromAnyCorner(float yaw)
        {
            Vector3 forward = PawnView.Facing(yaw) * Vector3.forward;
            Assert.That(Vector3.Dot(forward, BoardView.ScreenUp(yaw)), Is.EqualTo(-1f).Within(1e-4f), "the face looks at the camera");
            Assert.That(forward.y, Is.EqualTo(0f).Within(1e-4f), "and stays upright");
            Assert.Greater(PawnView.CharacterHeight, 0.55f, "a person stands taller than the town's shapes");
            Assert.Greater(Effects.SpeechBubble.Lift, PawnView.CharacterHeight, "the speech bubble floats over the head");
        }

        private static Color PartColor(GameObject character, string material) =>
            character.transform.Find(material).GetComponent<Renderer>().sharedMaterial.color;
    }
}
