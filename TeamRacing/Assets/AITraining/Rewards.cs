using System.Collections.Generic;
using UnityEngine;


public struct Rewards
{
    public float speed;
    public float steeringSmoothness;
    public float throttleSmoothness;
    public float collisionPenalty;
    public float grassPenalty;
    public float teamDistance;
    public float lapTime;
    public float teamLapTime;
    public float placement;
    public float teamPlacement;

    public float[] ToArray()
    {
        return new float[]
        {
        speed,
        steeringSmoothness,
        throttleSmoothness,
        collisionPenalty,
        grassPenalty,
        teamDistance,
        lapTime,
        teamLapTime,
        placement,
        teamPlacement
        };
    }
}

public class RewardsCalculator : ICarRewardProvider
{
    private CarAgent carAgent;
    private GameObject carObject;
    private gameControlScript gameControl;
    private CarController controller;
    public List<int> teammatesID;

    private CarInput lastInput;

    private bool collided = false;
    private bool onGrass = false;
    private bool outOfBounds = false;
    private float lastLapTime = 0f;
    private int finalPlacement = -1; // -1 means not registered
    private List<int> finalTeammatePlacement = new List<int>();
    private List<float> teammateLapTimes = new List<float>();

    const float idealDistance = 10f; //for closeness reward

    //placement reward scaling starts from index 1 and not 0!
    private static readonly int[] placementPoints = new int[]
    {
        0, 25, 18, 15, 12, 10, 8, 6, 4, 2, 0
    };

    public RewardsCalculator(CarAgent agent, gameControlScript gameControl, GameObject carObject, CarController controller, List<int> teammatesID = null)
    {
        this.carAgent = agent;
        this.carObject = carObject;
        this.gameControl = gameControl;
        this.teammatesID = teammatesID ?? new List<int>();
        this.controller = controller;

        lastInput = agent.agentInputProvider.getInput();
    }

    public Rewards CalculateReward()
    {
        Rewards output = new Rewards()
        {
            speed = SpeedReward(),
            steeringSmoothness = SteeringSmoothnessReward(),
            throttleSmoothness = ThrottleSmoothnessReward(),
            collisionPenalty = CollisionPenalty(),
            grassPenalty = GrassPenalty(),
            teamDistance = TeamDistanceReward(),
            lapTime = LapTimeReward(),
            teamLapTime = TeamLapTimeReward(),
            placement = PlacementReward(),
            teamPlacement = TeamPlacementReward()
        };
        // reset it here since its used at 2 places
        lastInput = carAgent.agentInputProvider.getInput();

        return output;
    }

    // Speed along local X
    private float SpeedReward()
    {
        Vector3 velocity = controller.speed;

        // Project velocity onto the car's forward vector (in world space)
        float forwardSpeed = Vector3.Dot(velocity, carObject.transform.forward);

        // Reward forward speed (positive = moving forward, negative = reversing)
        return forwardSpeed;
    }

    // Steering change penalty
    private float SteeringSmoothnessReward()
    {
        float delta = Mathf.Abs(carAgent.agentInputProvider.getInput().Steering - lastInput.Steering);
        return -delta;
    }

    // Throttle change penalty
    private float ThrottleSmoothnessReward()
    {
        float delta = Mathf.Abs(carAgent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
        return -delta;
    }

    // Collision penalty
    private float CollisionPenalty()
    {
        float output = 0f;
        if (collided)
            output = -1.0f;   
        collided = false;
        return output;
    }

    //Called from CarAgents OnCollisionEnter
    public void RegisterCollision() { collided = true; }

    // Distance from teammate
    private float TeamDistanceReward()
    {
        float output = 0.0f;
        foreach (int ID in this.teammatesID)
        {
            GameObject teammate = gameControl.GetCarByID(ID);
            float distance = Vector3.Distance(carObject.transform.position, teammate.transform.position);
            output += Mathf.Clamp01((distance - idealDistance) / 50f);
        }

        return output;
    }

    // Lap time reward
    private float LapTimeReward()
    {
        float output = lastLapTime;
        lastLapTime = 0;
        return output;
    }

    public void RegisterLapTime(float lapTime) { this.lastLapTime = lapTime; }

    // Placement reward
    private float PlacementReward()
    {
        if (finalPlacement == -1 || finalPlacement >= placementPoints.Length) return 0f;

        float output = placementPoints[finalPlacement];
        finalPlacement = -1;
        return output;
    }

    public void RegisterFinalPlacement(int place) { this.finalPlacement = place; }

    // Team placement reward
    // rewards car for teammate placement
    private float TeamPlacementReward()
    {
        float output = 0.0f;
        foreach (int teammatePlace in finalTeammatePlacement)
        {
            if (teammatePlace >= placementPoints.Length) continue;
            output += placementPoints[teammatePlace];
        }
        this.finalTeammatePlacement.Clear();
        return output;
    }

    public void RegisterFinalTeammatePlacement(int place)
    {
        this.finalTeammatePlacement.Add(place);
    }

    public float TeamLapTimeReward()
    {
        float output = 0.0f;
        foreach (int time in teammateLapTimes)
        {
            output += time;
        }
        this.teammateLapTimes.Clear();
        return output;
    }

    public void RegisterTeammateLapTime(float time)
    {
        this.teammateLapTimes.Add(time);
    }

    public float GrassPenalty()
    {
        float output = 0f;
        if (onGrass)
            output = -1.0f;
        onGrass = false;
        return output;
    }

    public void RegisterOnGrass() { this.onGrass = true; }

    public List<int> GetTeammateId()
    {
        return this.teammatesID;
    }

    public float OutOfBoundsPenalty()
    {
        float output = 0f;
        if (outOfBounds)
            output = -1.0f;
        outOfBounds = false;
        return output;
    }

    public void RegisterOutOfBounds() { this.outOfBounds = true; }

}
