using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

[System.Serializable]
public class SceneRotationData
{
    public int totalEpisodes = 0;
    public int currentSceneIndex = 0;
}

public class SceneRotationManager : MonoBehaviour
{
    private static SceneRotationManager instance;
    
    [Header("Scene Rotation Settings")]
    [Tooltip("Number of episodes before switching to the next scene")]
    public int episodesPerScene = 200;
    
    [Tooltip("Scene names in order of rotation")]
    public string[] sceneNames = new string[]
    {
        "Scene 1",
        "Scene 2",
        "Scene 3",
        "Scene 4",
        "Scene 5",
        "Scene Last"
    };
    
    private SceneRotationData data;
    private string saveFilePath;
    private bool isLoadingScene = false;
    
    void Awake()
    {
        // Singleton pattern - only one instance should exist
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Set up save file path
        saveFilePath = Path.Combine(Application.persistentDataPath, "SceneRotationData.json");
        
        // Load saved data or create new
        LoadData();
        
        // Load the appropriate scene based on saved episode count
        LoadCorrectScene();
    }
    
    private void LoadData()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(saveFilePath);
                data = JsonUtility.FromJson<SceneRotationData>(json);
                Debug.Log($"Scene Rotation: Loaded saved data - Total Episodes: {data.totalEpisodes}, Current Scene Index: {data.currentSceneIndex}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load scene rotation data: {e.Message}. Starting fresh.");
                data = new SceneRotationData();
            }
        }
        else
        {
            Debug.Log("Scene Rotation: No saved data found. Starting fresh.");
            data = new SceneRotationData();
        }
    }
    
    private void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"Scene Rotation: Saved data - Total Episodes: {data.totalEpisodes}, Scene: {sceneNames[data.currentSceneIndex]}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save scene rotation data: {e.Message}");
        }
    }
    
    private void LoadCorrectScene()
    {
        // Calculate which scene should be active based on total episodes
        int targetSceneIndex = Mathf.Min(data.totalEpisodes / episodesPerScene, sceneNames.Length - 1);
        
        // Update current scene index
        data.currentSceneIndex = targetSceneIndex;
        
        string currentSceneName = SceneManager.GetActiveScene().name;
        string targetSceneName = sceneNames[targetSceneIndex];
        
        // Only load if we're not already in the correct scene
        if (currentSceneName != targetSceneName)
        {
            Debug.Log($"Scene Rotation: Loading scene '{targetSceneName}' for episode {data.totalEpisodes}");
            LoadScene(targetSceneIndex);
        }
        else
        {
            Debug.Log($"Scene Rotation: Already in correct scene '{targetSceneName}' for episode {data.totalEpisodes}");
        }
    }
    
    /// <summary>
    /// Call this method when an episode ends. This should be called by the main training agent.
    /// </summary>
    public static void OnEpisodeEnd()
    {
        if (instance == null)
        {
            Debug.LogWarning("SceneRotationManager: No instance found. Make sure SceneRotationManager is in the scene.");
            return;
        }
        
        // Don't increment during scene loading
        if (instance.isLoadingScene)
            return;
        
        instance.data.totalEpisodes++;
        
        // Check if we need to switch scenes
        int newSceneIndex = Mathf.Min(instance.data.totalEpisodes / instance.episodesPerScene, instance.sceneNames.Length - 1);
        
        // Save data after every episode
        instance.SaveData();
        
        // Switch scene if needed
        if (newSceneIndex != instance.data.currentSceneIndex)
        {
            Debug.Log($"Episode {instance.data.totalEpisodes} completed. Switching to scene: {instance.sceneNames[newSceneIndex]}");
            instance.data.currentSceneIndex = newSceneIndex;
            instance.SaveData();
            instance.LoadScene(newSceneIndex);
        }
    }
    
    private void LoadScene(int sceneIndex)
    {
        if (sceneIndex < 0 || sceneIndex >= sceneNames.Length)
        {
            Debug.LogError($"Scene index {sceneIndex} out of range!");
            return;
        }
        
        isLoadingScene = true;
        string sceneName = sceneNames[sceneIndex];
        
        try
        {
            SceneManager.LoadScene(sceneName);
            Debug.Log($"Scene rotation: Loaded {sceneName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load scene '{sceneName}': {e.Message}");
            Debug.LogError("Make sure all scenes are added to Build Settings (File -> Build Settings -> Scenes in Build)");
        }
        finally
        {
            isLoadingScene = false;
        }
    }
    
    /// <summary>
    /// Get current episode count
    /// </summary>
    public static int GetTotalEpisodes()
    {
        return instance != null ? instance.data.totalEpisodes : 0;
    }
    
    /// <summary>
    /// Get current scene index
    /// </summary>
    public static int GetCurrentSceneIndex()
    {
        return instance != null ? instance.data.currentSceneIndex : 0;
    }
    
    /// <summary>
    /// Reset episode counter (useful for starting fresh training)
    /// </summary>
    public static void ResetEpisodeCount()
    {
        if (instance != null)
        {
            instance.data.totalEpisodes = 0;
            instance.data.currentSceneIndex = 0;
            instance.SaveData();
            Debug.Log("Scene Rotation: Episode count reset to 0");
        }
    }
    
    // Display current status in Unity Inspector
    void OnGUI()
    {
        if (data != null)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 14;
            style.normal.textColor = Color.yellow;
            style.padding = new RectOffset(10, 10, 10, 10);
            
            string sceneName = sceneNames[data.currentSceneIndex];
            int episodesInCurrentScene = data.totalEpisodes % episodesPerScene;
            
            GUI.Label(new Rect(5, 70, 400, 100), 
                $"Scene Rotation Manager\n" +
                $"Total Episodes: {data.totalEpisodes}\n" +
                $"Current Scene: {sceneName}\n" +
                $"Episodes in Scene: {episodesInCurrentScene}/{episodesPerScene}", 
                style);
        }
    }
}
