using UnityEngine;

public class MovingNPC : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;

    [Header("Sprites - Left Direction")]
    public Sprite leftSprite1;
    public Sprite leftSprite2;

    [Header("Sprites - Right Direction")]
    public Sprite rightSprite1;
    public Sprite rightSprite2;

    [Header("Sprites - Up Direction")]
    public Sprite upSprite1;
    public Sprite upSprite2;

    [Header("Sprites - Down Direction")]
    public Sprite downSprite1;
    public Sprite downSprite2;

    [Header("Animation Settings")]
    public float spriteChangeInterval = 0.4f; // How often to swap sprites

    private SpriteRenderer spriteRenderer;
    private Vector3 spawnPosition;
    private bool useFirstSprite = true;
    private float spriteTimer = 0f;
    private bool initialized = false;

    // Movement state machine
    private enum MovePhase { Left5, Down5, Right10, Up5, Left10 }
    private MovePhase currentPhase = MovePhase.Left5;
    private float distanceTraveled = 0f;
    private Vector2 moveDirection = Vector2.left;

    void Awake()
    {
        spawnPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError("MovingNPC requires a SpriteRenderer component!");
        }
        initialized = true;
    }

    void OnEnable()
    {
        if (initialized)
        {
            ResetToSpawn();
        }
    }

    public void ResetToSpawn()
    {
        transform.position = spawnPosition;
        currentPhase = MovePhase.Left5;
        distanceTraveled = 0f;
        moveDirection = Vector2.left;
        useFirstSprite = true;
        spriteTimer = 0f;
    }

    void Update()
    {
        // Move the NPC
        float moveAmount = moveSpeed * Time.deltaTime;
        transform.position += (Vector3)moveDirection * moveAmount;
        distanceTraveled += moveAmount;

        // Check if we've completed current phase
        float targetDistance = GetTargetDistance();
        if (distanceTraveled >= targetDistance)
        {
            // Move to next phase
            AdvanceToNextPhase();
        }

        // Handle sprite animation
        spriteTimer += Time.deltaTime;
        if (spriteTimer >= spriteChangeInterval)
        {
            spriteTimer = 0f;
            useFirstSprite = !useFirstSprite;
            UpdateSprite();
        }
    }

    float GetTargetDistance()
    {
        switch (currentPhase)
        {
            case MovePhase.Left5: return 5.5f;
            case MovePhase.Down5: return 6.5f;
            case MovePhase.Right10: return 11.5f;
            case MovePhase.Up5: return 6.5f;
            case MovePhase.Left10: return 11.5f;
            default: return 6f;
        }
    }

    void AdvanceToNextPhase()
    {
        distanceTraveled = 0f;

        switch (currentPhase)
        {
            case MovePhase.Left5:
                currentPhase = MovePhase.Down5;
                moveDirection = Vector2.down;
                break;
            case MovePhase.Down5:
                currentPhase = MovePhase.Right10;
                moveDirection = Vector2.right;
                break;
            case MovePhase.Right10:
                currentPhase = MovePhase.Up5;
                moveDirection = Vector2.up;
                break;
            case MovePhase.Up5:
                currentPhase = MovePhase.Left10;
                moveDirection = Vector2.left;
                break;
            case MovePhase.Left10:
                // Loop back to Down5 (not Left5, since we're already at the left side)
                currentPhase = MovePhase.Down5;
                moveDirection = Vector2.down;
                break;
        }
        
        UpdateSprite();
    }

    void UpdateSprite()
    {
        if (spriteRenderer == null) return;

        if (moveDirection == Vector2.left)
        {
            spriteRenderer.sprite = useFirstSprite ? leftSprite1 : leftSprite2;
        }
        else if (moveDirection == Vector2.right)
        {
            spriteRenderer.sprite = useFirstSprite ? rightSprite1 : rightSprite2;
        }
        else if (moveDirection == Vector2.up)
        {
            spriteRenderer.sprite = useFirstSprite ? upSprite1 : upSprite2;
        }
        else if (moveDirection == Vector2.down)
        {
            spriteRenderer.sprite = useFirstSprite ? downSprite1 : downSprite2;
        }
    }
}
