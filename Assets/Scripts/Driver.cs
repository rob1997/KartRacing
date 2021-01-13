using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Driver : MonoBehaviour
{
    public Motor motor;
    
    [HideInInspector] public InputMaster inputMaster;

    private float _brake = 0f;

    private void Awake()
    {
        inputMaster = new InputMaster();
        inputMaster.Enable();
    }

    private void Start()
    {
        inputMaster.Player.Brake.started += delegate { _brake = 1f; };
        inputMaster.Player.Brake.canceled += delegate { _brake = 0f; };
    }

    private void Update()
    {
        Vector2 moveVector = inputMaster.Player.Move.ReadValue<Vector2>();
        
        //get inputs
        float acceleration = moveVector.y;
        float direction = moveVector.x;

        motor.Drive(acceleration, direction, _brake);
    }
}
