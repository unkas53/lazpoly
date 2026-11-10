using System.IO;
using System.Linq;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class FigureTests
    {
        private static void AssertColor(Color expected, Color actual) =>
            Assert.That(Vector4.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");

        [Test]
        public void EveryFigureIsACharacterWithItsPictures()
        {
            CollectionAssert.AreEquivalent(FigureModels.Ids, CharacterChoice.Ids, "one character per figure in the build");
            Assert.AreEqual(8, CharacterChoice.All.Count, "eight players each get a character of their own");
            Assert.AreEqual(CharacterChoice.All.Count, CharacterChoice.All.Select(option => option.Name).Distinct().Count());
            foreach (CharacterOption option in CharacterChoice.All)
            {
                Sprite portrait = KitArt.Get(option.Portrait);
                Sprite face = KitArt.Get(option.Face);
                Assert.IsNotNull(portrait, $"{option.Name}: run Tools/Art/make_figure_portraits.py");
                Assert.IsNotNull(face, option.Name);
                Assert.Less(portrait.rect.width, portrait.rect.height, "the whole figure stands upright");
                Assert.AreEqual(face.rect.width, face.rect.height, "the face fits a round avatar");
            }

            for (int seat = 0; seat < 8; seat++) Assert.AreEqual(CharacterChoice.All[seat], CharacterChoice.ForSeat(seat));
            Assert.AreEqual(CharacterChoice.All[0], CharacterChoice.ForSeat(8), "a ninth seat would start again");
        }

        [Test]
        public void APawnIsItsPlayersCharacterAndAnotherStandsInForAMissingOne()
        {
            var holder = new GameObject("Pawns");
            try
            {
                BoardView board = BoardView.Create(holder.transform, Data.BoardDataLoader.LoadFromResources());
                var crowd = new PawnCrowd(4);
                PawnView boss = PawnView.Create(holder.transform, board, 0, Color.red, crowd, 1, "boss");
                Assert.IsTrue(boss.IsCharacter);
                Assert.AreEqual("boss", boss.Figure);
                StringAssert.Contains("boss", boss.GetComponentInChildren<MeshFilter>().sharedMesh.name, "the boss's own figure");

                PawnView unknown = PawnView.Create(holder.transform, board, 1, Color.blue, crowd, 2, "nobody");
                Assert.IsTrue(unknown.IsCharacter, "a figure that is not in the build is stood in for, not dropped");
                Assert.AreEqual(1, unknown.GetComponentsInChildren<Renderer>().Length);
            }
            finally
            {
                Object.DestroyImmediate(holder);
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

            Assert.AreEqual(1, GameSceneController.Reaction(300000), "passing start is worth a cheer");
            Assert.AreEqual(-1, GameSceneController.Reaction(-250000), "rent is worth a sulk");
            Assert.AreEqual(0, GameSceneController.Reaction(-20000), "small change is not");
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

        private static string[] FigureIds => FigureModels.Ids;

        private static string[] SceneIds => new[] { FigureModels.StationId, FigureModels.StadiumId, FigureModels.TrophyId, FigureModels.MoneyStackId, FigureModels.GoldBarId };

        [TestCaseSource(nameof(SceneIds))]
        public void TheOtherMeshyModelsAlsoComeWithAnImportedColourMap(string id)
        {
            var skin = Resources.Load<Texture2D>(FigureModels.SkinResource(id));
            Assert.IsNotNull(skin, "imported as a texture, not decoded while playing");
            Assert.Greater(skin.mipmapCount, 8, "with all its mipmaps");
            Assert.AreEqual(FilterMode.Trilinear, skin.filterMode);
            Assert.IsNull(Resources.Load<TextAsset>(FigureModels.SkinResource(id)), "no old runtime JPEG beside it");
        }

        [TestCaseSource(nameof(FigureIds))]
        public void TheFiguresAreOneUnitTallAndLighterForMediumAndLow(string id)
        {
            string folder = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models");
            string path = Path.Combine(folder, $"figure_{id}.bytes");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_figures.py");
            Assert.IsTrue(File.Exists(Path.Combine(folder, $"figure_{id}_skin.jpg")), "the colour map ships with the figure");
            Assert.IsFalse(File.Exists(Path.Combine(folder, $"figure_{id}_skin.bytes")), "no colour map left for the game to decode and squeeze itself");
            var skin = Resources.Load<Texture2D>(FigureModels.SkinResource(id));
            Assert.IsNotNull(skin, "Unity imports the colour map as a texture");
            Assert.Greater(skin.mipmapCount, 8, "with all its mipmaps, so the small figure is not grainy");
            Assert.AreEqual(FilterMode.Trilinear, skin.filterMode);
            Assert.GreaterOrEqual(skin.width, 2048, "sharp enough for the figure's size on the screen");
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
                Assert.That(bounds.size.x, Is.InRange(0.25f, 0.6f));
                Assert.AreEqual(level.Positions.Length, level.Uvs.Length);
                foreach (Vector2 uv in level.Uvs) Assert.IsTrue(uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f);
            }

            var root = new GameObject("Figure Test");
            try
            {
                GameObject figure = FigureModels.Create(id, "Model", root.transform);
                Assert.IsNotNull(figure.transform.Find(FigureModels.BodyPart), "the textured figure, not the stand-in");
                Assert.AreEqual(1, figure.GetComponentsInChildren<Renderer>().Length, "only the figure: no disc under its feet");
                Assert.AreEqual(0f, figure.transform.Find(FigureModels.BodyPart).localPosition.y, "standing right on the tile");
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

        [Test]
        public void APaintedFigureFileCarriesEachVertexsColour()
        {
            byte[] figure = TinyFigure(painted: true);
            FigureMesh level = FigurePack.Parse(figure).Single();
            Assert.IsNotNull(level.Colors);
            Assert.AreEqual(new Color32(10, 20, 30, 255), level.Colors[0]);
            Assert.AreEqual(new Color32(250, 0, 128, 255), level.Colors[2]);
            Assert.That(level.Uvs[2].x, Is.EqualTo(1f).Within(1e-4f), "the colours follow the texture coordinates");
            Assert.AreEqual(1, level.TriangleCount);
            Assert.Throws<System.FormatException>(() => FigurePack.Parse(figure.Take(figure.Length - 2).ToArray()), "cut short");
            Assert.IsNull(FigurePack.Parse(TinyFigure()).Single().Colors, "an older file has no colours");
            Mesh mesh = level.ToMesh("painted");
            Assert.AreEqual(3, mesh.colors32.Length, "the mesh carries them");
            Object.DestroyImmediate(mesh);
        }

        private static string[] PaintedIds => FigureModels.Ids.Concat(new[] { FigureModels.StationId, FigureModels.BusId, FigureModels.StadiumId, FigureModels.TrophyId, FigureModels.MoneyStackId, FigureModels.GoldBarId }).ToArray();

        [TestCaseSource(nameof(PaintedIds))]
        public void EveryMeshyModelIsPaintedAndDrawnWithThePaintedShader(string id)
        {
            Assert.IsTrue(FigureModels.IsPainted(id), "run Tools/Art/make_figures.py --paint (see the README)");
            var root = new GameObject("Figure Test");
            try
            {
                GameObject figure = FigureModels.Create(id, id, root.transform);
                Renderer body = figure.GetComponentInChildren<Renderer>();
                Mesh mesh = body.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length, "every vertex has its colour");
                Color32 first = mesh.colors32[0];
                Assert.IsTrue(mesh.colors32.Any(color => color.r != first.r || color.g != first.g || color.b != first.b), "painted from the colour map, not one flat colour");
                Assert.AreEqual(FigureModels.PaintedShaderName, body.sharedMaterial.shader.name);
                Assert.IsTrue(body.sharedMaterial.shader.isSupported, "the shader compiles");
                Assert.IsNotNull(body.sharedMaterial.mainTexture, "the colour map still shows where it is drawn large");
                Assert.AreEqual(Color.white, body.sharedMaterial.color);

                Material faded = ModelFade.Copy(body.sharedMaterial, 0.4f);
                Assert.AreEqual(FigureModels.PaintedShaderName, faded.shader.name, "a waiting player's figure fades in the same shader");
                Assert.AreEqual(0.4f, faded.color.a, 1e-4f);
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, faded.renderQueue);
                Assert.AreEqual(0f, faded.GetFloat("_ZWrite"));
                Assert.AreEqual(1f, body.sharedMaterial.GetFloat("_ZWrite"), "the solid one stays solid");
                Object.DestroyImmediate(faded);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>One triangle in the make_figures.py format (painted: the "LZF2" kind with vertex colours).</summary>
        private static byte[] TinyFigure(bool painted = false)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(painted ? FigurePack.PaintedMagic : FigurePack.Magic);
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
            if (painted)
            {
                foreach (byte value in new byte[] { 10, 20, 30, 0, 255, 0, 250, 0, 128 }) writer.Write(value);
            }

            writer.Write((byte)2);
            writer.Write(3);
            foreach (ushort index in new ushort[] { 0, 1, 2 }) writer.Write(index);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
