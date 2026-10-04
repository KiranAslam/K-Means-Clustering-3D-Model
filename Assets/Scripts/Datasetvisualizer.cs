using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;


public class DatasetVisualizer : MonoBehaviour
{
    public enum DatasetType
    {
        Moons = 0,
        Circles = 1,
        AnisotropicBlobs = 2,
        VaryingDensityBlobs = 3,
        CustomerSegmentation = 4,   // real data — Mall Customer Segmentation (Kaggle)
        ImageColorQuantization = 5, // real data — pixel R,G,B mapped to X,Y,Z
        Iris = 6                    // real data — Fisher/UCI Iris dataset
    }

    private struct DataPoint
    {
        public Vector3 position;
        public int label;
        public Color? trueColor; // set for datasets where each point HAS a real color (e.g. image pixels) — null everywhere else, meaning "use the label-based palette instead"

        public DataPoint(Vector3 position, int label, Color? trueColor = null)
        {
            this.position = position;
            this.label = label;
            this.trueColor = trueColor;
        }
    }

    [Header("UI")]
    [Tooltip("Dropdown option order must match DatasetType enum order (0 = Moons, 1 = Circles, ...).")]
    public TMP_Dropdown datasetDropdown;

    [Header("References")]
    [Tooltip("Axis system this dataset is plotted on. Auto-fetched from this GameObject if left empty.")]
    public AxisRenderer axisRenderer;

    [Header("Generation Settings")]
    [Min(4)] public int pointCount = 200;
    [Range(0f, 1f)] public float noise = 0.09f;
    [Tooltip("Same seed always produces the same layout. -1 = random every rebuild.")]
    public int randomSeed = 42;
    [Tooltip("Fraction of axisLength the data is scaled to fit within (margin from the grid edge). Closer to 1 = more spread out.")]
    [Range(0.5f, 1f)] public float fitMargin = 0.92f;
    [Tooltip("Lifts points above the floor grid so they don't visually merge with grid lines (z-fighting/clutter). Purely a rendering offset — never affects the actual data used for clustering.")]
    public float verticalOffset = 0.18f;

    [Header("Image Color Quantization")]
    [Tooltip("Image to sample pixels from. Must be in a Resources folder, Read/Write enabled.")]
    public string imageResourceName = "ColorBlocksSample";
    [Tooltip("How many pixels to randomly sample from the image as data points.")]
    public int imageSampleCount = 200;

    [Header("Point Appearance")]
    [Tooltip("Optional prefab for a single data point. Leave empty to use a primitive sphere.")]
    public GameObject pointPrefab;
    public float pointScale = 0.22f;
    public Material pointMaterial;
    public Color[] clusterColors = new Color[]
    {
        new Color32(0x5D, 0xCA, 0xA5, 255), // teal
        new Color32(0xF0, 0x99, 0x7B, 255), // coral
        new Color32(0x85, 0xB7, 0xEB, 255), // blue
        new Color32(0xF0, 0xD7, 0x6B, 255), // amber
        new Color32(0xD4, 0x53, 0x7E, 255), // pink
    };

    private Transform pointsRoot;
    private System.Random rng;
    private readonly List<GameObject> spawnedPoints = new List<GameObject>();
    private readonly List<int> pointLabels = new List<int>();
    private readonly List<Color?> pointTrueColors = new List<Color?>();
    private readonly List<Vector3> pointDataPositions = new List<Vector3>();
    private readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

    /// <summary>Fired after a dataset finishes generating and spawning — external systems (e.g. KMeansController) hook this to know fresh data is available.</summary>
    public event Action OnPointsReady;

    public int PointCount => spawnedPoints.Count;

    private void Awake()
    {
        if (axisRenderer == null) axisRenderer = GetComponent<AxisRenderer>();
        if (axisRenderer == null) axisRenderer = FindObjectOfType<AxisRenderer>();

        if (axisRenderer == null)
            Debug.LogWarning("[DatasetVisualizer] No AxisRenderer assigned or found in scene — points will use a default axisLength of 5.");
    }

