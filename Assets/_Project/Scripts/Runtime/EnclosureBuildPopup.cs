using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnclosureBuildPopup : MonoBehaviour
{
    private GameObject root;
    private RectTransform content;
    private Font font;

    private Action<int> onSelectIndex;
    private Action onClose;

    public bool IsOpen => root != null && root.activeSelf;

    public void Open(IReadOnlyList<EnclosureBuildOption> options, Action<int> onSelectIndex, Action onClose)
    {
        EnsureCreated();

        this.onSelectIndex = onSelectIndex;
        this.onClose = onClose;

        Rebuild(options);
        root.SetActive(true);
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        onSelectIndex = null;
        onClose = null;
    }

    private void EnsureCreated()
    {
        if (root != null) return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        root = new GameObject("_EnclosureBuildPopupRoot");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        // Dim background
        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(root.transform, false);
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        var dimBtn = dimGo.AddComponent<Button>();
        dimBtn.onClick.AddListener(() =>
        {
            Close();
            onClose?.Invoke();
        });

        // Panel
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(dimGo.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(920, 980);
        panelRt.anchoredPosition = Vector2.zero;

        // Title
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var title = titleGo.AddComponent<Text>();
        title.font = font;
        title.fontSize = 40;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.text = "BUILD INSIDE";

        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 90);
        titleRt.anchoredPosition = new Vector2(0f, -20f);

        // Scroll
        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(panelGo.transform, false);

        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.05f, 0.14f);
        scrollRt.anchorMax = new Vector2(0.95f, 0.88f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.16f, 0.16f, 0.16f, 0.95f);

        var mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.viewport = scrollRt;

        // Content
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);

        content = contentGo.AddComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 14;
        vlg.padding = new RectOffset(16, 16, 16, 16);

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = content;

        // Cancel
        CreateButton(panelGo.transform, "Cancel", new Vector2(0f, 50f), new Vector2(260, 80), () =>
        {
            Close();
            onClose?.Invoke();
        });

        root.SetActive(false);
    }

    private void Rebuild(IReadOnlyList<EnclosureBuildOption> options)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < options.Count; i++)
            CreateRow(options[i], i);
    }

    private void CreateRow(EnclosureBuildOption opt, int optionIndex)
    {
        var rowGo = new GameObject("Row_" + optionIndex);
        rowGo.transform.SetParent(content, false);

        var rowImg = rowGo.AddComponent<Image>();
        rowImg.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        var btn = rowGo.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            Debug.Log($"[EnclosurePopup] Row click: optionIndex={optionIndex}, title='{opt.title}', cost={opt.cost}");
            onSelectIndex?.Invoke(optionIndex);
            Close();
        });

        var rt = rowGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 140);

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(18, 18, 14, 14);
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;

        // Icon
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(rowGo.transform, false);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = opt.icon;
        iconImg.preserveAspect = true;
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 96);

        // Text block
        var textGo = new GameObject("Texts");
        textGo.transform.SetParent(rowGo.transform, false);
        textGo.AddComponent<RectTransform>().sizeDelta = new Vector2(600, 0f);

        var vlg = textGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.spacing = 6;

        var t1Go = new GameObject("Title");
        t1Go.transform.SetParent(textGo.transform, false);
        var t1 = t1Go.AddComponent<Text>();
        t1.font = font;
        t1.fontSize = 28;
        t1.color = Color.white;
        t1.alignment = TextAnchor.MiddleLeft;
        t1.text = string.IsNullOrWhiteSpace(opt.title) ? "Option" : opt.title;

        var t2Go = new GameObject("Desc");
        t2Go.transform.SetParent(textGo.transform, false);
        var t2 = t2Go.AddComponent<Text>();
        t2.font = font;
        t2.fontSize = 20;
        t2.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        t2.alignment = TextAnchor.UpperLeft;
        t2.text = string.IsNullOrWhiteSpace(opt.description) ? "" : opt.description;

        // Cost
        var costGo = new GameObject("Cost");
        costGo.transform.SetParent(rowGo.transform, false);
        costGo.AddComponent<RectTransform>().sizeDelta = new Vector2(120, 0f);

        var costTxt = costGo.AddComponent<Text>();
        costTxt.font = font;
        costTxt.fontSize = 26;
        costTxt.color = Color.white;
        costTxt.alignment = TextAnchor.MiddleRight;
        costTxt.text = opt.cost.ToString();
    }

    private void CreateButton(Transform parent, string text, Vector2 anchoredPos, Vector2 size, Action onClick)
    {
        var btnGo = new GameObject("Btn_" + text);
        btnGo.transform.SetParent(parent, false);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);

        var label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 28;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;

        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
    }
}
