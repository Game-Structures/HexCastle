using UnityEngine;

namespace HexCastle.Map
{
    public sealed class MapFogBootstrap : MonoBehaviour
    {
        [Tooltip("Радиус стартового открытия. 6 = 127 клеток.")]
        public int startRevealRadius = 6;

        private void Start()
        {
            // Находим все тайлы в сцене и открываем те, что в радиусе startRevealRadius от (0,0)
            var tiles = FindObjectsOfType<MapTileFogLink>(true);

            foreach (var t in tiles)
            {
                // У нас пока нет координат клетки, поэтому на этом шаге откроем только центральный тайл (пример).
                // Следующий шаг: привяжем координаты из твоего HexCellView/HexGridSpawner и откроем радиус правильно.
                // Чтобы уже сейчас увидеть, что система работает, откроем ближайшие по расстоянию к (0,0).
                float dist = Vector3.Distance(Vector3.zero, t.transform.position);

                // Грубый порог: подгони при необходимости (примерно под твой размер тайла).
                // После привязки к координатам заменим на axial distance.
                if (dist <= startRevealRadius * 1.8f)
                    t.SetRevealed(true);
                else
                    t.SetRevealed(false);
            }
        }
    }
}
