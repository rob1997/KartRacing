using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Road : MonoBehaviour
{
    public enum JointKey
    {
        IRoad = 0,
        LRoadL = 90,
        LRoadR = -90,
        URoadL = 180,
        URoadR = -180,
    }

    public Transform exit;
    
    [System.Serializable]
    public struct Joint
    {
        public JointKey key;
        public Transform transform;
    }

    public JointKey key;
    
    public Vector2 unit;
    
    public List<Joint> joints;

    public Road backRoad;
    
    public List<Transform> wayPoints;

    #region Raycast

    private bool _forwardCast;
    private bool _rightCast;
    private bool _leftCast;
    
    private RaycastHit _forwardHit;
    private RaycastHit _rightHit;
    private RaycastHit _leftHit;
    
    private float _forwardDistance = 0;
    private float _rightDistance = 0;
    private float _leftDistance = 0;
    
    #endregion

    private Vector3 _exitPosition;
    private Vector3 _exitForward;
    private Vector3 _exitRight;
    private Vector3 _exitLeft;

    private const float UnitThreshold = 10f;

    private void Start()
    {
        _exitPosition = exit.position;
        _exitForward = exit.forward;
        _exitRight = exit.right;
        _exitLeft = - exit.right;
        
        _forwardCast = Physics.Raycast(_exitPosition, _exitForward, out _forwardHit);
        
        if (_forwardCast) _forwardDistance = Vector3.Distance(_exitPosition, _forwardHit.point);

        else
        {
            Vector3 exitLocalPosition = exit.localPosition;

            Vector3 rightExitEdgePosition = exitLocalPosition;
            rightExitEdgePosition.x += 2;
            rightExitEdgePosition = exit.TransformPoint(rightExitEdgePosition);
        
            Vector3 leftExitEdgePosition = exitLocalPosition;
            leftExitEdgePosition.x -= 2;
            leftExitEdgePosition = exit.TransformPoint(leftExitEdgePosition);

            _forwardCast = Physics.Raycast(rightExitEdgePosition, _exitForward, out _forwardHit);

            if (_forwardCast) _forwardDistance = Vector3.Distance(rightExitEdgePosition, _forwardHit.point);

            else
            {
                _forwardCast = Physics.Raycast(leftExitEdgePosition, _exitForward, out _forwardHit);

                if (_forwardCast) _forwardDistance = Vector3.Distance(leftExitEdgePosition, _forwardHit.point);
            }
        }
        
        _rightCast = Physics.Raycast(_exitPosition, _exitRight, out _rightHit);

        if (_rightCast) _rightDistance = Vector3.Distance(_exitPosition, _rightHit.point);
        
        
        _leftCast = Physics.Raycast(_exitPosition, _exitLeft, out _leftHit);
        
        if (_leftCast) _leftDistance = Vector3.Distance(_exitPosition, _leftHit.point);

        if ((_forwardCast && _forwardDistance < UnitThreshold) || (_forwardCast && _leftCast && _rightCast))
        {
            TrackGenerator.Instance.frontRoad = backRoad;
            TrackGenerator.Instance.Generate(TrackGenerator.Instance.frontRoad.GetNextJoint());
            
            Destroy(gameObject);
        }

        else
        {
            TrackGenerator.Instance.Generate(GetNextJoint());
        }
    }

    public JointKey GetNextJoint()
    {
        List<JointKey> possibleJoints = new List<JointKey>();
        
        if (!_forwardCast || ((_forwardCast && (_forwardDistance > UnitThreshold)) && (!_leftCast || !_rightCast)))
        {
            possibleJoints.Add(JointKey.IRoad);
        }

        if (!_rightCast || ((_rightCast && (_rightDistance > UnitThreshold)) && (!_forwardCast || !_leftCast)))
        {
            possibleJoints.Add(JointKey.LRoadR);
            possibleJoints.Add(JointKey.URoadR);
        }
        
        if (!_leftCast || ((_leftCast && (_leftDistance > UnitThreshold)) && (!_forwardCast || !_rightCast)))
        {
            possibleJoints.Add(JointKey.LRoadL);
            possibleJoints.Add(JointKey.URoadL);
        }

        JointKey nextJoint = possibleJoints[Random.Range(0, possibleJoints.Count - 1)];
        
        return nextJoint;
    }
}
