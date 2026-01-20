using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DamagePopupWorld : MonoBehaviour
{
    public enum PopupKind
    {
        EnemyDamaged = 0,
        PlayerDamaged = 1,
    }

    private sealed class Item
    {
        public TextMeshPro tmp;
        public float t;
        public float life;
        public Vector3 startPos;
        public float floatUp;
        public bool active;
    }

    private static DamagePopupWorld _i;

    [Header("Defaults")]
    [SerializeField] private float defaultLife = 1.4f;
    [SerializeField] private float defaultFloatUp = 0.8f;

    // Было ~3.5 – делаем x2
    [SerializeField] private float defaultFontSize = 7.0f;

    [SerializeField] private float randomRadius = 0.15f;

    [Header("Render priority")]
    [SerializeField] private int sortingOrder = 500;
    [SerializeField] private float pushTowardCamera = 0.25f;

    [Header("Colors")]
    [SerializeField] private Color enemyDamageColor = Color.white;
    [SerializeField] private Color playerDamageColor = Color.red;

    private readonly List<Item> _items = new List<Item>(256);
    private readonly Queue<Item> _pool = new Queue<Item>(256);

    // Старый вызов (враги) – оставляем
    public static void Spawn(Vector3 worldPos, int amount)
        => Spawn(worldPos, amount, PopupKind.EnemyDamaged);

    public static void Spawn(Vector3 worldPos, int amount, PopupKind kind)
    {
        Ensure();
        _i.SpawnInternal(worldPos, amount, kind);
    }

    private static void Ensure()
    {
        if (_i != null) return;

        var go = new GameObject("_DamagePopups");
        DontDestroyOnLoad(go);
        _i = go.AddComponent<DamagePopupWorld>();
    }

    private void SpawnInternal(Vector3 worldPos, int amount, PopupKind kind)
    {
        Item it = (_pool.Count > 0) ? _pool.Dequeue() : CreateItem();

        it.active = true;
        it.t = 0f;
        it.life = Mathf.Max(0.05f, defaultLife);
        it.floatUp = defaultFloatUp;

        it.startPos = worldPos + new Vector3(
            Random.Range(-randomRadius, randomRadius),
            Random.Range(-randomRadius, randomRadius),
            0f
        );

        it.tmp.text = amount.ToString();
        it.tmp.alpha = 1f;
        it.tmp.color = (kind == PopupKind.PlayerDamaged) ? playerDamageColor : enemyDamageColor;

        it.tmp.transform.position = it.startPos;

        // Выталкиваем к камере, чтобы не пряталось внутри мешей (башни/стены)
        var cam = Camera.main;
        if (cam != null)
            it.tmp.transform.position += cam.transform.forward * pushTowardCamera;

        _items.Add(it);
    }

    private Item CreateItem()
    {
        var go = new GameObject("DamagePopup");
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = defaultFontSize;
        tmp.enableWordWrapping = false;

        // Приоритет отрисовки (чтобы не перекрывалось мешами)
        var r = tmp.renderer;
        if (r != null)
        {
            r.sortingOrder = sortingOrder;
        }

        return new Item { tmp = tmp };
    }

    private void Update()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            if (!it.active || it.tmp == null)
            {
                _items.RemoveAt(i);
                continue;
            }

            it.t += Time.deltaTime;
            float k = Mathf.Clamp01(it.t / it.life);

            it.tmp.transform.position = it.startPos + Vector3.up * (it.floatUp * k);

            // сохраняем выталкивание к камере на протяжении жизни
            var cam = Camera.main;
            if (cam != null)
                it.tmp.transform.position += cam.transform.forward * pushTowardCamera;

            it.tmp.alpha = 1f - k;

            if (k >= 1f)
            {
                it.active = false;
                it.tmp.text = "";
                _items.RemoveAt(i);
                _pool.Enqueue(it);
            }
        }
    }
}
