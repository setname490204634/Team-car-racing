using System.Collections.Generic;
using Unity.MLAgents;
using Unity.Services.Analytics;
using UnityEngine;


public struct RewardConfig
{
    public float speedWeight;
    public float steeringSmoothnessWeight;
    public float throttleSmoothnessWeight;
    public float collisionPenaltyWeight;
    public float teamDistanceWeight;
    public float lapTimeWeight;
    public float placementWeight;
    public float teamPlacementWeight;
}

public class Rewards : ICarRewardProvider
{
    private RewardConfig config;
    private Rigidbody rb;
    private CarAgent carAgent;
    private GameObject carObject;
    private gameControlScript gameControl;
    private int? teammateID;

    private CarInput lastInput;

    private bool collided = false;
    private float lastLapTime = 0f;
    private int finalPlacement = -1; // -1 means not registered
    private int finalTeammatePlacement = -1;

    const float idealDistance = 10f; //for closeness reward

    //placement reward scaling starts from index 1 and not 0!
    private static readonly int[] placementPoints = new int[]
    {
        0, 25, 18, 15, 12, 10, 8, 6, 4, 2, 0
    };

    public Rewards(CarAgent agent, gameControlScript gameControl, GameObject carObject, RewardConfig cfg, int? teammateID = null)
    {
        this.carAgent = agent;
        this.carObject = carObject;
        this.rb = carObject.GetComponent<Rigidbody>();
        this.config = cfg;
        this.gameControl = gameControl;
        this.teammateID = teammateID;

        lastInput = agent.agentInputProvider.getInput();
    }

    public float CalculateReward()
    {
        // sum of rewards the weights are calculated in methods
        float total = 0f;

        total += SpeedReward();
        total += SteeringSmoothnessReward();
        total += ThrottleSmoothnessReward();
        total += CollisionPenalty();
        total += TeamDistanceReward();
        total += LapTimeReward();
        total += PlacementReward();
        total += TeamPlacementReward();

        // reset it here since its used at 2 places
        lastInput = carAgent.agentInputProvider.getInput();

        return total;
    }

    // Speed along local X
    private float SpeedReward()
    {
        if (config.speedWeight == 0f) return 0f;

        Vector3 velocity = rb.linearVelocity;
        Vector3 localVelocity = carObject.transform.InverseTransformDirection(velocity);

        // Reward forward (local X axis) speed
        return config.speedWeight * localVelocity.x;
    }

    // Steering change penalty
    private float SteeringSmoothnessReward()
    {
        if (config.steeringSmoothnessWeight == 0f) return 0f;

        float delta = Mathf.Abs(carAgent.agentInputProvider.getInput().Steering - lastInput.Steering);
        return config.steeringSmoothnessWeight * -delta;
    }

    // Throttle change penalty
    private float ThrottleSmoothnessReward()
    {
        if (config.throttleSmoothnessWeight == 0f) return 0f;

        float delta = Mathf.Abs(carAgent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
        return config.throttleSmoothnessWeight * -delta;
    }

    // Collision penalty
    private float CollisionPenalty()
    {
        if (config.collisionPenaltyWeight == 0f) return 0f;

        float output = collided ? -1f : 0f;
        collided = false;
        return config.collisionPenaltyWeight * output;
    }

    //Called from CarAgents OnCollisionEnter
    public void RegisterCollision() { collided = true; }

    // Distance from teammate
    private float TeamDistanceReward()
    {
        if (config.teamDistanceWeight == 0f) return 0f;
        if (teammateID == null) return 0f;

        GameObject teammate = gameControl.GetCarByID(teammateID.Value);
        if (teammate == null) return 0f;

        float distance = Vector3.Distance(carObject.transform.position, teammate.transform.position);
        float normalized = Mathf.Clamp01((distance - idealDistance) / 50f);
        return config.teamDistanceWeight * normalized;
    }

    // Lap time reward
    private float LapTimeReward()
    {
        if (config.lapTimeWeight == 0f) return 0f;

        float output = lastLapTime;
        lastLapTime = 0;
        return config.lapTimeWeight * output;
    }

    public void RegisterLapTime(float lapTime) { this.lastLapTime = lapTime; }

    // Placement reward
    private float PlacementReward()
    {
        if (config.placementWeight == 0f) return 0f;
        if (finalPlacement == -1 || finalPlacement >= placementPoints.Length) return 0f;

        float output = placementPoints[finalPlacement];
        finalPlacement = -1;
        return config.placementWeight * output;
    }

    public void RegisterFinalPlacement(int place) { this.finalPlacement = place; }

    // Team placement reward
    // rewards car for teammate placement
    private float TeamPlacementReward()
    {
        if (config.teamPlacementWeight == 0f) return 0f;
        if (finalTeammatePlacement == -1 || finalTeammatePlacement >= placementPoints.Length) return 0f;

        float output = placementPoints[finalTeammatePlacement];
        finalTeammatePlacement = -1;
        return config.teamPlacementWeight * output;
    }

    public void RegisterFinalTeammatePlacement(int place)
    {
        this.finalTeammatePlacement = place;
    }

    public static readonly RewardConfig Default = new RewardConfig
    {
        speedWeight = 1.0f,
        steeringSmoothnessWeight = 1.0f,
        throttleSmoothnessWeight = 1.0f,
        collisionPenaltyWeight = 1.0f,
        teamDistanceWeight = 1.0f,
        lapTimeWeight = 1.0f,
        placementWeight = 1.0f,
        teamPlacementWeight = 1.0f,
    };

    public int? GetTeammateId()
    {
        return this.teammateID;
    }
}
