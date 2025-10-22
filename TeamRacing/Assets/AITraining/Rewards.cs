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

public class Rewards
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

    //placement reward scaling starts from 1!!
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
        float total = 0f;

        total += config.speedWeight * SpeedReward();
        total += config.steeringSmoothnessWeight * SteeringSmoothnessReward();
        total += config.throttleSmoothnessWeight * ThrottleSmoothnessReward();
        total += config.collisionPenaltyWeight * CollisionPenalty();
        total += config.teamDistanceWeight * TeamDistanceReward();
        total += config.lapTimeWeight * LapTimeReward();
        total += config.placementWeight * PlacementReward();
        total += config.teamPlacementWeight * TeamPlacementReward();

        collided = false; // reset for next frame
        lastInput = carAgent.agentInputProvider.getInput();

        return total;
    }

    // Speed along local X
    private float SpeedReward()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 localVelocity = carObject.transform.InverseTransformDirection(velocity);

        // Reward forward (local X axis) speed
        return localVelocity.x;
    }

    // Steering change penalty
    private float SteeringSmoothnessReward()
    {
        return -Mathf.Abs(carAgent.agentInputProvider.getInput().Steering - lastInput.Steering);
    }

    // Throttle change penalty
    private float ThrottleSmoothnessReward()
    {
        return -Mathf.Abs(carAgent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
    }

    // Collision penalty
    private float CollisionPenalty()
    {
        float output = collided ? -1f : 0f;
        collided = false;
        return output;
    }

    // Called from CarAgents OnCollisionEnter
    public void RegisterCollision()
    {
        collided = true;
    }

    // Distance from teammate
    private float TeamDistanceReward()
    {
        if (teammateID == null) return 0f;

        GameObject teammate = gameControl.GetCarByID(teammateID.Value);

        Vector3 myPos = carObject.transform.position;
        Vector3 matePos = teammate.transform.position;

        float distance = Vector3.Distance(myPos, matePos);
        return Mathf.Clamp01((distance - idealDistance) / 50f); // normalize over 50m max
    }

    // Lap time reward
    private float LapTimeReward()
    {
        float output = lastLapTime;
        lastLapTime = 0;
        return output;
    }

    public void RegisterLapTime(float lapTime)
    {
        this.lastLapTime = lapTime;
    }

    // Placement reward (final)
    private float PlacementReward()
    {
        if (finalPlacement == -1 || finalPlacement >= placementPoints.Length) return 0f;
        float output = (float)placementPoints[finalPlacement];
        finalPlacement = -1;
        return output;
    }

    public void RegisterFinalPlacement(int place)
    {
        this.finalPlacement = place;
    }

    // Team placement reward
    private float TeamPlacementReward()
    {
        if (finalTeammatePlacement == -1 || finalTeammatePlacement >= placementPoints.Length) return 0f;
        float output = (float)placementPoints[finalTeammatePlacement];
        finalTeammatePlacement = -1;
        return output;
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
