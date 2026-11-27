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

    [Header("Flash ao levar dano")]
    [Tooltip("Opacidade do flash para o dano mais pequeno.")]
    [Range(0f, 1f)] public float minFlashAlpha = 0.25f;
    [Tooltip("Opacidade máxima do flash.")]
    [Range(0f, 1f)] public float maxFlashAlpha = 0.65f;
    [Tooltip("Dano a partir do qual o flash chega à opacidade máxima.")]
    public float damageForMaxFlash = 30f;
    [Tooltip("Segundos que o flash máximo demora a desaparecer.")]
    public float fadeDuration = 0.6f;

    [Header("Vida baixa")]
    [Tooltip("Fração de vida (0 a 1) a partir da qual o aviso aparece. Com 45 % e 100 de vida, aparece aos 45.")]
    [Range(0.05f, 1f)] public float lowHealthThreshold = 0.45f;
    [Tooltip("Opacidade mínima do vermelho a pulsar.")]
    [Range(0f, 1f)] public float lowMinAlpha = 0.15f;
    [Tooltip("Opacidade máxima do vermelho a pulsar.")]
    [Range(0f, 1f)] public float lowMaxAlpha = 0.45f;
    [Tooltip("Velocidade do pulso quando a vida está no limiar (radianos por segundo).")]
    public float pulseSpeedAtThreshold = 4f;
    [Tooltip("Velocidade do pulso quando a vida está quase a zero.")]
    public float pulseSpeedAtZero = 9f;
    [Tooltip("Segundos que o aviso demora a aparecer e a desaparecer.")]
    public float lowHealthFadeTime = 0.5f;

    [Header("Batimento cardíaco (opcional)")]
    [Tooltip("Som em loop que toca enquanto a vida está baixa. Deixa vazio para não ter som.")]
    public AudioClip heartbeatClip;
    [Range(0f, 1f)] public float heartbeatVolume = 0.6f;

    [Header("Debug")]
    [Tooltip("Liga isto durante o Play para veres o aviso de vida baixa sem levares dano.")]
    public bool previewLowHealth;

    private RawImage image;
    private Texture2D vignetteTexture;
    private AudioSource heartbeat;
    private PlayerHealth health;

    private float flash;       // intensidade do flash de dano
    private float lowBlend;    // 0 = sem aviso, 1 = aviso completo
    private float pulsePhase;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;          // não bloqueia cliques
        vignetteTexture = BuildVignetteTexture();
        image.texture = vignetteTexture;
        SetAlpha(0f);

        if (heartbeatClip != null)
        {
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.clip = heartbeatClip;
            heartbeat.loop = true;
            heartbeat.playOnAwake = false;
            heartbeat.spatialBlend = 0f;      // som 2D
            heartbeat.volume = 0f;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.currentHealth.OnValueChanged -= OnHealthChanged;
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }

    private void Update()
    {
        if (health == null) TryBind();

        float dt = Time.unscaledDeltaTime;

        // 1) Flash ao levar dano
        if (flash > 0f)
            flash = Mathf.MoveTowards(flash, 0f, (maxFlashAlpha / Mathf.Max(0.01f, fadeDuration)) * dt);

        // 2) Aviso de vida baixa
        float lowAlpha = UpdateLowHealth(dt);

        // Mostra o mais forte dos dois
        SetAlpha(Mathf.Max(flash, lowAlpha));
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
    }

    // Devolve a opacidade do aviso de vida baixa
    private float UpdateLowHealth(float dt)
    {
        // Fração de vida atual (0 a 1). Sem jogador ligado, considera-se vida cheia.
        float fraction = 1f;
        if (health != null && health.maxHealth > 0f)
            fraction = Mathf.Clamp01(health.currentHealth.Value / health.maxHealth);
        if (previewLowHealth) fraction = lowHealthThreshold * 0.5f;

        bool isLow = fraction <= lowHealthThreshold;
        bool isDead = fraction <= 0f;

        lowBlend = Mathf.MoveTowards(lowBlend, isLow ? 1f : 0f, dt / Mathf.Max(0.01f, lowHealthFadeTime));

        UpdateHeartbeat(isDead);

        if (lowBlend <= 0f) return 0f;

        // Morto: vermelho fixo, sem pulsar
        if (isDead) return lowMaxAlpha * lowBlend;

        // Quanto menos vida, mais depressa pulsa
        float severity = 1f - Mathf.Clamp01(fraction / lowHealthThreshold);
        pulsePhase += Mathf.Lerp(pulseSpeedAtThreshold, pulseSpeedAtZero, severity) * dt;
        float pulse = Mathf.Sin(pulsePhase) * 0.5f + 0.5f;
        return Mathf.Lerp(lowMinAlpha, lowMaxAlpha, pulse) * lowBlend;
    }

    private void UpdateHeartbeat(bool isDead)
    {
        if (heartbeat == null) return;

        float volume = isDead ? 0f : heartbeatVolume * lowBlend;
        heartbeat.volume = volume;

        if (volume > 0.01f && !heartbeat.isPlaying) heartbeat.Play();
        else if (volume <= 0.01f && heartbeat.isPlaying) heartbeat.Stop();
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
    }
}