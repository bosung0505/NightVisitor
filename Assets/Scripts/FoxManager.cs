using UnityEngine;
using System.Collections;

public class FoxManager : MonoBehaviour
{
    [Tooltip("List of Fox GameObjects in order of appearance.")]
    public GameObject[] foxes;
    
    [Tooltip("Delay in seconds before the next fox spawns.")]
    public float spawnDelay = 2.0f;

    private int currentFoxIndex = 0;
    private bool isWaitingForNext = false;
    
    // We will store the original prefab/instantiated object information to clone them later if they are destroyed.
    private GameObject[] foxPrefabs;

    void Start()
    {
        // Save references to the original GameObjects to act as our pseudo-prefabs.
        foxPrefabs = new GameObject[foxes.Length];
        for (int i = 0; i < foxes.Length; i++)
        {
            if (foxes[i] != null)
            {
                // Instantiate a hidden copy to serve as a clean prefab for respawning
                // We save their EXACT start position and rotation here
                foxPrefabs[i] = Instantiate(foxes[i], foxes[i].transform.position, foxes[i].transform.rotation);
                foxPrefabs[i].SetActive(false);
                foxPrefabs[i].name = foxes[i].name + "_PrefabRef";
                
                // Keep the first fox active, disable the rest
                foxes[i].SetActive(i == 0);
            }
        }
    }

    void Update()
    {
        if (foxes.Length == 0) return;

        GameObject currentFox = foxes[currentFoxIndex];

        // Check if the current fox was destroyed (escaped)
        if (currentFox == null)
        {
            if (!isWaitingForNext)
            {
                StartCoroutine(SpawnNextFoxRoutine());
            }
            return;
        }

        // Check if the current fox was shot (Dead state)
        RandomFoxAnimation anim = currentFox.GetComponent<RandomFoxAnimation>();
        if (anim != null && anim.currentState == RandomFoxAnimation.FoxState.Dead)
        {
            if (!isWaitingForNext)
            {
                StartCoroutine(SpawnNextFoxRoutine());
            }
        }
    }

    private IEnumerator SpawnNextFoxRoutine()
    {
        isWaitingForNext = true;
        yield return new WaitForSeconds(spawnDelay);

        // Before moving to the next index, if the current fox is dead, we leave it alone (as a corpse).
        // Since we want to re-use this slot in the array eventually, we just let it be.
        // It will be overwritten by a new Instantiate when we loop back.
        
        currentFoxIndex++;

        // --- Loop back to 0 if we passed the end ---
        if (currentFoxIndex >= foxes.Length)
        {
            currentFoxIndex = 0;
            Debug.Log("FoxManager: Last fox reached, looping back to the first fox.");
        }

        // --- Always revive by Instantiating a BRAND NEW copy from the prefab ---
        // This ensures it spawns at the original starting position, and the old dead body remains where it died.
        if (foxPrefabs[currentFoxIndex] != null)
        {
            // We overwrite the array index with a newly spawned fox at the original start coordinates.
            foxes[currentFoxIndex] = Instantiate(foxPrefabs[currentFoxIndex], foxPrefabs[currentFoxIndex].transform.position, foxPrefabs[currentFoxIndex].transform.rotation);
            foxes[currentFoxIndex].name = foxPrefabs[currentFoxIndex].name.Replace("_PrefabRef", "");
            
            // Activate the new fox
            foxes[currentFoxIndex].SetActive(true);
        }

        isWaitingForNext = false;
    }
}
