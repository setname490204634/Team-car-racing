using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public struct Rewards
{
    //input change
    public float steeringSmoothnessPenalty;
    public float throttleSmoothnessPenalty;

    //collisions
    public float outOfBoundsPenalty; //used to detect bug for now
    public float collisionPenalty;
    public float grassPenalty;

    //game state
    public float teamDistancePenalty;
    public float lapTimePenalty;
    public float teamLapTimePenalty;
    public float finalPlacementReward;
    public float currentPlacementReward;
    public float teamFinalPlacementReward;
    public float currentTeamPlacementReward;
    public float tickPenalty;

    //car controller
    public float speedReward;
    public float accelerationReward;
    public float distanceReward;

    //segments
    public float speedRewardI;
    public float speedRewardII;
    public float speedRewardIII;
    public float speedRewardIV;
    public float speedRewardV;

    public float accelerationRewardI;
    public float accelerationRewardII;
    public float accelerationRewardIII;
    public float accelerationRewardIV;
    public float accelerationRewardV;

    public float anglePenaltyI;
    public float anglePenaltyII;
    public float anglePenaltyIII;
    public float anglePenaltyIV;
    public float anglePenaltyV;

    public float distancePenaltyI;
    public float distancePenaltyII;
    public float distancePenaltyIII;

    public float progressReward;

    public float[] ToArray()
    {
        float[] arr = new float[50]
        {
            //input change
            steeringSmoothnessPenalty,
            throttleSmoothnessPenalty,

            //collisions
            outOfBoundsPenalty,
            collisionPenalty,
            grassPenalty,

            //game state
            teamDistancePenalty,
            lapTimePenalty,
            teamLapTimePenalty,
            finalPlacementReward,
            currentPlacementReward,
            teamFinalPlacementReward,
            currentTeamPlacementReward,
            tickPenalty,

            //car controller
            speedReward,
            accelerationReward,
            distanceReward,

            //segments
            speedRewardI,
            speedRewardII,
            speedRewardIII,
            speedRewardIV,
            speedRewardV,

            accelerationRewardI,
            accelerationRewardII,
            accelerationRewardIII,
            accelerationRewardIV,
            accelerationRewardV,

            anglePenaltyI,
            anglePenaltyII,
            anglePenaltyIII,
            anglePenaltyIV,
            anglePenaltyV,

            distancePenaltyI,
            distancePenaltyII,
            distancePenaltyIII,
            progressReward,

            //reserved for later use
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
        };

        return arr;
    }
}

public class RewardsCalculator : ICarRewardProvider
{
    private gameControlScript gameControl;
    public List<int> teammatesID;
    private CarEntry entry;
    private MapSegmentHandler segmentHandler;

    //input change
    private CarInput lastInput;

    //collisions
    private bool collided = false;
    private float collidedPenalty = 0f;
    private bool onGrass = false;
    private float onGrassPenalty = 0f;
    private bool outOfBounds = false;

    //game state
    const float idealDistance = 10f; //for closeness reward
    //placement reward scaling starts from index 1 and not 0!
    private static readonly int[] placementPoints = new int[]
    {
        0, 25, 18, 15, 12, 10, 8, 6, 4, 2, 0
    };

    private float lastLapTime = 0f;
    private float lapTimeReward = 0f;

    private int finalPlacement = 0; // 0 means not registered
    private float placementReward = 0f;

    private int currentPlacement = 0;
    private float currentPlacementReward = 0f;

    private List<int> finalTeammatePlacement = new List<int>();
    private float finalTeammatePlacementReward = 0f;

    private List<int> currentTeammatePlacement = new List<int>();
    private float currentTeammatePlacementReward = 0f;

    private List<float> teammateLapTimes = new List<float>();
    private float teammateLapPenalty = 0f;

    //segments
    private float segmentProgress = 0f;
    private float segmentProgressReward = 0f;

    public RewardsCalculator(CarEntry entry, gameControlScript gameControl, MapSegmentHandler segmentHandler, List<int> teammatesID = null)
    {
        this.entry = entry;
        this.gameControl = gameControl;
        this.segmentHandler = segmentHandler;
        this.teammatesID = teammatesID ?? new List<int>();

        lastInput = entry.agent.agentInputProvider.getInput();
    }

