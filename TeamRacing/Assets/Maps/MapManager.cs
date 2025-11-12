using System;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Available Map Prefabs (each must have a MapSegmentHandler)")]
    public GameObject[] mapPrefabs;


    public GameObject currentMapInstance;
    public MapSegmentHandler currentSegmentHandler;

    private void Start()
    {
        if (mapPrefabs.Length > 0)
        {
            LoadMap(0); // load the first map by default
        }
    }

    public void LoadMap(int index)
    {
        if (index < 0 || index >= mapPrefabs.Length)
        {
            Debug.LogError($"Invalid map index {index}");
            return;
        }

        // Remove old map
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
            currentMapInstance = null;
            currentSegmentHandler = null;
        }

        // Instantiate new one
        currentMapInstance = Instantiate(mapPrefabs[index], Vector3.zero, Quaternion.identity);
        currentSegmentHandler = currentMapInstance.GetComponentInChildren<MapSegmentHandler>();

        if (currentSegmentHandler == null)
        {
            Debug.LogError($"Map {mapPrefabs[index].name} has no MapSegmentHandler component!");
            return;
        }

        // Build the road if needed
        if (currentSegmentHandler.road == null)
        {
            Debug.Log($"Building road for {mapPrefabs[index].name}...");
            currentSegmentHandler.BuildRoad();
        }

        Debug.Log($"Loaded map: {mapPrefabs[index].name}");
    }

    public void LoadRandomMap()
    {
        LoadMap(UnityEngine.Random.Range(0, mapPrefabs.Length));
    }
}
