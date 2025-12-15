using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class EvaderAgent : Agent
{
    [Header("Movement Settings")]
    public float moveSpeed = 4.5f; // Slightly faster than chaser for balance
    public float acceleration = 15f;
    public float arenaSize = 10f;
    
    [Header("Boundary Settings")]
    public float wallCollisionPenalty = -1.0f;
    
    [Header("Episode Settings")]
    public float maxEpisodeTime = 30f;
    private float episodeTimer = 0f;
    private float lastDistanceToChaser = 0f;
    
    [Header("References")]
    public Transform chaserTransform;
    public GameUIManager uiManager;
    
    [Header("Directional Sprites")]
    public Sprite spriteUp;        // Art_80
    public Sprite spriteDown;      // Art_84
    public Sprite spriteLeft;      // Art_86
    public Sprite spriteRight;     // Art_82
    public Sprite spriteUpLeft;
    public Sprite spriteUpRight;
    public Sprite spriteDownLeft;
    public Sprite spriteDownRight;
    
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Vector3 startPosition;
    
    // Stun mechanic for wall collisions
    private bool isStunned = false;
    private float stunTimer = 0f;
    private const float stunDuration = 2.0f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPosition = transform.position;
        SetupRigidbodyConstraints();
    }
    
    private void SetupRigidbodyConstraints()
    {
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 5f; // Increased drag for smoother movement
        rb.gravityScale = 0f;
    }
    
    public float GetEpisodeTimer()
    {
        return episodeTimer;
    }
    
    public override void OnEpisodeBegin()
    {
        // Initialize components if not already done
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        episodeTimer = 0f;
        lastDistanceToChaser = 0f;
        
        // Note: ChaserAgent handles spawn positioning for both agents
        // Just reset velocity and stun state here
        
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        // Reset stun state
        isStunned = false;
        stunTimer = 0f;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Safety check - ensure components are initialized
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) return; // Still null, skip this observation cycle
        }
        
        // Evader's normalized position (2)
        sensor.AddObservation(transform.localPosition.x / arenaSize);
        sensor.AddObservation(transform.localPosition.y / arenaSize);
        
        // Evader's normalized velocity (2)
        sensor.AddObservation(rb.linearVelocity.x / moveSpeed);
        sensor.AddObservation(rb.linearVelocity.y / moveSpeed);
        
        if (chaserTransform != null)
        {
            Vector2 dirToChaser = chaserTransform.position - transform.position;
            float distance = dirToChaser.magnitude;
            
            // Normalized direction to chaser (2)
            sensor.AddObservation(dirToChaser.x / arenaSize);
            sensor.AddObservation(dirToChaser.y / arenaSize);
            
            // Normalized distance (1)
            sensor.AddObservation(distance / arenaSize);
            
            // Unit direction vector away from chaser (2) - helps with evasion
            Vector2 normalizedDir = distance > 0.01f ? dirToChaser / distance : Vector2.zero;
            sensor.AddObservation(normalizedDir.x);
            sensor.AddObservation(normalizedDir.y);
            
            // Chaser's velocity (2)
            Rigidbody2D chaserRb = chaserTransform.GetComponent<Rigidbody2D>();
            if (chaserRb != null)
            {
                sensor.AddObservation(chaserRb.linearVelocity.x / moveSpeed);
                sensor.AddObservation(chaserRb.linearVelocity.y / moveSpeed);
                
                // Relative velocity (2)
                Vector2 relativeVel = rb.linearVelocity - chaserRb.linearVelocity;
                sensor.AddObservation(relativeVel.x / moveSpeed);
                sensor.AddObservation(relativeVel.y / moveSpeed);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    sensor.AddObservation(0f);
            }
        }
        else
        {
            for (int i = 0; i < 9; i++)
                sensor.AddObservation(0f);
        }
        
        // Time remaining normalized (1)
        sensor.AddObservation(1f - (episodeTimer / maxEpisodeTime));
        
        // Total: 16 observations
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeTimer += Time.fixedDeltaTime;
        
        // 1. SURVIVAL REWARD (The most important one)
        // Give a small positive reward every frame just for being alive.
        // This encourages prolonging the game as long as possible.
        AddReward(0.01f); 

        // Time limit reached - BIG WIN
        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(10.0f); // Bonus for making it to the end
            SceneRotationManager.OnEvaderWin();
            if (uiManager != null) uiManager.OnEvaderWin();
            EndEpisode();
            return;
        }
        
        // Stun logic (Keep existing)
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f) isStunned = false;
            return;
        }
        
        // Movement Logic (Keep existing)
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector2 inputDir = new Vector2(moveX, moveY);
        Vector2 targetVelocity = inputDir.normalized * moveSpeed * inputDir.magnitude;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        
        // 2. INTELLIGENT EVASION REWARDS
        if (chaserTransform != null)
        {
            float currentDistance = Vector2.Distance(transform.position, chaserTransform.position);
            
            // REWARD 1: Moving Away (The "Run!" instinct)
            // Instead of punishing proximity, we REWARD increasing the distance.
            if (lastDistanceToChaser > 0)
            {
                float distanceChange = currentDistance - lastDistanceToChaser;
                // If we moved away, give a nice reward.
                // If we got closer, give a SMALL penalty (don't discourage movement).
                if (distanceChange > 0)
                {
                    AddReward(distanceChange * 1.0f); // Strong reward for creating a gap
                }
            }
            lastDistanceToChaser = currentDistance;
            
            // REWARD 2: "Juking" Bonus
            // If the Chaser is very close, but the Evader is moving FAST, reward it.
            // This teaches it to sprint when in danger, rather than freeze.
            if (currentDistance < 3f && rb.linearVelocity.magnitude > 2f)
            {
                AddReward(0.005f); 
            }
        }
        
        UpdateSpriteDirection();
    }
    
    private void UpdateSpriteDirection()
    {
        if (spriteRenderer == null || rb.linearVelocity.magnitude < 0.1f)
            return;
        
        Vector2 velocity = rb.linearVelocity;
        float absX = Mathf.Abs(velocity.x);
        float absY = Mathf.Abs(velocity.y);
        
        // Check for diagonal movement (both X and Y are significant)
        // Use a threshold to determine if it's diagonal vs cardinal
        float diagonalThreshold = 0.5f; // If both components are > 50% of the larger one
        
        if (absX > 0.01f && absY > 0.01f)
        {
            float ratio = Mathf.Min(absX, absY) / Mathf.Max(absX, absY);
            
            if (ratio > diagonalThreshold) // Moving diagonally
            {
                if (velocity.x > 0 && velocity.y > 0 && spriteUpRight != null)
                    spriteRenderer.sprite = spriteUpRight;    // Up-Right
                else if (velocity.x < 0 && velocity.y > 0 && spriteUpLeft != null)
                    spriteRenderer.sprite = spriteUpLeft;     // Up-Left
                else if (velocity.x > 0 && velocity.y < 0 && spriteDownRight != null)
                    spriteRenderer.sprite = spriteDownRight;  // Down-Right
                else if (velocity.x < 0 && velocity.y < 0 && spriteDownLeft != null)
                    spriteRenderer.sprite = spriteDownLeft;   // Down-Left
                return;
            }
        }
        
        // Cardinal directions (one component dominates)
        if (absX > absY)
        {
            // Moving more horizontally
            if (velocity.x > 0 && spriteRight != null)
                spriteRenderer.sprite = spriteRight; // Right
            else if (velocity.x < 0 && spriteLeft != null)
                spriteRenderer.sprite = spriteLeft;  // Left
        }
        else
        {
            // Moving more vertically
            if (velocity.y > 0 && spriteUp != null)
                spriteRenderer.sprite = spriteUp;    // Up
            else if (velocity.y < 0 && spriteDown != null)
                spriteRenderer.sprite = spriteDown;  // Down
        }
    }
    
    // DELETE your old OnCollisionEnter2D and OnTriggerEnter2D
    // PASTE these two methods in their place:

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. Logic for LOSING (Caught by Chaser)
        if (collision.gameObject.CompareTag("Chaser"))
        {
            Debug.Log("Evader caught by chaser!");
            AddReward(-15.0f);
            EndEpisode();
            return;
        }

        // 2. Logic for HITTING WALLS (Tag is "Collider" based on your screenshot)
        if (collision.gameObject.CompareTag("Collider")) 
        {
            AddReward(-10.0f); // Heavy penalty to prevent oscillation loops
            
            isStunned = true;
            stunTimer = 2.0f; // 2 second stun
            
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; 
                rb.AddForce(collision.contacts[0].normal * 3f, ForceMode2D.Impulse);
            }
        }
    }
    
    void OnCollisionStay2D(Collision2D collision)
    {
        // Punish for hugging the wall
        if (collision.gameObject.CompareTag("Collider")) 
        {
            AddReward(-2.0f); // Doubled penalty for wall hugging 
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
}
