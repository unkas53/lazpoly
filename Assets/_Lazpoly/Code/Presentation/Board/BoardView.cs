using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Builds the board from data with meshes made in code (<see cref="MeshFactory"/>). When the
    /// Blender models arrive they replace these shapes; the layout and the API stay the same.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float HalfSize = 6f;
        private const float TileHeight = 0.2f;

        private static readonly Color StartColor = new Color(0.40f, 0.73f, 0.42f);
        private static readonly Color TaxColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color RestColor = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color TableColor = new Color(0.36f, 0.24f, 0.16f);
        private static readonly Color FeltColor = new Color(0.20f, 0.42f, 0.31f);
        private static readonly Color PropertyBodyColor = new Color(0.96f, 0.94f, 0.88f);
        private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.55f);

        private static readonly Color HouseColor = new Color(0.30f, 0.69f, 0.31f);
        private static readonly Color HotelColor = new Color(0.83f, 0.18f, 0.18f);

        private GameObject[] _ownerMarkers;
        private Color?[] _ownerColors;
        private bool[] _mortgaged;
        private GameObject[][] _buildings;
        private float _tile;

        public BoardLayout Layout { get; private set; }

        /// <summary>Top surface height of the tiles, where pawns stand.</summary>
        public float SurfaceY => TileHeight;

        /// <summary>Half the width of the table the board sits on.</summary>
        public float HalfExtent { get; private set; }

        public static BoardView Create(Transform parent, BoardData data)
        {
            var root = new GameObject("Board");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<BoardView>();
            view.Build(data);
            return view;
        }

        public Vector3 SquarePosition(int index) => transform.TransformPoint(Layout.SquareCenter(index)) + Vector3.up * SurfaceY;

        /// <summary>Shows who owns a square; pass null to show it as unowned.</summary>
        public void SetOwner(int squareIndex, Color? ownerColor)
        {
            GameObject marker = _ownerMarkers[squareIndex];
            if (marker == null) return;

            _ownerColors[squareIndex] = ownerColor;
            RefreshOwnerMarker(squareIndex);
        }

        /// <summary>A mortgaged property shows its owner marker dimmed.</summary>
        public void SetMortgaged(int squareIndex, bool mortgaged)
        {
            if (_ownerMarkers[squareIndex] == null) return;
            _mortgaged[squareIndex] = mortgaged;
            RefreshOwnerMarker(squareIndex);
        }

        /// <summary>Shows 1-4 small houses, or one hotel at the top level.</summary>
        public void SetBuildingLevel(int squareIndex, int level)
        {
            GameObject[] pieces = _buildings[squareIndex];
            if (pieces == null) return;

            bool hotel = level >= SquareDefinition.MaxBuildingLevel;
            for (int i = 0; i < pieces.Length - 1; i++) pieces[i].SetActive(!hotel && i < level);
            pieces[pieces.Length - 1].SetActive(hotel);
        }

        private void RefreshOwnerMarker(int squareIndex)
        {
            GameObject marker = _ownerMarkers[squareIndex];
            Color? color = _ownerColors[squareIndex];
            marker.SetActive(color.HasValue);
            if (!color.HasValue) return;
            Paint(marker, _mortgaged[squareIndex] ? Color.Lerp(color.Value, Color.black, 0.65f) : color.Value);
        }

        private void Build(BoardData data)
        {
            BoardDefinition board = data.Board;
            Layout = new BoardLayout(board.Count, HalfSize);
            float tile = Layout.Spacing * 0.92f;

            float width = 2f * HalfSize + 2f * tile;
            HalfExtent = width / 2f;
            GameObject table = ModelParts.Create("Table", transform, MeshFactory.RoundedBox(new Vector3(width, 0.3f, width), 0.15f), TableColor, 0.35f);
            table.transform.localPosition = new Vector3(0f, -0.15f, 0f);

            float inner = 2f * HalfSize - tile * 1.05f;
            GameObject felt = ModelParts.Create("Felt", transform, MeshFactory.RoundedBox(new Vector3(inner, 0.04f, inner), 0.02f), FeltColor, 0.05f);
            felt.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            CreateTitle(new Vector3(0f, 0.045f, inner * 0.22f), inner);

            _tile = tile;
            _ownerMarkers = new GameObject[board.Count];
            _ownerColors = new Color?[board.Count];
            _mortgaged = new bool[board.Count];
            _buildings = new GameObject[board.Count][];
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                Vector3 center = Layout.SquareCenter(i);

                bool property = square.Type == SquareType.Property;
                GameObject tileObject = ModelParts.Create($"Square {i:00} {square.Name}", transform,
                    MeshFactory.RoundedBox(new Vector3(tile, TileHeight, tile), 0.05f, 4), property ? PropertyBodyColor : TileColor(square, data));
                tileObject.transform.localPosition = center + Vector3.up * (TileHeight / 2f);

                if (property)
                {
                    // The group colour runs as a band along the tile's outer edge.
                    Vector3 outward = Outward(center);
                    GameObject band = ModelParts.Create("Group band", transform,
                        MeshFactory.RoundedBox(new Vector3(tile * 0.96f, 0.04f, tile * 0.24f), 0.015f, 2), TileColor(square, data));
                    band.transform.localPosition = center + outward * (tile * 0.35f) + Vector3.up * (TileHeight + 0.01f);
                    band.transform.localRotation = Quaternion.LookRotation(outward);
                }

                string caption = square.Type == SquareType.Property ? $"{square.Name}\n{square.Price}"
                    : square.Type == SquareType.Tax ? $"{square.Name}\n-{square.TaxAmount}"
                    : square.Name;
                CreateLabel(caption, center + Vector3.up * (TileHeight + 0.01f), tile);

                if (square.Type == SquareType.Property)
                {
                    // Owner token: a coloured disc on the band, beside the price.
                    GameObject marker = ModelParts.Create("Owner", transform, MeshFactory.Lathe("owner", OwnerProfile), Color.white, 0.5f);
                    marker.transform.localPosition = center + Outward(center) * (tile * 0.35f) + Vector3.up * (TileHeight + 0.03f) +
                                                     Vector3.Cross(Vector3.up, Outward(center)) * (tile * 0.33f);
                    marker.transform.localScale = Vector3.one * tile * 0.5f;
                    marker.SetActive(false);
                    _ownerMarkers[i] = marker;
                    if (square.IsDevelopable) _buildings[i] = CreateBuildings(center);
                }
            }
        }

        /// <summary>Owner token outline (radius, height): a low rounded disc.</summary>
        private static readonly Vector2[] OwnerProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.18f, 0f), new Vector2(0.2f, 0.03f), new Vector2(0.18f, 0.06f), new Vector2(0f, 0.065f),
        };

        /// <summary>Direction from a square on the edge towards the outside of the board.</summary>
        private static Vector3 Outward(Vector3 center) =>
            Mathf.Abs(center.z) >= Mathf.Abs(center.x)
                ? new Vector3(0f, 0f, Mathf.Sign(center.z))
                : new Vector3(Mathf.Sign(center.x), 0f, 0f);

        // Four houses in a row along the tile's inner edge plus one hotel, all hidden until built.
        private GameObject[] CreateBuildings(Vector3 center)
        {
            const int houses = SquareDefinition.MaxBuildingLevel - 1;
            var pieces = new GameObject[houses + 1];
            Vector3 inward = -Outward(center);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 rowCenter = center + inward * (_tile * 0.36f) + Vector3.up * TileHeight;

            for (int i = 0; i < houses; i++)
            {
                float offset = (i - (houses - 1) / 2f) * _tile * 0.2f;
                pieces[i] = CreatePiece("House", rowCenter + side * offset, new Vector3(_tile * 0.15f, _tile * 0.16f, _tile * 0.15f), inward, HouseColor);
            }

            pieces[houses] = CreatePiece("Hotel", rowCenter, new Vector3(_tile * 0.5f, _tile * 0.24f, _tile * 0.2f), inward, HotelColor);
            return pieces;
        }

        // Houses and the hotel share the house mesh (base at y = 0), with the ridge along the edge.
        private GameObject CreatePiece(string name, Vector3 baseCenter, Vector3 size, Vector3 inward, Color color)
        {
            GameObject piece = ModelParts.Create(name, transform, MeshFactory.House(), color, 0.3f);
            piece.transform.localPosition = baseCenter;
            piece.transform.localRotation = Quaternion.LookRotation(inward);
            piece.transform.localScale = size;
            piece.SetActive(false);
            return piece;
        }

        private void CreateTitle(Vector3 localPosition, float width)
        {
            var title = new GameObject("Title");
            title.transform.SetParent(transform, false);
            title.transform.localPosition = localPosition;
            title.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = title.AddComponent<TextMesh>();
            mesh.text = "LAZPOLY";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 192;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = width * 0.006f;
            mesh.color = TitleColor;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        private void CreateLabel(string text, Vector3 localPosition, float tile)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            // A large font size drawn small keeps the letters sharp when the camera is far away.
            mesh.fontSize = 128;
            mesh.characterSize = tile * 0.0115f;
            mesh.color = Color.black;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        private static Color TileColor(SquareDefinition square, BoardData data)
        {
            switch (square.Type)
            {
                case SquareType.Start: return StartColor;
                case SquareType.Tax: return TaxColor;
                case SquareType.Property: return data.GroupColor(square.GroupId);
                default: return RestColor;
            }
        }

        private static void Paint(GameObject target, Color color) => ModelParts.Paint(target, color, 0.5f);
    }
}
