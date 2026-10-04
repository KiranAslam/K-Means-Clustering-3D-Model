using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class WhyThisPointController : MonoBehaviour
{
    [Header("References")]
    public KMeansController kmeans;
    public Camera targetCamera;

    [Header("Panel")]
    public Transform rowsParent;
    public GameObject infoRowPrefab;
    public GameObject contentRoot;
    public GameObject placeholderText;

    [Header("Colors")]
    public Color nearestColor = new Color(0.3f, 0.85f, 0.7f);
    public Color normalColor = Color.white;

    public LayerMask pointLayerMask = ~0;

    private readonly List<GameObject> activeRows = new List<GameObject>();
    private readonly List<TMP_Text> rowLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> rowValues = new List<TMP_Text>();

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        if (kmeans == null) return;

        if (!kmeans.HasValidClustering)
        {
            if (Input.GetMouseButtonDown(0))
                Debug.Log("[WhyThisPoint] Click ignored — clustering data is not valid.");
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI())
            {
                Debug.Log("[WhyThisPoint] Click ignored — pointer was over UI.");
                return;
            }

            Debug.Log("[WhyThisPoint] Click detected, attempting raycast...");
            TrySelectPoint();
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void TrySelectPoint()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("[WhyThisPoint] targetCamera is null.");
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 1000f, Color.red, 2f);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, pointLayerMask))
        {
            Debug.Log($"[WhyThisPoint] Raycast HIT: '{hit.collider.gameObject.name}'");

            DatasetPointMarker marker = hit.collider.GetComponent<DatasetPointMarker>();
            if (marker == null)
            {
                Debug.LogWarning($"[WhyThisPoint] Hit object '{hit.collider.gameObject.name}' has NO DatasetPointMarker.");
                return;
            }

            Debug.Log($"[WhyThisPoint] Marker found, pointIndex = {marker.pointIndex}.");
            ShowDistancesForPoint(marker.pointIndex);
        }
        else
        {
            Debug.Log("[WhyThisPoint] Raycast MISS — nothing with a Collider was under the mouse.");
        }
    }

    /// <summary>
    /// Called by KMeansController when a session starts. Builds exactly K rows once, all showing
    /// "0.000" as a placeholder. Clicking a point later only updates these rows' text/color —
    /// it never instantiates or destroys rows again until the next fresh session.
    /// </summary>
    public void SetupDefaultRows(int k)
    {
        ClearRows();

        if (contentRoot != null) contentRoot.SetActive(true);
        if (placeholderText != null) placeholderText.SetActive(false);

        if (rowsParent == null || infoRowPrefab == null)
        {
            Debug.LogError("[WhyThisPoint] SetupDefaultRows: rowsParent or infoRowPrefab is not assigned in the Inspector.");
            return;
        }

        for (int i = 0; i < k; i++)
        {
            GameObject row = Instantiate(infoRowPrefab, rowsParent);
            row.SetActive(true);

            Transform labelT = row.transform.Find("Label");
            Transform valueT = row.transform.Find("Value");

            if (labelT == null || valueT == null)
            {
                Debug.LogError($"[WhyThisPoint] Row prefab is missing a 'Label' and/or 'Value' child (index {i}).");
                continue;
            }

            TMP_Text label = labelT.GetComponent<TMP_Text>();
            TMP_Text value = valueT.GetComponent<TMP_Text>();

            if (label == null || value == null)
            {
                Debug.LogError("[WhyThisPoint] 'Label'/'Value' object has no TMP_Text component.");
                continue;
            }

            label.text = $"Centroid {i + 1}";
            value.text = "--";
            label.color = normalColor;
            value.color = normalColor;

            activeRows.Add(row);
            rowLabels.Add(label);
            rowValues.Add(value);
        }

        Debug.Log($"[WhyThisPoint] SetupDefaultRows created {activeRows.Count} rows for K={k}.");
    }

    /// <summary>Updates the existing rows' values in place — no Instantiate/Destroy here.</summary>
    public void ShowDistancesForPoint(int pointIndex)
    {
        if (kmeans == null || !kmeans.HasValidClustering)
        {
            Debug.LogWarning("[WhyThisPoint] Ignored selection because clustering is not valid.");
            return;
        }

        float[] distances = kmeans.GetDistancesFromPoint(pointIndex);

        if (distances == null || distances.Length == 0)
        {
            Debug.LogWarning($"[WhyThisPoint] GetDistancesFromPoint({pointIndex}) returned null/empty.");
            return;
        }

        if (rowValues.Count == 0)
        {
            Debug.LogWarning("[WhyThisPoint] No rows exist yet — SetupDefaultRows was never called (or K changed after session start).");
            return;
        }

        int nearestIndex = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] < minDist) { minDist = distances[i]; nearestIndex = i; }
        }

        int count = Mathf.Min(distances.Length, rowValues.Count);
        for (int i = 0; i < count; i++)
        {
            rowValues[i].text = distances[i].ToString("0.000");
            Color rowColor = (i == nearestIndex) ? nearestColor : normalColor;
            rowLabels[i].color = rowColor;
            rowValues[i].color = rowColor;
        }

        Debug.Log($"[WhyThisPoint] Updated {count} rows for point {pointIndex}.");
    }

    public void ClearSelection()
    {
        for (int i = 0; i < rowValues.Count; i++)
        {
            rowValues[i].text = "--";
            rowLabels[i].color = normalColor;
            rowValues[i].color = normalColor;
        }
    }

    private void ClearRows()
    {
        foreach (var r in activeRows)
            if (r != null) Destroy(r);
        activeRows.Clear();
        rowLabels.Clear();
        rowValues.Clear();
    }
}