    private void Start()
    {
        if (datasetDropdown == null)
        {
            Debug.LogWarning("[DatasetVisualizer] No TMP_Dropdown assigned — cannot listen for dataset changes.");
            return;
        }

        datasetDropdown.onValueChanged.AddListener(OnDatasetChanged);
        OnDatasetChanged(datasetDropdown.value);
    }

    private void OnDestroy()
    {
        if (datasetDropdown != null)
            datasetDropdown.onValueChanged.RemoveListener(OnDatasetChanged);
    }

    public void OnDatasetChanged(int dropdownIndex)
    {
        if (!Enum.IsDefined(typeof(DatasetType), dropdownIndex))
        {
            Debug.LogWarning($"[DatasetVisualizer] Dropdown index {dropdownIndex} has no matching DatasetType.");
            return;
        }

        RenderDataset((DatasetType)dropdownIndex);
    }

    public void RenderDataset(DatasetType type)
    {
        rng = randomSeed >= 0 ? new System.Random(randomSeed) : new System.Random();

        List<DataPoint> points = Generate(type);
        ClearPoints();

        if (points.Count == 0)
        {
            Debug.LogWarning($"[DatasetVisualizer] '{type}' has no generator yet — nothing to render.");
            return;
        }

        NormalizeToAxis(points);
        SpawnPoints(points);
        OnPointsReady?.Invoke();
    }

    private List<DataPoint> Generate(DatasetType type)
    {
        switch (type)
        {
            case DatasetType.Moons: return GenerateMoons(pointCount, noise);
            case DatasetType.Circles: return GenerateCircles(pointCount, noise);
            case DatasetType.AnisotropicBlobs: return GenerateAnisotropicBlobs(pointCount);
            case DatasetType.VaryingDensityBlobs: return GenerateVaryingDensityBlobs(pointCount);
            case DatasetType.CustomerSegmentation: return GenerateCustomerSegmentation();
            case DatasetType.ImageColorQuantization: return GenerateImageColorQuantization();
            case DatasetType.Iris: return GenerateIris();
            default: return new List<DataPoint>();
        }
    }

