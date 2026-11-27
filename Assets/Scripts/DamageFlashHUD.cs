using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DamageFlashHUD : MonoBehaviour
{
    [Header("Aspeto")]
    public Color flashColor = new Color(0.8f, 0f, 0f, 1f);
    [Tooltip("Onde o vermelho começa, do centro (0) aos cantos (1). Mais alto = só nos cantos.")]
    [Range(0.1f, 0.9f)] public float innerRadius = 0.5f;

    [Header("Intensidade")]
    [Tooltip("Opacidade do flash para o dano mais pequeno.")]
    [Range(0f, 1f)] public float minFlashAlpha = 0.25f;
    [Tooltip("Opacidade máxima do flash.")]
    [Range(0f, 1f)] public float maxFlashAlpha = 0.65f;
    [Tooltip("Dano a partir do qual o flash chega à opacidade máxima.")]
    public float damageForMaxFlash = 30f;
    [Tooltip("Segundos que o flash máximo demora a desaparecer.")]
    public float fadeDuration = 0.6f;

    private RawImage image;
    private Texture2D vignetteTexture;
    private PlayerHealth health;
    private float flash;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;          // não bloqueia cliques
        vignetteTexture = BuildVignetteTexture();
        image.texture = vignetteTexture;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (health != null) health.currentHealth.OnValueChanged -= OnHealthChanged;
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }

    private void Update()
    {
        if (health == null) TryBind();

        if (flash > 0f)
        {
            flash = Mathf.MoveTowards(flash, 0f, (maxFlashAlpha / fadeDuration) * Time.unscaledDeltaTime);
            SetAlpha(flash);
        }
    }

    // Espera pelo jogador local e liga-se à vida dele
    private void TryBind()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var playerObj = nm.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        health = playerObj.GetComponent<PlayerHealth>();
        if (health != null) health.currentHealth.OnValueChanged += OnHealthChanged;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        float damage = oldValue - newValue;
        if (damage <= 0f) return;             // só reage quando a vida desce

        float t = Mathf.Clamp01(damage / damageForMaxFlash);
        float target = Mathf.Lerp(minFlashAlpha, maxFlashAlpha, t);
        flash = Mathf.Max(flash, target);     // vários golpes seguidos não "apagam" o flash
        SetAlpha(flash);
    }

    private void SetAlpha(float alpha)
    {
        image.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
    }

    // Textura branca com o centro transparente e as bordas opacas (a cor vem do RawImage)
    private Texture2D BuildVignetteTexture()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f)) * 2f - 1f;
                float dy = (y / (size - 1f)) * 2f - 1f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f; // 0 no centro, 1 nos cantos
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, 1f, dist));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    // Para testar sem levar dano: no Play, menu ⋮ do componente -> "Testar flash"
    [ContextMenu("Testar flash")]
    private void TestFlash()
    {
        flash = maxFlashAlpha;
        SetAlpha(flash);
    }
}