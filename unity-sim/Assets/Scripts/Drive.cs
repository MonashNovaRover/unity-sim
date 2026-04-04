using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Robotics.UrdfImporter;
using UnityEngine;
using UnityEngine.InputSystem;

struct Twist
{
    public Vector3 Linear;
    public Vector3 Angular;
}

struct Joy
{
    public float[] axes;
    public int[] buttons;
}

struct WheelCommand
{
    public float flw, frw, blw, brw;
    public float flp, frp, blp, brp;
}

public class Drive : MonoBehaviour
{
    const float INFINITY = Single.PositiveInfinity;
    public float wheelBase = 0.95752883f;
    public float steeringTrack = 0.81564001f;
    
    public float inputCurveFactor = 2.0f;
    public float driveMaxVelocity = 5.0f;
  
    float halfSteeringTrack, halfWheelBase, zeroRadius, innerRadius;
    
    public float positionForce = 35f;
    public float wheelForce = 10000.0f;
    
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
      halfSteeringTrack = 0.5f * steeringTrack;
      halfWheelBase = 0.5f * wheelBase;
      zeroRadius = (float) Math.Sqrt(halfWheelBase*halfWheelBase + halfSteeringTrack*halfSteeringTrack);
      innerRadius = (float) (Math.Pow(halfSteeringTrack, 2) + Math.Pow(halfWheelBase, 2)) /
                      (2 * halfSteeringTrack);
      
      GetArticulationBodiesOfChildren();
    }

    Joy GetCurrentJoy()
    {
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
        }

        return result;
    }

    Twist GetTwist(Joy joy)
    {
        float linear_x = joy.axes[1];
        float linear_y = joy.axes[0];
        float angular_z = joy.axes[2];

        Twist result = new();
        result.Linear.x = linear_x;
        result.Linear.y = linear_y;
        result.Angular.z = angular_z;

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

    void ApplyCommand(WheelCommand command)
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
        WheelCommand command = PivotDriveTwistToCommand(twist);
        ApplyCommand(command);
        //Debug.Log(command.blw);
    }
    
    float TurningRadiusFromAngularInput(float angular_input)
    {
      return (angular_input == 0)
        ? INFINITY
        : (float)(inputCurveFactor * ((1.0 / angular_input) - Math.Sign(angular_input)));
    }
    
    float GetAngularFromRadiusAndSpeed(
      float radius, float speed, bool turning_left, float zero_radius, float inner_radius)
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
    
    float GetPivotAngleFromRadius(
      double radius, bool left_pivot, bool turning_left, double half_steering_track,
      double half_wheel_base)
    {
      if (radius == INFINITY)
      {
        return 0.0f;  // straight line, no pivot angle
      }
      radius -= (left_pivot ? 1 : -1) * half_steering_track;
      return (float) (((turning_left ? 1 : -1) * 0.5f * Math.PI) - Math.Atan(radius / half_wheel_base));
    }

    float Hypot(double a, double b)
    {
      return (float)Math.Sqrt(a * a + b * b);
    }
    
    float GetSpeedRatio(
      double radius, bool left_pivot, double half_steering_track, double half_wheel_base,
      double zero_radius, double inner_radius)
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
    
    WheelCommand PivotDriveTwistToCommand(Twist twist_msg)
    { 
        float linear_input = twist_msg.Linear.x;
        float angular_input = -twist_msg.Angular.z; //TODO: Fix this -1 pls 
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

        WheelCommand result = new();
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
