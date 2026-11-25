using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    private CharacterController controller;
    private PlayerInputActions inputActions;

    [Header("Movimento")]
    public float speed = 6f;
    public float gravity = -9.81f;
    private Vector2 moveInput;
    private Vector3 velocity;

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
        //INPUT SYSTEM

    }

    private void OnEnable()
    {
        // 1. Unity checks: "Does inputActions exist?"
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
    }
    public override void OnNetworkSpawn()
    {
        if (IsOwner) return;

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

    private void HandleMovement()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        Weapon weapon = ActiveWeapon;
        float weaponSpeed = weapon != null ? weapon.MovementSpeedMultiplier : 1f; // heavier weapons slow you down
        controller.Move(move * speed * weaponSpeed * Time.deltaTime);

        //GRAVIDADE
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}