using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player input, animation, and feedback effects for character movement.
/// Works in conjunction with AdvancedMoveController to provide a complete character control system.
/// </summary>
[RequireComponent(typeof(AdvancedMoveController))]
public class PlayerController : MonoBehaviour
{
    public ThirdPersonCamera CameraFollower {get; private set;}
    private Animator characterAnimator;
    private AdvancedMoveController moveController;
    private Rigidbody rb;
    private DashController dashController;
    
    // Movement state
    private Vector3 moveDirection;
    private Vector3 cameraAlignedForward;
    private Vector3 cameraAlignedRight;
    private Vector3 inputVector;

    private HealthController healthComponent;
    private PlayerInput playerInput;
    
    public bool JoinedThroughGameManager { get; set; } = false;
    public static List<PlayerController> players = new List<PlayerController>();

    // ADDED - GLIDING MECHANIC
    private bool isFloating = false;
    private float floatGravity = 0.5f; // descent speed
    private float floatForwardSpeed = 2.0f; // forward momentum
    private float normalGravity = 9.81f; // Default gravity 

    [Header("Added - Gliding Mechanic")]
    public GameObject gliderPrefab;
    private GameObject gliderInstance;
    [Header("Glider Audio")]
    [SerializeField] private AudioClip glidingSound;

    private void OnEnable()
    {
        if(moveController != null)
            moveController.enabled = true;
    }

    private void OnDisable()
    {
        if (moveController != null)
            {
                inputVector = Vector3.zero;
                moveDirection = Vector3.zero;
                rb.velocity = Vector3.zero;
                moveController.ApplyMovement(Vector3.zero);
                moveController.UpdateMovement();
                moveController.enabled=false;
                UpdateVisualFeedback();
            }
    }

