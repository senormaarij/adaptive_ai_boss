using UnityEngine;

public class SimpleGridChaser : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 30;
    public int gridHeight = 30;
    public float cellSize = 1f;
    
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float decisionInterval = 0.2f;
    
    [Header("Spawn Settings")]
    public Vector2Int fixedSpawnGridPosition = new Vector2Int(5, 5); // Grid coordinates where chaser will spawn
    
    // Predefined reward grid - SAME EVERY TIME
    private float[,] rewardGrid;
    private Vector2Int currentCell;
    private Vector2Int targetCell;
    
    private float decisionTimer = 0f;
    private Rigidbody2D rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Initialize the SAME reward grid every time
        InitializeRewardGrid();
        
        // Spawn at fixed GRID position every time
        currentCell = fixedSpawnGridPosition;
        targetCell = currentCell;
        transform.position = GridToWorld(currentCell);
        rb.linearVelocity = Vector2.zero;
    }
    
    void InitializeRewardGrid()
    {
        rewardGrid = new float[gridWidth, gridHeight];
        
        // Fill with rewards that encourage moving straight UP
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Primary reward: Higher Y position = much higher reward
                // This strongly encourages upward movement
                float upwardBonus = y * 2.0f; // Linear increase going up
                
                // Small horizontal penalty to discourage sideways movement
                float horizontalPenalty = Mathf.Abs(x - gridWidth / 2) * 0.1f;
                
                // Combine rewards - upward movement dominates
                rewardGrid[x, y] = upwardBonus - horizontalPenalty;
            }
        }
        
        Debug.Log("Reward grid initialized - chaser will always want to move UP");
    }
    
    void FixedUpdate()
    {
        decisionTimer += Time.fixedDeltaTime;
        
        // Update current grid position
        currentCell = WorldToGrid(transform.position);
        
        // Make decision periodically
        if (decisionTimer >= decisionInterval)
        {
            decisionTimer = 0f;
            targetCell = ChooseBestNeighbor(currentCell);
        }
        
        // Move towards target cell
        MoveTowardsTarget();
    }
    
    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.x / cellSize) + gridWidth / 2, 0, gridWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(worldPos.y / cellSize) + gridHeight / 2, 0, gridHeight - 1);
        return new Vector2Int(x, y);
    }
    
    Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = (gridPos.x - gridWidth / 2) * cellSize + cellSize / 2f;
        float y = (gridPos.y - gridHeight / 2) * cellSize + cellSize / 2f;
        return new Vector3(x, y, 0);
    }
    
    Vector2Int ChooseBestNeighbor(Vector2Int cell)
    {
        // Check all 4 neighbors + stay
        Vector2Int[] neighbors = new Vector2Int[5];
        neighbors[0] = new Vector2Int(cell.x, Mathf.Min(cell.y + 1, gridHeight - 1)); // Up
        neighbors[1] = new Vector2Int(cell.x, Mathf.Max(cell.y - 1, 0)); // Down
        neighbors[2] = new Vector2Int(Mathf.Max(cell.x - 1, 0), cell.y); // Left
        neighbors[3] = new Vector2Int(Mathf.Min(cell.x + 1, gridWidth - 1), cell.y); // Right
        neighbors[4] = cell; // Stay
        
        // Find neighbor with highest reward
        Vector2Int bestCell = cell;
        float bestReward = rewardGrid[cell.x, cell.y];
        
        foreach (Vector2Int neighbor in neighbors)
        {
            float reward = rewardGrid[neighbor.x, neighbor.y];
            if (reward > bestReward)
            {
                bestReward = reward;
                bestCell = neighbor;
            }
        }
        
        return bestCell;
    }
    
    void MoveTowardsTarget()
    {
        Vector3 targetWorldPos = GridToWorld(targetCell);
        Vector2 direction = (targetWorldPos - transform.position).normalized;
        
        rb.linearVelocity = direction * moveSpeed;
    }
    
    // Visualize grid in editor
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || rewardGrid == null) return;
        
        // Draw current cell
        Gizmos.color = Color.blue;
        Vector3 currentWorldPos = GridToWorld(currentCell);
        Gizmos.DrawWireCube(currentWorldPos, Vector3.one * cellSize * 0.9f);
        
        // Draw target cell
        Gizmos.color = Color.green;
        Vector3 targetWorldPos = GridToWorld(targetCell);
        Gizmos.DrawWireCube(targetWorldPos, Vector3.one * cellSize * 0.8f);
    }
}