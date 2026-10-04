using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GroundTruthToggle : MonoBehaviour
{
    [Header("References")]
    public DatasetVisualizer datasetVisualizer;
    public KMeansController kmeans;
    public Button toggleButton;

    [Tooltip("Optional — shows 'Show True Clusters' / 'Show My Clusters'.")]
    public TMP_Text toggleButtonLabel;


    private bool showingGroundTruth;


    private void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(OnToggleClicked);


        if (datasetVisualizer != null)
            datasetVisualizer.OnPointsReady += HandleDatasetChanged;


        UpdateLabel();
    }


    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(OnToggleClicked);


        if (datasetVisualizer != null)
            datasetVisualizer.OnPointsReady -= HandleDatasetChanged;
    }


    private void OnToggleClicked()
    {
        // Pause running K-Means so colors are not overwritten
        if (kmeans != null)
            kmeans.PauseIfPlaying();


        if (!HasReferenceVisualization())
        {
            showingGroundTruth = false;

            Debug.LogWarning(
                "[GroundTruthToggle] No reference visualization available for this dataset."
            );

            UpdateLabel();
            return;
        }


        showingGroundTruth = !showingGroundTruth;


        if (showingGroundTruth)
        {
            datasetVisualizer.ApplyGroundTruthColors();
        }
        else
        {
            kmeans.ReapplyCurrentView();
        }


        UpdateLabel();
    }


    private void HandleDatasetChanged()
    {
        // Every new dataset starts from K-Means view
        showingGroundTruth = false;

        UpdateLabel();
    }


    /// <summary>
    /// Checks whether the selected dataset has a valid reference visualization.
    /// This can be:
    /// - True cluster labels
    /// - Original pixel colors (Image Color Quantization)
    /// </summary>
    private bool HasReferenceVisualization()
    {
        if (datasetVisualizer == null)
            return false;


        DatasetVisualizer.DatasetType dataset =
            (DatasetVisualizer.DatasetType)datasetVisualizer.datasetDropdown.value;


        switch (dataset)
        {
            case DatasetVisualizer.DatasetType.CustomerSegmentation:

                // No real cluster labels available
                return false;


            case DatasetVisualizer.DatasetType.ImageColorQuantization:

                // Original pixel colors are the reference
                return datasetVisualizer.HasOriginalColors();


            default:

                // Synthetic + Iris datasets with labels
                return datasetVisualizer.HasGroundTruthLabels();
        }
    }


    private void UpdateLabel()
    {
        if (toggleButton != null)
            toggleButton.interactable = HasReferenceVisualization();


        if (toggleButtonLabel == null)
            return;


        toggleButtonLabel.text =
            showingGroundTruth
            ? "Show My Clusters"
            : "Show True Clusters";
    }
}