    // ------------------------------------------------------------------
    // SYNTHETIC GENERATORS (2D — Y stays 0)
    // ------------------------------------------------------------------
    private List<DataPoint> GenerateMoons(int count, float noiseAmount)
    {
        List<DataPoint> result = new List<DataPoint>(count);
        int outerCount = count / 2;
        int innerCount = count - outerCount;

        for (int i = 0; i < outerCount; i++)
        {
            float t = Mathf.PI * i / (outerCount - 1);
            float x = Mathf.Cos(t);
            float z = Mathf.Sin(t);
            Vector2 p = AddNoise(new Vector2(x, z), noiseAmount);
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), 0));
        }

        for (int i = 0; i < innerCount; i++)
        {
            float t = Mathf.PI * i / (innerCount - 1);
            float x = 1f - Mathf.Cos(t);
            float z = 1f - Mathf.Sin(t) - 0.5f;
            Vector2 p = AddNoise(new Vector2(x, z), noiseAmount);
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), 1));
        }

        return result;
    }

    private List<DataPoint> GenerateCircles(int count, float noiseAmount, float factor = 0.5f)
    {
        List<DataPoint> result = new List<DataPoint>(count);
        int outerCount = count / 2;
        int innerCount = count - outerCount;

        for (int i = 0; i < outerCount; i++)
        {
            float t = 2f * Mathf.PI * i / outerCount;
            Vector2 p = AddNoise(new Vector2(Mathf.Cos(t), Mathf.Sin(t)), noiseAmount);
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), 0));
        }

        for (int i = 0; i < innerCount; i++)
        {
            float t = 2f * Mathf.PI * i / innerCount;
            Vector2 p = AddNoise(new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * factor, noiseAmount);
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), 1));
        }

        return result;
    }

    private List<DataPoint> GenerateAnisotropicBlobs(int count, int clusterCount = 3)
    {
        List<DataPoint> result = new List<DataPoint>(count);
        Vector2[] centers = RandomClusterCenters(clusterCount, 3f);

        float a = 0.6f, b = -0.6f, c = 0.4f, d = 0.8f;

        for (int i = 0; i < count; i++)
        {
            int cluster = i % clusterCount;
            Vector2 raw = new Vector2(NextGaussian(0f, 1f), NextGaussian(0f, 1f));
            Vector2 sheared = new Vector2(a * raw.x + b * raw.y, c * raw.x + d * raw.y);
            Vector2 p = centers[cluster] + sheared;
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), cluster));
        }

        return result;
    }

    private List<DataPoint> GenerateVaryingDensityBlobs(int count, int clusterCount = 3)
    {
        List<DataPoint> result = new List<DataPoint>(count);
        Vector2[] centers = RandomClusterCenters(clusterCount, 3.5f);
        float[] spreads = new float[clusterCount];
        for (int c = 0; c < clusterCount; c++)
            spreads[c] = Mathf.Lerp(0.25f, 1.1f, c / (float)Mathf.Max(1, clusterCount - 1));

        for (int i = 0; i < count; i++)
        {
            int cluster = i % clusterCount;
            Vector2 offset = new Vector2(NextGaussian(0f, spreads[cluster]), NextGaussian(0f, spreads[cluster]));
            Vector2 p = centers[cluster] + offset;
            result.Add(new DataPoint(new Vector3(p.x, 0f, p.y), cluster));
        }

        return result;
    }

    // ------------------------------------------------------------------
    // SHARED HELPERS
    // ------------------------------------------------------------------
    private Vector2[] RandomClusterCenters(int count, float spread)
    {
        Vector2[] centers = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float radius = spread * (float)rng.NextDouble();
            centers[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return centers;
    }

    private Vector2 AddNoise(Vector2 point, float amount)
    {
        return point + new Vector2(NextGaussian(0f, amount), NextGaussian(0f, amount));
    }

    private float NextGaussian(float mean, float stdDev)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * (float)randStdNormal;
    }

    // ------------------------------------------------------------------
    // NORMALIZATION — fit generated points inside the axis grid range (all 3 axes)
    // ------------------------------------------------------------------
    private void NormalizeToAxis(List<DataPoint> points)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.position.x);
            maxX = Mathf.Max(maxX, p.position.x);
            minY = Mathf.Min(minY, p.position.y);
            maxY = Mathf.Max(maxY, p.position.y);
            minZ = Mathf.Min(minZ, p.position.z);
            maxZ = Mathf.Max(maxZ, p.position.z);
        }

        float rangeX = Mathf.Max(0.0001f, maxX - minX);
        float rangeY = Mathf.Max(0.0001f, maxY - minY);
        float rangeZ = Mathf.Max(0.0001f, maxZ - minZ);
        float range = Mathf.Max(rangeX, Mathf.Max(rangeY, rangeZ));

        float axisLength = axisRenderer != null ? axisRenderer.axisLength : 5f;
        float targetHalfExtent = axisLength * fitMargin;
        float scale = (targetHalfExtent * 2f) / range;

        Vector3 centerOffset = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f
        );

        for (int i = 0; i < points.Count; i++)
        {
            DataPoint p = points[i];
            p.position = (p.position - centerOffset) * scale;
            points[i] = p;
        }
    }

    // ------------------------------------------------------------------
    // SPAWNING
    // ------------------------------------------------------------------
    private void SpawnPoints(List<DataPoint> points)
    {
        EnsurePointsRoot();

        for (int i = 0; i < points.Count; i++)
        {
            DataPoint p = points[i];
            GameObject go = CreatePointObject();
            DatasetPointMarker marker = go.AddComponent<DatasetPointMarker>();
            marker.pointIndex = i;
            go.transform.SetParent(pointsRoot, false);

            // verticalOffset is a pure rendering lift (keeps points off the grid lines) —
            // it is NEVER part of the actual data used for clustering.
            go.transform.localPosition = new Vector3(p.position.x, p.position.y + verticalOffset, p.position.z);
            go.transform.localScale = Vector3.one * pointScale;

            Color color = p.trueColor ?? (clusterColors.Length > 0
                ? clusterColors[p.label % clusterColors.Length]
                : Color.white);
            ApplyColor(go, color);

            spawnedPoints.Add(go);
            pointLabels.Add(p.label);
            pointTrueColors.Add(p.trueColor);
            pointDataPositions.Add(p.position);
        }
    }

    // ------------------------------------------------------------------
    // PUBLIC DATA ACCESS — used by KMeansController
    // ------------------------------------------------------------------

    /// <summary>The true 3D data position of every point (no rendering offset applied) — this is
    /// what clustering runs on.</summary>
    public List<Vector3> GetPointPositions3D()
    {
        return new List<Vector3>(pointDataPositions);
    }

    public void SetPointColor(int index, Color color)
    {
        if (index < 0 || index >= spawnedPoints.Count) return;
        if (spawnedPoints[index] == null) return;
        ApplyColor(spawnedPoints[index], color);
    }

    public void ApplyGroundTruthColors()
    {
        for (int i = 0; i < spawnedPoints.Count; i++)
        {
            if (spawnedPoints[i] == null) continue;
            Color color = pointTrueColors[i] ?? (clusterColors.Length > 0
                ? clusterColors[pointLabels[i] % clusterColors.Length]
                : Color.white);
            ApplyColor(spawnedPoints[i], color);
        }
    }

    public bool HasGroundTruthLabels()
    {
        if (pointLabels == null || pointLabels.Count == 0)
            return false;

        HashSet<int> uniqueLabels = new HashSet<int>();
        for (int i = 0; i < pointLabels.Count; i++)
        {
            int label = pointLabels[i];
            if (label == -1) continue;
            uniqueLabels.Add(label);
        }

        return uniqueLabels.Count > 1;
    }

    private void EnsurePointsRoot()
    {
        if (pointsRoot != null) return;

        Transform parent = axisRenderer != null ? axisRenderer.transform : transform;
        GameObject rootObj = new GameObject("DatasetPoints");
        rootObj.transform.SetParent(parent, false);
        pointsRoot = rootObj.transform;
    }

    private void ClearPoints()
    {
        for (int i = spawnedPoints.Count - 1; i >= 0; i--)
        {
            if (spawnedPoints[i] != null)
            {
                if (Application.isPlaying) Destroy(spawnedPoints[i]);
                else DestroyImmediate(spawnedPoints[i]);
            }
        }
        spawnedPoints.Clear();
        pointLabels.Clear();
        pointTrueColors.Clear();
        pointDataPositions.Clear();
    }

    private GameObject CreatePointObject()
    {
        if (pointPrefab != null) return Instantiate(pointPrefab);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "DataPoint";
        return go;
    }

    private void ApplyColor(GameObject go, Color color)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr == null) return;

        mr.sharedMaterial = GetMaterial(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private Material GetMaterial(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null) return cached;

        Material m;
        if (pointMaterial != null)
        {
            m = new Material(pointMaterial);
        }
        else
        {
            Shader s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");
            m = new Material(s);
        }

        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);

        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        materialCache[color] = m;
        return m;
    }

    // ------------------------------------------------------------------
    // REAL DATASETS — Iris (Fisher/UCI) and Mall Customer Segmentation (Kaggle).
    // Embedded directly as data — the values below ARE the real published datasets.
    // ------------------------------------------------------------------
    private static readonly string IrisPetalData =
