using UnityEngine;

namespace HexCastle.Map
{
    public sealed class MapTerrainColorView : MonoBehaviour
    {
        public MapTileFogLink fogLink;
        public MeshRenderer tileRenderer;

        [Header("Colors")]
        public Color waterColor = new Color(0.20f, 0.45f, 0.95f, 1f);
        public Color forestColor = new Color(0.12f, 0.35f, 0.18f, 1f);
        public Color mountainColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        public Color normalColor = new Color(0.60f, 0.90f, 0.95f, 1f);

        private MapTerrainTag terrainTag;
        private MaterialPropertyBlock mpb;

        private void Awake()
        {
            if (fogLink == null) fogLink = GetComponent<MapTileFogLink>();
            if (tileRenderer == null) tileRenderer = GetComponent<MeshRenderer>();

            mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (fogLink == null || tileRenderer == null) return;

            // Пока в тумане – не красим (там FogMat)
            if (!fogLink.Revealed) return;

            // Тег может появиться позже (его добавляет MapTerrainGenerator)
            if (terrainTag == null) terrainTag = GetComponent<MapTerrainTag>();

            var t = terrainTag != null ? terrainTag.type : MapTerrainType.Normal;

            Color c = normalColor;
            if (t == MapTerrainType.Water) c = waterColor;
            else if (t == MapTerrainType.Forest) c = forestColor;
            else if (t == MapTerrainType.Mountain) c = mountainColor;

            tileRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            tileRenderer.SetPropertyBlock(mpb);
        }
    }
}
