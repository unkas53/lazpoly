using System.IO;
using Lazpoly.Presentation.Game;
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

        [Test]
        public void TheWomanStandsInTheSameStyleWithASkirtAndLongHair()
        {
            string path = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "business_woman.txt");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_businesswoman.py and Tools/Art/make_character.py woman");
            ObjModel woman = ObjModel.Parse(File.ReadAllText(path));

            var materials = new System.Collections.Generic.HashSet<string>();
            foreach (ObjGroup group in woman.Groups)
            {
                Assert.IsTrue(materials.Add(group.Material), $"material {group.Material} is split over several groups");
                Assert.AreNotEqual(Color.magenta, CharacterParts.ColorFor(group.Material), $"{group.Material} is not in the palette");
            }

            foreach (string painted in CharacterParts.PaintedParts) Assert.IsNotNull(woman.Find(painted), $"no '{painted}' part to paint");
            Assert.IsNotNull(woman.Find(CharacterParts.SkirtPart), "the woman wears a skirt");
            Assert.IsNull(woman.Find("beard"), "no beard");
            Assert.IsNull(woman.Find("jeans"), "tights and a skirt, not jeans");
            Assert.That(woman.TriangleCount, Is.InRange(10000, 14000));

            Bounds bounds = woman.Bounds;
            Assert.That(bounds.size.y, Is.EqualTo(1f).Within(0.01f), "one unit tall like the man");
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(bounds.size.x, Is.InRange(0.3f, 0.5f));
            Assert.Less(bounds.size.x, CharacterParts.Load().Bounds.size.x, "slimmer than the man");
            Bounds skirt = woman.Find(CharacterParts.SkirtPart).Bounds;
            Assert.That(skirt.min.y, Is.InRange(0.22f, 0.34f), "the skirt ends near the knee");
            Assert.That(skirt.max.y, Is.InRange(0.42f, 0.52f), "and starts at the waist");
            Bounds hair = woman.Find("hair_w").Bounds;
            Assert.Less(hair.min.y, 0.73f, "the hair falls to the shoulders");
            Assert.Less(hair.center.z, 0f, "behind the head, the face stays open");
            Assert.Greater(woman.Find("tablet").Bounds.center.z, 0.05f, "she holds the tablet in front too");
        }

        [Test]
        public void SeatsAlternateBetweenTheTwoFigures()
        {
            Assert.AreEqual(CharacterKind.Man, CharacterParts.KindFor(0));
            Assert.AreEqual(CharacterKind.Woman, CharacterParts.KindFor(1));
            Assert.AreEqual(CharacterKind.Man, CharacterParts.KindFor(6));
            Assert.AreEqual(CharacterKind.Woman, CharacterParts.KindFor(7));
            Assert.AreNotEqual(CharacterParts.ResourceOf(CharacterKind.Man), CharacterParts.ResourceOf(CharacterKind.Woman));

            var root = new GameObject("Woman Test");
            try
            {
                GameObject woman = CharacterParts.Character(CharacterKind.Woman, "Model", root.transform);
                Assert.IsNotNull(woman);
                Assert.AreEqual(CharacterParts.Load(CharacterKind.Woman).Groups.Count, woman.transform.childCount);
                var player = new Color(0.2f, 0.45f, 0.85f);
                CharacterParts.Paint(woman, player);
                AssertColor(player, PartColor(woman, CharacterParts.JacketPart));
                AssertColor(CharacterParts.Skirt(player), PartColor(woman, CharacterParts.SkirtPart));
                Assert.Less(CharacterParts.Skirt(player).grayscale, CharacterParts.Lapel(player).grayscale, "the skirt is darker than the lapels");
                Assert.IsTrue(CharacterParts.IsPainted(CharacterParts.SkirtPart));
                AssertColor(CharacterParts.ColorFor("blouse"), PartColor(woman, "blouse"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheFiguresMovesStartAndEndAtRest()
        {
            (float height, float spin) = CharacterMotion.CheerPose(0f);
            Assert.That(height, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(spin, Is.EqualTo(0f).Within(1e-3f));
            (height, spin) = CharacterMotion.CheerPose(1f);
            Assert.That(height, Is.EqualTo(0f).Within(1e-4f), "lands again");
            Assert.That(spin, Is.EqualTo(360f).Within(1e-3f), "a full turn, so the figure faces the camera again");
            float highest = 0f;
            for (float t = 0f; t <= 1f; t += 0.01f) highest = Mathf.Max(highest, CharacterMotion.CheerPose(t).Height);
            Assert.That(highest, Is.EqualTo(CharacterMotion.CheerHeight).Within(0.01f));

            (float slump, float shake) = CharacterMotion.SulkPose(0f);
            Assert.That(slump, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(CharacterMotion.SulkPose(0.5f).Slump, Is.EqualTo(1f).Within(1e-4f), "slumped in the middle");
            (slump, shake) = CharacterMotion.SulkPose(1f);
            Assert.That(slump, Is.EqualTo(0f).Within(1e-4f), "and standing up again");
            Assert.That(shake, Is.EqualTo(0f).Within(1e-4f));
            float widest = 0f;
            for (float t = 0f; t <= 1f; t += 0.005f) widest = Mathf.Max(widest, Mathf.Abs(CharacterMotion.SulkPose(t).Shake));
            Assert.That(widest, Is.InRange(CharacterMotion.SulkShakeDegrees * 0.6f, CharacterMotion.SulkShakeDegrees + 0.01f), "a visible shake of the head");

            for (float time = 0f; time < 4f; time += 0.1f) Assert.That(CharacterMotion.Breath(time, 0.3f), Is.InRange(1f - CharacterMotion.BreathDepth, 1f + CharacterMotion.BreathDepth));
            Assert.AreEqual((0f, 0f), CharacterMotion.WalkPose(0f, 1.3f), "no lean when standing");
            Assert.That(CharacterMotion.WalkPose(1f, 0f).Lean, Is.EqualTo(CharacterMotion.WalkLeanDegrees));

            Assert.AreEqual(1, GameSceneController.Reaction(200), "passing start is worth a cheer");
            Assert.AreEqual(-1, GameSceneController.Reaction(-120), "rent is worth a sulk");
            Assert.AreEqual(0, GameSceneController.Reaction(-20), "small change is not");
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
