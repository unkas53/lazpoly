using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The backdrop behind the table: the sky the board floats in (<see cref="SkySkin"/>; the owner's wishes, 2026-11-11:
    /// a backdrop that suits the game's atmosphere, alive but calm, since the eye belongs on the board). Pictures filling
    /// the camera's view at the far end of it, so the board and everything on it stand in front: the sky with its glow
    /// behind the board; two small far islands rising and sinking a little in the haze, three-dimensional since
    /// 2026-11-12 (the owner's wish; <see cref="IslandParts.Far"/>) and turning very slowly, so they show their depth;
    /// now and then a little aeroplane crossing behind the table (<see cref="SkyPlane"/>, the owner's wish too); and two
    /// layers of soft cloud drifting sideways, the nearer one faster, so the sky has depth. All of it moves slowly and by
    /// little. The islands and the aeroplane are models held in the camera's view, between the sky and the clouds. The
    /// pictures follow the zoom and the window's shape without being stretched, and warm with the evening like the flat
    /// sky did (<see cref="SetEvening"/>). Without the picture shader the camera's flat sky colour shows, as before.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        /// <summary>How the whole backdrop is tinted at dusk (white at noon).</summary>
        public static readonly Color DuskTint = new Color(1f, 0.78f, 0.66f);

        /// <summary>How long the near and the far clouds take to drift once round, in seconds: slow enough to go unnoticed while playing.</summary>
        public const float NearDriftSeconds = 420f;
        public const float FarDriftSeconds = 900f;

        /// <summary>How far a far island rises and sinks from its rest (in screen heights), and how long the first takes for it; the others a little longer each.</summary>
        public const float IslandRise = 0.004f;
        public const float IslandSeconds = 11f;

        /// <summary>How long the first far island takes to turn once round; the others a little longer each, every other one the other way.</summary>
        public const float IslandTurnSeconds = 240f;

        /// <summary>
        /// The owner's far-island model has a front, so it does not turn round: it keeps its front towards the camera
        /// (each island turned from that by its own <see cref="SkySkin.FarIsland.Turn"/>) and only sways about its
        /// upright, this far either way, the first once in this many seconds, the others a little slower each.
        /// </summary>
        public const float IslandSway = 9f;
        public const float IslandSwaySeconds = 46f;

        /// <summary>
        /// How deep in the camera's view each thing lies, as a share of the way to its far end: the sky last, the far
        /// islands before it with room for their depth (the owner's model is as deep as it is wide, and stands larger
        /// than the islands cut in code did), the aeroplane before them, then the clouds; all far behind the table.
        /// </summary>
        public const float SkyDepth = 0.99f;
        public const float IslandDepth = 0.943f;
        public const float PlaneDepth = 0.892f;
        public const float CloudDepth = 0.88f;

        public const string ModelsName = "Sky Models";
        public const string PlaneName = "Sky Plane";

        /// <summary>The pictures are drawn before everything else that is see-through: the sky first, then the far islands, the far clouds, the near clouds.</summary>
        public const int SkyQueue = 2900;

        public const string NearCloudsName = "Sky Clouds Near";
        public const string FarCloudsName = "Sky Clouds Far";

        // How much larger than the view the sky is, so no edge shows while the camera eases between zooms.
        private const float Spare = 1.05f;

        private Camera _camera;
        private Material _material, _nearMaterial, _farMaterial;
        private Transform _near, _far;
        private Transform[] _islands;
        private Transform _plane;
        private bool _modelled;
        private int _heldPass = -1;
        private float _heldProgress;

        /// <summary>The cloud layers in front of the sky; null without the picture shader.</summary>
        public Transform NearClouds => _near;
        public Transform FarClouds => _far;

        /// <summary>The far islands, in <see cref="SkySkin.FarIslands"/>' order: each a model about its own middle.</summary>
        public System.Collections.Generic.IReadOnlyList<Transform> FarIslands => _islands;

        /// <summary>Whether the far islands are the owner's model (<see cref="IslandParts.FarModel"/>), which sways with its front to the camera, rather than the ones cut in code, which turn round.</summary>
        public bool Modelled => _modelled;

        /// <summary>The aeroplane; switched off while none is crossing.</summary>
        public Transform Plane => _plane;

        /// <summary>Puts the backdrop behind <paramref name="camera"/>'s view, once; null when the picture shader is missing.</summary>
        public static SkyBackdrop Attach(Camera camera)
        {
            if (camera == null) return null;
            SkyBackdrop backdrop = camera.GetComponentInChildren<SkyBackdrop>();
            if (backdrop != null) return backdrop;
            if (!PicturePlate.IsAvailable) return null;
            GameObject plate = PicturePlate.Create("Sky Backdrop", camera.transform, SkySkin.Sky(), Vector2.one);
            backdrop = plate.AddComponent<SkyBackdrop>();
            backdrop._camera = camera;
            backdrop._material = Layer(plate, 0);

            // The models hang in the camera's view beside the sky's plate (which is stretched to the view, and would stretch them).
            var models = new GameObject(ModelsName);
            models.transform.SetParent(camera.transform, false);
            int count = SkySkin.FarIslands.Count;
            backdrop._islands = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                int seed = SkySkin.FarIslands[i].Seed;
                // The owner's model where its files are in the build; else the island cut in code, its loose stones adrift.
                GameObject modelled = IslandParts.FarModel(models.transform, $"Far island {i + 1}");
                backdrop._modelled = modelled != null;
                Transform island = (modelled ?? IslandParts.Far(models.transform, $"Far island {i + 1}", seed)).transform;
                IslandDrift.Attach(island.Find(IslandParts.FarModelName).Find(IslandParts.StonesName), null, seed);
                backdrop._islands[i] = island;
            }

            backdrop._plane = PlaneParts.Create(models.transform, PlaneName).transform;

            GameObject far = PicturePlate.Create(FarCloudsName, plate.transform, SkySkin.Clouds(false), Vector2.one);
            backdrop._far = far.transform;
            backdrop._farMaterial = Layer(far, 2);
            GameObject near = PicturePlate.Create(NearCloudsName, plate.transform, SkySkin.Clouds(true), Vector2.one);
            backdrop._near = near.transform;
            backdrop._nearMaterial = Layer(near, 3);
            backdrop.Show(0f);
            return backdrop;
        }

        private static Material Layer(GameObject plate, int order)
        {
            Material material = plate.GetComponent<MeshRenderer>().sharedMaterial;
            material.renderQueue = SkyQueue + order;
            return material;
        }

        /// <summary>Warms the backdrop towards dusk: 0 by day, 1 at dusk.</summary>
        public void SetEvening(float evening)
        {
            Color tint = Color.Lerp(Color.white, DuskTint, Mathf.Clamp01(evening));
            if (_material != null) _material.color = tint;
            if (_nearMaterial != null) _nearMaterial.color = tint;
            if (_farMaterial != null) _farMaterial.color = tint;
        }

        /// <summary>
        /// Holds the aeroplane at one point of a crossing whatever the time (for a picture of it), or lets it fly by the
        /// clock again with a pass below 0.
        /// </summary>
        public void HoldPlane(int pass, float progress)
        {
            _heldPass = pass;
            _heldProgress = Mathf.Clamp01(progress);
            Show(Time.time);
        }

        private void LateUpdate() => Show(Time.time);

        /// <summary>
        /// The part of a picture <paramref name="picture"/> wide for its height that a view <paramref name="view"/> wide
        /// for its height shows, so the picture covers the view without being stretched: the scale (x, y) and the offset
        /// (z, w) of its texture, the cut taken evenly from both sides.
        /// </summary>
        public static Vector4 Cover(float view, float picture)
        {
            float across = Mathf.Min(1f, view / picture), up = Mathf.Min(1f, picture / view);
            return new Vector4(across, up, (1f - across) / 2f, (1f - up) / 2f);
        }

        /// <summary>How far a cloud layer has drifted at a time: a share of its picture's width, 0 to 1, round and round.</summary>
        public static float DriftAt(float seconds, float driftSeconds) => Mathf.Repeat(seconds / driftSeconds, 1f);

        /// <summary>How long a far island takes for one rise and fall.</summary>
        public static float IslandSecondsOf(int island) => IslandSeconds * (1f + 0.27f * island);

        /// <summary>How far a far island is above (or below) its rest at a time, in screen heights; each island in its own time.</summary>
        public static float IslandRiseAt(float seconds, int island) =>
            IslandRise * Mathf.Sin(2f * Mathf.PI * (seconds / IslandSecondsOf(island) + 0.37f * island));

        /// <summary>How far round a far island has turned at a time, in degrees about the world's upright: each from its own start, at its own slow pace, every other one the other way.</summary>
        public static float IslandTurnAt(float seconds, int island)
        {
            float turns = seconds / (IslandTurnSeconds * (1f + 0.31f * island));
            return Mathf.Repeat(137f * island + 360f * (island % 2 == 0 ? turns : -turns), 360f);
        }

        /// <summary>How long a far island's model takes to sway once there and back.</summary>
        public static float IslandSwaySecondsOf(int island) => IslandSwaySeconds * (1f + 0.31f * island);

        /// <summary>How far a far island's model has swayed from its rest at a time, in degrees about its upright; each island in its own time.</summary>
        public static float IslandSwayAt(float seconds, int island) =>
            IslandSway * Mathf.Sin(2f * Mathf.PI * (seconds / IslandSwaySecondsOf(island) + 0.23f * island));

        /// <summary>
        /// Which way a far island's model is turned at a time, in degrees about the world's upright, for a camera looking
        /// along <paramref name="cameraYaw"/>: its front (+Z) towards the camera, turned from that by the island's own
        /// <see cref="SkySkin.FarIsland.Turn"/> and by its sway.
        /// </summary>
        public static float IslandFacingAt(float seconds, int island, float cameraYaw) =>
            Mathf.Repeat(cameraYaw + 180f + SkySkin.FarIslands[island].Turn + IslandSwayAt(seconds, island), 360f);

        /// <summary>How high the camera's view is at a depth (a share of the way to its far end): the same at every depth for the game's flat view.</summary>
        private float HeightAt(float depth) => _camera.orthographic
            ? 2f * _camera.orthographicSize
            : 2f * _camera.farClipPlane * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// Fills the camera's view just inside its far end, whatever the zoom and the window's shape, and puts the clouds,
        /// the far islands and the aeroplane where they are <paramref name="seconds"/> into their drift.
        /// </summary>
        public void Show(float seconds)
        {
            if (_camera == null) return;
            float far = _camera.farClipPlane, aspect = _camera.aspect, step = far * 0.004f;
            float height = HeightAt(1f);
            transform.localScale = new Vector3(height * aspect * Spare, height * Spare, 1f);
            transform.localPosition = new Vector3(0f, 0f, far * SkyDepth);
            transform.localRotation = Quaternion.identity;
            Vector4 cover = Cover(aspect, (float)SkySkin.Width / SkySkin.Height);
            _material.mainTextureScale = new Vector2(cover.x, cover.y);
            _material.mainTextureOffset = new Vector2(cover.z, cover.w);

            // The far islands: where they are on the screen, their size a share of the view's height, standing upright in
            // the world (so the slanted camera sees their meadows) and turning slowly about that upright; the owner's
            // model keeps its front to the camera and only sways.
            float tall = HeightAt(IslandDepth);
            for (int i = 0; i < _islands.Length; i++)
            {
                SkySkin.FarIsland island = SkySkin.FarIslands[i];
                _islands[i].localPosition = new Vector3((island.U - 0.5f) * tall * aspect, (island.V - 0.5f + IslandRiseAt(seconds, i)) * tall, far * IslandDepth);
                _islands[i].localScale = Vector3.one * (island.Size * tall / IslandShape.FarSpan);
                float turn = _modelled ? IslandFacingAt(seconds, i, _camera.transform.eulerAngles.y) : IslandTurnAt(seconds, i);
                _islands[i].rotation = Quaternion.Euler(0f, turn, 0f);
            }

            ShowPlane(seconds, far, aspect);

            // The clouds nearer still; their pictures repeat sideways, so a wide view just shows more of them.
            var scale = new Vector2(aspect * SkySkin.CloudHeight / SkySkin.CloudWidth, 1f);
            float clouds = far * (CloudDepth - SkyDepth);
            _far.localPosition = new Vector3(0f, 0f, clouds);
            _farMaterial.mainTextureScale = scale;
            _farMaterial.mainTextureOffset = new Vector2(DriftAt(seconds, FarDriftSeconds), 0f);
            _near.localPosition = new Vector3(0f, 0f, clouds - step);
            _nearMaterial.mainTextureScale = scale;
            // Started half way round, so the two layers never line up as they were drawn.
            _nearMaterial.mainTextureOffset = new Vector2(DriftAt(seconds + NearDriftSeconds / 2f, NearDriftSeconds), 0f);
        }

        /// <summary>
        /// The aeroplane: out of sight between crossings; on one, flying level and straight across the view, banked a
        /// little away from the camera (<see cref="SkyPlane.Bank"/>) so the slanted view shows its side, its propeller turning.
        /// </summary>
        private void ShowPlane(float seconds, float far, float aspect)
        {
            if (_plane == null) return;
            int pass = _heldPass >= 0 ? _heldPass : SkyPlane.PassAt(seconds);
            float progress = _heldPass >= 0 ? _heldProgress : SkyPlane.ProgressAt(seconds);
            bool flying = pass >= 0 && progress >= 0f;
            if (_plane.gameObject.activeSelf != flying) _plane.gameObject.SetActive(flying);
            if (!flying) return;
            float tall = HeightAt(PlaneDepth);
            Vector2 spot = SkyPlane.SpotAt(pass, progress);
            _plane.localPosition = new Vector3((spot.x - 0.5f) * tall * aspect, (spot.y - 0.5f) * tall, far * PlaneDepth);
            _plane.localScale = Vector3.one * (SkyPlane.Size * tall / PlaneParts.Length);
            Vector3 way = _camera.transform.right * (SkyPlane.Rightward(pass) ? 1f : -1f);
            way.y = 0f;
            way = way.sqrMagnitude > 1e-6f ? way.normalized : Vector3.right;
            // Its top leans away from the camera, about its own line of flight.
            Vector3 away = Vector3.Cross(way, Vector3.up) * (SkyPlane.Rightward(pass) ? 1f : -1f);
            Vector3 top = Quaternion.AngleAxis(SkyPlane.Bank, Vector3.Cross(Vector3.up, away)) * Vector3.up;
            _plane.rotation = Quaternion.LookRotation(way, top);
            PlaneParts.Spin(_plane, seconds);
        }
    }
}
