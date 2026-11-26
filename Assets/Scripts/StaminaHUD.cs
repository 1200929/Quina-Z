using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class StaminaHUD : MonoBehaviour
{
    [Tooltip("Slider da barra (por defeito, o do próprio objeto).")]
    public Slider slider;
    [Tooltip("Velocidade do fade (alfa por segundo).")]
    public float fadeSpeed = 8f;

    private CanvasGroup group;
    private bool visible;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (slider == null) slider = GetComponent<Slider>();

        group.alpha = 0f;            // começa escondida
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void Show(bool show) => visible = show;

    public void SetValue(float normalized)
    {
        if (slider != null) slider.value = Mathf.Clamp01(normalized);
    }

    private void Update()
    {
        float target = visible ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
    }
}