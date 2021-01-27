using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using Util;

public class TrackGenerator : Singleton<TrackGenerator>
{
    [System.Serializable]
    public struct RoadObj
    {
        public Road.JointKey key;
        public GameObject obj;
    }

    public List<RoadObj> roads;
    
    public Road frontRoad;

    public int turn;
    
    public Vector2 unit;

    private void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (frontRoad == null)
            {
                Generate(Road.JointKey.IRoad);
            }

            else
            {
                Generate(frontRoad.GetNextJoint());
            }
        }
        
        if (Keyboard.current.digit1Key.wasPressedThisFrame) Generate(Road.JointKey.IRoad);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) Generate(Road.JointKey.LRoadL);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) Generate(Road.JointKey.LRoadR);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) Generate(Road.JointKey.URoadL);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) Generate(Road.JointKey.URoadR);
    }

    public void Generate(Road.JointKey jointKey)
    {
        GameObject obj = Instantiate(roads.Find(r => r.key == jointKey).obj, 
            frontRoad != null ? frontRoad.joints.First(j => j.key == jointKey).transform : transform);

        Road road = obj.GetComponent<Road>();

        road.backRoad = frontRoad;

        frontRoad = road;
        
        obj.transform.SetParent(transform);

        switch (turn)
        {
            case 0:
                unit += frontRoad.unit;
                break;
            case 90:
                unit += new Vector2(- frontRoad.unit.y, frontRoad.unit.x);
                break;
            case 180:
                unit += -frontRoad.unit;
                break;
            case 270:
                unit += new Vector2(frontRoad.unit.y, - frontRoad.unit.x);
                break;
        }
        
        turn += (int) jointKey;

        if (turn >= 360) turn -= 360;

        if (turn < 0) turn += 360;
    }
}
