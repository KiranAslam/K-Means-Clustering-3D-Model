using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KMeansButtonColorFeedback : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private Graphic targetGraphic;
    private TMP_Text[] buttonTexts;
    private Color[] originalTextColors;
    private Color originalBackgroundColor;

    [SerializeField] private Color selectedBackgroundColor = new Color(0f, 0.4f, 0f);
    [SerializeField] private Color selectedTextColor = Color.white;

    private void Awake()
    {
        button = GetComponent<Button>();
        targetGraphic = button != null ? button.targetGraphic : GetComponent<Graphic>();
        if (targetGraphic != null)
            originalBackgroundColor = targetGraphic.color;

        buttonTexts = GetComponentsInChildren<TMP_Text>(true);
        originalTextColors = new Color[buttonTexts.Length];
        for (int i = 0; i < buttonTexts.Length; i++)
            originalTextColors[i] = buttonTexts[i].color;
    }

    public void SetSelectedColors(Color backgroundColor, Color textColor)
    {
        selectedBackgroundColor = backgroundColor;
        selectedTextColor = textColor;

        if (button == null || button.transition != Selectable.Transition.ColorTint)
            return;

        ColorBlock colors = button.colors;
        colors.selectedColor = selectedBackgroundColor;
        colors.pressedColor = colors.normalColor;
        button.colors = colors;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button != null)
            button.Select();
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetTextColors(selectedTextColor);
        if (button == null || button.transition != Selectable.Transition.ColorTint)
            SetBackgroundColor(selectedBackgroundColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        RestoreTextColors();

        if (button == null || button.transition != Selectable.Transition.ColorTint)
            SetBackgroundColor(originalBackgroundColor);
    }

    private void SetTextColors(Color color)
    {
        for (int i = 0; i < buttonTexts.Length; i++)
        {
            if (buttonTexts[i] != null)
                buttonTexts[i].color = color;
        }
    }

    private void RestoreTextColors()
    {
        for (int i = 0; i < buttonTexts.Length; i++)
        {
            if (buttonTexts[i] != null)
                buttonTexts[i].color = originalTextColors[i];
        }
    }

    private void SetBackgroundColor(Color color)
    {
        if (targetGraphic != null)
            targetGraphic.color = color;
    }
}
