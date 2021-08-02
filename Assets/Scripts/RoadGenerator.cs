using System;
using System.Collections;
using System.Collections.Generic;
using ERMG;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoadGenerator : MonoBehaviour
{
    public ERMeshGen meshGen;

    [Space]
    
    public Transform vehicleTransform;

    [Space]
    
    public float lowerRandomAngle;
    public float upperRandomAngle;
    
    public float lowerRandomLength;
    public float upperRandomLength;

    private float _currentDistance;
    private float _previousDistance;
    
    private float _currentAngle;
    private float _previousAngle;
    
    private float _currentDistanceFromMidRoad;
    private float _previousDistanceFromMidRoad;

    public Vector3 Target => meshGen.navPoints[3].position;
    public Vector3 TargetForward => meshGen.navPoints[4].position - Target;
    public float RoadHalfWidth => meshGen.deltaWidth / 2f;
    public Vector3 RoadForward => Target - meshGen.navPoints[2].position;
    
    public float DistanceDelta { get; private set; }
    
    public float AngleDelta { get; private set; }
    
    public float DistanceFromMidRoadDelta { get; private set; }

    public float OffRoad => CalculateDistanceFromMidRoad() - RoadHalfWidth;
    
    //boundary tag
    private string _boundary = "Boundary";

    public void Initialize()
    {
        for (int i = 0; i < 5; i++)
        {
            AddNavPoint(true);
        }

        meshGen.enableMeshBorders = 1;
        meshGen.UpdateData();
        meshGen.leftBorder.tag = _boundary;
        meshGen.rightBorder.tag = _boundary;
        
        InstantiateVehicle();
        
        AddBackCollider();
        
        InitializeDeltas();
    }

    public void InitializeDeltas()
    {
        _currentDistance = CalculateDistance();
        _previousDistance = _currentDistance;

        _currentAngle = CalculateAngle();
        _previousAngle = _currentAngle;

        _currentDistanceFromMidRoad = CalculateDistanceFromMidRoad();
        _previousDistanceFromMidRoad = _currentDistanceFromMidRoad;
    }
    
    public void InstantiateVehicle()
    {
        vehicleTransform.position = meshGen.navPoints[2].position;
//        vehicleTransform.rotation = Quaternion.LookRotation(RoadForward);
        vehicleTransform.rotation = Quaternion.LookRotation(RoadForward) * Quaternion.Euler(0, Random.Range(0, 360f), 0);
    }

    public void Generate()
    {
//        vehicleTransform.position += ((meshGen.navPoints[3].position - vehicleTransform.position).normalized * .05f);
//        
//        vehicleTransform.rotation = Quaternion.Lerp(vehicleTransform.rotation, 
//            Quaternion.LookRotation((meshGen.navPoints[3].position - vehicleTransform.position).normalized), .05f);
        
        if (_currentDistance < RoadHalfWidth)
        {
            AddNavPoint(true);
            
            RemoveNavPoint(false);

            _previousDistance = CalculateDistance();

            _previousAngle = CalculateAngle();

            _previousDistanceFromMidRoad = CalculateDistanceFromMidRoad();
            
            AddBackCollider();
        }

        _currentDistance = CalculateDistance();

        DistanceDelta = _previousDistance - _currentDistance;
        
        _previousDistance = _currentDistance;
        
        _currentAngle = CalculateAngle();

        AngleDelta = _previousAngle - _currentAngle;
        
        _previousAngle = _currentAngle;

        _currentDistanceFromMidRoad = CalculateDistanceFromMidRoad();

        DistanceFromMidRoadDelta = _previousDistanceFromMidRoad - _currentDistanceFromMidRoad;

        _previousDistanceFromMidRoad = _currentDistanceFromMidRoad;
    }

    public float CalculateDistance()
    {
        Vector3 vehiclePositionXz = vehicleTransform.position;
        vehiclePositionXz.y = 0;

        Vector3 targetXz = Target;
        targetXz.y = 0;
        
        return Vector3.Distance(vehiclePositionXz, targetXz);
    }
    
    public float CalculateAngle()
    {
        Vector3 vehicleForward = vehicleTransform.forward;
        vehicleForward.y = 0;

        Vector3 directionToTarget = Target - vehicleTransform.position;
        directionToTarget.y = 0;

        return Vector3.SignedAngle(vehicleForward, directionToTarget, Vector3.up);
    }

    public float CalculateDistanceFromMidRoad()
    {
        Vector3 previousTarget = meshGen.navPoints[2].position;
        Vector3 vehiclePosition = vehicleTransform.position;
        
        Vector3 roadForward = Target - previousTarget;
        roadForward.y = 0;

        Vector3 directionToVehicle = vehiclePosition - previousTarget;
        directionToVehicle.y = 0;

        float angleFromMidRoad = Vector3.Angle(roadForward, directionToVehicle);
        
        previousTarget.y = 0;
        vehiclePosition.y = 0;
        float distanceToVehicle = Vector3.Distance(previousTarget, vehiclePosition);

        return distanceToVehicle * Mathf.Sin(angleFromMidRoad * Mathf.Deg2Rad);
    }

    public float CalculateNextTargetAngle()
    {
        return Vector3.SignedAngle(RoadForward, TargetForward, Vector3.up);
    }

    public void AddNavPoint(bool front)
    {
        int count = meshGen.navPoints.Count;
        
        Vector3 previousPoint = front ? meshGen.navPoints[count - 1].position : meshGen.navPoints[0].position;

        GameObject point = null;

        if (front)
        {
            point = meshGen.CreateNavPoint();
        }
        
        else
        {
            Vector3 zeroPos = meshGen.navPoints[0].position;
            
            meshGen.CreateNavPoint(0);

            meshGen.navPoints[1].transform.position = zeroPos;
            
            point = meshGen.navPoints[0].gameObject;
        }

        float angle = Random.Range(lowerRandomAngle, upperRandomAngle);
        float length = Random.Range(lowerRandomLength, upperRandomLength);

        Vector3 forward = point.transform.forward;
        Vector3 right = point.transform.right;
        
        point.transform.position = previousPoint + 
                                   (length * Mathf.Cos(Mathf.Deg2Rad * angle) * (front ? forward : - forward)) + 
                                   (length * Mathf.Sin(Mathf.Deg2Rad * angle) * (front ? right : - right));
    }

    public void RemoveNavPoint(bool front)
    {
        if (front)
        {
            meshGen.DeleteNavPoint();
        }

        else
        {
            meshGen.DeleteNavPoint(0);
        }
    }

    public void AddBackCollider()
    {
        BoxCollider backCollider = meshGen.navPoints[0].gameObject.AddComponent<BoxCollider>();

        float height = 5;
            
        backCollider.center = Vector3.up * height / 2f;
        backCollider.size = new Vector3(meshGen.deltaWidth, height, 1f);
            
        backCollider.tag = _boundary;
    }
}