@"1.4,0.2,0
1.4,0.2,0
1.3,0.2,0
1.5,0.2,0
1.4,0.2,0
1.7,0.4,0
1.4,0.3,0
1.5,0.2,0
1.4,0.2,0
1.5,0.1,0
1.5,0.2,0
1.6,0.2,0
1.4,0.1,0
1.1,0.1,0
1.2,0.2,0
1.5,0.4,0
1.3,0.4,0
1.4,0.3,0
1.7,0.3,0
1.5,0.3,0
1.7,0.2,0
1.5,0.4,0
1.0,0.2,0
1.7,0.5,0
1.9,0.2,0
1.6,0.2,0
1.6,0.4,0
1.5,0.2,0
1.4,0.2,0
1.6,0.2,0
1.6,0.2,0
1.5,0.4,0
1.5,0.1,0
1.4,0.2,0
1.5,0.2,0
1.2,0.2,0
1.3,0.2,0
1.4,0.1,0
1.3,0.2,0
1.5,0.2,0
1.3,0.3,0
1.3,0.3,0
1.3,0.2,0
1.6,0.6,0
1.9,0.4,0
1.4,0.3,0
1.6,0.2,0
1.4,0.2,0
1.5,0.2,0
1.4,0.2,0
4.7,1.4,1
4.5,1.5,1
4.9,1.5,1
4.0,1.3,1
4.6,1.5,1
4.5,1.3,1
4.7,1.6,1
3.3,1.0,1
4.6,1.3,1
3.9,1.4,1
3.5,1.0,1
4.2,1.5,1
4.0,1.0,1
4.7,1.4,1
3.6,1.3,1
4.4,1.4,1
4.5,1.5,1
4.1,1.0,1
4.5,1.5,1
3.9,1.1,1
4.8,1.8,1
4.0,1.3,1
4.9,1.5,1
4.7,1.2,1
4.3,1.3,1
4.4,1.4,1
4.8,1.4,1
5.0,1.7,1
4.5,1.5,1
3.5,1.0,1
3.8,1.1,1
3.7,1.0,1
3.9,1.2,1
5.1,1.6,1
4.5,1.5,1
4.5,1.6,1
4.7,1.5,1
4.4,1.3,1
4.1,1.3,1
4.0,1.3,1
4.4,1.2,1
4.6,1.4,1
4.0,1.2,1
3.3,1.0,1
4.2,1.3,1
4.2,1.2,1
4.2,1.3,1
4.3,1.3,1
3.0,1.1,1
4.1,1.3,1
6.0,2.5,2
5.1,1.9,2
5.9,2.1,2
5.6,1.8,2
5.8,2.2,2
6.6,2.1,2
4.5,1.7,2
6.3,1.8,2
5.8,1.8,2
6.1,2.5,2
5.1,2.0,2
5.3,1.9,2
5.5,2.1,2
5.0,2.0,2
5.1,2.4,2
5.3,2.3,2
5.5,1.8,2
6.7,2.2,2
6.9,2.3,2
5.0,1.5,2
5.7,2.3,2
4.9,2.0,2
6.7,2.0,2
4.9,1.8,2
5.7,2.1,2
6.0,1.8,2
4.8,1.8,2
4.9,1.8,2
5.6,2.1,2
5.8,1.6,2
6.1,1.9,2
6.4,2.0,2
5.6,2.2,2
5.1,1.5,2
5.6,1.4,2
6.1,2.3,2
5.6,2.4,2
5.5,1.8,2
4.8,1.8,2
5.4,2.1,2
5.6,2.4,2
5.1,2.3,2
5.1,1.9,2
5.9,2.3,2
5.7,2.5,2
5.2,2.3,2
5.0,1.9,2
5.2,2.0,2
5.4,2.3,2
5.1,1.8,2";

    private List<DataPoint> GenerateIris()
    {
        List<DataPoint> result = new List<DataPoint>();
        foreach (string line in IrisPetalData.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            string[] parts = trimmed.Split(',');
            float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
            float z = float.Parse(parts[1], CultureInfo.InvariantCulture);
            int label = int.Parse(parts[2], CultureInfo.InvariantCulture);
            result.Add(new DataPoint(new Vector3(x, 0f, z), label));
        }
        return result;
    }

    private static readonly string CustomerSegmentationRawData =
