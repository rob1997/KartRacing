using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class AiDriver : Agent
{
    public Circuit circuit;
    
    //checkpoint (current target of vehicle)
    private Vector3 _target;
    
    //index of current target/checkpoint/wayPoint
    private int _index;
    
//    private string _vehicle = "Vehicle";
    
    //boundary tag
    private string _boundary = "Boundary";

    private Motor _motor;

    //distance to target in previous frame
    private float _previousDistance;
    //distance to target in current frame
    private float _currentDistance;
    
    public override void Initialize()
    {
        _motor = GetComponent<Motor>();

        _target = circuit.waypoints[_index].position;

        Vector3 targetXZ = _target;
        targetXZ.y = 0; //disregard y axis

        Vector3 positionXZ = _motor.rBody.transform.position;
        positionXZ.y = 0; //disregard y axis
        
        _previousDistance = Vector3.Distance(targetXZ, positionXZ);
    }

    public override void OnEpisodeBegin()
    {
        _motor.rBody.velocity = Vector3.zero;
        _motor.rBody.angularVelocity = Vector3.zero;

        //get random entry
        Transform entry = circuit.entries[Random.Range(0, circuit.entries.Count - 1)];
        
        //spawn vehicle in random road location
        _motor.transform.position = entry.position;
        _motor.transform.rotation = entry.rotation;
    }

    /// <summary>
    /// Called when an action is received from player input or the neural network
    /// vectorAction[i] represents:
    ///Index 0: move vector forward and backward (+1 = forward, -1 = backward)
    ///Index 1: turn vector left and right (+1 = right, -1 = left)
    ///Index 2: brake vector on and off (0 = off, 1 = on)
    /// </summary>
    /// <param name="vectorAction">The actions to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        _motor.Drive(vectorAction[0], vectorAction[1], vectorAction[2]);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 targetXZ = _target;
        targetXZ.y = 0;

        Vector3 positionXZ = _motor.rBody.transform.position;
        positionXZ.y = 0;
        
        //calculate distance in previous frame
        _currentDistance = Vector3.Distance(targetXZ, positionXZ);
        
        if (_currentDistance < 2f) //2 is a threshold before changing to next checkpoints
        {
            _index++;
            
            if (_index >= circuit.waypoints.Count) _index = 0; //if lap complete reset _index

            _target = circuit.waypoints[_index].position;
        }
        
        //position if _target with respect to vehicle (localized)
        Vector3 localTarget = _motor.rBody.transform.InverseTransformPoint(_target);

        //3 Observations
        sensor.AddObservation(localTarget);
        
        //1 observation
        sensor.AddObservation(_currentDistance);

        //add reward if vehicle is closer to checkpoint and punish if further
        AddReward(_previousDistance - _currentDistance);

        _previousDistance = _currentDistance;
        
        //angle between forward of checkpoint and forward of vehicle
        float angle = Vector3.Angle(_motor.rBody.transform.forward, circuit.waypoints[_index].forward);
        
        //add reward if vehicle is facing checkpoint by an angle less than 90 degrees and punish if more
//        AddReward(1f - Mathf.Abs(angle) / 90f);

        //1 Observation
        sensor.AddObservation(angle);
        
        //1 Observation
        sensor.AddObservation(Mathf.Sign(_motor.rBody.velocity.magnitude)); //is going forward/backward

        Vector3 localVelocity = transform.InverseTransformDirection(_motor.rBody.velocity);
        
        //1 observation
        sensor.AddObservation(localVelocity.x);
            
        //1 observation
        sensor.AddObservation(localVelocity.z);
            
        //4 observations
        sensor.AddObservation(transform.localRotation.normalized);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.collider.CompareTag(_boundary)) AddReward(-.5f);
    }
}
