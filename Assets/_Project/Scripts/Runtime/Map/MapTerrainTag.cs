using UnityEngine;

namespace HexCastle.Map
{
    // Вешается на тот же объект, где HexCellView (тайл клетки).
    public sealed class MapTerrainTag : MonoBehaviour
    {
        public MapTerrainType type = MapTerrainType.Normal;
    }
}
