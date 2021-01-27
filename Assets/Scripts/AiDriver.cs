using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    
    private string _vehicle = "Vehicle";
    
    //boundary tag
    private string _boundary = "Boundary";

    private Motor _motor;

    //distance to target in previous frame
    private float _previousDistance;
    //least distance left to target
    private float _leastDistance;
    //distance to target in current frame
    private float _currentDistance;
    
    //angle between vehicle's forward & target's forward in previous frame
    private float _previousAngle;
    //angle between vehicle's forward & target's forward in current frame
    private float _currentAngle;

    private InputMaster _inputMaster;

    private float _brake;
    
    private void Awake()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Enable();
        
        _inputMaster.Player.Brake.started += delegate { _brake = 1f; };
        _inputMaster.Player.Brake.canceled += delegate { _brake = 0f; };
    }

    public override void Initialize()
    {
        _motor = GetComponent<Motor>();

        _target = circuit.waypoints[_index].position;

        _previousDistance = CalculateDistance();

        _leastDistance = _previousDistance;

        _previousAngle = CalculateAngle();
    }

    public override void OnEpisodeBegin()
    {
        _motor.rBody.velocity = Vector3.zero;
        _motor.rBody.angularVelocity = Vector3.zero;

        FindRandomIndex();
        
        //get random entry
        Transform entry = circuit.waypoints[_index];
        
        //spawn vehicle in random road location
        _motor.transform.position = entry.position;
        _motor.transform.rotation = entry.rotation;

        _target = circuit.waypoints[++_index].position;
        
        _previousDistance = CalculateDistance();

        _leastDistance = _previousDistance;

        _previousAngle = CalculateAngle();
    }

    void FindRandomIndex()
    {
        _index = Random.Range(0, circuit.waypoints.Count - 1);

        if (circuit.indexes.Contains(_index)) FindRandomIndex();

        else
        {
            circuit.indexes.Add(_index);

            if (circuit.indexes.Count >= 10) circuit.indexes.Clear();
        }
    }

    private void Update()
    {
        Debug.DrawLine(_target + Vector3.up * .5f, _motor.rBody.position + Vector3.up * .5f, Color.green);
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

    public override void Heuristic(float[] actionsOut)
    {
        Vector2 moveVector = _inputMaster.Player.Move.ReadValue<Vector2>();
        
        //get inputs
        float acceleration = moveVector.y;
        float direction = moveVector.x;

        actionsOut[0] = acceleration;
        actionsOut[1] = direction;
        actionsOut[2] = _brake;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //calculate distance in previous frame
        _currentDistance = CalculateDistance();
        
        _currentAngle = CalculateAngle();
        
        //position if _target with respect to vehicle (localized)
        Vector3 localTarget = _motor.rBody.transform.InverseTransformPoint(_target);

        //3 Observations
        sensor.AddObservation(localTarget);
        
        //1 observation
        sensor.AddObservation(_currentDistance);

        //1 Observation
        sensor.AddObservation((_currentAngle));
        
        float increment = _previousDistance - _currentDistance;
        
        float deviation = Mathf.Abs(_previousAngle) - Mathf.Abs(_currentAngle);
        
//        if (increment > 0) //moving forward
//        {
//            if (_currentDistance < _leastDistance)
//            {
//                //add reward if vehicle is closer to checkpoint than ever
//                AddReward(increment);
//
//                _leastDistance = _currentDistance;
//            }
//
//            else //getting closer to checkpoint but not closer than ever
//            {
//                
//            }
//        }
//        
//        else if (increment < 0) //moving backward
//        {
//            AddReward(deviation);
//        }
//
//        else //increment == 0 //stationary
//        {
//            AddReward(-.05f);
//        }

        AddReward(increment + deviation);

        _previousDistance = _currentDistance;
        
        _previousAngle = _currentAngle;
        
        Vector3 localVelocity = transform.InverseTransformDirection(_motor.rBody.velocity);
        
        //1 observation
        sensor.AddObservation(localVelocity.x);
            
        //1 observation
        sensor.AddObservation(localVelocity.z);
            
        //4 observations
        sensor.AddObservation(transform.localRotation.normalized);
    }

    private float CalculateDistance()
    {
        Vector3 targetXZ = _target;
        targetXZ.y = 0;

        Vector3 positionXZ = _motor.rBody.transform.position;
        positionXZ.y = 0;
        
        return Vector3.Distance(targetXZ, positionXZ);   
    }
    
    private float CalculateAngle()
    {
        Vector3 targetForwardXZ = circuit.waypoints[_index].forward;
        targetForwardXZ.y = 0;

        Vector3 positionForwardXZ = _motor.rBody.transform.forward;
        positionForwardXZ.y = 0;
        
        return Vector3.Angle(targetForwardXZ, positionForwardXZ);   
    }
    
    private void OnCollisionEnter(Collision other)
    {
        if (other.collider.CompareTag(_boundary)) AddReward(-.5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (circuit.waypoints[_index].name != other.name) return;
        
        _index++;
            
        if (_index >= circuit.waypoints.Count) _index = 0; //if lap complete reset _index

        _target = circuit.waypoints[_index].position;
            
        //recalculate _currentDistance
        float distance = CalculateDistance();

        _previousDistance = distance + (_previousDistance - _currentDistance);
        
        _currentDistance = distance;

        _leastDistance = _currentDistance;
        
        //recalculate _currentAngle
        float angle = CalculateAngle();

        _previousAngle = angle + (_previousAngle - _currentAngle);
        
        _currentAngle = angle;
    }
}
