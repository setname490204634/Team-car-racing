using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public struct Rewards
{
    //input change
    public float steeringSmoothness;
    public float throttleSmoothness;

    //collisions
    public float outOfBoundsPenalty; //used to detect bug for now
    public float collisionPenalty;
    public float dicountedCollisionPenalty;
    public float grassPenalty;
    public float dicountedGrassPenalty;

    //game state
    public float teamDistance;
    public float lapTime;
    public float dicountedLapTime;
    public float teamLapTime;
    public float dicountedTeamLapTime;
    public float placement;
    public float dicountedPlacement;
    public float teamPlacement;
    public float dicountedTeamPlacement;

    //car controller
    public float speed;
    public float acceleration;
    public float distance;
    public float discountedDistance;

    //segments
    public float speedI;
    public float speedII;
    public float speedIII;
    public float speedIV;
    public float speedV;

    public float accelerationI;
    public float accelerationII;
    public float accelerationIII;
    public float accelerationIV;
    public float accelerationV;

    public float angleI;
    public float angleII;
    public float angleIII;
    public float angleIV;
    public float angleV;

    public float distanceI;
    public float distanceII;
    public float distanceIII;
    public float progressReward;
    public float survivalReward;

    public float[] ToArray()
    {
        float[] arr = new float[50]
        {
            steeringSmoothness,
            throttleSmoothness,

            outOfBoundsPenalty,
            collisionPenalty,
            dicountedCollisionPenalty,
            grassPenalty,
            dicountedGrassPenalty,

            teamDistance,
            lapTime,
            dicountedLapTime,
            teamLapTime,
            dicountedTeamLapTime,
            placement,
            dicountedPlacement,
            teamPlacement,
            dicountedTeamPlacement,


            speed,
            acceleration,
            distance,
            discountedDistance,

            speedI,
            speedII,
            speedIII,
            speedIV,
            speedV,

            accelerationI,
            accelerationII,
            accelerationIII,
            accelerationIV,
            accelerationV,

            angleI,
            angleII,
            angleIII,
            angleIV,
            angleV,

            distanceI,
            distanceII,
            distanceIII,
            progressReward,
            survivalReward,

            //reserved for later use
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

    private CarInput lastInput;


    private bool collided = false;
    private float collidedReward = 0f;
    private float collidedSum = 0f;
    private float collidedDiscount = 0.8f;

    private bool onGrass = false;
    private float onGrassReward = 0f;
    private float onGrassSum = 0f;
    private float onGrassDiscount = 0.9f;

    private bool outOfBounds = false;

    private float lastLapTime = 0f;
    private float lapTimeReward = 0f;
    private float lastLapTimeSum = 0f;
    private float lastLapTimeDiscount = 0.7f;

    private int finalPlacement = 0; // 0 means not registered
    private float placementReward = 0f;
    private float finalPlacementSum = 0f;
    private float finalPlacementDiscount = 0.7f;

    private List<int> finalTeammatePlacement = new List<int>();
    private float finalTeammatePlacementReward = 0f;
    private float finalTeammatePlacementSum = 0f;
    private float finalTeammatePlacementDiscount = 0.7f;

    private List<float> teammateLapTimes = new List<float>();
    private float teammateLapReward = 0f;
    private float teammateLapTimeSum = 0f;
    private float teammateLapDiscount = 0.7f;

    private float segmentProgress = 0f;
    private float segmentProgressReward = 0f;

    const float idealDistance = 10f; //for closeness reward

    //placement reward scaling starts from index 1 and not 0!
    private static readonly int[] placementPoints = new int[]
    {
        0, 25, 18, 15, 12, 10, 8, 6, 4, 2, 0
    };

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
    public void RegisterFinalTeammatePlacement(int place) { this.finalTeammatePlacement.Add(place); }
    public void RegisterTeammateLapTime(float time) { this.teammateLapTimes.Add(time); }
    public void RegisterOnGrass() { this.onGrass = true; }
    public void RegisterOutOfBounds() { this.outOfBounds = true; }
    public void RegisterProgressReward(float reward) {  this.segmentProgress =  reward; }

    public void Reset()
    {
        collided = false;
        onGrass = false;
        outOfBounds = false;
        lastLapTime = 0f;
        finalPlacement = 0;
        finalTeammatePlacement.Clear();
        teammateLapTimes.Clear();
        lastInput = entry.agent.agentInputProvider.getInput();
        this.segmentHandler = this.gameControl.currentSegmentHandler;
        this.segmentProgress = 0f;

        collidedSum = 0f;
        onGrassSum = 0f;
        lastLapTimeSum = 0f;
        finalPlacementSum = 0f;
        finalTeammatePlacementSum = 0f;
        teammateLapTimeSum = 0f;
    }

    public Rewards CalculateReward()
    {
        UpdateLastRewards();
        UpdateDiscountedRewards();
        Rewards output = new Rewards()
        {
            steeringSmoothness = SteeringSmoothnessReward(),
            throttleSmoothness = ThrottleSmoothnessReward(),

            outOfBoundsPenalty = OutOfBoundsPenalty(),
            collisionPenalty = collidedReward,
            dicountedCollisionPenalty = collidedSum,
            grassPenalty = onGrassReward,
            dicountedGrassPenalty = onGrassSum,

            teamDistance = TeamDistanceReward(),
            lapTime = lapTimeReward,
            dicountedLapTime = lastLapTimeSum,
            teamLapTime = teammateLapReward,
            dicountedTeamLapTime = teammateLapTimeSum,
            placement = placementReward,
            dicountedPlacement = finalPlacementSum,
            teamPlacement = finalTeammatePlacementReward,
            dicountedTeamPlacement = finalTeammatePlacementSum,

            speed = SpeedReward(),
            acceleration = AccelerationReward(),
            discountedDistance = DiscountedDistanceReward(),
            distance = DistanceReward(),

            speedI = SpeedRewardInDirection(segmentHandler.GetVectorI),
            speedII = SpeedRewardInDirection(segmentHandler.GetVectorII),
            speedIII = SpeedRewardInDirection(segmentHandler.GetVectorIII),
            speedIV = SpeedRewardInDirection(segmentHandler.GetVectorIV),
            speedV = SpeedRewardInDirection(segmentHandler.GetVectorV),

            accelerationI = AccelerationRewardInDirection(segmentHandler.GetVectorI),
            accelerationII = AccelerationRewardInDirection(segmentHandler.GetVectorII),
            accelerationIII = AccelerationRewardInDirection(segmentHandler.GetVectorIII),
            accelerationIV = AccelerationRewardInDirection(segmentHandler.GetVectorIV),
            accelerationV = AccelerationRewardInDirection(segmentHandler.GetVectorV),

            angleI = AngleRewardInDirection(segmentHandler.GetVectorI),
            angleII = AngleRewardInDirection(segmentHandler.GetVectorII),
            angleIII = AngleRewardInDirection(segmentHandler.GetVectorIII),
            angleIV = AngleRewardInDirection(segmentHandler.GetVectorIV),
            angleV = AngleRewardInDirection(segmentHandler.GetVectorV),

            distanceI = DistanceRewardTo(segmentHandler.GetDistanceI),
            distanceII = DistanceRewardTo(segmentHandler.GetDistanceII),
            distanceIII = DistanceRewardTo(segmentHandler.GetDistanceIII),
            progressReward = segmentProgressReward,
            survivalReward = 1
        };

        // reset it here since its used at 2 places
        lastInput = entry.agent.agentInputProvider.getInput();

        return output;
    }

    public void UpdateLastRewards()
    {
        collidedReward = collided ? 1.0f : 0.0f;
        collided = false;

        onGrassReward = onGrass ? 1.0f : 0.0f;
        onGrass = false;

        lapTimeReward = lastLapTime;
        lastLapTime = 0f;

        if (finalPlacement < placementPoints.Length)
            placementReward = placementPoints[finalPlacement];
        else
            placementReward = 0f;
        finalPlacement = 0;

        finalTeammatePlacementReward = 0f;
        foreach (int place in finalTeammatePlacement)
        {
            if (place < placementPoints.Length)
                finalTeammatePlacementReward += placementPoints[place];
        }
        finalTeammatePlacement.Clear();

        teammateLapReward = 0f;
        foreach (float t in teammateLapTimes)
            teammateLapReward += t;
        teammateLapTimes.Clear();

        segmentProgressReward = segmentProgress;
        segmentProgress = 0f;
    }

    public void UpdateDiscountedRewards()
    {
        collidedSum = collidedSum * collidedDiscount + collidedReward;
        onGrassSum = onGrassSum * onGrassDiscount + onGrassReward;
        lastLapTimeSum = lastLapTimeSum * lastLapTimeDiscount + lapTimeReward;
        finalPlacementSum = finalPlacementSum * finalPlacementDiscount + placementReward;
        finalTeammatePlacementSum = finalTeammatePlacementSum * finalTeammatePlacementDiscount + finalTeammatePlacementReward;
        teammateLapTimeSum = teammateLapTimeSum * teammateLapDiscount + teammateLapReward;
    }

    public List<int> GetTeammateId()
    {
        return this.teammatesID;
    }

    // Speed along local X
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

    private float DiscountedDistanceReward()
    {
        return entry.controller.discountedDistance;
    }

    private float DistanceReward()
    {
        return entry.controller.distance;
    }

    // Steering change penalty
    private float SteeringSmoothnessReward()
    {
        float delta = Mathf.Abs(entry.agent.agentInputProvider.getInput().Steering - lastInput.Steering);
        return delta / 255;
    }

    // Throttle change penalty
    private float ThrottleSmoothnessReward()
    {
        float delta = Mathf.Abs(entry.agent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
        return delta / 255;
    }

    // Distance from teammate
    private float TeamDistanceReward()
    {
        float output = 0.0f;
        foreach (int ID in this.teammatesID)
        {
            GameObject teammate = gameControl.GetCarByID(ID);
            float distance = Vector3.Distance(entry.carObject.transform.position, teammate.transform.position);
            output += Mathf.Clamp01((distance - idealDistance) / 50f);
        }

        return output;
    }

    private float OutOfBoundsPenalty()
    {
        float output = 0f;
        if (outOfBounds)
            output = 1.0f;
        outOfBounds = false;
        return output;
    }

    private float SpeedRewardInDirection(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return GetVectorMagnitudeInDirection(entry.controller.speed2D, dir);
    }

    private float AccelerationRewardInDirection(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return GetVectorMagnitudeInDirection(entry.controller.acceleration2D, dir);
    }

    private float AngleRewardInDirection(Func<int, Vector2, Vector2> getVectorFunc)
    {
        Vector2 dir = getVectorFunc(entry.segmentIndex, entry.controller.position2D);
        return GetVectorAngleInDirection(entry.controller.speed2D, dir) / 3.14f;
    }

    private float DistanceRewardTo(Func<int, Vector2, float> getDistanceFunc)
    {
        return getDistanceFunc(entry.segmentIndex, entry.controller.position2D);
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
