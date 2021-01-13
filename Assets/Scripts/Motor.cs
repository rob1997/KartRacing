using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Motor : MonoBehaviour
{
    [System.Serializable]
    public struct Wheel
    {
        public WheelCollider collider;
        //wheel mesh/separate transform from wheel collider
        //used for rotating mesh
        public Transform meshTransform;
        //is wheel a front wheel
        public bool isFront;
        //is wheel on the right relative to the steering wheel
        public bool isRight;
    }

    //all wheels
    public List<Wheel> wheels;
    //car rigid-body
    public Rigidbody rBody;
    public Transform centerOfMass;
    
    //wheel rotation force
    public float torque = 200f;
    //max steer angle of front wheels
    public float maxSteerAngle = 30f;
    //max brake torque
    public float maxBrakeTorque = 5000f;
    //max anti-roll force/ force that stops car from rolling
    public float maxAntiRollForce = 5000f;
    
    void Start()
    {
        //set center of mass
        rBody.centerOfMass = centerOfMass.localPosition;
    }

    private void FixedUpdate()
    {
        //anti-roll
        GroundWheels(true);
        GroundWheels(false);
    }

    public void Drive(float acceleration, float direction, float brake)
    {
        acceleration = Mathf.Clamp(acceleration, -1f, 1f);
        direction = Mathf.Clamp(direction, -1f, 1f);
        brake = Mathf.Clamp(brake, 0f, 1f);
        
        wheels.ForEach(wheel =>
        {
            //apply torque
            wheel.collider.motorTorque = acceleration * torque;
            
            //apply brake
            wheel.collider.brakeTorque = brake * maxBrakeTorque;
            
            if (wheel.isFront)
            {
                //steer front wheels based on direction value
                wheel.collider.steerAngle = direction * maxSteerAngle;
            }

            //rotate wheel mesh
            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);

            wheel.meshTransform.position = position;
            wheel.meshTransform.rotation = rotation;
        });
    }

    /// <summary>
    /// anti-roll function that stops car from rolling
    /// </summary>
    /// <param name="isFront">ground front wheels or not</param>
    void GroundWheels(bool isFront)
    {
        WheelCollider leftCollider = wheels.FirstOrDefault(w => !w.isRight && w.isFront == isFront).collider;
        WheelCollider rightCollider = wheels.FirstOrDefault(w => w.isRight && w.isFront == isFront).collider;

        //how far the left wheel has traveled from the ground along it's suspension
        float travelLeft = 1f;
        float travelRight = 1f;

        bool isLeftGrounded = leftCollider.GetGroundHit(out WheelHit hit);
        
        if (isLeftGrounded)
        {
            travelLeft = (-leftCollider.transform.InverseTransformPoint(hit.point).y - leftCollider.radius) /
                         leftCollider.suspensionDistance;
        }
        
        bool isRightGrounded = rightCollider.GetGroundHit(out hit);
        
        if (isRightGrounded)
        {
            travelRight = (-rightCollider.transform.InverseTransformPoint(hit.point).y - rightCollider.radius) /
                         rightCollider.suspensionDistance;
        }

        float antiRollForce = (travelLeft - travelRight) * maxAntiRollForce;

        if (isLeftGrounded)
        {
            Transform leftColliderTransform = leftCollider.transform;
            rBody.AddForceAtPosition(leftColliderTransform.up * - antiRollForce, leftColliderTransform.position);
        }
        
        if (isRightGrounded)
        {
            Transform rightColliderTransform = rightCollider.transform;
            rBody.AddForceAtPosition(rightColliderTransform.up * antiRollForce, rightColliderTransform.position);
        }
    }
}
