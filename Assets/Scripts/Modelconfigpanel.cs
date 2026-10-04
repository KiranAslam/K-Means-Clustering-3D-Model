using UnityEngine;
using TMPro;

public class ModelConfigPanel : MonoBehaviour
{
    [Header("References")]
    public KMeansController kmeans;
    [Tooltip("Same dropdown DatasetVisualizer uses — read here just to display its current text.")]
    public TMP_Dropdown datasetDropdown;

    [Header("Value Text Fields (right column only)")]
    public TMP_Text datasetValueText;
    public TMP_Text kValueText;
    public TMP_Text initValueText;
    public TMP_Text distanceValueText;

    private void Update()
    {
        if (kmeans == null) return;

        if (datasetValueText != null && datasetDropdown != null && datasetDropdown.options.Count > 0)
            datasetValueText.text = datasetDropdown.options[datasetDropdown.value].text;

        if (kValueText != null)
            kValueText.text = kmeans.kValue.ToString();

        if (initValueText != null)
        {
            if (kmeans.distanceMetric == KMeansController.DistanceMetric.Euclidean &&
                kmeans.initMethod == KMeansController.InitMethod.KMeansPlusPlus)
            {
                initValueText.text = "K-Means++";
            }
            else if (kmeans.initMethod == KMeansController.InitMethod.KMeansPlusPlus)
            {
                initValueText.text = "Random (K-Means++ unavailable)";
            }
            else
            {
                initValueText.text = "Random";
            }
        }

        if (distanceValueText != null)
            distanceValueText.text = kmeans.distanceMetric.ToString();
    }
}