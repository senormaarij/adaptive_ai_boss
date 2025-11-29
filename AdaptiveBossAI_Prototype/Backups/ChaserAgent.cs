using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class ChaserAgent : Agent
{
    [Header("Movement Settings")]
    public float moveForce = 20f;
    public float maxSpeed = 5f;
    public float arenaSize = 10f;
    
    [Header("Episode Settings")]
    public float maxEpisodeTime = 30f;
    private float episodeTimer = 0f;
    private float previousDistance = 0f;
    
    [Header("References")]
    public Transform evaderTransform;
    public EvaderAgent evaderAgent;
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
    private const float stunDuration = 5.0f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPosition = transform.position;
        
        // Smooth physics setup
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 2f; // Natural slowdown
        rb.angularDamping = 1f;
        rb.gravityScale = 0f;
    }
    
    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        
        // Random spawn
        transform.position = startPosition;
        
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        // Initialize previousDistance to current distance
        if (evaderTransform != null)
        {
            previousDistance = Vector2.Distance(transform.position, evaderTransform.position);
        }
        else
        {
            previousDistance = 0f;
        }
        
        // Reset stun state
        isStunned = false;
        stunTimer = 0f;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        if (evaderTransform != null)
        {
            // Direction to evader (normalized) - THE MOST IMPORTANT INFO
            Vector2 toEvader = evaderTransform.position - transform.position;
            float distance = toEvader.magnitude;
            
            if (distance > 0.01f)
            {
                Vector2 dirNormalized = toEvader / distance;
                sensor.AddObservation(dirNormalized.x); // Direction X
                sensor.AddObservation(dirNormalized.y); // Direction Y
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            
            // Distance (normalized)
            sensor.AddObservation(distance / arenaSize);
            
            // My velocity (normalized)
            sensor.AddObservation(rb.linearVelocity.x / maxSpeed);
            sensor.AddObservation(rb.linearVelocity.y / maxSpeed);
            
            // Evader's velocity (to predict where they're going)
            Rigidbody2D evaderRb = evaderTransform.GetComponent<Rigidbody2D>();
            if (evaderRb != null)
            {
                float evaderSpeed = evaderAgent != null ? evaderAgent.moveSpeed : maxSpeed;
                sensor.AddObservation(evaderRb.linearVelocity.x / evaderSpeed);
                sensor.AddObservation(evaderRb.linearVelocity.y / evaderSpeed);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
        else
        {
            for (int i = 0; i < 7; i++)
                sensor.AddObservation(0f);
        }
        
        // Total: 7 simple observations
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeTimer += Time.fixedDeltaTime;
        
        // Time limit - chaser failed
        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-10.0f);
            if (uiManager != null)
                uiManager.OnEvaderWin();
            EndEpisode();
            if (evaderAgent != null)
                evaderAgent.EndEpisode();
            return;
        }
        
        // Update stun timer
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
            }
            return; // Skip movement while stunned
        }
        
        // Apply force for smooth movement
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        
        Vector2 force = new Vector2(moveX, moveY) * moveForce;
        rb.AddForce(force);
        
        // Clamp max speed
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
        
        // SIMPLE AGGRESSIVE REWARD: Just reward getting closer!
        if (evaderTransform != null)
        {
            float currentDistance = Vector2.Distance(transform.position, evaderTransform.position);
            
            // Continuous distance-based reward (always calculated)
            float distanceReward = (arenaSize - currentDistance) / arenaSize;
            AddReward(distanceReward * 0.1f);
            
            // Simple proximity bonus - reward just for being close
            if (currentDistance < 2f)
            {
                AddReward(0.5f); // Huge bonus for being very close
            }
            else if (currentDistance < 4f)
            {
                AddReward(0.1f);
            }
            
            // Penalty for being far
            if (currentDistance > 8f)
            {
                AddReward(-0.1f);
            }
        }
        
        // Small time penalty to create urgency
        AddReward(-0.001f);
        
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
        if (collision.gameObject.CompareTag("Evader"))
        {
            Debug.Log("CHASER WON!");
            
            // MASSIVE SUCCESS REWARD
            AddReward(20.0f);
            
            // Bonus for speed
            float timeBonus = (1f - episodeTimer / maxEpisodeTime) * 10.0f;
            AddReward(timeBonus);
            
            if (uiManager != null)
                uiManager.OnChaserWin();
            
            EndEpisode();
            if (evaderAgent != null)
                evaderAgent.EndEpisode();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Wall>(out Wall wall) || other.TryGetComponent<House>(out House house))
        {
            // Penalty for hitting wall
            AddReward(-2.0f);
            Debug.Log("Chaser hit wall - bouncing back");
            
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
        
        float horizontal = 0f;
        float vertical = 0f;
        
        if (Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        if (Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        
        continuousActions[0] = horizontal;
        continuousActions[1] = vertical;
    }
}