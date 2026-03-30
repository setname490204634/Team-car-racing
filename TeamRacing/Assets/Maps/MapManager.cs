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
        currentSegmentHandler = currentMapInstance.GetComponentInChildren<MapSegmentHandler>();

        if (currentSegmentHandler == null)
        {
            Debug.LogError($"Map {mapPrefabs[index].name} has no MapSegmentHandler component!");
            yield break;
        }

        // Build the road if needed
        if (currentSegmentHandler.road == null)
        {
            Debug.Log($"Building road for {mapPrefabs[index].name}...");
            currentSegmentHandler.BuildRoad();
            yield return null;
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
