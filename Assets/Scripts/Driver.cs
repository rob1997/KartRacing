using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Driver : MonoBehaviour
{
    public Motor motor;
    
    [HideInInspector] public InputMaster inputMaster;

    private bool _isBraking = false;

    private void Awake()
    {
        inputMaster = new InputMaster();
        inputMaster.Enable();
    }

    private void Start()
    {
        inputMaster.Player.Brake.started += delegate(InputAction.CallbackContext context) { _isBraking = true; };
        inputMaster.Player.Brake.canceled += delegate(InputAction.CallbackContext context) { _isBraking = false; };
    }

    private void Update()
    {
        Vector2 moveVector = inputMaster.Player.Move.ReadValue<Vector2>();
        
        //get inputs
        float acceleration = moveVector.y;
        float direction = moveVector.x;

        motor.Drive(acceleration, direction, _isBraking);
    }
}
