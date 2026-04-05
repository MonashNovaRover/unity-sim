using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Robotics.UrdfImporter;
using UnityEngine;
using UnityEngine.InputSystem;

public struct Twist
{
    public Vector3 linear;
    public Vector3 angular;
}

public struct Commands
{
    public float flw, frw, blw, brw;
    public float flp, frp, blp, brp;
}

public abstract class DriveControllerBase
{
    const float INFINITY = Single.PositiveInfinity;
    protected float wheelBase = 0.95752883f;
    protected float steeringTrack = 0.81564001f;
    
    protected float inputCurveFactor = 2.0f;
  
    protected float halfSteeringTrack, halfWheelBase, zeroRadius, innerRadius;

    protected DriveControllerBase()
    {
        halfSteeringTrack = 0.5f * steeringTrack;
        halfWheelBase = 0.5f * wheelBase;
        zeroRadius = Mathf.Sqrt(halfWheelBase*halfWheelBase + halfSteeringTrack*halfSteeringTrack);
        innerRadius = (Mathf.Pow(halfSteeringTrack, 2) + Mathf.Pow(halfWheelBase, 2)) /
                      (2 * halfSteeringTrack);
    }
    
    protected float TurningRadiusFromAngularInput(float angular_input)
    {
        return (angular_input == 0)
            ? INFINITY
            : (inputCurveFactor * ((1.0f / angular_input) - Mathf.Sign(angular_input)));
    }
    
    protected float GetAngularFromRadiusAndSpeed(float radius, float speed, bool turning_left, float zero_radius, float inner_radius)
    {
        int dir = turning_left ? 1 : -1;
        if (radius == INFINITY)
        {
            return 0.0f;  // straight line, no angular velocity
        }
        if (Math.Abs(radius) < inner_radius)
        {
            return (speed / zero_radius) * dir;  // turning on the spot
        }
        return speed / radius;
    }
    
    protected float GetPivotAngleFromRadius(float radius, bool left_pivot, bool turning_left, float half_steering_track, float half_wheel_base)
    {
        if (radius == INFINITY)
        {
            return 0.0f;  // straight line, no pivot angle
        }
        radius -= (left_pivot ? 1 : -1) * half_steering_track;
        return (((turning_left ? 1 : -1) * 0.5f * Mathf.PI) - Mathf.Atan(radius / half_wheel_base));
    }

    protected float Hypot(float a, float b)
    {
        return Mathf.Sqrt(a * a + b * b);
    }
    
    protected float GetSpeedRatio( float radius, bool left_pivot, float half_steering_track, float half_wheel_base, float zero_radius, float inner_radius)
    {
        if (radius == INFINITY || radius == 0)
        {
            // straight line or turning on the spot, left and right wheels should be the same speed
            return 1.0f;
        }
        float wheel_turn_radius =
            Hypot(radius - ((left_pivot ? 1 : -1) * half_steering_track), half_wheel_base);
        return Mathf.Abs(wheel_turn_radius / (Mathf.Abs(radius) < inner_radius ? zero_radius : radius));
    }

    public abstract Commands TwistToCommands(Twist twist_msg);
}

public class PivotDriveController : DriveControllerBase
{
    private float driveMaxVelocity = 5.0f;
    public override Commands TwistToCommands(Twist twist_msg)
    {
        float linear_input = twist_msg.linear.x;
        float angular_input = -twist_msg.angular.z; //TODO: Fix this -1 pls 
        bool turning_left;
        float turning_radius;
        
        // Manual operation: left stick controls speed and right stick controls the pivot angle
        // Process raw angular input through a curve to calculate the turning radius
        // Prioritise keeping turning radius over speed
        turning_radius = TurningRadiusFromAngularInput(angular_input);
        turning_left = turning_radius == 0 ? angular_input > 0 : turning_radius > 0;

        float speed = linear_input * driveMaxVelocity;
        float requested_speed = speed;
        
        // Calculate the angular velocity based on the limited speed
        float angular_velocity = GetAngularFromRadiusAndSpeed(
            turning_radius, speed, turning_left, zeroRadius, innerRadius);
        
        // Calculate commands
        float left_angle = GetPivotAngleFromRadius(
            turning_radius, true, turning_left, halfSteeringTrack, halfWheelBase);
        float right_angle = GetPivotAngleFromRadius(
            turning_radius, false, turning_left, halfSteeringTrack, halfWheelBase);
        float left_ratio = GetSpeedRatio(
            turning_radius, true, halfSteeringTrack, halfWheelBase, zeroRadius, innerRadius);
        float right_ratio = GetSpeedRatio(
            turning_radius, false, halfSteeringTrack, halfWheelBase, zeroRadius, innerRadius);
        float left_speed = speed * left_ratio;
        float right_speed = speed * right_ratio;

        Commands result = new();
        result.flw = left_speed;
        result.blw = left_speed;
        result.frw = right_speed;
        result.brw = right_speed;
        result.flp = left_angle;
        result.blp = -left_angle;
        result.frp = right_angle;
        result.brp = -right_angle;

        return result;
    }
}

public class StrafeDriveController : DriveControllerBase
{
    private float driveMaxVelocity = 3.0f;
    public override Commands TwistToCommands(Twist twist_msg)
    {
        float linear_input = twist_msg.linear.y;  // lateral velocity
        float linear_velocity = linear_input * driveMaxVelocity;
        
        // Calculate comamnds
        Commands result = new();
        result.blw = linear_velocity;
        result.flw = linear_velocity;
        result.brw = -linear_velocity;
        result.frw = -linear_velocity;
        
        // Set pivots to be parallel sideways
        // Angles are set at +- 90 degrees due to the offset angle
        result.blp = -0.5f * Mathf.PI;
        result.brp = 0.5f * Mathf.PI;
        result.flp = -0.5f * Mathf.PI;
        result.frp = 0.5f * Mathf.PI;

        return result;
    }
}

