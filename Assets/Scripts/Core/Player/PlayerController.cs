using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Fatigue")]
    [SerializeField] private float movementEnergyDrainPerSecond = 0.35f;

    [Tooltip("Năng lượng tối đa dùng để tính hệ số tốc độ (thường 100, khớp HUD/slider).")]
    [SerializeField] private float energyReferenceMax = 100f;

    [Tooltip("Tốc độ di chuyển khi năng lượng = 0 (tỉ lệ so với moveSpeed).")]
    [SerializeField, Range(0.05f, 1f)] private float moveSpeedFactorAtZeroEnergy = 0.35f;

    private Vector2 lastMoveDir = Vector2.down;

    private bool isMoving;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
                animator = GetComponent<Animator>();
        }
    }

    void Start()
    {
        // Hướng mặc định khi bắt đầu game
        if (animator != null)
        {
            animator.SetFloat("Horizontal", lastMoveDir.x);
            animator.SetFloat("Vertical", lastMoveDir.y);
            animator.SetFloat("Speed", 0f);
            animator.SetBool("isMoving", false);
        }
    }

    void Update()
    {
        if (PauseMenuController.BlocksPlayerMovement)
        {
            movement = Vector2.zero;
            isMoving = false;
            SyncAnimatorStopped();
            return;
        }

        // Nhận input
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        movement = movement.normalized;

        isMoving = movement.sqrMagnitude > 0.01f;

        // Lưu hướng di chuyển cuối cùng
        if (isMoving)
        {
            lastMoveDir = movement;
        }

        // Cập nhật Animator
        if (animator != null)
        {
            Vector2 dir = isMoving ? movement : lastMoveDir;
            float spdMul = GetEnergyMoveSpeedMultiplier();

            animator.SetFloat("Horizontal", dir.x);
            animator.SetFloat("Vertical", dir.y);
            animator.SetFloat("Speed", movement.sqrMagnitude * spdMul);
            animator.SetBool("isMoving", isMoving);
        }
    }


    private void FixedUpdate()
    {
        if (PauseMenuController.BlocksPlayerMovement)
        {
            return;
        }

        float speedMul = GetEnergyMoveSpeedMultiplier();
        rb.MovePosition(rb.position + movement * (moveSpeed * speedMul) * Time.fixedDeltaTime);

        if (movement.sqrMagnitude <= 1e-6f)
        {
            return;
        }

        if (movementEnergyDrainPerSecond <= 0f || StatManager.Instance == null)
        {
            return;
        }

        StatManager.Instance.energy = Mathf.Max(
            0f,
            StatManager.Instance.energy - movementEnergyDrainPerSecond * Time.fixedDeltaTime);

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    float GetEnergyMoveSpeedMultiplier()
    {
        if (StatManager.Instance == null)
        {
            return 1f;
        }

        float maxRef = Mathf.Max(1f, energyReferenceMax);
        float t = Mathf.Clamp01(StatManager.Instance.energy / maxRef);
        return Mathf.Lerp(moveSpeedFactorAtZeroEnergy, 1f, t);
    }

    void SyncAnimatorStopped()
    {
        if (animator == null) return;

        animator.SetFloat("Horizontal", lastMoveDir.x);
        animator.SetFloat("Vertical", lastMoveDir.y);
        animator.SetFloat("Speed", 0f);
        animator.SetBool("isMoving", false);
    }
}