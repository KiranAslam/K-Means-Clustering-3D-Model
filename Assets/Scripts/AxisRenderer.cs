using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class AxisRenderer : MonoBehaviour
{
    [Header("Axis")]
    public float axisLength = 5f;
    public float axisLineWidth = 0.03f;
    public bool showXAxis = true;
    public bool showYAxis = true;
    public bool showZAxis = true;

    [Header("Ticks")]
    public float tickStep = 2.5f;
    public float tickSize = 0.14f;
    public float tickLineWidth = 0.02f;
    public bool showZeroLabel = true;
    public string numberFormat = "0.#";

    [Header("Arrow Heads")]
    public bool showArrows = true;
    public float arrowLength = 0.45f;
    public float arrowRadius = 0.11f;
    [Range(6, 32)] public int arrowSegments = 18;

    [Header("Floor Grid (XZ plane)")]
    public bool showGrid = true;
    public float gridExtent = 7f;
    public float gridStep = 0.5f;
    public float gridLineWidth = 0.008f;
    public bool highlightGridBorder = false;

    [Header("Z-Fighting Fix")]
    [Tooltip("Skip grid center lines that overlap the X/Z axes to prevent flickering.")]
    public bool skipGridCenterLines = true;
    [Tooltip("Place the grid slightly below the axis plane to prevent coplanar surfaces.")]
    public float gridYOffset = -0.01f;
    [Tooltip("Depth bias for axis and tick lines when using the FYP/AxisLine shader. More negative values render in front.")]
    public float axisDepthOffsetUnits = -6f;
    [Tooltip("Depth bias for grid lines. Zero is usually appropriate.")]
    public float gridDepthOffsetUnits = 0f;

    [Header("Colors")]
    public Color axisColor = new Color32(0x9F, 0xE1, 0xCB, 255);
    public Color tickColor = new Color32(0x9F, 0xE1, 0xCB, 255);
    public Color labelColor = new Color32(0x9F, 0xE1, 0xCB, 255);
    public Color axisNameColor = new Color32(0x9F, 0xE1, 0xCB, 255);
    public Color gridColor = new Color32(0x4E, 0x8C, 0x7E, 80);
    public Color gridBorderColor = new Color32(0x6A, 0x91, 0x86, 140);

    [Header("Labels")]
    public float tickLabelSize = 0.16f;
    public float axisNameSize = 0.32f;
    public float labelOffset = 0.30f;
    public float axisNameOffset = 0.45f;
    public Vector3 xLabelsExtraOffset = Vector3.zero;
    public Vector3 yLabelsExtraOffset = Vector3.zero;
    public Vector3 zLabelsExtraOffset = Vector3.zero;
    public bool labelsFaceCamera = true;

    [Header("Axis Names")]
    public string xLabel = "X";
    public string yLabel = "Y";
    public string zLabel = "Z";

    [Header("References")]
    [Tooltip("Assign an FYP/AxisLine shader material, or leave empty to create one automatically.")]
    public Material lineMaterial;
    public TMP_FontAsset labelFont;

    private Transform root;
    private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    void Start()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        ClearOld();
        materialCache.Clear();

        GameObject rootObj = new GameObject("AxisSystem");
        rootObj.transform.SetParent(transform, false);
        root = rootObj.transform;

        if (showGrid) BuildFloorGrid();
        if (showXAxis) BuildAxis(Vector3.right, xLabel, xLabelsExtraOffset);
        if (showYAxis) BuildAxis(Vector3.up, yLabel, yLabelsExtraOffset);
        if (showZAxis) BuildAxis(Vector3.forward, zLabel, zLabelsExtraOffset);
    }

    void ClearOld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "AxisSystem") continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    void BuildFloorGrid()
    {
        Transform gridParent = NewChild("Grid", root);

        int steps = Mathf.Max(1, Mathf.RoundToInt(gridExtent / Mathf.Max(0.0001f, gridStep)));
        float extent = steps * gridStep;
        float y = gridYOffset;

        for (int i = -steps; i <= steps; i++)
        {
            if (skipGridCenterLines && i == 0) continue;

            float p = i * gridStep;
            bool isBorder = highlightGridBorder && (i == -steps || i == steps);
            Color c = isBorder ? gridBorderColor : gridColor;

            DrawLine(new Vector3(-extent, y, p), new Vector3(extent, y, p),
                     c, gridLineWidth, "GridLine_X" + i, gridParent, gridDepthOffsetUnits);

            DrawLine(new Vector3(p, y, -extent), new Vector3(p, y, extent),
                     c, gridLineWidth, "GridLine_Z" + i, gridParent, gridDepthOffsetUnits);
        }
    }

    void BuildAxis(Vector3 dir, string axisName, Vector3 extraOffset)
    {
        Transform axisParent = NewChild("Axis_" + axisName, root);

        DrawLine(-dir * axisLength, dir * axisLength, axisColor, axisLineWidth,
                 "Line", axisParent, axisDepthOffsetUnits);

        if (showArrows) CreateArrow(dir * axisLength, dir, axisParent);

        Vector3 labelDir = GetLabelDirection(dir);
        Vector3 tickDir = GetTickDirection(dir);

        int count = Mathf.FloorToInt(axisLength / Mathf.Max(0.0001f, tickStep) + 0.0001f);
        for (int i = -count; i <= count; i++)
        {
            float value = i * tickStep;
            if (Mathf.Approximately(value, 0f) && !showZeroLabel) continue;

            Vector3 pos = dir * value;

            if (!Mathf.Approximately(value, 0f))
            {
                DrawLine(pos - tickDir * (tickSize * 0.5f),
                         pos + tickDir * (tickSize * 0.5f),
                         tickColor, tickLineWidth, "Tick_" + value, axisParent, axisDepthOffsetUnits);
            }

            CreateLabel(pos + labelDir * labelOffset + extraOffset,
                        value.ToString(numberFormat),
                        axisParent, tickLabelSize, labelColor);
        }

        float namePos = axisLength + (showArrows ? arrowLength : 0f) + axisNameOffset;
        CreateLabel(dir * namePos + labelDir * (labelOffset * 0.5f) + extraOffset,
                    axisName, axisParent, axisNameSize, axisNameColor);
    }

    Vector3 GetLabelDirection(Vector3 dir)
    {
        if (dir == Vector3.right) return Vector3.back;
        if (dir == Vector3.up) return Vector3.right;
        return Vector3.left;
    }

    Vector3 GetTickDirection(Vector3 dir)
    {
        if (dir == Vector3.right) return Vector3.forward;
        return Vector3.right;
    }

    void CreateArrow(Vector3 basePos, Vector3 dir, Transform parent)
    {
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(parent, false);
        arrow.transform.localPosition = basePos;
        arrow.transform.localRotation = Quaternion.LookRotation(
            dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up);

        MeshFilter mf = arrow.AddComponent<MeshFilter>();
        mf.mesh = BuildConeMesh(arrowRadius, arrowLength, arrowSegments);

        MeshRenderer mr = arrow.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = GetMaterial(axisColor, axisDepthOffsetUnits);
    }

    Mesh BuildConeMesh(float radius, float length, int segments)
    {
        segments = Mathf.Max(6, segments);

        Vector3[] vertices = new Vector3[segments * 2 + 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 6];

        int tip = 0;
        vertices[tip] = new Vector3(0f, 0f, length);
        int centre = 1;
        vertices[centre] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            vertices[2 + i] = p;
            vertices[2 + segments + i] = p;
        }

        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            triangles[t++] = tip;
            triangles[t++] = 2 + next;
            triangles[t++] = 2 + i;

            triangles[t++] = centre;
            triangles[t++] = 2 + segments + i;
            triangles[t++] = 2 + segments + next;
        }

        Mesh mesh = new Mesh();
        mesh.name = "AxisArrowCone";
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Transform NewChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }

    void DrawLine(Vector3 a, Vector3 b, Color color, float width, string name, Transform parent, float depthOffsetUnits)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(parent, false);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.sharedMaterial = GetMaterial(color, depthOffsetUnits);

        lr.startColor = Color.white;
        lr.endColor = Color.white;

        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
    }

    Material GetMaterial(Color c, float depthOffsetUnits)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + "_" + depthOffsetUnits.ToString("F1");
        if (materialCache.TryGetValue(key, out Material cached) && cached != null) return cached;

        Material m;
        if (lineMaterial != null)
        {
            m = new Material(lineMaterial);
        }
        else
        {
            Shader s = Shader.Find("FYP/AxisLine");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            m = new Material(s);
        }

        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);

        if (m.HasProperty("_OffsetFactor")) m.SetFloat("_OffsetFactor", depthOffsetUnits * 0.5f);
        if (m.HasProperty("_OffsetUnits")) m.SetFloat("_OffsetUnits", depthOffsetUnits);

        materialCache[key] = m;
        return m;
    }

    GameObject CreateLabel(Vector3 localPosition, string text, Transform parent, float fontSize, Color color)
    {
        GameObject labelObj = new GameObject("Label_" + text);
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = localPosition;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize * 20f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (labelFont != null) tmp.font = labelFont;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(2f, 0.6f);

        if (labelsFaceCamera) labelObj.AddComponent<AxisRendererBillboard>();
        return labelObj;
    }
}

public class AxisRendererBillboard : MonoBehaviour
{
    private Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position,
            cam.transform.up);
    }
}
