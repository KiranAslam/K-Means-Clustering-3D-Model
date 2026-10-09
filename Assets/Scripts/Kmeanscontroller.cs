using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KMeansController : MonoBehaviour
{
    public enum InitMethod { Random, KMeansPlusPlus }
    public enum DistanceMetric { Euclidean, Manhattan, Cosine }

    [Header("References")]
    public DatasetVisualizer datasetVisualizer;
    public AxisRenderer axisRenderer;

    [Header("UI — K Value")]
    public Slider kSlider;
    public TMP_Text kValueLabel;

    [Header("UI — Initialization")]
    public Button randomInitButton;
    public Button kMeansPlusPlusButton;

    [Header("UI — Distance Metric")]
    public TMP_Dropdown distanceMetricDropdown;

    [Header("UI — Playback")]
    public Button startButton;
    public TMP_Text startButtonLabel;
    public Button restartButton;
    public TMP_Text iterationLabel;
    public Slider speedSlider;

    [Header("UI — Button Selected Colors")]
    public Color buttonSelectedBackgroundColor = new Color(0f, 0.4f, 0f);
    public Color buttonSelectedTextColor = Color.white;

    [Header("Algorithm Settings")]
    [Range(2, 10)] public int kValue = 3;
    public InitMethod initMethod = InitMethod.KMeansPlusPlus;
    public DistanceMetric distanceMetric = DistanceMetric.Euclidean;
    [Min(1)] public int maxIterations = 100;
    public float convergenceEpsilon = 0.001f;
    public int randomSeed = 7;

    [Header("Animation Timing")]
    public float maxStepDuration = 2.5f;
    public float minStepDuration = 0.6f;

    [Header("Point Reveal Animation")]
    public bool sequentialReveal = true;

    [Header("Point Coloring")]
    public Color[] clusterColors;
    public Color neutralPointColor = new Color32(0x6A, 0x91, 0x86, 255);

    [Header("Centroid Markers")]
    public float centroidScaleMultiplier = 2.4f;
    [Range(0f, 1f)] public float centroidBrighten = 0.35f;
    public Material centroidMaterial;

    [Header("UI — Silhouette Score")]
    public TMP_Text silhouetteScoreLabel;
    public GameObject silhouetteContentRoot;

    [Header("UI — Why This Point")]
    public GameObject whyThisPointContentRoot;

    public bool IsRunning => isRunning;
    public bool HasValidClustering =>
        currentPoints != null &&
        currentAssignments != null &&
        currentCentroids != null;

    [Header("Centroid Trail")]
    public bool showCentroidTrail = true;
    public float trailWidth = 0.03f;
    [Range(0f, 1f)] public float trailAlpha = 0.6f;

    [Header("Side Panels")]
    public MonoBehaviour silhouetteController;
    public WhyThisPointController whyThisPointController;

    private System.Random rng;
    private Transform centroidsRoot;
    private readonly List<GameObject> spawnedCentroids = new List<GameObject>();
    private readonly List<LineRenderer> centroidTrails = new List<LineRenderer>();
    private readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();
    private Mesh diamondMesh;

    private List<Vector3> currentPoints;
    private Vector3[] currentCentroids;
    private int[] currentAssignments;
    private int currentK;
    private int iterationCount;
    private bool hasConverged;
    private bool sessionFinished;

    private bool isRunning;
    private bool isPlaying;
    private bool isBusy;

    public bool IsSessionRunning => isRunning;
    public int CurrentK => currentK;
    public Vector3 GetPointPosition(int index) => currentPoints[index];
    public List<Vector3> GetAllPoints() => currentPoints;
    public Vector3[] GetCurrentCentroids() => currentCentroids;
    public int[] GetCurrentAssignments() => currentAssignments;
    public float ComputeDistance(Vector3 a, Vector3 b) => Distance(a, b);

    private void Awake()
    {
        if (datasetVisualizer == null) datasetVisualizer = FindObjectOfType<DatasetVisualizer>();
        if (axisRenderer == null) axisRenderer = FindObjectOfType<AxisRenderer>();
        if (clusterColors == null || clusterColors.Length == 0)
            clusterColors = datasetVisualizer != null ? datasetVisualizer.clusterColors : new Color[] { Color.white };
    }

    private void Start()
    {
        if (datasetVisualizer != null)
            datasetVisualizer.OnPointsReady += HandleDatasetChanged;

        WireSlider();
        WireInitButtons();
        WireDistanceDropdown();
        WirePlaybackControls();

        UpdateKLabel();
        UpdateStartButtonLabel();
    }

    private void OnDestroy()
    {
        if (datasetVisualizer != null)
            datasetVisualizer.OnPointsReady -= HandleDatasetChanged;

        if (kSlider != null) kSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        if (randomInitButton != null) randomInitButton.onClick.RemoveListener(OnRandomInitClicked);
        if (kMeansPlusPlusButton != null) kMeansPlusPlusButton.onClick.RemoveListener(OnKMeansPlusPlusClicked);
        if (distanceMetricDropdown != null) distanceMetricDropdown.onValueChanged.RemoveListener(OnDistanceMetricChanged);
        if (startButton != null) startButton.onClick.RemoveListener(OnStartButtonClicked);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
    }

    private void WireSlider()
    {
        if (kSlider == null) return;

        kSlider.wholeNumbers = true;
        kSlider.minValue = 2;
        kSlider.maxValue = 10;
        kSlider.value = kValue;
        kSlider.onValueChanged.AddListener(OnSliderValueChanged);

        EventTrigger trigger = kSlider.GetComponent<EventTrigger>();
        if (trigger == null) trigger = kSlider.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry releaseEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        releaseEntry.callback.AddListener(_ => OnSliderReleased());
        trigger.triggers.Add(releaseEntry);
    }

    private void WireInitButtons()
    {
        ConfigureButtonColorFeedback(randomInitButton);
        ConfigureButtonColorFeedback(kMeansPlusPlusButton);

        if (randomInitButton != null) randomInitButton.onClick.AddListener(OnRandomInitClicked);
        if (kMeansPlusPlusButton != null) kMeansPlusPlusButton.onClick.AddListener(OnKMeansPlusPlusClicked);
    }

    private void WireDistanceDropdown()
    {
        if (distanceMetricDropdown == null) return;
        distanceMetricDropdown.onValueChanged.AddListener(OnDistanceMetricChanged);
    }

    private void WirePlaybackControls()
    {
        ConfigureButtonColorFeedback(startButton);
        ConfigureButtonColorFeedback(restartButton);

        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);

        if (speedSlider != null)
        {
            speedSlider.wholeNumbers = false;
            speedSlider.minValue = 0f;
            speedSlider.maxValue = 1f;
            if (Mathf.Approximately(speedSlider.value, 0f)) speedSlider.value = 0.25f;
        }
    }

    private void ConfigureButtonColorFeedback(Button button)
    {
        if (button == null) return;

        KMeansButtonColorFeedback colorFeedback = button.GetComponent<KMeansButtonColorFeedback>();
        if (colorFeedback == null)
            colorFeedback = button.gameObject.AddComponent<KMeansButtonColorFeedback>();

        colorFeedback.SetSelectedColors(buttonSelectedBackgroundColor, buttonSelectedTextColor);
    }

    private void OnSliderValueChanged(float value)
    {
        kValue = Mathf.RoundToInt(value);
        UpdateKLabel();
    }

    private void OnSliderReleased()
    {
        kValue = Mathf.RoundToInt(kSlider.value);
        StopSession();
    }

    private void OnRandomInitClicked()
    {
        initMethod = InitMethod.Random;
        StopSession();
    }

    private void OnKMeansPlusPlusClicked()
    {
        initMethod = InitMethod.KMeansPlusPlus;
        StopSession();
    }

    private void OnDistanceMetricChanged(int dropdownIndex)
    {
        if (!Enum.IsDefined(typeof(DistanceMetric), dropdownIndex)) return;
        distanceMetric = (DistanceMetric)dropdownIndex;
        StopSession();
    }

    private void HandleDatasetChanged()
    {
        if (silhouetteScoreLabel != null)
            silhouetteScoreLabel.text = "--";

        if (whyThisPointController != null)
            whyThisPointController.ClearSelection();

        StopSession();
    }

    private void UpdateKLabel()
    {
        if (kValueLabel != null) kValueLabel.text = kValue.ToString();
    }

    private void UpdateIterationLabel()
    {
        if (iterationLabel != null) iterationLabel.text = iterationCount.ToString();
    }

    private void UpdateSilhouetteScore()
    {
        if (silhouetteScoreLabel == null) return;
        if (currentPoints == null || currentAssignments == null || currentK < 2)
        {
            silhouetteScoreLabel.text = "--";
            return;
        }

        float score = ComputeSilhouetteScore(currentPoints, currentAssignments, currentK);
        silhouetteScoreLabel.text = score.ToString("0.00");
    }

    private float ComputeSilhouetteScore(List<Vector3> points, int[] assignments, int k)
    {
        int n = points.Count;
        if (n < 2) return 0f;

        float totalScore = 0f;

        for (int i = 0; i < n; i++)
        {
            int ownCluster = assignments[i];
            float sumSame = 0f;
            int countSame = 0;
            float[] sumOther = new float[k];
            int[] countOther = new int[k];

            for (int j = 0; j < n; j++)
            {
                if (j == i) continue;
                float d = Distance(points[i], points[j]);

                if (assignments[j] == ownCluster)
                {
                    sumSame += d;
                    countSame++;
                }
                else
                {
                    sumOther[assignments[j]] += d;
                    countOther[assignments[j]]++;
                }
            }

            float a = countSame > 0 ? sumSame / countSame : 0f;
            float b = float.MaxValue;

            for (int c = 0; c < k; c++)
            {
                if (c == ownCluster || countOther[c] == 0) continue;
                float avg = sumOther[c] / countOther[c];
                if (avg < b) b = avg;
            }
            if (b == float.MaxValue) b = 0f;

            float s = (countSame == 0) ? 0f : (b - a) / Mathf.Max(a, b, 0.0001f);
            totalScore += s;
        }

        return totalScore / n;
    }

    private void UpdateStartButtonLabel()
    {
        if (startButtonLabel == null) return;
        if (!isRunning || sessionFinished) startButtonLabel.text = "Start";
        else startButtonLabel.text = isPlaying ? "Pause" : "Resume";
    }

    private void OnStartButtonClicked()
    {
        if (!isRunning || sessionFinished)
        {
            InitializeSession();
            CreateCentroidVisuals(currentCentroids);
            isPlaying = true;
            StartCoroutine(RunLoop());
        }
        else if (isPlaying)
        {
            isPlaying = false;
        }
        else
        {
            isPlaying = true;
            if (!isBusy) StartCoroutine(RunLoop());
        }

        UpdateStartButtonLabel();
    }

    /// <summary>Pauses the auto-play loop if it's currently running, without touching whether a
    /// session exists at all. Used by external UI (e.g. GroundTruthToggle) so a toggled view
    /// isn't immediately overwritten by the next in-flight iteration's recoloring.</summary>
    public void PauseIfPlaying()
    {
        if (isPlaying)
        {
            isPlaying = false;
            UpdateStartButtonLabel();
        }
    }

    private void OnRestartClicked()
    {
        StopSession();
        InitializeSession();
        CreateCentroidVisuals(currentCentroids);
        UpdateStartButtonLabel();
    }

    private void InitializeSession()
    {
        currentPoints = datasetVisualizer.GetPointPositions3D();
        currentK = Mathf.Clamp(kValue, 1, Mathf.Max(1, currentPoints.Count));
        rng = randomSeed >= 0 ? new System.Random(randomSeed) : new System.Random();

        currentCentroids = InitializeCentroids(currentPoints, currentK);
        currentAssignments = new int[currentPoints.Count];
        iterationCount = 0;
        hasConverged = false;
        sessionFinished = false;
        isRunning = true;

        UpdateIterationLabel();

        if (silhouetteContentRoot != null) silhouetteContentRoot.SetActive(true);
        if (whyThisPointContentRoot != null) whyThisPointContentRoot.SetActive(true);

        if (whyThisPointController != null) whyThisPointController.SetupDefaultRows(currentK);
    }

    private void StopSession()
    {
        StopAllCoroutines();
        isPlaying = false;
        isBusy = false;
        isRunning = false;
        hasConverged = false;
        sessionFinished = false;
        iterationCount = 0;

        ClearCentroids();
        ResetPointColorsToNeutral();
        UpdateStartButtonLabel();
        UpdateIterationLabel();

        if (silhouetteScoreLabel != null)
            silhouetteScoreLabel.text = "--";

        if (silhouetteContentRoot != null) silhouetteContentRoot.SetActive(false);
        if (whyThisPointContentRoot != null) whyThisPointContentRoot.SetActive(false);
        if (whyThisPointController != null) whyThisPointController.ClearSelection();
    }

    public void ReapplyCurrentView()
    {
        if (isRunning && currentAssignments != null)
            ApplyPointColors(currentAssignments);
        else
            ResetPointColorsToNeutral();
    }

    private IEnumerator RunLoop()
    {
        isBusy = true;

        do
        {
            yield return StartCoroutine(PerformIteration());
            iterationCount++;
            UpdateIterationLabel();
            UpdateSilhouetteScore();

            if (hasConverged || iterationCount >= maxIterations)
            {
                isPlaying = false;
                sessionFinished = true;

                if (AudioController.Instance != null)
                    AudioController.Instance.PlayClusteringCompleteVoice();

                break;
            }
        }
        while (isPlaying);

        isBusy = false;
        UpdateStartButtonLabel();
    }

    private IEnumerator PerformIteration()
    {
        AssignPointsToCentroids(currentPoints, currentCentroids, currentAssignments);

        float revealDuration = GetStepDuration() * 0.3f;
        if (sequentialReveal)
        {
            yield return StartCoroutine(RevealAssignmentsSequentially(currentAssignments, revealDuration));
        }
        else
        {
            ApplyPointColors(currentAssignments);
            yield return new WaitForSeconds(revealDuration);
        }

        Vector3[] newCentroids = RecomputeCentroids(currentPoints, currentAssignments, currentK);
        float movement = TotalMovement(currentCentroids, newCentroids);

        yield return StartCoroutine(AnimateCentroidsMove(currentCentroids, newCentroids, GetStepDuration()));
        currentCentroids = newCentroids;
        hasConverged = movement < convergenceEpsilon;

        if (silhouetteController != null)
            silhouetteController.SendMessage("RecomputeAndDisplay", SendMessageOptions.DontRequireReceiver);

        yield return new WaitForSeconds(GetStepDuration() * 0.4f);
    }

    private IEnumerator RevealAssignmentsSequentially(int[] assignments, float duration)
    {
        int total = assignments.Length;
        if (total == 0) yield break;

        duration = Mathf.Max(0.01f, duration);
        int revealed = 0;
        float elapsed = 0f;

        while (revealed < total)
        {
            elapsed += Time.deltaTime;
            int targetRevealed = Mathf.Clamp(Mathf.FloorToInt((elapsed / duration) * total), 0, total);

            for (; revealed < targetRevealed; revealed++)
            {
                Color color = clusterColors[assignments[revealed] % clusterColors.Length];
                datasetVisualizer.SetPointColor(revealed, color);
            }

            yield return null;
        }

        for (; revealed < total; revealed++)
        {
            Color color = clusterColors[assignments[revealed] % clusterColors.Length];
            datasetVisualizer.SetPointColor(revealed, color);
        }
    }

    private float GetStepDuration()
    {
        float t = speedSlider != null ? speedSlider.value : 0.5f;
        return Mathf.Lerp(maxStepDuration, minStepDuration, t);
    }

    private Vector3[] InitializeCentroids(List<Vector3> points, int k)
    {
        if (points == null || points.Count == 0 || k <= 0) return new Vector3[0];

        if (distanceMetric == DistanceMetric.Euclidean && initMethod == InitMethod.KMeansPlusPlus)
            return InitKMeansPlusPlus(points, k);

        return InitRandom(points, k);
    }

    private Vector3[] InitRandom(List<Vector3> points, int k)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < points.Count; i++) indices.Add(i);

        for (int i = 0; i < k; i++)
        {
            int swapIndex = i + rng.Next(indices.Count - i);
            int tmp = indices[i];
            indices[i] = indices[swapIndex];
            indices[swapIndex] = tmp;
        }

        Vector3[] centroids = new Vector3[k];
        for (int i = 0; i < k; i++) centroids[i] = points[indices[i]];
        return centroids;
    }

    private Vector3[] InitKMeansPlusPlus(List<Vector3> points, int k)
    {
        Vector3[] centroids = new Vector3[k];
        centroids[0] = points[rng.Next(points.Count)];

        float[] distSq = new float[points.Count];

        for (int c = 1; c < k; c++)
        {
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float nearest = float.MaxValue;
                for (int existing = 0; existing < c; existing++)
                {
                    float d = Distance(points[i], centroids[existing]);
                    if (d < nearest) nearest = d;
                }
                distSq[i] = nearest * nearest;
                total += distSq[i];
            }

            double target = rng.NextDouble() * total;
            double running = 0;
            int chosen = points.Count - 1;
            for (int i = 0; i < points.Count; i++)
            {
                running += distSq[i];
                if (running >= target) { chosen = i; break; }
            }

            centroids[c] = points[chosen];
        }

        return centroids;
    }

    private void AssignPointsToCentroids(List<Vector3> points, Vector3[] centroids, int[] assignments)
    {
        for (int i = 0; i < points.Count; i++)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int c = 0; c < centroids.Length; c++)
            {
                float d = Distance(points[i], centroids[c]);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            assignments[i] = best;
        }
    }

    private Vector3[] RecomputeCentroids(List<Vector3> points, int[] assignments, int k)
    {
        if (points == null || assignments == null || points.Count == 0 || k <= 0)
            return new Vector3[0];

        List<List<Vector3>> assignedPoints = new List<List<Vector3>>(k);
        for (int c = 0; c < k; c++)
            assignedPoints.Add(new List<Vector3>());

        for (int i = 0; i < points.Count; i++)
        {
            int clusterIndex = assignments[i];
            if (clusterIndex < 0) clusterIndex = 0;
            if (clusterIndex >= k) clusterIndex = k - 1;
            assignedPoints[clusterIndex].Add(points[i]);
        }

        Vector3[] result = new Vector3[k];
        for (int c = 0; c < k; c++)
        {
            if (assignedPoints[c].Count == 0)
            {
                result[c] = points[rng.Next(points.Count)];
                continue;
            }

            switch (distanceMetric)
            {
                case DistanceMetric.Manhattan:
                    // Manhattan distance corresponds to L1 clustering, where the centroid should be the
                    // coordinate-wise median of the assigned points instead of the arithmetic mean.
                    result[c] = ComputeMedianCentroid(assignedPoints[c]);
                    break;
                case DistanceMetric.Cosine:
                    // Cosine distance compares directions, so the centroid should be the mean direction
                    // of the assigned points, normalized back to the unit sphere.
                    result[c] = ComputeCosineCentroid(assignedPoints[c]);
                    break;
                case DistanceMetric.Euclidean:
                default:
                    // Euclidean K-Means minimizes squared Euclidean distance, which makes the arithmetic
                    // mean the correct centroid for each cluster.
                    result[c] = ComputeMeanCentroid(assignedPoints[c]);
                    break;
            }
        }

        return result;
    }

    private Vector3 ComputeMeanCentroid(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
            sum += points[i];

        return points.Count > 0 ? sum / points.Count : Vector3.zero;
    }

    private Vector3 ComputeMedianCentroid(List<Vector3> points)
    {
        if (points == null || points.Count == 0) return Vector3.zero;

        float[] xValues = new float[points.Count];
        float[] yValues = new float[points.Count];
        float[] zValues = new float[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            xValues[i] = points[i].x;
            yValues[i] = points[i].y;
            zValues[i] = points[i].z;
        }

        Array.Sort(xValues);
        Array.Sort(yValues);
        Array.Sort(zValues);

        int middle = points.Count / 2;
        float medianX = points.Count % 2 == 0
            ? (xValues[middle - 1] + xValues[middle]) * 0.5f
            : xValues[middle];
        float medianY = points.Count % 2 == 0
            ? (yValues[middle - 1] + yValues[middle]) * 0.5f
            : yValues[middle];
        float medianZ = points.Count % 2 == 0
            ? (zValues[middle - 1] + zValues[middle]) * 0.5f
            : zValues[middle];

        return new Vector3(medianX, medianY, medianZ);
    }

    private Vector3 ComputeCosineCentroid(List<Vector3> points)
    {
        if (points == null || points.Count == 0) return Vector3.zero;

        Vector3 mean = ComputeMeanCentroid(points);
        float magnitude = mean.magnitude;
        if (magnitude < 0.0001f) return Vector3.zero;

        return mean / magnitude;
    }

    private float TotalMovement(Vector3[] previous, Vector3[] updated)
    {
        float total = 0f;
        for (int i = 0; i < previous.Length; i++)
            total += Vector3.Distance(previous[i], updated[i]);
        return total;
    }

    public float Distance(Vector3 a, Vector3 b)
    {
        switch (distanceMetric)
        {
            case DistanceMetric.Manhattan:
                return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
            case DistanceMetric.Cosine:
                return CosineDistance(a, b);
            case DistanceMetric.Euclidean:
            default:
                return Vector3.Distance(a, b);
        }
    }

    public float[] GetDistancesFromPoint(int pointIndex)
    {
        if (currentPoints == null || currentCentroids == null) return null;
        if (pointIndex < 0 || pointIndex >= currentPoints.Count) return null;

        float[] distances = new float[currentCentroids.Length];
        for (int c = 0; c < currentCentroids.Length; c++)
            distances[c] = Distance(currentPoints[pointIndex], currentCentroids[c]);

        return distances;
    }

    private float CosineDistance(Vector3 a, Vector3 b)
    {
        float magA = a.magnitude;
        float magB = b.magnitude;
        if (magA < 0.0001f || magB < 0.0001f) return 1f;

        float cosineSimilarity = Vector3.Dot(a, b) / (magA * magB);
        return 1f - cosineSimilarity;
    }

    private void ApplyPointColors(int[] assignments)
    {
        for (int i = 0; i < assignments.Length; i++)
        {
            Color color = clusterColors[assignments[i] % clusterColors.Length];
            datasetVisualizer.SetPointColor(i, color);
        }
    }

    private void ResetPointColorsToNeutral()
    {
        if (datasetVisualizer == null) return;
        for (int i = 0; i < datasetVisualizer.PointCount; i++)
            datasetVisualizer.SetPointColor(i, neutralPointColor);
    }

    private void CreateCentroidVisuals(Vector3[] positions)
    {
        ClearCentroids();
        EnsureCentroidsRoot();

        float pointScale = datasetVisualizer.pointScale;
        float pointHeight = datasetVisualizer.verticalOffset + 0.02f;

        for (int c = 0; c < positions.Length; c++)
        {
            GameObject marker = new GameObject($"Centroid_{c}");
            marker.transform.SetParent(centroidsRoot, false);
            Vector3 startPos = new Vector3(positions[c].x, positions[c].y + pointHeight, positions[c].z);
            marker.transform.localPosition = startPos;
            marker.transform.localScale = Vector3.one * (pointScale * centroidScaleMultiplier);

            MeshFilter mf = marker.AddComponent<MeshFilter>();
            mf.mesh = GetDiamondMesh();

            MeshRenderer mr = marker.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Color baseColor = clusterColors[c % clusterColors.Length];
            Color dominant = Color.Lerp(baseColor, Color.white, centroidBrighten);
            mr.sharedMaterial = GetMaterial(dominant);

            spawnedCentroids.Add(marker);

            LineRenderer trail = null;
            if (showCentroidTrail)
            {
                GameObject trailObj = new GameObject($"Trail_{c}");
                trailObj.transform.SetParent(centroidsRoot, false);

                trail = trailObj.AddComponent<LineRenderer>();
                trail.useWorldSpace = false;
                trail.material = GetMaterial(baseColor);
                Color trailColor = new Color(baseColor.r, baseColor.g, baseColor.b, trailAlpha);
                trail.startColor = trailColor;
                trail.endColor = trailColor;
                trail.startWidth = trailWidth;
                trail.endWidth = trailWidth;
                trail.positionCount = 1;
                trail.SetPosition(0, startPos);
            }
            centroidTrails.Add(trail);
        }
    }

    private void AppendTrailPoint(int index, Vector3 localPos)
    {
        if (index >= centroidTrails.Count) return;
        LineRenderer trail = centroidTrails[index];
        if (trail == null) return;

        trail.positionCount++;
        trail.SetPosition(trail.positionCount - 1, localPos);
    }

    private IEnumerator AnimateCentroidsMove(Vector3[] from, Vector3[] to, float duration)
    {
        float pointHeight = datasetVisualizer.verticalOffset + 0.02f;
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float frac = Mathf.Clamp01(elapsed / duration);

            for (int c = 0; c < spawnedCentroids.Count && c < to.Length; c++)
            {
                Vector3 lerped = Vector3.Lerp(from[c], to[c], frac);
                spawnedCentroids[c].transform.localPosition = new Vector3(lerped.x, lerped.y + pointHeight, lerped.z);
            }
            yield return null;
        }

        for (int c = 0; c < spawnedCentroids.Count && c < to.Length; c++)
        {
            Vector3 finalPos = new Vector3(to[c].x, to[c].y + pointHeight, to[c].z);
            spawnedCentroids[c].transform.localPosition = finalPos;
            AppendTrailPoint(c, finalPos);
        }
    }

    private void EnsureCentroidsRoot()
    {
        if (centroidsRoot != null) return;

        Transform parent = axisRenderer != null ? axisRenderer.transform : transform;
        GameObject rootObj = new GameObject("Centroids");
        rootObj.transform.SetParent(parent, false);
        centroidsRoot = rootObj.transform;
    }

    private void ClearCentroids()
    {
        for (int i = spawnedCentroids.Count - 1; i >= 0; i--)
        {
            if (spawnedCentroids[i] != null)
            {
                if (Application.isPlaying) Destroy(spawnedCentroids[i]);
                else DestroyImmediate(spawnedCentroids[i]);
            }
        }
        spawnedCentroids.Clear();

        for (int i = centroidTrails.Count - 1; i >= 0; i--)
        {
            if (centroidTrails[i] != null)
            {
                if (Application.isPlaying) Destroy(centroidTrails[i].gameObject);
                else DestroyImmediate(centroidTrails[i].gameObject);
            }
        }
        centroidTrails.Clear();
    }

    private Mesh GetDiamondMesh()
    {
        if (diamondMesh != null) return diamondMesh;

        Vector3 top = Vector3.up;
        Vector3 bottom = Vector3.down;
        Vector3 front = Vector3.forward * 0.7f;
        Vector3 back = Vector3.back * 0.7f;
        Vector3 right = Vector3.right * 0.7f;
        Vector3 left = Vector3.left * 0.7f;

        Vector3[] vertices = { top, front, right, back, left, bottom };
        int[] triangles =
        {
            0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
            5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4
        };

        Mesh mesh = new Mesh();
        mesh.name = "CentroidDiamond";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        diamondMesh = mesh;
        return mesh;
    }

    private Material GetMaterial(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null) return cached;

        Material m;
        if (centroidMaterial != null)
        {
            m = new Material(centroidMaterial);
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
}