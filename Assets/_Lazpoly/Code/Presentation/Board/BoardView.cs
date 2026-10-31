using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Builds the board from data out of primitive shapes. Placeholder art: Phase 6 swaps the
    /// tiles for the Blender models while keeping the same layout and API.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float HalfSize = 6f;
        private const float TileHeight = 0.2f;

        private static readonly Color StartColor = new Color(0.40f, 0.73f, 0.42f);
        private static readonly Color TaxColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color RestColor = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color TableColor = new Color(0.20f, 0.36f, 0.27f);

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

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.SetParent(transform, false);
            table.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            table.transform.localScale = new Vector3(2f * HalfSize + 2f * tile, 0.1f, 2f * HalfSize + 2f * tile);
            Paint(table, TableColor);

            _tile = tile;
            _ownerMarkers = new GameObject[board.Count];
            _ownerColors = new Color?[board.Count];
            _mortgaged = new bool[board.Count];
            _buildings = new GameObject[board.Count][];
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                Vector3 center = Layout.SquareCenter(i);

                GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileObject.name = $"Square {i:00} {square.Name}";
                tileObject.transform.SetParent(transform, false);
                tileObject.transform.localPosition = center + Vector3.up * (TileHeight / 2f);
                tileObject.transform.localScale = new Vector3(tile, TileHeight, tile);
                Paint(tileObject, TileColor(square, data));

                string caption = square.Type == SquareType.Property ? $"{square.Name}\n{square.Price}"
                    : square.Type == SquareType.Tax ? $"{square.Name}\n-{square.TaxAmount}"
                    : square.Name;
                CreateLabel(caption, center + Vector3.up * (TileHeight + 0.01f), tile);

                if (square.Type == SquareType.Property)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.name = "Owner";
                    marker.transform.SetParent(transform, false);
                    marker.transform.localPosition = center + new Vector3(tile * 0.32f, TileHeight + 0.05f, tile * 0.32f);
                    marker.transform.localScale = new Vector3(tile * 0.22f, 0.05f, tile * 0.22f);
                    Object.Destroy(marker.GetComponent<Collider>());
                    marker.SetActive(false);
                    _ownerMarkers[i] = marker;
                    if (square.IsDevelopable) _buildings[i] = CreateBuildings(center);
                }
            }
        }

        // Four houses in a row along the tile's inner edge plus one hotel, all hidden until built.
        private GameObject[] CreateBuildings(Vector3 center)
        {
            const int houses = SquareDefinition.MaxBuildingLevel - 1;
            var pieces = new GameObject[houses + 1];
            Vector3 inward = -new Vector3(center.x, 0f, center.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 rowCenter = center + inward * (_tile * 0.36f) + Vector3.up * TileHeight;

            for (int i = 0; i < houses; i++)
            {
                float offset = (i - (houses - 1) / 2f) * _tile * 0.2f;
                pieces[i] = CreatePiece("House", rowCenter + side * offset, new Vector3(_tile * 0.14f, _tile * 0.14f, _tile * 0.14f), HouseColor);
            }

            pieces[houses] = CreatePiece("Hotel", rowCenter, new Vector3(_tile * 0.5f, _tile * 0.2f, _tile * 0.18f), HotelColor);
            return pieces;
        }

        private GameObject CreatePiece(string name, Vector3 baseCenter, Vector3 size, Color color)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = baseCenter + Vector3.up * (size.y / 2f);
            piece.transform.localScale = size;
            Object.Destroy(piece.GetComponent<Collider>());
            Paint(piece, color);
            piece.SetActive(false);
            return piece;
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
            mesh.fontSize = 48;
            mesh.characterSize = tile * 0.025f;
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

        private static void Paint(GameObject target, Color color)
        {
            target.GetComponent<Renderer>().material.color = color;
        }
    }
}
