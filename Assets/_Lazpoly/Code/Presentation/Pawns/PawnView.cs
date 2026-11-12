using System;
using System.Collections;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// A player's piece: the character they picked, a textured figure from <see cref="FigureModels"/> in its own
    /// clothes, standing on the square and facing the camera; or, when no figure is in the build, one of the
    /// town's shapes from <see cref="PawnShapes"/> painted in the player's colour. The model is a child of the pawn so the hop's squash and stretch scale the pawn, not the model's
    /// size; the character's own body language (breathing, leaning into a walk, cheering, sulking) is
    /// <see cref="CharacterMotion"/> on that child.
    /// </summary>
    public sealed class PawnView : MonoBehaviour
    {
        /// <summary>
        /// The character's height on the board; the shapes are about 0.55. The owner asked for him
        /// large (at 0.9 he was about 60 pixels at 1080p): he stands over one and a half tiles tall,
        /// so the figure reads from across the room even though he may cover a name behind him.
        /// Faz 21: the owner asked for half as tall again (1.8 to 2.7).
        /// </summary>
        public const float CharacterHeight = 2.7f;

        /// <summary>The child that holds the character or the shape.</summary>
        public const string ModelName = "Model";

        private const float StepSeconds = 0.18f;
        private const float HopHeight = 0.35f;
        private const float SquashSeconds = 0.07f;
        private const float ToppleSeconds = 0.55f;

        /// <summary>A walk never takes longer than this at normal speed; long moves hop faster instead.</summary>
        public const float MaxWalkSeconds = 2f;

        private BoardView _board;
        private PawnCrowd _crowd;
        private int _playerId;
        private int _slot;
        private int _square;
        private Transform _model;
        private CharacterMotion _motion;
        private float _speed = 1f;
        private Renderer[] _parts;
        private Material[][] _solid;
        private Material[][] _faded;

        /// <summary>How much of a waiting player's piece still shows: the one on turn stands solid, the rest fade to this.</summary>
        public const float FadeAlpha = 0.4f;

        /// <summary>True while the piece is see-through (its player waits for their turn).</summary>
        public bool Faded { get; private set; }

        /// <summary>True when the pawn is the character; false when it is one of the shapes.</summary>
        public bool IsCharacter { get; private set; }

        /// <summary>Which figure the pawn is (<see cref="FigureModels.Ids"/>), when <see cref="IsCharacter"/>.</summary>
        public string Figure { get; private set; }

        /// <summary>Animation speed multiplier from the settings (1 = normal).</summary>
        public float Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                if (_motion != null) _motion.Speed = value;
            }
        }

        /// <summary>Raised each time the pawn lands on a square while walking; the scene plays a step sound.</summary>
        public event Action<int> Stepped;

        /// <summary>The square the pawn stands on (or is walking from).</summary>
        public int Square => _square;

        /// <summary>True from the hop into the bus until the piece lands on its square again: its turn's ring stays away meanwhile.</summary>
        public bool Riding { get; private set; }

        /// <summary>
        /// How small a locked-up piece is, as a share of its size (the owner's wish, 2026-11-12: a player sent to jail
        /// shrinks and goes into the jail): the character small enough to stand under the cell's lintel and show between
        /// its bars; a shape, small as it is, shrinks less.
        /// </summary>
        public const float JailedSize = 0.29f;
        public const float ShapeJailedSize = 0.6f;

        /// <summary>How long the hop between the square and the cell's door takes, and the step through the door.</summary>
        public const float JailHopSeconds = 0.55f;
        public const float JailStepSeconds = 0.3f;

        /// <summary>How far that hop swings out round the building's corner, as a share of its length: the piece never passes through the stone.</summary>
        public const float JailSwing = 0.7f;

        /// <summary>
        /// True from the moment the piece sets off into the jail's cell until it stands free on the square again: it is
        /// small, stands inside the cell, stays solid when its player waits and carries no turn ring.
        /// </summary>
        public bool Jailed { get; private set; }

        // The piece's size as a share of its own: 1 on the board, smaller on the way into the cell and inside it.
        private float _size = 1f;

        /// <param name="crowd">Shared by every pawn on the board, so two never take the same slot of a square.</param>
        /// <param name="playerId">The pawn's key in <paramref name="crowd"/>.</param>
        /// <param name="figure">The character the player plays as (a figure id); another figure stands in when its files are missing.</param>
        public static PawnView Create(Transform parent, BoardView board, int seatIndex, Color color, PawnCrowd crowd, int playerId, string figure)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            GameObject model = FigureModels.CreateOrStandIn(figure, ModelName, null);
            bool character = model != null;
            var root = new GameObject($"Pawn {seatIndex + 1} {(character ? figure : PawnShapes.NameOf(seatIndex))}");
            root.transform.SetParent(parent, false);
            CharacterMotion motion = null;
            if (character)
            {
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * CharacterHeight;
                model.transform.localRotation = Facing(BoardCameraRig.BaseYaw);
                motion = CharacterMotion.Attach(model.transform, Vector3.one * CharacterHeight, seatIndex * 0.37f);
            }
            else
            {
                model = ModelParts.Create(ModelName, root.transform, PawnShapes.For(seatIndex), color, 0.55f);
            }

            var pawn = root.AddComponent<PawnView>();
            pawn._model = model.transform;
            pawn.IsCharacter = character;
            pawn.Figure = character ? figure : null;
            pawn._motion = motion;
            pawn._board = board;
            pawn._crowd = crowd;
            pawn._playerId = playerId;
            pawn.PlaceOn(0);
            return pawn;
        }

        /// <summary>The character's turn to look at a camera at <paramref name="yaw"/>: straight at it, across the board.</summary>
        public static Quaternion Facing(float yaw) => Quaternion.LookRotation(-BoardView.ScreenUp(yaw));

        /// <summary>Turns the character to a camera at <paramref name="yaw"/> (the shapes look the same from every side).</summary>
        public void FaceCamera(float yaw)
        {
            if (_motion != null) _motion.SetFacing(Facing(yaw));
            else if (IsCharacter && _model != null) _model.localRotation = Facing(yaw);
        }

        /// <summary>A happy jump with a full turn (a purchase, a new house, money coming in); nothing for the shapes.</summary>
        public void Cheer()
        {
            if (_motion != null && gameObject.activeInHierarchy) _motion.Cheer();
        }

        /// <summary>A slump and a shake of the head (rent, tax); nothing for the shapes.</summary>
        public void Sulk()
        {
            if (_motion != null && gameObject.activeInHierarchy) _motion.Sulk();
        }

        public void PlaceOn(int square)
        {
            transform.position = Reserve(square);
            _square = square;
            Riding = false;
            Jailed = false;
            _size = 1f;
            SetStretch(1f); // Its own size again, were it caught riding the bus or in the cell.
        }

        /// <summary>Puts the piece on the jail's <paramref name="square"/> locked up: small, inside the cell (a table set straight from the state).</summary>
        public void PlaceInJail(int square)
        {
            PlaceOn(square);
            Jailed = true;
            _size = CellSize;
            transform.position = _board.CellSpot(_slot);
            SetStretch(1f);
            SetFaded(false);
        }

        /// <summary>How small this piece is in the cell: <see cref="JailedSize"/> for a character, <see cref="ShapeJailedSize"/> for a shape.</summary>
        public float CellSize => IsCharacter ? JailedSize : ShapeJailedSize;

        /// <summary>
        /// Goes to jail on the square it stands on: one hop to the cell's door, shrinking on the way, and a step through
        /// the door to its place inside, where it stays in sight behind the bars until <see cref="LeaveJail"/>.
        /// </summary>
        public IEnumerator EnterJail()
        {
            if (Jailed || !gameObject.activeSelf) yield break;
            Jailed = true;
            SetFaded(false);
            float speed = Mathf.Max(Speed, 0.1f);
            float small = CellSize;
            Vector3 from = transform.position, door = _board.CellDoor, spot = _board.CellSpot(_slot);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / JailHopSeconds)
            {
                transform.position = JailHopPoint(from, door, _board.CellMiddle, t);
                _size = JailingSize(t, small);
                SetStretch(1f);
                yield return null;
            }

            _size = small;
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / JailStepSeconds)
            {
                transform.position = CellStep(door, spot, t, small);
                SetStretch(1f);
                yield return null;
            }

            transform.position = spot;
            SetStretch(1f);
        }

        /// <summary>Comes out free: a step from its place in the cell out of the door, and one hop back onto the square, growing to its size.</summary>
        public IEnumerator LeaveJail()
        {
            if (!Jailed) yield break;
            if (!gameObject.activeSelf)
            {
                Jailed = false;
                _size = 1f;
                yield break;
            }

            float speed = Mathf.Max(Speed, 0.1f);
            float small = _size;
            Vector3 spot = transform.position, door = _board.CellDoor, to = Target(_square);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / JailStepSeconds)
            {
                transform.position = CellStep(spot, door, t, small);
                yield return null;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / JailHopSeconds)
            {
                transform.position = JailHopPoint(to, door, _board.CellMiddle, 1f - t);
                _size = JailingSize(1f - t, small);
                SetStretch(1f);
                yield return null;
            }

            Release();
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / SquashSeconds)
            {
                SetStretch(1f - 0.16f * Ease.Bump(t));
                yield return null;
            }

            SetStretch(1f);
        }

        /// <summary>
        /// Where the piece is a share <paramref name="t"/> of the way from its square (<paramref name="ground"/>) to the
        /// cell's <paramref name="door"/>: one arc over the ground, swung out away from the <paramref name="building"/>'s
        /// middle (<see cref="JailSwing"/>), since the square's pieces wait behind the jail and its door is round the corner.
        /// </summary>
        public static Vector3 JailHopPoint(Vector3 ground, Vector3 door, Vector3 building, float t)
        {
            t = Mathf.Clamp01(t);
            float along = Ease.InOutCubic(t);
            Vector3 middle = (ground + door) / 2f;
            Vector3 away = middle - building;
            away.y = 0f;
            Vector3 bend = middle + away.normalized * (JailSwing * Vector3.Distance(ground, door));
            Vector3 point = (1f - along) * (1f - along) * ground + 2f * along * (1f - along) * bend + along * along * door;
            point.y += Mathf.Sin(t * Mathf.PI) * HopHeight * 2f;
            return point;
        }

        /// <summary>How big the piece is a share <paramref name="t"/> of the way from the square to the cell's door: its own size at first, <paramref name="small"/> at the door; it shrinks late, so the hop reads.</summary>
        public static float JailingSize(float t, float small)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(1f, small, t * t);
        }

        /// <summary>Where a piece of <paramref name="size"/> is a share <paramref name="t"/> of the way through the cell's door, from one side to the other: a little step, no higher than its own small hop.</summary>
        public static Vector3 CellStep(Vector3 from, Vector3 to, float t, float size)
        {
            t = Mathf.Clamp01(t);
            Vector3 point = Vector3.Lerp(from, to, Ease.InOutCubic(t));
            point.y += Mathf.Sin(t * Mathf.PI) * HopHeight * size;
            return point;
        }

        // Stands the piece free on its square at its own size, whatever it was doing about the cell.
        private void Release()
        {
            Jailed = false;
            _size = 1f;
            transform.position = Target(_square);
            SetStretch(1f);
        }

        /// <summary>Takes a slot on <paramref name="square"/> and says where the pawn stands there.</summary>
        private Vector3 Reserve(int square)
        {
            _slot = _crowd.Place(_playerId, square);
            return Target(square);
        }

        /// <summary>Walks square by square from the current square to <paramref name="destination"/>, forward or back.</summary>
        public IEnumerator WalkTo(int destination, bool backward = false)
        {
            int count = _board.Layout.SquareCount;
            int steps = backward ? (_square - destination + count) % count : (destination - _square + count) % count;
            float speed = Mathf.Max(Speed, 0.1f) * Mathf.Max(1f, steps * (StepSeconds + SquashSeconds) / MaxWalkSeconds);
            if (Jailed) Release(); // A walk starts from the square, never from inside the cell.
            if (_motion != null) _motion.Walking = steps > 0;
            while (_square != destination)
            {
                int next = (_square + (backward ? count - 1 : 1)) % count;
                Vector3 from = transform.position;
                Vector3 to = Reserve(next);
                for (float t = 0f; t < 1f; t += Time.deltaTime * speed / StepSeconds)
                {
                    Vector3 position = Vector3.Lerp(from, to, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * HopHeight;
                    transform.position = position;
                    // Stretches a little in the air.
                    SetStretch(1f + 0.08f * Ease.Bump(t));
                    yield return null;
                }

                transform.position = to;
                _square = next;
                Stepped?.Invoke(next);
                // A short squash on landing, back to shape before the next hop.
                for (float t = 0f; t < 1f; t += Time.deltaTime * speed / SquashSeconds)
                {
                    SetStretch(1f - 0.16f * Ease.Bump(t));
                    yield return null;
                }

                SetStretch(1f);
            }

            if (_motion != null) _motion.Walking = false;
        }

        /// <summary>One high arc straight to <paramref name="destination"/> (a ride from a travel square).</summary>
        public IEnumerator JumpTo(int destination)
        {
            const float seconds = 0.8f;
            float speed = Mathf.Max(Speed, 0.1f);
            if (Jailed) Release();
            Vector3 from = transform.position;
            Vector3 to = Reserve(destination);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / seconds)
            {
                Vector3 position = Vector3.Lerp(from, to, Ease.OutCubic(t));
                position.y += Mathf.Sin(t * Mathf.PI) * HopHeight * 5f;
                transform.position = position;
                SetStretch(1f + 0.12f * Ease.Bump(t));
                yield return null;
            }

            transform.position = to;
            _square = destination;
            SetStretch(1f);
            Stepped?.Invoke(destination);
        }

        /// <summary>How small the piece is when it slips into the bus, as a share of its size, and how long a hop in or out takes.</summary>
        public const float RidingSize = 0.12f;
        public const float BoardingSeconds = 0.42f;

        /// <summary>
        /// Hops from where it stands into the bus at <paramref name="seat"/> (world space), shrinking as it goes, and is
        /// out of sight inside it (the owner's wish, 2026-11-10). It keeps its square until <see cref="HopOutOf"/>.
        /// </summary>
        public IEnumerator HopInto(Vector3 seat)
        {
            float speed = Mathf.Max(Speed, 0.1f);
            if (Jailed) Release();
            Vector3 from = transform.position;
            Riding = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / BoardingSeconds)
            {
                transform.position = BoardingPoint(from, seat, t);
                transform.localScale = Vector3.one * BoardingSize(t);
                yield return null;
            }

            transform.position = seat;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Keeps the piece, out of sight, in the bus at <paramref name="seat"/> (world space) while the bus drives.</summary>
        public void RideAt(Vector3 seat)
        {
            Riding = true;
            transform.position = seat;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Hops out of the bus at <paramref name="seat"/> (world space) onto <paramref name="destination"/>, growing back to its size.</summary>
        public IEnumerator HopOutOf(Vector3 seat, int destination)
        {
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 to = Reserve(destination);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / BoardingSeconds)
            {
                transform.position = BoardingPoint(to, seat, 1f - t);
                transform.localScale = Vector3.one * BoardingSize(1f - t);
                yield return null;
            }

            transform.position = to;
            _square = destination;
            Riding = false;
            Stepped?.Invoke(destination);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / SquashSeconds)
            {
                SetStretch(1f - 0.16f * Ease.Bump(t));
                yield return null;
            }

            SetStretch(1f);
        }

        /// <summary>Where the piece is a share <paramref name="t"/> of the way from its square into the bus: one arc over the ground.</summary>
        public static Vector3 BoardingPoint(Vector3 ground, Vector3 seat, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 point = Vector3.Lerp(ground, seat, Ease.InOutCubic(t));
            point.y += Mathf.Sin(t * Mathf.PI) * HopHeight * 2f;
            return point;
        }

        /// <summary>How big the piece is on that way: its own size on the square, <see cref="RidingSize"/> at the bus's door; it shrinks late, so the hop reads.</summary>
        public static float BoardingSize(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(1f, RidingSize, t * t);
        }

        /// <summary>A small hop in place, to say "this one is on turn".</summary>
        public IEnumerator Bob()
        {
            if (!gameObject.activeSelf) yield break;
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 rest = Jailed ? _board.CellSpot(_slot) : Target(_square);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / 0.32f)
            {
                transform.position = rest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight * 0.6f * _size);
                SetStretch(1f + 0.1f * Ease.Bump(t));
                yield return null;
            }

            transform.position = rest;
            SetStretch(1f);
        }

        /// <summary>
        /// Makes the piece see-through (<paramref name="faded"/>, a player waiting for their turn) or solid again
        /// (the player on turn): each part swaps to a faded copy of its own material, kept for the next time.
        /// A locked-up piece stays solid: small and behind bars, a see-through one would be lost.
        /// </summary>
        public void SetFaded(bool faded)
        {
            if (Jailed) faded = false;
            if (Faded == faded) return;
            Faded = faded;
            if (_parts == null)
            {
                _parts = _model.GetComponentsInChildren<Renderer>(true);
                _solid = System.Array.ConvertAll(_parts, part => part.sharedMaterials);
            }

            if (faded && _faded == null)
            {
                _faded = new Material[_parts.Length][];
                for (int i = 0; i < _parts.Length; i++)
                {
                    Material[] solid = _solid[i];
                    // A one-material part also gets the depth mask: an extra material draws the same mesh again,
                    // first (its queue is lower), so the see-through colour shows only the part's front.
                    Material mask = solid.Length == 1 ? ModelFade.DepthMask() : null;
                    _faded[i] = new Material[solid.Length + (mask != null ? 1 : 0)];
                    for (int m = 0; m < solid.Length; m++) _faded[i][m] = ModelFade.Copy(solid[m], FadeAlpha) ?? solid[m];
                    if (mask != null) _faded[i][solid.Length] = mask;
                }
            }

            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null) _parts[i].sharedMaterials = faded ? _faded[i] : _solid[i];
            }
        }

        /// <summary>Hides a bankrupt player's pawn at once (used when the board is rebuilt from the state).</summary>
        public void SetEliminated()
        {
            _crowd.Remove(_playerId);
            gameObject.SetActive(false);
        }

        /// <summary>A bankrupt pawn tips over, sinks into the table and disappears.</summary>
        public IEnumerator Topple()
        {
            if (!gameObject.activeSelf) yield break;
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 start = transform.position;
            Vector3 axis = Vector3.Cross(Vector3.up, (start - _board.transform.position).normalized);
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
            Quaternion upright = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / ToppleSeconds)
            {
                transform.rotation = Quaternion.AngleAxis(88f * Ease.InCubic(t), axis) * upright;
                yield return null;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / (ToppleSeconds * 0.6f))
            {
                transform.position = start + Vector3.down * (0.25f * t);
                transform.localScale = Vector3.one * (_size * (1f - t));
                yield return null;
            }

            SetEliminated();
        }

        // Keeps the pawn's volume: taller and thinner, or shorter and wider.
        private void SetStretch(float height)
        {
            float width = 1f / Mathf.Sqrt(height);
            transform.localScale = new Vector3(width, height, width) * _size;
        }

        // Characters stand in a line across the screen (PawnSlots.Line, turned by the camera's yaw) so a tall figure never hides
        // one behind it. The small shapes keep the grid turned to the square, its first row towards the board's middle.
        private Vector3 Target(int square)
        {
            float width = _board.Layout.Width;
            if (IsCharacter) return _board.PawnSpot(square) + Quaternion.Euler(0f, BoardCameraRig.BaseYaw, 0f) * PawnSlots.Line(_slot, _crowd.Capacity, width);
            return _board.PawnSpot(square) + Quaternion.LookRotation(_board.Layout.Outward(square)) * PawnSlots.Offset(_slot, _crowd.Capacity, width);
        }
    }
}
