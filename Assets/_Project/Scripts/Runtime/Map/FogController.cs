// Assets/_Project/Scripts/Runtime/Map/FogController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexCastle.Map
{
    public sealed class FogController : MonoBehaviour
    {
        [Header("Start reveal")]
        public int startRevealRadius = 6;
        public string castleObjectName = "Castle";
        public float applyDelay = 0.2f;

        [Header("Reveal on build")]
        public int revealOnBuildRadius = 3;

        [Header("Diagnostics")]
        public bool logStarted = true;
        public bool logBuilds = true;

        private readonly HashSet<int> knownBuilds = new HashSet<int>();
        private bool initialized;

        private IEnumerator Start()
        {
            if (logStarted) Debug.Log("[FogController] STARTED");

            yield return new WaitForSeconds(applyDelay);
            yield return null;

            ApplyStartReveal();

            // Запоминаем уже существующие стены (периметр и т.п.), чтобы не считать их "новыми"
            var existing = FindObjectsByType<WallTileLink>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
                knownBuilds.Add(existing[i].gameObject.GetInstanceID());

            initialized = true;

            if (logBuilds) Debug.Log($"[FogController] Init complete. Known builds={knownBuilds.Count}");
        }

        private void Update()
        {
            if (!initialized) return;

            var builds = FindObjectsByType<WallTileLink>(FindObjectsSortMode.None);

            for (int i = 0; i < builds.Length; i++)
            {
                var b = builds[i];
                int id = b.gameObject.GetInstanceID();
                if (knownBuilds.Contains(id)) continue;

                knownBuilds.Add(id);

                if (logBuilds)
    Debug.Log($"[FogController] New wall: {b.name} pos={b.transform.position}");

if (HasFogNearby(b.transform.position, revealOnBuildRadius))
    RevealAroundWorldPos(b.transform.position, revealOnBuildRadius);

            }
        }

        public void RevealAroundWorldPos(Vector3 worldPos, int radius)
        {
            var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
            if (cells == null || cells.Length == 0) return;

            HexCellView center = cells[0];
            float best = float.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                float d = (cells[i].transform.position - worldPos).sqrMagnitude;
                if (d < best) { best = d; center = cells[i]; }
            }

            if (logBuilds)
                Debug.Log($"[FogController] Reveal around cell q={center.q} r={center.r} radius={radius}");

            for (int i = 0; i < cells.Length; i++)
            {
                int dist = AxialDistance(center.q, center.r, cells[i].q, cells[i].r);
                if (dist <= radius)
                {
                    var fog = cells[i].GetComponent<MapTileFogLink>();
                    if (fog != null) fog.SetRevealed(true);
                }
            }
        }

        private void ApplyStartReveal()
        {
            var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
            if (cells == null || cells.Length == 0)
            {
                Debug.LogWarning("[FogController] No HexCellView found.");
                return;
            }

            Vector3 cpos = Vector3.zero;
            var go = GameObject.Find(castleObjectName);
            if (go != null) cpos = go.transform.position;

            HexCellView center = cells[0];
            float best = float.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                float d = (cells[i].transform.position - cpos).sqrMagnitude;
                if (d < best) { best = d; center = cells[i]; }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                var fog = cells[i].GetComponent<MapTileFogLink>();
                if (fog != null) fog.SetRevealed(false);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                var fog = cells[i].GetComponent<MapTileFogLink>();
                if (fog == null) continue;

                int dist = AxialDistance(center.q, center.r, cells[i].q, cells[i].r);
                if (dist <= startRevealRadius) fog.SetRevealed(true);
            }

            if (logBuilds)
                Debug.Log($"[FogController] Start revealed radius={startRevealRadius} center q={center.q} r={center.r}");
        }

private bool HasFogNearby(Vector3 worldPos, int radius)
{
    var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
    if (cells == null || cells.Length == 0) return false;

    // центр – ближайшая клетка к постройке
    HexCellView center = cells[0];
    float best = float.MaxValue;
    for (int i = 0; i < cells.Length; i++)
    {
        float d = (cells[i].transform.position - worldPos).sqrMagnitude;
        if (d < best) { best = d; center = cells[i]; }
    }

    // если в радиусе есть хоть одна скрытая клетка – значит мы "рядом с туманом"
    for (int i = 0; i < cells.Length; i++)
    {
        int dist = AxialDistance(center.q, center.r, cells[i].q, cells[i].r);
        if (dist <= radius)
        {
            var fog = cells[i].GetComponent<MapTileFogLink>();
            if (fog != null && !fog.Revealed) return true;
        }
    }

    return false;
}

        private static int AxialDistance(int aq, int ar, int bq, int br)
        {
            int ax = aq, az = ar, ay = -ax - az;
            int bx = bq, bz = br, by = -bx - bz;
            return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
        }
    }
}
