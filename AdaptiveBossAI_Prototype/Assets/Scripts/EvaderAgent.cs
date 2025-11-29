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
    public float wallCollisionPenalty = -10.0f; // Increased from -5.0f
    
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
    private const float stunDuration = 0.2f; // Reduced from 5.0f - agent needs quick feedback!
    
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
        episodeTimer = 0f;
        lastDistanceToChaser = 0f;
        
        transform.position = startPosition;
        
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        

        // Reset stun state
        isStunned = false;
        stunTimer = 0f;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
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
        
        // Survived the time limit - BIG WIN
        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(15.0f); // MASSIVE reward for surviving
            if (uiManager != null)
                uiManager.OnEvaderWin();
            EndEpisode();
            return;
        }
        
        // Update stun timer - EXIT BEFORE ANY REWARDS
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
            }
            return; // EXIT - No movement, no rewards during stun
        }
        
        // Smooth movement with acceleration
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        
        Vector2 inputDir = new Vector2(moveX, moveY);
        Vector2 targetVelocity = inputDir.normalized * moveSpeed * inputDir.magnitude;
        
        // Smooth acceleration using lerp
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        
        if (chaserTransform != null)
        {
            float currentDistance = Vector2.Distance(transform.position, chaserTransform.position);
            
            // AGGRESSIVE DISTANCE-BASED REWARDS
            // Reward for increasing distance from chaser
            if (lastDistanceToChaser > 0)
            {
                float distanceChange = currentDistance - lastDistanceToChaser;
                if (distanceChange > 0) // Getting farther - GOOD!
                {
                    AddReward(distanceChange * 0.4f); // Strong reward for escaping
                }
                else // Getting closer - BAD!
                {
                    AddReward(distanceChange * 0.5f); // Strong penalty for letting chaser close in
                }
            }
            lastDistanceToChaser = currentDistance;
            
            // Distance-based rewards (exponentially stronger when in danger)
            if (currentDistance < 2f)
            {
                AddReward(-0.15f); // DANGER ZONE - massive penalty
            }
            else if (currentDistance < 3.5f)
            {
                AddReward(-0.05f); // Close - big penalty
            }
            else if (currentDistance < 5f)
            {
                AddReward(-0.01f); // Medium range - small penalty
            }
            else if (currentDistance > 6f)
            {
                AddReward(0.02f); // Safe distance - reward
            }
            else if (currentDistance > 8f)
            {
                AddReward(0.04f); // Very safe - bigger reward
            }
            
            // Velocity alignment reward - reward for moving away from chaser
            Vector2 dirAwayFromChaser = (transform.position - chaserTransform.position).normalized;
            float velocityAlignment = Vector2.Dot(rb.linearVelocity.normalized, dirAwayFromChaser);
            AddReward(velocityAlignment * 0.015f); // Reward for moving away
            
            // Bonus for maintaining high speed when being chased
            if (currentDistance < 5f)
            {
                float speedRatio = rb.linearVelocity.magnitude / moveSpeed;
                AddReward(speedRatio * 0.005f); // Reward for moving fast when in danger
            }
        }
        
        // Survival time reward (incremental reward for each step survived)
        AddReward(0.001f); // Small reward just for surviving
        
        // Small action smoothness reward
        float actionMagnitude = inputDir.magnitude;
        if (actionMagnitude > 0.1f)
        {
            AddReward(0.0001f); // Tiny reward for taking action
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
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Chaser"))
        {
            Debug.Log("Evader caught by chaser!");
            
            // MASSIVE penalty for being caught
            AddReward(-15.0f);
            
            if (uiManager != null)
                uiManager.OnChaserWin();
            
            EndEpisode();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Wall>(out Wall wall) || other.TryGetComponent<House>(out House house))
        {
            // Penalty for hitting wall - increased to make it hurt more
            AddReward(wallCollisionPenalty);
            Debug.Log("Evader hit wall - bouncing back");
            
            // Bounce back - reverse velocity
            rb.linearVelocity = -rb.linearVelocity * 0.5f; // Bounce with 50% dampening
            
            // Apply stun
            isStunned = true;
            stunTimer = stunDuration;
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
}