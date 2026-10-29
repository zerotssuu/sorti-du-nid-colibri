using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// A hummingbird Machine Learning Agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the beak")]
    public Transform beakTip;

    [Tooltip("The agent's camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    // The rigidbody of the agent
    new private Rigidbody rigidbody;

    // The flower area that the agent is in
    private FlowerArea flowerArea;

    // The nearest flower to the agent
    private Flower nearestFlower;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // Maximum angle that the bird can pitch up or down
    private const float MaxPitchAngle = 80f;

    // Maximum distance from the beak tip to accept nectar collision
    private const float BeakTipRadius = 0.008f;

    // Whether the agent is frozen (intentionally not flying)
    private bool frozen = false;

    /// <summary>
    /// The amount of nectar the agent has obtained this episode
    /// </summary>
    public float NectarObtained { get; private set; }

    /// <summary>
    /// Prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Resume agent movement and actions
    /// </summary>
    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Move the agent to a safe random position (i.e. does not collide with anything)
    /// If in front of flower, also point the beak at the flower
    /// </summary>
    /// <param name="inFrontOfFlower">Whether to choose a spot in front of a flower</param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Prevent an infinite loop
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // Pick a random flower
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // Position 10 to 20 cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Point beak at flower (bird's head is center of transform)
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // Pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // Pick a random radius from the center of the area
                float radius = UnityEngine.Random.Range(2f, 7f);

                // Pick a random direction rotated around the y axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combine height, radius, and direction to pick a potential position
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Choose and set random starting pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            // Safe position has been found if no colliders are overlapped
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set the position and rotation
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    /// <summary>
    /// Update the nearest flower to the agent
    /// </summary>
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                // No current nearest flower and this flower has nectar, so set to this flower
                nearestFlower = flower;
            }
            else if (flower.HasNectar)
            {
                // Calculate distance to this flower and distance to the current nearest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // If current nearest flower is empty OR this flower is closer, update the nearest flower
                if (!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }
}
