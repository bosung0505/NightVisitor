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

    void Start()
    {
        // Disable all foxes except the first one
        for (int i = 0; i < foxes.Length; i++)
        {
            if (foxes[i] != null)
            {
                foxes[i].SetActive(i == 0);
            }
        }
    }

    void Update()
    {
        if (currentFoxIndex >= foxes.Length) return;

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

        currentFoxIndex++;
        if (currentFoxIndex < foxes.Length && foxes[currentFoxIndex] != null)
        {
            foxes[currentFoxIndex].SetActive(true);
        }
        
        isWaitingForNext = false;
    }
}
