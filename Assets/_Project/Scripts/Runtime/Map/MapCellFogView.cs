using UnityEngine;

namespace HexCastle.Map
{
    // Отдельно от твоего HexCellView, чтобы не конфликтовать.
    public sealed class MapCellFogView : MonoBehaviour
    {
        [Header("Coords")]
        public HexAxialCoord axial;

        [Header("Optional renderers")]
        public Renderer baseRenderer3D;           // если у тебя 3D меш
        public SpriteRenderer baseSpriteRenderer; // если у тебя 2D sprite
        public SpriteRenderer fogRenderer;        // туман (overlay)

        [SerializeField] private bool revealed;

        public bool Revealed => revealed;

        public void SetRevealed(bool isRevealed)
        {
            revealed = isRevealed;

            // Базовую клетку не скрываем (часто удобнее), скрываем именно fog overlay
            if (fogRenderer != null) fogRenderer.enabled = !revealed;
        }
    }
}