    public void RegisterCollision() { collided = true; }
    public void RegisterLapTime(float lapTime) { this.lastLapTime = lapTime; }
    public void RegisterFinalPlacement(int place) { this.finalPlacement = place; }
    public void RegisterCurrentPlacement(int place) { this.currentPlacement = place; }
    public void RegisterFinalTeammatePlacement(int place) { this.finalTeammatePlacement.Add(place); }
    public void RegisterCurrentTeammatePlacement(int place) { this.currentTeammatePlacement.Add(place); }
    public void RegisterTeammateLapTime(float time) { this.teammateLapTimes.Add(time); }
    public void RegisterOnGrass() { this.onGrass = true; }
    public void RegisterOutOfBounds() { this.outOfBounds = true; }
    public void RegisterProgressReward(float reward) { this.segmentProgress = reward; }

    public void Reset()
    {
        //input change
        lastInput = entry.agent.agentInputProvider.getInput();

        //collisions
        collided = false;
        onGrass = false;
        outOfBounds = false;

        //game state
        lastLapTime = 0f;
        finalPlacement = 0;
        currentPlacement = 0;
        finalTeammatePlacement.Clear();
        teammateLapTimes.Clear();
        currentTeammatePlacement.Clear();

        //segments
        this.segmentHandler = this.gameControl.currentSegmentHandler;
        this.segmentProgress = 0f;
    }

    public Rewards CalculateReward()
    {
        UpdateLastRewards();
        Rewards output = new Rewards()
        {
            //input change
            steeringSmoothnessPenalty = SteeringSmoothnessPenalty(),
            throttleSmoothnessPenalty = ThrottleSmoothnessPenalty(),

            //collisions
            outOfBoundsPenalty = OutOfBoundsPenalty(),
            collisionPenalty = collidedPenalty,
            grassPenalty = onGrassPenalty,

            //game state
            teamDistancePenalty = TeamDistancePenalty(),
            lapTimePenalty = lapTimeReward,
            teamLapTimePenalty = teammateLapPenalty,
            finalPlacementReward = placementReward,
            currentPlacementReward = currentPlacementReward,
            teamFinalPlacementReward = finalTeammatePlacementReward,
            currentTeamPlacementReward = currentTeammatePlacementReward,
            tickPenalty = -1,

            //car controller
            speedReward = SpeedReward(),
            accelerationReward = AccelerationReward(),
            distanceReward = DistanceReward(),

            //segments
            speedRewardI = SpeedInDirectionReward(segmentHandler.GetVectorI),
            speedRewardII = SpeedInDirectionReward(segmentHandler.GetVectorII),
            speedRewardIII = SpeedInDirectionReward(segmentHandler.GetVectorIII),
            speedRewardIV = SpeedInDirectionReward(segmentHandler.GetVectorIV),
            speedRewardV = SpeedInDirectionReward(segmentHandler.GetVectorV),

            accelerationRewardI = AccelerationInDirectionReward(segmentHandler.GetVectorI),
            accelerationRewardII = AccelerationInDirectionReward(segmentHandler.GetVectorII),
            accelerationRewardIII = AccelerationInDirectionReward(segmentHandler.GetVectorIII),
            accelerationRewardIV = AccelerationInDirectionReward(segmentHandler.GetVectorIV),
            accelerationRewardV = AccelerationInDirectionReward(segmentHandler.GetVectorV),

            anglePenaltyI = AngleInDirectionPenalty(segmentHandler.GetVectorI),
            anglePenaltyII = AngleInDirectionPenalty(segmentHandler.GetVectorII),
            anglePenaltyIII = AngleInDirectionPenalty(segmentHandler.GetVectorIII),
            anglePenaltyIV = AngleInDirectionPenalty(segmentHandler.GetVectorIV),
            anglePenaltyV = AngleInDirectionPenalty(segmentHandler.GetVectorV),

            //I and II are not used since the rewards are bad
            distancePenaltyI = 0,
            distancePenaltyII = 0,
            distancePenaltyIII = DistanceToPenalty(segmentHandler.GetDistanceIII),

            progressReward = segmentProgressReward
        };

        // reset it here since its used at 2 places
        lastInput = entry.agent.agentInputProvider.getInput();

        return output;
    }

