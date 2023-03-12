using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;


//TODO: remove back collider implementation with -ve reward for back movement
//TODO: remove boundaries with -ve reward for straying off road
//TODO: remove raycasts
public class AiDriver : Agent
{
    public RoadGenerator roadGenerator;
    
    //boundary tag
    private string _boundary = "Boundary";

    private Motor _motor;
    
    public override void Initialize()
    {
        roadGenerator.Initialize();
        
        _motor = GetComponent<Motor>();
    }

    public override void OnEpisodeBegin()
    {
        _motor.rBody.velocity = Vector3.zero;
        _motor.rBody.angularVelocity = Vector3.zero;

        roadGenerator.InstantiateVehicle();
        
        roadGenerator.InitializeDeltas();
    }

    private void Update()
    {
        Debug.DrawLine(roadGenerator.Target + Vector3.up * .5f, _motor.rBody.position + Vector3.up * .5f, Color.green);
    }

    /// <summary>
    /// Called when an action is received from player input or the neural network
    /// vectorAction[i] represents:
    ///Index 0: move vector forward and backward (+1 = forward, -1 = backward)
    ///Index 1: turn vector left and right (+1 = right, -1 = left)
    ///Index 2: brake vector on and off (0 = off, 1 = on)
    /// </summary>
    /// <param name="buffers">The actions to take</param>
    public override void OnActionReceived(ActionBuffers buffers)
    {
        float[] vectorAction = buffers.ContinuousActions.Array;
        
        _motor.Drive(vectorAction[0], vectorAction[1], vectorAction[2]);
        
        float speed = Vector3.Dot(roadGenerator.RoadForward, _motor.rBody.velocity);

        if (speed > 0)
        {
            //is it facing the road while moving in it's direction
            //the more it faces the road the more rewarded it gets
            float normalizedAlignment = 1f - Vector3.Angle(roadGenerator.RoadForward, _motor.rBody.transform.forward) / 180f;
            AddReward((speed * (1f + normalizedAlignment)) * .1f);
        }

        else if (speed < 0)
        {
            AddReward(roadGenerator.AngleDelta);
        }
        
        //how further is vehicle from mid road, -ve if outside road
        float normalizedDistanceFromMidRoad =
            1f - (roadGenerator.CalculateDistanceFromMidRoad() / roadGenerator.RoadHalfWidth);
        //punish agent if outside of road
        if (normalizedDistanceFromMidRoad < 0)
        {
            //AddReward(normalizedDistanceFromMidRoad * .1f);
            //AddReward(roadGenerator.DistanceFromMidRoadDelta);
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        roadGenerator.Generate();

        //position of _target with respect to vehicle (localized)
        Vector3 localTarget = _motor.rBody.transform.InverseTransformPoint(roadGenerator.Target);
        localTarget.y = 0;

        //3 Observations
        sensor.AddObservation(localTarget);
        
        //1 observation
        sensor.AddObservation(roadGenerator.CalculateDistance());

        //add reward if vehicle is closer to checkpoint and punish if further
        //if (roadGenerator.DistanceDelta > 0) AddReward(roadGenerator.DistanceDelta);

        //1 Observation
        sensor.AddObservation(roadGenerator.CalculateAngle() / 180f);
        
        //1 Observation
        sensor.AddObservation(roadGenerator.CalculateNextTargetAngle() / 45f);
        
        //1 Observation
        sensor.AddObservation(roadGenerator.CalculateDistanceFromMidRoad());
        //how further is vehicle from mid road, -ve if outside road
        //sensor.AddObservation(1f - (roadGenerator.CalculateDistanceFromMidRoad() / roadGenerator.RoadHalfWidth));
        
        //1 Observation
        sensor.AddObservation(Mathf.Sign(_motor.rBody.velocity.magnitude)); //is going forward/backward

        Vector3 localVelocity = transform.InverseTransformDirection(_motor.rBody.velocity);
        
        //3 observation
        sensor.AddObservation(localVelocity);
            
        //4 observations
        //sensor.AddObservation(transform.localRotation.normalized);
    }
    
    private void OnCollisionEnter(Collision other)
    {
        if (other.collider.CompareTag(_boundary)) AddReward(-.2f);
    }
    
    private void OnCollisionStay(Collision other)
    {
        if (other.collider.CompareTag(_boundary)) AddReward(-.2f);
    }
}