public class Teleop
{
    // Params, a la teleop_drive_joy.yaml
    const float speedLimitMin = 0.05f;
    const float speedLimitMax = 1.0f;
    const float initialSpeed = 0.1f;
    const float speedChangeFineVal = 0.025f;
    const float speedChangeCoarseVal = 0.1f;
    const float handbrakeSpeedMultiplier = 0.6f;
    const float triggerPressedThreshold = 0.1f;
    
    bool locked = true;
    float speed = initialSpeed;
    
    private PivotDriveController pivotDriveController;
    private StrafeDriveController strafeDriveController;
    private DriveControllerBase currentController;

    public Teleop()
    {
        pivotDriveController = new();
        strafeDriveController = new();

        currentController = pivotDriveController;
    }
    private void ChangeSpeed(float delta)
    {
        if (!locked)
        {
            speed = Mathf.Clamp(speed + delta, speedLimitMin, speedLimitMax);
            Debug.Log("Speed: " + speed.ToString("F3"));
        }
    }
    public Commands ProcessGamepadAndGetCommands()
    {
        var gamepad = Gamepad.current;
        if (gamepad is null)
        {
            Debug.Log("No gamepad found");
            return new();
        }
        
        // Do button presses
        if (gamepad.startButton.wasPressedThisFrame)
        {
            locked = false;
            Debug.Log("Gamepad unlocked");
        }

        if (gamepad.selectButton.wasPressedThisFrame)
        {
            locked = true;
            Debug.Log("Gamepad locked");
        }
        
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            currentController = pivotDriveController;
            Debug.Log("Switched to pivot drive controller");
        }
        
        if (gamepad.buttonWest.wasPressedThisFrame)
        {
            currentController = strafeDriveController;
            Debug.Log("Switched to strafe drive controller");
        }
        
        if (gamepad.buttonEast.wasPressedThisFrame)
        {
            Debug.Log("ACKERMANN NOT SUPPORTED");
        }

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            Debug.Log("TANK MODE NOT SUPPORTED");
        }

        if (gamepad.dpad.left.isPressed)
        {
            ChangeSpeed(-speedChangeFineVal);
        }
        
        if (gamepad.dpad.right.wasPressedThisFrame)
        {
            ChangeSpeed(speedChangeFineVal);
        }

        if (gamepad.dpad.up.wasPressedThisFrame)
        {
            ChangeSpeed(speedChangeCoarseVal);
        }

        if (gamepad.dpad.down.wasPressedThisFrame)
        {
            ChangeSpeed(-speedChangeCoarseVal);
        }

        if (!locked)
        {
            Twist twist = new();
            twist.linear.x = gamepad.leftStick.ReadValue().y;
            twist.linear.y = gamepad.leftStick.ReadValue().x;
            twist.angular.z = gamepad.rightStick.ReadValue().x;

            twist.linear.x *= speed;
            twist.linear.y *= speed;

            // Handbrake
            if (Mathf.Abs(gamepad.rightTrigger.value) > triggerPressedThreshold)
            {
                twist.linear.x *= handbrakeSpeedMultiplier;
                twist.linear.y *= handbrakeSpeedMultiplier;
            }

            Commands result = currentController.TwistToCommands(twist);
            return result;
        }

        return new();
    }
}

public class Drive : MonoBehaviour
{
    public float wheelForce = 10000.0f;
    private Teleop teleop = new();
    
    private Dictionary<string, ArticulationBody> articulationBodies = new();

    void GetArticulationBodiesOfChildren()
    {
        UrdfJoint[] urdfJoints = GetComponentsInChildren<UrdfJoint>();
        foreach (UrdfJoint urdfJoint in urdfJoints)
        {
            articulationBodies.Add(urdfJoint.jointName, urdfJoint.GetComponent<ArticulationBody>());
        }
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetArticulationBodiesOfChildren();
    }

    void ApplyPivotCommand(string name, float angleRadians)
    {
        ArticulationDrive jointState = articulationBodies[name].xDrive;
        jointState.forceLimit = 10.0f;
        jointState.damping = 2500.0f;
        jointState.target = Mathf.Rad2Deg * angleRadians;
        jointState.driveType = ArticulationDriveType.Target;
        articulationBodies[name].xDrive = jointState;
    }

    void ApplyDriveCommand(string name, float radiansPerSecond)
    {
      ArticulationDrive jointState = articulationBodies[name].xDrive;
      jointState.stiffness = 0.0f;
      jointState.damping = 10000f;
      jointState.forceLimit = wheelForce;
      jointState.targetVelocity = Mathf.Rad2Deg * radiansPerSecond;
      jointState.driveType = ArticulationDriveType.Velocity;
      articulationBodies[name].xDrive = jointState;
    }

    void ApplyCommands(Commands command)
    {
        ApplyPivotCommand("flp", command.flp);
        ApplyPivotCommand("blp", command.blp);
        ApplyPivotCommand("frp", command.frp);
        ApplyPivotCommand("brp", command.brp);
        
        ApplyDriveCommand("flw", command.flw);
        ApplyDriveCommand("blw", command.blw);
        ApplyDriveCommand("frw", command.frw);
        ApplyDriveCommand("brw", command.brw);
    }
    // Update is called once per frame
    void Update()
    {
        Commands command = teleop.ProcessGamepadAndGetCommands();
        ApplyCommands(command);
    }
}
