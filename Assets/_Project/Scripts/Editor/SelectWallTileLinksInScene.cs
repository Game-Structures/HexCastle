#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SelectWallTileLinksInScene
{
    [MenuItem("Tools/HexCastle/Select all WallTileLink in Scene")]
    private static void SelectAll()
    {
        // Важно: находит и неактивные/скрытые, если они есть в сцене
        var all = Resources.FindObjectsOfTypeAll<WallTileLink>()
            .Where(x =>
                x != null &&
                x.gameObject.scene.IsValid() &&                 // только объекты сцены
                !EditorUtility.IsPersistent(x.gameObject)       // не ассеты/префабы в Project
            )
            .Select(x => x.gameObject)
            .Distinct()
            .ToArray();

        Selection.objects = all;

        Debug.Log($"[Tools] Selected WallTileLink objects: {all.Length}");
        if (all.Length > 0) EditorGUIUtility.PingObject(all[0]);
    }
}
#endif