    /// <summary>
    /// Initialize components and verify required setup
    /// </summary>
    void Awake()
    {
        players.Add(this);
        // Ensure correct tag for player identification
        if(!gameObject.CompareTag("Player"))
            tag = "Player";

        TryGetComponent(out playerInput);
        TryGetComponent(out dashController);

        // Cache component references
        moveController = GetComponent<AdvancedMoveController>();
        rb = GetComponent<Rigidbody>();
        CameraFollower = GetComponentInChildren<ThirdPersonCamera>();
        characterAnimator = GetComponentInChildren<Animator>();
        healthComponent = GetComponent<HealthController>();

        if (CameraFollower)
        {
            if (playerInput.camera == null) {
                //Debug.Log(actions["Jump"].GetBindingDisplayString());
                playerInput.camera = CameraFollower.GetComponent<Camera>();
            }
            CameraFollower.transform.SetParent(transform.parent);
            DontDestroyOnLoad(CameraFollower.gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {
        if (!JoinedThroughGameManager)
        {
            Destroy(gameObject);
            return;
        }
        CheckpointManager.TeleportPlayerToCheckpoint(gameObject);
    }

    /// <summary>
    /// Clean up camera follower on destruction
    /// </summary>
    void OnDestroy()
    {
        if (players.Contains(this))
            players.Remove(this);
        if (playerInput)
            Destroy(playerInput);
        if (CameraFollower)
            Destroy(CameraFollower.gameObject);
    }

    void OnMove(InputValue inputVal)
    {
        if (GameManager.Instance.IsShowingPauseMenu)
            inputVector = Vector3.zero;
        else
            inputVector = inputVal.Get<Vector2>();
    }
    /// <summary>
    /// Handle jump input from the input system, now with gliding mechanic if pressed space while in the air.
    /// </summary>
    void OnJump()
    {
        if (!GameManager.Instance.IsShowingPauseMenu)
        {
            if (moveController.isGrounded)
            {
                moveController.RequestJump();
                isFloating = false; // Reset floating state when grounded
            }
            else if (isFloating)
            {
                EndFloating(); // Cancel floating if already floating
                //StopGlidingSound();
            }
            else
            {
                StartFloating();
                glidingSound.PlaySound(transform.position);
            }
        }
    }

    /// <summary>
    /// Float forward at set gravity. Follow player's rotation and adjust prefab instantiation rotation, offset & scale.
    /// </summary>
    void StartFloating() // ADDED - GLIDING MECHANIC
    {
        if (gliderInstance) return;

        // start gliding sound
        

        // handle floating state and parameters
        isFloating = true;
        rb.velocity = new Vector3(moveDirection.x * floatForwardSpeed, -floatGravity, moveDirection.z * floatForwardSpeed); // player gravity fall
        characterAnimator.SetBool("isFloating", true);

        float playerYRotation = transform.eulerAngles.y; // follow player's rotation
    
        // Adjusting the default rotation of the prefab
        Quaternion baseGliderRotation = Quaternion.Euler(90, 0, 0); // initial rotation of glider
        Quaternion gliderRotation = Quaternion.Euler(0, playerYRotation, 90) * baseGliderRotation; // rotation on instances

        Vector3 gliderOffset = transform.TransformPoint(new Vector3(0f, 1f, -3.5f)); // position glider above player
        gliderInstance = Instantiate(gliderPrefab, gliderOffset, gliderRotation, transform); // Instantiate prefab with all adjustments.

        gliderInstance.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // scale down
    }

    /// <summary>
    /// Press space to end floating state and apply normal gravity
    /// </summary>
    void EndFloating() // ADDED - GLIDING MECHANIC
    {
        isFloating = false;

        // stop glidign sound
        

        // handle end float state
        rb.velocity = new Vector3(rb.velocity.x, -normalGravity, rb.velocity.z); // Apply normal gravity
        characterAnimator.SetBool("isFloating", false);

        if (gliderInstance)
        {
            Destroy(gliderInstance);
            gliderInstance = null; // reset instance for next use
        }
    }
    void OnPause()
    {
        GameManager.Instance.TogglePauseMenu();
    }

    /// <summary>
    /// Handle dash input from the input system
    /// </summary>
    void OnDash()
    {
        if (!GameManager.Instance.IsShowingPauseMenu && dashController)
            dashController.TryStartDash(moveDirection);
    }

    void OnCameraOrbit(InputValue inputVal)
    {
        CameraFollower.OrbitInput = inputVal.Get<float>();
    }

    /// <summary>
    /// Calculate movement direction based on camera orientation
    /// </summary>
    void Update()
    {
        // Convert input to camera-relative movement direction
        Quaternion cameraRotation = Quaternion.Euler(0, CameraFollower.transform.eulerAngles.y, 0);
        cameraAlignedForward = cameraRotation * Vector3.forward;
        cameraAlignedRight = cameraRotation * Vector3.right;
        
        moveDirection = ((cameraAlignedForward * inputVector.y) + (cameraAlignedRight * inputVector.x)).normalized;
    }

    /// <summary>
    /// Handle physics-based movement and animation updates
    /// </summary>
    void FixedUpdate()
    {
        if (moveController.enabled)
        {
            moveController.ApplyMovement(moveDirection);
            moveController.UpdateMovement();
        }

        if (isFloating)
        {
            rb.velocity = new Vector3(rb.velocity.x, -floatGravity, rb.velocity.z); // slow descent while floating
        }

        // End floating when player touches the ground
        if (moveController.isGrounded && isFloating)
        {
            EndFloating();
        }

        UpdateVisualFeedback();

        if (transform.position.y < -1000f)
        {
            CheckpointManager.TeleportPlayerToCheckpoint(gameObject);
            if (CameraFollower)
                CameraFollower.transform.position = gameObject.transform.position;
        }
    }

    /// <summary>
    /// Update animator parameters and handle squash/stretch effects
    /// </summary>
    private void UpdateVisualFeedback()
    {
        if (!characterAnimator) return;

        // Update animator parameters
        characterAnimator.SetFloat(MovementController.AnimationID_DistanceToTarget, moveController.distanceToDestination);
        characterAnimator.SetBool(MovementController.AnimationID_IsGrounded, moveController.isGrounded);
        characterAnimator.SetFloat(MovementController.AnimationID_YVelocity, rb.velocity.y);
    }

} 