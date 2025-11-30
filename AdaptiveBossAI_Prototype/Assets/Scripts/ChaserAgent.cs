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
    private const float stunDuration = 2.0f;
    
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
        // Initialize components if not already done
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        // Capture initial position if not set
        if (startPosition == Vector3.zero)
        {
            startPosition = transform.position;
        }
        
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
            // Check if we have a target
            if (evaderTransform != null)
            {
                // 1. OBSERVE: Where is the Evader? (This was missing!)
                // We give the direction vector pointing to the evader
                Vector2 directionToEvader = (evaderTransform.position - transform.position).normalized;
                sensor.AddObservation(directionToEvader.x);
                sensor.AddObservation(directionToEvader.y);

                // 2. OBSERVE: Distance to Evader (Helps it know when to lunge)
                float distance = Vector2.Distance(transform.position, evaderTransform.position);
                sensor.AddObservation(distance / arenaSize); // Normalize by arena size

                // 3. OBSERVE: My Velocity (Am I moving?)
                sensor.AddObservation(rb.linearVelocity.x / maxSpeed);
                sensor.AddObservation(rb.linearVelocity.y / maxSpeed);
                
                // 4. OBSERVE: Evader's Velocity (Where are they going?)
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
                // If target is missing, fill with zeros to prevent errors
                sensor.AddObservation(Vector2.zero); // Direction
                sensor.AddObservation(0f);           // Distance
                sensor.AddObservation(Vector2.zero); // My Vel
                sensor.AddObservation(Vector2.zero); // Target Vel
            }
            
            // TOTAL OBSERVATIONS: 2 (Dir) + 1 (Dist) + 2 (MyVel) + 2 (TgtVel) = 7
        }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeTimer += Time.fixedDeltaTime;
        
        //Time limit - chaser failed
        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-10.0f);
            // Note: EvaderAgent handles the UI update
            SceneRotationManager.OnEpisodeEnd(); // Track episode completion
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
        // 1. Logic for WINNING (Catching the Evader)
        if (collision.gameObject.CompareTag("Evader"))
        {
            Debug.Log("CHASER WON!");
            AddReward(20.0f);
            
            float timeBonus = (1f - episodeTimer / maxEpisodeTime) * 10.0f;
            AddReward(timeBonus);
            
            SceneRotationManager.OnChaserWin();
            if (uiManager != null) uiManager.OnChaserWin();
            
            SceneRotationManager.OnEpisodeEnd();
            EndEpisode();
            if (evaderAgent != null) evaderAgent.EndEpisode();
            return;
        }

        // 2. Logic for HITTING WALLS (Tag is "Collider" based on your screenshot)
        if (collision.gameObject.CompareTag("Collider") || collision.gameObject.CompareTag("Wall")) 
        {
            AddReward(-2.0f); // Penalty
            
            // Stun logic
            isStunned = true;
            stunTimer = 1.0f; // 1 second stun
            
            // Physical Bounce
            if (rb != null)
            {
                // Stop momentum
                rb.linearVelocity = Vector2.zero; 
                // Push back slightly based on collision normal
                rb.AddForce(collision.contacts[0].normal * 10f, ForceMode2D.Impulse);
            }
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Punish for hugging the wall
        if (collision.gameObject.CompareTag("Collider") || collision.gameObject.CompareTag("Wall")) 
        {
            AddReward(-1.0f); 
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
