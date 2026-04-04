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

public struct Joy
{
    public float[] axes;
    public int[] buttons;
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
        zeroRadius = (float) Math.Sqrt(halfWheelBase*halfWheelBase + halfSteeringTrack*halfSteeringTrack);
        innerRadius = (float) (Math.Pow(halfSteeringTrack, 2) + Math.Pow(halfWheelBase, 2)) /
                      (2 * halfSteeringTrack);
    }
    
    protected float TurningRadiusFromAngularInput(float angular_input)
    {
        return (angular_input == 0)
            ? INFINITY
            : (float)(inputCurveFactor * ((1.0 / angular_input) - Math.Sign(angular_input)));
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
        return (float) (((turning_left ? 1 : -1) * 0.5f * Math.PI) - Math.Atan(radius / half_wheel_base));
    }

    protected float Hypot(float a, float b)
    {
        return (float)Math.Sqrt(a * a + b * b);
    }
    
    protected float GetSpeedRatio( float radius, bool left_pivot, float half_steering_track, float half_wheel_base, float zero_radius, float inner_radius)
    {
        if (radius == INFINITY || radius == 0)
        {
            // straight line or turning on the spot, left and right wheels should be the same speed
            return 1.0f;
        }
        double wheel_turn_radius =
            Hypot(radius - ((left_pivot ? 1 : -1) * half_steering_track), half_wheel_base);
        return (float) Math.Abs(wheel_turn_radius / (Math.Abs(radius) < inner_radius ? zero_radius : radius));
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

public class Drive : MonoBehaviour
{
    public float positionForce = 35f;
    public float wheelForce = 10000.0f;
    
    private Dictionary<string, ArticulationBody> articulationBodies = new();

    private PivotDriveController pivotDriveController;
    private StrafeDriveController strafeDriveController;
    private DriveControllerBase currentController;

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
        pivotDriveController = new();
        strafeDriveController = new();

        currentController = pivotDriveController;
    }

    Joy GetCurrentJoy()
    {
        // Read values
        Joy result = new();
        result.axes = new float[6];
        result.buttons = new int[21];
        
        var gamepad = Gamepad.current;
        if (gamepad is not null)
        {
            result.axes[0] = gamepad.leftStick.ReadValue().x;
            result.axes[1] = gamepad.leftStick.ReadValue().y;
            result.axes[2] = gamepad.rightStick.ReadValue().x;
            result.axes[3] = gamepad.rightStick.ReadValue().y;
            result.axes[4] = gamepad.leftTrigger.ReadValue();
            result.axes[5] = gamepad.rightTrigger.ReadValue();

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                currentController = pivotDriveController;
                Debug.Log("Switched to pivot drive controller");
            }
            else if (gamepad.buttonWest.wasPressedThisFrame)
            {
                currentController = strafeDriveController;
                Debug.Log("Switched to strafe drive controller");
            }
        }

        return result;
    }

    Twist GetTwist(Joy joy)
    {
        float linear_x = joy.axes[1];
        float linear_y = joy.axes[0];
        float angular_z = joy.axes[2];

        Twist result = new();
        result.linear.x = linear_x;
        result.linear.y = linear_y;
        result.angular.z = angular_z;

        return result;
    }

    void ApplyPivotCommand(string name, float angleRadians)
    {
        ArticulationDrive jointState = articulationBodies[name].xDrive;
        jointState.forceLimit = positionForce;
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
        Joy joy = GetCurrentJoy();
        Twist twist = GetTwist(joy);
        Commands command = currentController.TwistToCommands(twist);
        ApplyCommands(command);
        //Debug.Log(command.blw);
    }
}
