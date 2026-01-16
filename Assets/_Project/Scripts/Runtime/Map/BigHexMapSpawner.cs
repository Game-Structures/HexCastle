using System.Collections.Generic;
using UnityEngine;

namespace HexCastle.Map
{
    public sealed class BigHexMapSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Префаб одной клетки. Можно твой текущий префаб тайла. На нём добавь MapCellFogView и (опционально) назначь fogRenderer.")]
        public GameObject cellPrefab;

        [Tooltip("Куда складывать созданные клетки (можно оставить пустым – будет этот объект).")]
        public Transform cellsRoot;

        [Header("Radii")]
        [Tooltip("Полный радиус карты (включая скрытую под туманом область).")]
        public int totalRadius = 12;

        [Tooltip("Стартово открытый радиус. 6 = 127 клеток.")]
        public int startRevealRadius = 6;

        [Header("Layout")]
        [Tooltip("Размер хекса (влияет на расстояние между клетками).")]
        public float hexSize = 1f;

        public IReadOnlyDictionary<HexAxialCoord, MapCellFogView> Cells => _cells;

        private readonly Dictionary<HexAxialCoord, MapCellFogView> _cells = new Dictionary<HexAxialCoord, MapCellFogView>();
        private readonly HexAxialCoord _castle = new HexAxialCoord(0, 0);

        private void Start()
        {
            Spawn();
            RevealInitial();
        }

        [ContextMenu("Respawn Map")]
        public void Respawn()
        {
            Clear();
            Spawn();
            RevealInitial();
        }

        private void Clear()
        {
            if (cellsRoot == null) cellsRoot = transform;

            for (int i = cellsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(cellsRoot.GetChild(i).gameObject);
            }
            _cells.Clear();
        }

        private void Spawn()
        {
            if (cellPrefab == null)
            {
                Debug.LogError("BigHexMapSpawner: cellPrefab is not set.");
                return;
            }

            if (cellsRoot == null) cellsRoot = transform;

            // hex radius grid
            for (int q = -totalRadius; q <= totalRadius; q++)
            {
                int r1 = Mathf.Max(-totalRadius, -q - totalRadius);
                int r2 = Mathf.Min(totalRadius, -q + totalRadius);

                for (int r = r1; r <= r2; r++)
                {
                    var a = new HexAxialCoord(q, r);

                    var go = Instantiate(cellPrefab, cellsRoot);
                    go.name = $"Cell_{q}_{r}";
                    go.transform.position = AxialToWorld(a);

                    // ставим/добавляем MapCellFogView
                    var view = go.GetComponent<MapCellFogView>();
                    if (view == null) view = go.AddComponent<MapCellFogView>();

                    view.axial = a;
                    view.SetRevealed(false);

                    _cells[a] = view;
                }
            }
        }

        private void RevealInitial()
        {
            foreach (var kv in _cells)
            {
                bool open = HexAxialCoord.Distance(kv.Key, _castle) <= startRevealRadius;
                kv.Value.SetRevealed(open);
            }
        }

        private Vector3 AxialToWorld(HexAxialCoord a)
        {
            // pointy-top axial
            float x = hexSize * Mathf.Sqrt(3f) * (a.q + a.r * 0.5f);
            float z = hexSize * (3f / 2f) * a.r;
            return new Vector3(x, 0f, z);
        }
    }
}
