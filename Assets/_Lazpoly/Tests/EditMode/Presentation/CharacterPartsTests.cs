using System.IO;
using System.Linq;
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
        public void SeatsTakeTheFourFiguresInTurn()
        {
            Assert.AreEqual(CharacterKind.Man, CharacterParts.KindFor(0));
            Assert.AreEqual(CharacterKind.Woman, CharacterParts.KindFor(1));
            Assert.AreEqual(CharacterKind.Suit, CharacterParts.KindFor(2));
            Assert.AreEqual(CharacterKind.Gentleman, CharacterParts.KindFor(3));
            Assert.AreEqual(CharacterKind.Man, CharacterParts.KindFor(4));
            Assert.AreEqual(CharacterKind.Suit, CharacterParts.KindFor(6));
            Assert.AreEqual(CharacterKind.Gentleman, CharacterParts.KindFor(7));
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

        [TestCase(CharacterKind.Suit)]
        [TestCase(CharacterKind.Gentleman)]
        public void TheTexturedFiguresAreOneUnitTallAndLighterForMediumAndLow(CharacterKind kind)
        {
            string id = CharacterParts.FigureOf(kind);
            string folder = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models");
            string path = Path.Combine(folder, $"figure_{id}.bytes");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_figures.py");
            Assert.IsTrue(File.Exists(Path.Combine(folder, $"figure_{id}_skin.bytes")), "the colour map ships with the figure");
            var levels = FigurePack.Parse(File.ReadAllBytes(path));
            FigureMesh high = levels.Single(level => level.Name == FigurePack.HighLevel);
            FigureMesh low = levels.Single(level => level.Name == FigurePack.LowLevel);
            Assert.That(high.TriangleCount, Is.InRange(60000, 130000), "detailed on High, yet eight of them stay smooth");
            Assert.That(low.TriangleCount, Is.InRange(10000, 30000));
            Assert.Less(low.TriangleCount, high.TriangleCount / 3);

            foreach (FigureMesh level in levels)
            {
                var bounds = new Bounds(level.Positions[0], Vector3.zero);
                foreach (Vector3 position in level.Positions) bounds.Encapsulate(position);
                Assert.That(bounds.size.y, Is.EqualTo(1f).Within(0.01f), $"{level.Name}: one unit tall like the others");
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), $"{level.Name}: the soles stand on the tile");
                Assert.That(bounds.size.x, Is.InRange(0.3f, 0.6f));
                Assert.AreEqual(level.Positions.Length, level.Uvs.Length);
                foreach (Vector2 uv in level.Uvs) Assert.IsTrue(uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f);
            }

            var root = new GameObject("Figure Test");
            try
            {
                GameObject figure = CharacterParts.Character(kind, "Model", root.transform);
                Assert.IsNotNull(figure.transform.Find(FigureModels.BodyPart), "the textured figure, not the stand-in");
                Assert.AreEqual(1, figure.GetComponentsInChildren<Renderer>().Length, "only the figure: no disc under its feet");
                Assert.AreEqual(0f, figure.transform.Find(FigureModels.BodyPart).localPosition.y, "standing right on the tile");
                CharacterParts.Paint(figure, new Color(0.85f, 0.39f, 0.33f));
                Renderer body = figure.transform.Find(FigureModels.BodyPart).GetComponent<Renderer>();
                AssertColor(Color.white, body.sharedMaterial.color);
                Assert.IsNotNull(body.sharedMaterial.mainTexture, "the figure wears its own colour map");
                Assert.AreEqual(high.Positions.Length, body.GetComponent<MeshFilter>().sharedMesh.vertexCount, "the whole model on High");
                FigureModels.SetFullDetail(false);
                Assert.AreEqual(low.Positions.Length, body.GetComponent<MeshFilter>().sharedMesh.vertexCount, "the light model on Medium and Low");
            }
            finally
            {
                FigureModels.SetFullDetail(true);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AFigureFileIsReadWholeOrRefused()
        {
            byte[] figure = TinyFigure();
            FigureMesh level = FigurePack.Parse(figure).Single();
            Assert.AreEqual("High", level.Name);
            Assert.AreEqual(1, level.TriangleCount);
            Assert.That(level.Positions[1].y, Is.EqualTo(1f));
            Assert.That(level.Normals[0].z, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(level.Uvs[2].x, Is.EqualTo(1f).Within(1e-4f));
            Assert.Throws<System.FormatException>(() => FigurePack.Parse(figure.Take(figure.Length - 2).ToArray()), "cut short");
            Assert.Throws<System.FormatException>(() => FigurePack.Parse(figure.Concat(new byte[] { 0 }).ToArray()), "extra bytes");
            byte[] wrong = (byte[])figure.Clone();
            wrong[0] = (byte)'X';
            Assert.Throws<System.FormatException>(() => FigurePack.Parse(wrong));
        }

        /// <summary>One triangle in the make_figures.py format.</summary>
        private static byte[] TinyFigure()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(FigurePack.Magic);
            writer.Write((byte)1);
            writer.Write((byte)4);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("High"));
            writer.Write(3);
            foreach (float value in new[] { 0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f }) writer.Write(value);
            for (int i = 0; i < 3; i++)
            {
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((short)32767);
            }

            foreach (ushort value in new ushort[] { 0, 0, 0, 65535, 65535, 0 }) writer.Write(value);
            writer.Write((byte)2);
            writer.Write(3);
            foreach (ushort index in new ushort[] { 0, 1, 2 }) writer.Write(index);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
