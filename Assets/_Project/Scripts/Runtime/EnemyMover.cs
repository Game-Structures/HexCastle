using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyMover : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;

    [Header("Kind")]
    [SerializeField] private EnemyTargetKind targetKind = EnemyTargetKind.Ground;

    private Transform target;
    private Rigidbody rb;

    private WallHealth wallTarget;
    private CastleHealth castleTarget;

    private float attackTimer;

    // ---- Pathfinding (Ground only) ----
    private Dictionary<Vector2Int, HexCellView> cellsByAxial;
    private List<Vector2Int> currentPath;
    private int pathIndex;

    private float repathTimer;
    private const float RepathInterval = 0.5f;

    private Vector3 lastPos;
    private float stuckTimer;
    private const float StuckCheckInterval = 0.5f;
    private const float StuckDistanceEps = 0.03f;
    private const float StuckRepathDelay = 0.7f;

    public void SetTarget(Transform t) => target = t;
    public void SetStats(EnemyStats s) => stats = s;

    public EnemyTargetKind TargetKind => targetKind;

    // НОВОЕ: стелсу нужно знать, что враг сейчас бьёт стену/замок
    public bool IsAttacking => wallTarget != null || castleTarget != null;

    public void SetTargetKind(EnemyTargetKind kind)
    {
        targetKind = kind;
        ApplyMovementMode();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ApplyMovementMode();

        BuildCellCache();
        lastPos = transform.position;
    }

    private void ApplyMovementMode()
    {
        if (rb == null) return;

        // Air ignores collisions and obstacles – movement handled by MovePosition
        if (targetKind == EnemyTargetKind.Air)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        else
        {
            // Ground – keep physics
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    private void BuildCellCache()
    {
        cellsByAxial = new Dictionary<Vector2Int, HexCellView>(512);
        var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
        for (int i = 0; i < cells.Length; i++)
        {
            var key = new Vector2Int(cells[i].q, cells[i].r);
            if (!cellsByAxial.ContainsKey(key))
                cellsByAxial.Add(key, cells[i]);
        }
    }

    private void FixedUpdate()
    {
        if (GameState.IsGameOver)
        {
            if (rb != null) rb.velocity = Vector3.zero;
            return;
        }

        if (target == null || rb == null)
            return;

        float speed = stats != null ? stats.speed : 0.5f;
        float attackInterval = stats != null ? stats.attackInterval : 5f;
        int attackDamage = stats != null ? stats.attackDamage : 50;

        if (targetKind == EnemyTargetKind.Air)
        {
            TickAir(speed, attackInterval, attackDamage);
            return;
        }

        TickGround(speed, attackInterval, attackDamage);
    }

    private void TickAir(float speed, float attackInterval, int attackDamage)
    {
        // move directly to castle, ignore all obstacles
        Vector3 p = rb.position;
        Vector3 tp = target.position;
        tp.y = p.y;

        Vector3 next = Vector3.MoveTowards(p, tp, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        // attack castle when close (no collisions needed)
        if (castleTarget == null)
        {
            var c = target.GetComponent<CastleHealth>();
            if (c == null) c = target.GetComponentInParent<CastleHealth>();
            if (c == null) c = target.gameObject.AddComponent<CastleHealth>();
            castleTarget = c;
        }

        const float attackRange = 0.55f;
        float d2 = (target.position - transform.position).sqrMagnitude;

        if (d2 <= attackRange * attackRange)
        {
            attackTimer -= Time.fixedDeltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = Mathf.Max(0.02f, attackInterval);
                if (castleTarget != null) castleTarget.Damage(attackDamage);
            }
        }
        else
        {
            attackTimer = Mathf.Min(attackTimer, 0.05f);
        }
    }

    private void TickGround(float speed, float attackInterval, int attackDamage)
    {
        bool isAttacking = wallTarget != null || castleTarget != null;

        if (isAttacking)
        {
            rb.velocity = Vector3.zero;

            attackTimer -= Time.fixedDeltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackInterval;

                if (wallTarget != null) wallTarget.Damage(attackDamage);
                else if (castleTarget != null) castleTarget.Damage(attackDamage);
            }

            return;
        }

        if (cellsByAxial == null || cellsByAxial.Count == 0)
            BuildCellCache();

        repathTimer -= Time.fixedDeltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = RepathInterval;
            Repath();
        }

        // stuck check
        stuckTimer += Time.fixedDeltaTime;
        if (stuckTimer >= StuckCheckInterval)
        {
            float moved = (transform.position - lastPos).magnitude;
            lastPos = transform.position;
            stuckTimer = 0f;

            if (moved < StuckDistanceEps)
            {
                repathTimer = Mathf.Min(repathTimer, StuckRepathDelay);
                Repath();
            }
        }

        // path smoothing
        TrySkipWaypointsByLineOfSight();

        // move
        Vector3 waypoint = GetCurrentWaypoint();
        Vector3 dir = waypoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f)
        {
            AdvanceWaypoint();
            rb.velocity = Vector3.zero;
            return;
        }

        rb.velocity = dir.normalized * speed;
    }

    private void Repath()
    {
        var startCell = FindNearestCell(transform.position);
        var goalCell = FindNearestCell(target.position);

        if (startCell == null || goalCell == null)
            return;

        var startKey = new Vector2Int(startCell.q, startCell.r);
        var goalKey = new Vector2Int(goalCell.q, goalCell.r);

        if (AxialDistance(startKey, goalKey) <= 1)
        {
            currentPath = null;
            pathIndex = 0;
            return;
        }

        var path = FindPathAStar(startKey, goalKey);
        if (path == null || path.Count == 0)
        {
            currentPath = null;
            pathIndex = 0;
            return;
        }

        currentPath = path;
        pathIndex = 0;
    }

    private void TrySkipWaypointsByLineOfSight()
    {
        if (currentPath == null || currentPath.Count == 0) return;

        var curCell = FindNearestCell(transform.position);
        if (curCell == null) return;

        var curKey = new Vector2Int(curCell.q, curCell.r);
        pathIndex = Mathf.Clamp(pathIndex, 0, currentPath.Count - 1);

        for (int i = currentPath.Count - 1; i > pathIndex; i--)
        {
            if (HasLineOfSight(curKey, currentPath[i]))
            {
                pathIndex = i;
                break;
            }
        }
    }

    private Vector3 GetCurrentWaypoint()
    {
        if (currentPath == null || currentPath.Count == 0)
            return new Vector3(target.position.x, transform.position.y, target.position.z);

        pathIndex = Mathf.Clamp(pathIndex, 0, currentPath.Count - 1);
        var key = currentPath[pathIndex];

        if (!cellsByAxial.TryGetValue(key, out var cell) || cell == null)
            return new Vector3(target.position.x, transform.position.y, target.position.z);

        var p = cell.transform.position;
        return new Vector3(p.x, transform.position.y, p.z);
    }

    private void AdvanceWaypoint()
    {
        if (currentPath == null) return;

        pathIndex++;
        if (pathIndex >= currentPath.Count)
        {
            currentPath = null;
            pathIndex = 0;
        }
    }

    private HexCellView FindNearestCell(Vector3 worldPos)
    {
        HexCellView bestCell = null;
        float best = float.MaxValue;

        foreach (var kv in cellsByAxial)
        {
            var c = kv.Value;
            if (c == null) continue;

            float d = (c.transform.position - worldPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = c;
            }
        }

        return bestCell;
    }

    // ---- Terrain rules (Ground only) ----
    private bool IsWalkableCell(Vector2Int key)
    {
        if (!cellsByAxial.TryGetValue(key, out var cell) || cell == null)
            return false;

        var tag = cell.GetComponent<HexCastle.Map.MapTerrainTag>();
        if (tag == null) return true;

        return tag.type != HexCastle.Map.MapTerrainType.Water
            && tag.type != HexCastle.Map.MapTerrainType.Mountain;
    }

    private bool HasLineOfSight(Vector2Int a, Vector2Int b)
    {
        int n = AxialDistance(a, b);
        if (n <= 1) return true;

        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            var p = CubeRound(CubeLerp(AxialToCube(a), AxialToCube(b), t));
            var k = CubeToAxial(p);

            if (!cellsByAxial.ContainsKey(k))
                return false;

            if (!IsWalkableCell(k))
                return false;
        }

        return true;
    }

    // ---- A* ----
    private List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int goal)
    {
        var open = new List<Vector2Int>(128) { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(512);

        var gScore = new Dictionary<Vector2Int, int>(512) { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int>(512) { [start] = Heuristic(start, goal) };

        int guard = 20000;

        while (open.Count > 0 && guard-- > 0)
        {
            int bestIdx = 0;
            int bestF = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int f = fScore.TryGetValue(open[i], out var fv) ? fv : int.MaxValue;
                if (f < bestF) { bestF = f; bestIdx = i; }
            }

            var current = open[bestIdx];
            open.RemoveAt(bestIdx);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            var neighbors = GetNeighbors(current);
            for (int i = 0; i < neighbors.Count; i++)
            {
                var n = neighbors[i];

                if (!cellsByAxial.ContainsKey(n))
                    continue;

                if (n != goal && n != start && !IsWalkableCell(n))
                    continue;

                int tentativeG = (gScore.TryGetValue(current, out var gc) ? gc : int.MaxValue) + 1;

                int oldG = gScore.TryGetValue(n, out var gn) ? gn : int.MaxValue;
                if (tentativeG < oldG)
                {
                    cameFrom[n] = current;
                    gScore[n] = tentativeG;
                    fScore[n] = tentativeG + Heuristic(n, goal);

                    if (!open.Contains(n))
                        open.Add(n);
                }
            }
        }

        return null;
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var total = new List<Vector2Int>(128) { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            total.Add(current);
        }
        total.Reverse();

        if (total.Count > 1)
            total.RemoveAt(0);

        return total;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b) => AxialDistance(a, b);

    private static int AxialDistance(Vector2Int a, Vector2Int b)
    {
        int ax = a.x, az = a.y, ay = -ax - az;
        int bx = b.x, bz = b.y, by = -bx - bz;
        return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
    }

    private static List<Vector2Int> GetNeighbors(Vector2Int a)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(a.x + 1, a.y + 0),
            new Vector2Int(a.x + 1, a.y - 1),
            new Vector2Int(a.x + 0, a.y - 1),
            new Vector2Int(a.x - 1, a.y + 0),
            new Vector2Int(a.x - 1, a.y + 1),
            new Vector2Int(a.x + 0, a.y + 1),
        };
    }

    private struct Cube
    {
        public float x, y, z;
        public Cube(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    private static Cube AxialToCube(Vector2Int a)
    {
        float x = a.x;
        float z = a.y;
        float y = -x - z;
        return new Cube(x, y, z);
    }

    private static Vector2Int CubeToAxial(Cube c)
    {
        return new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.z));
    }

    private static Cube CubeLerp(Cube a, Cube b, float t)
    {
        return new Cube(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.z, b.z, t)
        );
    }

    private static Cube CubeRound(Cube c)
    {
        int rx = Mathf.RoundToInt(c.x);
        int ry = Mathf.RoundToInt(c.y);
        int rz = Mathf.RoundToInt(c.z);

        float xDiff = Mathf.Abs(rx - c.x);
        float yDiff = Mathf.Abs(ry - c.y);
        float zDiff = Mathf.Abs(rz - c.z);

        if (xDiff > yDiff && xDiff > zDiff) rx = -ry - rz;
        else if (yDiff > zDiff) ry = -rx - rz;
        else rz = -rx - ry;

        return new Cube(rx, ry, rz);
    }

    // ---- Combat collisions (Ground only) ----
    private void OnCollisionEnter(Collision collision)
    {
        if (targetKind == EnemyTargetKind.Air)
            return;

        if (collision.gameObject.name.Contains("WallPrefab"))
        {
            var w = collision.gameObject.GetComponent<WallHealth>();
            if (w == null) w = collision.gameObject.GetComponentInParent<WallHealth>();
            if (w == null) w = collision.gameObject.AddComponent<WallHealth>();

            wallTarget = w;
            castleTarget = null;
            attackTimer = 0f;
            return;
        }

        if (collision.gameObject.name == "Castle")
        {
            var c = collision.gameObject.GetComponent<CastleHealth>();
            if (c == null) c = collision.gameObject.GetComponentInParent<CastleHealth>();
            if (c == null) c = collision.gameObject.AddComponent<CastleHealth>();

            castleTarget = c;
            wallTarget = null;
            attackTimer = 0f;
            return;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (targetKind == EnemyTargetKind.Air)
            return;

        if (collision.gameObject.name.Contains("WallPrefab"))
        {
            if (wallTarget != null && collision.gameObject == wallTarget.gameObject)
                wallTarget = null;
        }

        if (collision.gameObject.name == "Castle")
        {
            if (castleTarget != null && collision.gameObject == castleTarget.gameObject)
                castleTarget = null;
        }
    }
}