@"15,39
15,81
16,6
16,77
17,40
17,76
18,6
18,94
19,3
19,72
19,14
19,99
20,15
20,77
20,13
20,79
21,35
21,66
23,29
23,98
24,35
24,73
25,5
25,73
28,14
28,82
28,32
28,61
29,31
29,87
30,4
30,73
33,4
33,92
33,14
33,81
34,17
34,73
37,26
37,75
38,35
38,92
39,36
39,61
39,28
39,65
40,55
40,47
40,42
40,42
42,52
42,60
43,54
43,60
43,45
43,41
44,50
44,46
46,51
46,46
46,56
46,55
47,52
47,59
48,51
48,59
48,50
48,48
48,59
48,47
49,55
49,42
50,49
50,56
54,47
54,54
54,53
54,48
54,52
54,42
54,51
54,55
54,41
54,44
54,57
54,46
57,58
57,55
58,60
58,46
59,55
59,41
60,49
60,40
60,42
60,52
60,47
60,50
61,42
61,49
62,41
62,48
62,59
62,55
62,56
62,42
63,50
63,46
63,43
63,48
63,52
63,54
64,42
64,46
65,48
65,50
65,43
65,59
67,43
67,57
67,56
67,40
69,58
69,91
70,29
70,77
71,35
71,95
71,11
71,75
71,9
71,75
72,34
72,71
73,5
73,88
73,7
73,73
74,10
74,72
75,5
75,93
76,40
76,87
77,12
77,97
77,36
77,74
78,22
78,90
78,17
78,88
78,20
78,76
78,16
78,89
78,1
78,78
78,1
78,73
79,35
79,83
81,5
81,93
85,26
85,75
86,20
86,95
87,27
87,63
87,13
87,75
87,10
87,92
88,13
88,86
88,15
88,69
93,14
93,90
97,32
97,86
98,15
98,88
99,39
99,97
101,24
101,68
103,17
103,85
103,23
103,69
113,8
113,91
120,16
120,79
126,28
126,74
137,18
137,83";

    private List<DataPoint> GenerateCustomerSegmentation()
    {
        List<DataPoint> result = new List<DataPoint>();
        foreach (string line in CustomerSegmentationRawData.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            string[] parts = trimmed.Split(',');
            float income = float.Parse(parts[0], CultureInfo.InvariantCulture);
            float spending = float.Parse(parts[1], CultureInfo.InvariantCulture);
            result.Add(new DataPoint(new Vector3(income, 0f, spending), 0));
        }
        return result;
    }

    public bool HasOriginalColors()
{
    return pointTrueColors != null &&
           pointTrueColors.Count > 0;
}

    // ------------------------------------------------------------------
    // IMAGE COLOR QUANTIZATION — genuinely 3D: R -> X, G -> Y (height), B -> Z
    // ------------------------------------------------------------------
    private List<DataPoint> GenerateImageColorQuantization()
    {
        List<DataPoint> result = new List<DataPoint>();

        Texture2D tex = Resources.Load<Texture2D>(imageResourceName);
        if (tex == null)
        {
            Debug.LogWarning($"[DatasetVisualizer] Could not load '{imageResourceName}' from a Resources folder. Check the name and that it's Read/Write enabled.");
            return result;
        }

        int width = tex.width;
        int height = tex.height;
        int sampleCount = Mathf.Min(imageSampleCount, width * height);

        for (int i = 0; i < sampleCount; i++)
        {
            int px = rng.Next(width);
            int py = rng.Next(height);
            Color32 pixel = tex.GetPixel(px, py);

            Vector3 pos = new Vector3(pixel.r, pixel.g, pixel.b);
            Color trueColor = new Color(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f);
            result.Add(new DataPoint(pos, 0, trueColor));
        }

        return result;
    }
}