    public void UpdateLastRewards()
    {
        collidedPenalty = collided ? -1.0f : 0.0f;
        collided = false;

        onGrassPenalty = onGrass ? -1.0f : 0.0f;
        onGrass = false;

        lapTimeReward = -lastLapTime;
        lastLapTime = 0f;

        if (finalPlacement < placementPoints.Length)
            placementReward = placementPoints[finalPlacement];
        else
            placementReward = 0f;
        finalPlacement = 0;

        if (currentPlacement < placementPoints.Length)
            currentPlacementReward = placementPoints[currentPlacement];
        else
            currentPlacementReward = 0f;
        currentPlacement = 0;

        finalTeammatePlacementReward = 0f;
        foreach (int place in finalTeammatePlacement)
        {
            if (place < placementPoints.Length)
                finalTeammatePlacementReward += placementPoints[place];
        }
        finalTeammatePlacement.Clear();

        currentTeammatePlacementReward = 0f;
        foreach (int place in currentTeammatePlacement)
        {
            if (place < placementPoints.Length)
                currentTeammatePlacementReward += placementPoints[place];
        }
        currentTeammatePlacement.Clear();

        teammateLapPenalty = 0f;
        foreach (float t in teammateLapTimes)
            teammateLapPenalty -= t;
        teammateLapTimes.Clear();

        segmentProgressReward = segmentProgress;
        segmentProgress = 0f;
    }
    public List<int> GetTeammateId()
    {
        return this.teammatesID;
    }
    private float SpeedReward()
    {
        Vector3 velocity = entry.controller.speed;

        // Project velocity onto the car's forward vector (in world space)
        float forwardSpeed = Vector3.Dot(velocity, entry.carObject.transform.forward);

        // Reward forward speed (positive = moving forward, negative = reversing)
        return forwardSpeed;
    }
    private float AccelerationReward()
    {
        Vector3 acc = entry.controller.acceleration;

        // Project Acceleration onto the car's forward vector (in world space)
        float forwardAcceleration = Vector3.Dot(acc, entry.carObject.transform.forward);

        return math.max(forwardAcceleration, 0f);
    }
    private float DistanceReward()
    {
        return entry.controller.distance;
    }
    private float SteeringSmoothnessPenalty()
    {
        float delta = Mathf.Abs(entry.agent.agentInputProvider.getInput().Steering - lastInput.Steering);
        return -delta / 255;
    }
    private float ThrottleSmoothnessPenalty()
    {
        float delta = Mathf.Abs(entry.agent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
        return -delta / 255;
    }
    private float TeamDistancePenalty()
    {
        float output = 0.0f;
        foreach (int ID in this.teammatesID)
        {
            GameObject teammate = gameControl.GetCarByID(ID);
            float distance = Vector3.Distance(entry.carObject.transform.position, teammate.transform.position);
            output += Mathf.Clamp01((distance - idealDistance) / 50f);
        }

        return -output;
    }
    private float OutOfBoundsPenalty()
    {
        float output = 0f;
        if (outOfBounds)
            output = -1.0f;
        Vector3 acc = entry.controller.acceleration;
        if (acc.magnitude > 100)
            output = -1.0f;
        outOfBounds = false;
        return output;
    }
    private float SpeedInDirectionReward(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return GetVectorMagnitudeInDirection(entry.controller.speed2D, dir);
    }
    private float AccelerationInDirectionReward(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return GetVectorMagnitudeInDirection(entry.controller.acceleration2D, dir);
    }
    private float AngleInDirectionPenalty(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return -GetVectorAngleInDirection(entry.controller.speed2D, dir) / 3.14f;
    }
    private float DistanceToPenalty(Func<int, Vector2, float> getDistanceFunc)
    {
        return -getDistanceFunc(entry.segmentIndex, entry.controller.position2D);
    }
    private float GetVectorMagnitudeInDirection(Vector2 vector, Vector2 direction)
    {
        return Vector2.Dot(vector, direction.normalized); ;
    }
    private float GetVectorAngleInDirection(Vector2 vector, Vector2 direction)
    {
        if (vector == Vector2.zero || direction == Vector2.zero)
            return 0f;

        float dot = Vector2.Dot(vector.normalized, direction.normalized);
        return Mathf.Abs(Mathf.Acos(Mathf.Clamp(dot, -1f, 1f))); // angle in radians
    }
}
