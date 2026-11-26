using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    private CharacterController controller;
    private PlayerInputActions inputActions;

    [Header("Movimento")]
    [Tooltip("Velocidade a andar (antes era 6).")]
    public float walkSpeed = 4.5f;
    public float gravity = -9.81f;
    private Vector2 moveInput;
    private Vector3 velocity;

    [Header("Sprint (Shift esquerdo)")]
    [Tooltip("Multiplicador da velocidade a correr (4.5 x 1.6 = 7.2).")]
    public float sprintMultiplier = 1.6f;
    public float maxStamina = 100f;
    [Tooltip("Stamina gasta por segundo a correr (25 = cerca de 4 s de sprint).")]
    public float staminaDrainPerSecond = 25f;
    [Tooltip("Stamina recuperada por segundo quando não corres.")]
    public float staminaRegenPerSecond = 15f;
    [Tooltip("Segundos sem correr antes de a stamina começar a recuperar.")]
    public float staminaRegenDelay = 1f;
    [Tooltip("Depois de esgotar a stamina, só voltas a correr quando ela chegar a este valor.")]
    public float minStaminaToResume = 20f;

    private float stamina;
    private bool isSprinting;
    private bool exhausted;
    private float regenTimer;
    private StaminaHUD staminaHUD;

    [Header("Camara / Olhar")]
    public Transform playerCamera;
    public float mouseSensitivity = 0.1f;
    private Vector2 lookInput;
    private float xRotation = 0f;

    private WeaponManager weaponManager;
    private Weapon ActiveWeapon => weaponManager != null ? weaponManager.ActiveWeapon : null;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        weaponManager = GetComponent<WeaponManager>();
        stamina = maxStamina;
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();

            inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

            inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        }

        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        // Se o jogador morrer a correr, a barra não fica presa no ecrã
        if (staminaHUD != null) staminaHUD.Show(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // A barra de sprint é só do jogador local
            staminaHUD = FindAnyObjectByType<StaminaHUD>();
            return;
        }

        // Este jogador pertence a outra pessoa: sem input, sem câmara, sem arma
        enabled = false;
        GetComponent<CharacterController>().enabled = false;
        // Every camera of other players (including a viewmodel camera, if you add one)
        foreach (var cam in GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        var listener = playerCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;

        var weapon = GetComponentInChildren<Weapon>();
        if (weapon != null) weapon.enabled = false;
    }

    private void Start()
    {
        //CURSOR NO MEIO E INVISIVEL
        Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    private void Update()
    {
        // Tell the equipped weapon whether we're moving (moving accuracy)
        Weapon activeWeapon = ActiveWeapon;
        if (activeWeapon != null) activeWeapon.IsMoving = moveInput.sqrMagnitude > 0.01f;

        HandleLook();
        HandleSprint();
        HandleMovement();
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Weapon recoil kicks the view (x = degrees up, y = degrees to the right)
        Weapon weapon = ActiveWeapon;
        Vector2 recoil = weapon != null ? weapon.ConsumeRecoil() : Vector2.zero;
        mouseX += recoil.y;
        mouseY += recoil.x;

        //MOVER CAMARA COM RATO
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleSprint()
    {
        var kb = Keyboard.current;
        bool wantsSprint = kb != null && kb.leftShiftKey.isPressed;
        bool movingForward = moveInput.y > 0.1f; // só corres para a frente

        // Esgotaste a stamina: só voltas a correr quando recuperares um mínimo
        if (exhausted && stamina >= minStaminaToResume) exhausted = false;

        isSprinting = wantsSprint && movingForward && !exhausted && stamina > 0f;

        if (isSprinting)
        {
            stamina = Mathf.Max(0f, stamina - staminaDrainPerSecond * Time.deltaTime);
            regenTimer = staminaRegenDelay;

            if (stamina <= 0f) exhausted = true;
        }
        else
        {
            if (regenTimer > 0f)
                regenTimer -= Time.deltaTime;
            else
                stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSecond * Time.deltaTime);
        }

        // A barra fica visível enquanto corres ou a recuperar, e só desaparece quando está cheia
        if (staminaHUD != null)
        {
            staminaHUD.SetValue(stamina / maxStamina);
            staminaHUD.Show(isSprinting || stamina < maxStamina);
        }
    }

    private void HandleMovement()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        Weapon weapon = ActiveWeapon;
        float weaponSpeed = weapon != null ? weapon.MovementSpeedMultiplier : 1f; // heavier weapons slow you down
        float currentSpeed = walkSpeed * (isSprinting ? sprintMultiplier : 1f);
        controller.Move(move * currentSpeed * weaponSpeed * Time.deltaTime);

        //GRAVIDADE
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}