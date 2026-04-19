using System;
using UnityEngine;
using System.Collections;

public class MapManager : MonoBehaviour
{
    [Header("Available Map Prefabs (each must have a MapSegmentHandler)")]
    public GameObject[] mapPrefabs;


    public GameObject currentMapInstance;
    public MapSegmentHandler currentSegmentHandler;

    public bool IsMapReady { get; private set; } = false;

    private void Start()
    {
        if (mapPrefabs.Length > 0)
        {
            LoadMap(0); // load the first map by default
        }
    }

    public IEnumerator LoadMap(int index)
    {
        IsMapReady = false;
        if (index < 0 || index >= mapPrefabs.Length)
        {
            Debug.LogError($"Invalid map index {index}");
            yield break;
        }

        // Remove old map
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
            currentMapInstance = null;
            currentSegmentHandler = null;
            yield return null;
        }

        // Instantiate new one
        currentMapInstance = Instantiate(mapPrefabs[index], Vector3.zero, Quaternion.identity);
        yield return null;
        yield return null; // Extra yield to allow Awake() to complete
        
        currentSegmentHandler = currentMapInstance.GetComponentInChildren<MapSegmentHandler>();

        if (currentSegmentHandler == null)
        {
            Debug.LogError($"Map {mapPrefabs[index].name} has no MapSegmentHandler component!");
            yield break;
        }

        // Wait for MapSegmentHandler.Start() to run
        yield return null;

        // Build the road if needed
        if (currentSegmentHandler.road == null || currentSegmentHandler.road.Count == 0)
        {
            Debug.Log($"Building road for {mapPrefabs[index].name}...");
            currentSegmentHandler.BuildRoad();
            yield return null;
        }
        
        // Validate road was built successfully
        if (currentSegmentHandler.road == null || currentSegmentHandler.road.Count == 0)
        {
            Debug.LogError($"Failed to build road for map {mapPrefabs[index].name}");
            yield break;
        }

        yield return new WaitForFixedUpdate();

        IsMapReady = true;

        Debug.Log($"Loaded map: {mapPrefabs[index].name}");
    }

    public void LoadRandomMap()
    {
        StartCoroutine(LoadMap(UnityEngine.Random.Range(0, mapPrefabs.Length)));
    }
}
