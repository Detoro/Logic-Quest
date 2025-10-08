using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    // A list of level names that correspond to your Unity scene names.
    public List<string> levelNames;

    // The currently active level.
    private int currentLevelIndex = -1;

    public void LoadLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < levelNames.Count)
        {
            string levelToLoad = levelNames[levelIndex];
            Debug.Log($"Loading level: {levelToLoad}");

            // Load the scene asynchronously to avoid freezing the game.
            SceneManager.LoadScene(levelToLoad);
            currentLevelIndex = levelIndex;
        }
        else
        {
            Debug.LogError("Invalid level index.");
        }
    }

    public void LoadNextLevel()
    {
        // Calculate the next level index, and loop back to the first level if at the end.
        int nextLevelIndex = (currentLevelIndex + 1) % levelNames.Count;
        LoadLevel(nextLevelIndex);
    }
}