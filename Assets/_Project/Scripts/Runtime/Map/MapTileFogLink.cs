using UnityEngine;

namespace HexCastle.Map
{
    public sealed class MapTileFogLink : MonoBehaviour
    {
        public MeshRenderer tileRenderer;
        public Material revealedMaterial;
        public Material fogMaterial;

        [SerializeField] private bool revealed = false;
        public bool Revealed => revealed;

        [Tooltip("Если включено – в LateUpdate возвращает нужный материал (защита от скриптов, которые перезаписывают материал тайла).")]
        public bool stickyMaterial = true;

        private Material wanted;

        private void Awake()
        {
            if (tileRenderer == null) tileRenderer = GetComponent<MeshRenderer>();
            Apply();
        }

        public void SetRevealed(bool isRevealed)
        {
            revealed = isRevealed;
            Apply();
        }

        private void Apply()
        {
            wanted = revealed ? revealedMaterial : fogMaterial;
            if (tileRenderer != null && wanted != null)
                tileRenderer.sharedMaterial = wanted;
        }

        private void LateUpdate()
        {
            if (!stickyMaterial) return;
            if (tileRenderer == null || wanted == null) return;

            if (tileRenderer.sharedMaterial != wanted)
                tileRenderer.sharedMaterial = wanted;
        }

        [ContextMenu("Fog/Reveal")]
        private void CM_Reveal() => SetRevealed(true);

        [ContextMenu("Fog/Hide")]
        private void CM_Hide() => SetRevealed(false);
    }
}
