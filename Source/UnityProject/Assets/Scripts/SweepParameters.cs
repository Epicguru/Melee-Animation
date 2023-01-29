using UnityEngine;

[CreateAssetMenu(fileName = "SweepParams", menuName = "Sweep Parameters")]
public class SweepParameters : ScriptableObject
{
    [Header("Time step")]
    [Min(0.01f)]
    public float BaseTimeStep = 1f / 60f;

    [Min(1f)]
    public float TimeStepCoefficient = 2f;
    [Min(0)]
    public float TimeStepOffset = 0f;

    [Header("Distance")]
    [Min(0.01f)]
    public float TargetDistance = 0.1f;
    [Range(0f, 1f)]
    public float TargetTolerance = 0.1f;

    [Header("Stability")]
    [Min(float.Epsilon)]
    public float QuitTime = 0.00001f;
    [Min(1)]
    public int MaxBinaryIterations = 50;

    [Header("Other")]
    [Min(0.01f)]
    public float Radius = 1f;
    
}

