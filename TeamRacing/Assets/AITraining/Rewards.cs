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
    public float outOfBoundsPenalty;
    public float collisionPenalty;
    public float grassPenalty;

    //game state
    public float teamDistance;
    public float lapTime;
    public float teamLapTime;
    public float placement;
    public float teamPlacement;

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

    public float[] ToArray()
    {
        float[] arr = new float[50]
        {
            steeringSmoothness,
            throttleSmoothness,

            outOfBoundsPenalty,
            collisionPenalty,
            grassPenalty,

            teamDistance,
            lapTime,
            teamLapTime,
            placement,
            teamPlacement,


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

            //reserved for later use
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,

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

    public void Reset()
    {
        collided = false;
        onGrass = false;
        outOfBounds = false;
        lastLapTime = 0f;
        finalPlacement = -1;
        finalTeammatePlacement.Clear();
        teammateLapTimes.Clear();
        lastInput = entry.agent.agentInputProvider.getInput();
        this.segmentHandler = this.gameControl.currentSegmentHandler;
    }

    public Rewards CalculateReward()
    {
        Rewards output = new Rewards()
        {
            steeringSmoothness = SteeringSmoothnessReward(),
            throttleSmoothness = ThrottleSmoothnessReward(),

            outOfBoundsPenalty = OutOfBoundsPenalty(),
            collisionPenalty = CollisionPenalty(),
            grassPenalty = GrassPenalty(),

            teamDistance = TeamDistanceReward(),
            lapTime = LapTimeReward(),
            teamLapTime = TeamLapTimeReward(),
            placement = PlacementReward(),
            teamPlacement = TeamPlacementReward(),

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
        };

        // reset it here since its used at 2 places
        lastInput = entry.agent.agentInputProvider.getInput();

        return output;
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
        return -delta;
    }

    // Throttle change penalty
    private float ThrottleSmoothnessReward()
    {
        float delta = Mathf.Abs(entry.agent.agentInputProvider.getInput().Throttle - lastInput.Throttle);
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

    // Lap time reward
    private float LapTimeReward()
    {
        float output = lastLapTime;
        lastLapTime = 0;
        return output;
    }

    // Placement reward
    private float PlacementReward()
    {
        if (finalPlacement == -1 || finalPlacement >= placementPoints.Length) return 0f;

        float output = placementPoints[finalPlacement];
        finalPlacement = -1;
        return output;
    }

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

    private float TeamLapTimeReward()
    {
        float output = 0.0f;
        foreach (int time in teammateLapTimes)
        {
            output += time;
        }
        this.teammateLapTimes.Clear();
        return output;
    }

    private float GrassPenalty()
    {
        float output = 0f;
        if (onGrass)
            output = -1.0f;
        onGrass = false;
        return output;
    }

    private float OutOfBoundsPenalty()
    {
        float output = 0f;
        if (outOfBounds)
            output = -1.0f;
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
        return GetVectorAngleInDirection(entry.controller.speed2D, dir);
    }

    private float DistanceRewardTo(Func<int, Vector2, float> getDistanceFunc)
    {
        return getDistanceFunc(entry.segmentIndex, entry.controller.position2D);
    }

    private float GetVectorMagnitudeInDirection(Vector2 vector, Vector2 direction)
    {
        return Mathf.Abs(Vector2.Dot(vector, direction.normalized)); ;
    }

    private float GetVectorAngleInDirection(Vector2 vector, Vector2 direction)
    {
        if (vector == Vector2.zero || direction == Vector2.zero)
            return 0f;

        float dot = Vector2.Dot(vector.normalized, direction.normalized);
        return Mathf.Abs(Mathf.Acos(Mathf.Clamp(dot, -1f, 1f))); // angle in radians
    }
}
