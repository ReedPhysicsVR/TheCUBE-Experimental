using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;

public enum PresetMovement
{
    Sin, //0
    Circle, //1
    Breaking, //2
    Fig8, //3
    Helix //4
}

/// <summary>
/// Handles the preset movement information for the particle.
/// </summary>
public class PresetControl : MonoBehaviour
{
    public bool sphereMode;
    public bool displayPathMode;
    public float cycleLength;
    private XRGrabInteractable grabber;
    [HideInInspector] public Vector3 accel;
    [HideInInspector] public Vector3 velocity;
    public PresetMovement presetType;
    public Vector3[] presetOriginPositions;
    [Header("Sin Settings")]
    public float sinAmp;
    [Header("Circle Settings")]
    public float circleRadius;
    [Header("Breaking Settings")]
    public float breakDistance;
    [Header("Figure 8 Settings")]
    public float fig8Width;
    public float fig8Height;
    [Header("Helix Settings")]
    public float helixHeight;
    public float helixWidth;
    public int helixCoils;
    private Vector3[,] motionData;
    private float omega;
    private int timesteps;
    private int stepN;
    private Vector3 xaxis, yaxis, zaxis;
    private Func<float, Vector3> positionFunc, velocityFunc, accelerationFunc;
    private Vector3[][] paths;
    public LineRenderer pathLine;

    private void OnEnable()
    {
        // Disable the ability for the player to interact with the sphere
        grabber = GetComponent<XRGrabInteractable>();
        grabber.enabled = false;

        timesteps = Mathf.RoundToInt(cycleLength / Time.fixedDeltaTime);
        SetPreset(PresetMovement.Sin);

        // OnEnable runs before Start, so need to check if Start has run before displaying the pathLine since Start initializes the pathLine
        if (displayPathMode && pathLine != null && pathLine.positionCount > 3) pathLine.gameObject.SetActive(true);
    }

    void Start()
    {
        xaxis = Vector3.right;
        yaxis = Vector3.up;
        zaxis = Vector3.back;
        velocity = Vector3.zero;
        accel = Vector3.zero;

        SetPresetEqs();
        PreCalcMotionData();

        if (!Debug.isDebugBuild) displayPathMode = false;
        if (displayPathMode)
        {
            GenerateParticlePaths();
            pathLine.gameObject.SetActive(true);
            pathLine.SetPositions(paths[(int)presetType]);
        }
    }

    void FixedUpdate()
    {
        if (stepN >= timesteps) stepN = 0;
        (transform.localPosition, velocity, accel) = (motionData[0, stepN], motionData[1, stepN], motionData[2, stepN]);
        stepN++;
    }

    private void OnDisable()
    {
        //Re-enable the ability for the player to interact with the sphere
        grabber.enabled = true;
        if (displayPathMode && pathLine != null && pathLine.positionCount > 3) pathLine.gameObject.SetActive(false);
    }

    /// <summary>
    /// Function for UI. Changes the preset type, calculates the new motion data, and restarts the movement cycle.
    /// </summary>
    /// <param name="i"></param>
    public void SetPreset(int i)
    {
        presetType = (PresetMovement)i;
        SetPresetEqs();
        PreCalcMotionData();
        stepN = 0;
        if (displayPathMode && pathLine != null && pathLine.positionCount > 3) pathLine.SetPositions(paths[i]);
    }

    /// <summary>
    /// Function for use in script. Changes the preset type, calculates the new motion data, and restarts the movement cycle.
    /// </summary>
    /// <param name="pm"></param>
    public void SetPreset(PresetMovement pm)
    {
        presetType = pm;
        SetPresetEqs();
        PreCalcMotionData();
        stepN = 0;
        if (displayPathMode && pathLine != null && pathLine.positionCount > 3) pathLine.SetPositions(paths[(int)pm]);
    }

    /// <summary>
    /// Sets the equations to use for calculating motion data.
    /// </summary>
    private void SetPresetEqs()
    {
        switch (presetType)
        {
            case PresetMovement.Sin:
                positionFunc = t => presetOriginPositions[0] + sinAmp * Mathf.Sin(omega * t) * yaxis;
                velocityFunc = t => sinAmp * omega * Mathf.Cos(omega * t) * yaxis;
                accelerationFunc = t => -sinAmp * omega * omega * Mathf.Sin(omega * t) * yaxis;
                break;
            case PresetMovement.Circle:
                if (!sphereMode)
                {
                    positionFunc = t => presetOriginPositions[1] + circleRadius * (Mathf.Cos(omega * t) * yaxis + Mathf.Sin(omega * t) * xaxis);
                    velocityFunc = t => circleRadius * omega * (-Mathf.Sin(omega * t) * yaxis + Mathf.Cos(omega * t) * xaxis);
                    accelerationFunc = t => circleRadius * omega * omega * (-Mathf.Cos(omega * t) * yaxis - Mathf.Sin(omega * t) * xaxis);
                }
                else
                {
                    positionFunc = t => presetOriginPositions[1] + circleRadius * (Mathf.Cos(omega * t) * zaxis + Mathf.Sin(omega * t) * xaxis);
                    velocityFunc = t => circleRadius * omega * (-Mathf.Sin(omega * t) * zaxis + Mathf.Cos(omega * t) * xaxis);
                    accelerationFunc = t => circleRadius * omega * omega * (-Mathf.Cos(omega * t) * zaxis - Mathf.Sin(omega * t) * xaxis);
                }
                break;
            case PresetMovement.Breaking:
                positionFunc = t => presetOriginPositions[2] - 4f * breakDistance * ((-t * t / (cycleLength * cycleLength)) + (t / cycleLength)) * xaxis;
                velocityFunc = t => -4f * breakDistance * ((-2f * t / (cycleLength * cycleLength)) + (1f / cycleLength)) * xaxis;
                accelerationFunc = t => 8f * (breakDistance / cycleLength) * xaxis;
                break;
            case PresetMovement.Fig8:
                positionFunc = t => presetOriginPositions[3] + fig8Width * 2f * Mathf.Cos(omega * t) * xaxis + fig8Height * Mathf.Sin(2f * omega * t) * yaxis;
                velocityFunc = t => 2f * omega * (-fig8Width * Mathf.Sin(omega * t) * xaxis + fig8Height * Mathf.Cos(2f * omega * t) * yaxis);
                accelerationFunc = t => -2f * omega * omega * (fig8Width * Mathf.Cos(omega * t) * xaxis + 2f * fig8Height * Mathf.Sin(2f * omega * t) * yaxis);
                break;
            case PresetMovement.Helix:
                positionFunc = t => presetOriginPositions[4] + helixHeight * Mathf.Sin(omega * t) * yaxis + helixWidth * (Mathf.Cos(helixCoils * omega * t) * xaxis + Mathf.Sin(helixCoils * omega * t) * zaxis);
                velocityFunc = t => omega * (helixHeight * Mathf.Cos(omega * t) * yaxis + helixWidth * helixCoils * (-Mathf.Sin(helixCoils * omega * t) * xaxis + Mathf.Cos(helixCoils * omega * t) * zaxis));
                accelerationFunc = t => omega * omega * (-helixHeight * Mathf.Sin(omega * t) * yaxis - helixWidth * helixCoils * helixCoils * (Mathf.Cos(helixCoils * omega * t) * xaxis + Mathf.Sin(helixCoils * omega * t) * zaxis));
                break;
        }
    }

    /// <summary>
    /// Generates the points for the line renderer which displays the path of the preset. 
    /// Originally used to create images for paper, but good for debugging as well.
    /// </summary>
    private void GenerateParticlePaths()
    {
        if (gameObject.transform.parent.Find("PresetPath") == null)
        {
            GameObject line = new("PresetPath", typeof(LineRenderer));
            line.transform.SetParent(gameObject.transform.parent);
            line.transform.localPosition = Vector3.zero;
            pathLine = line.GetComponent<LineRenderer>();
            pathLine.useWorldSpace = false;
            pathLine.loop = true;
        }
        paths = new Vector3[5][];
        pathLine.positionCount = timesteps;

        for (int i = 0; i < 5; i++)
        {
            presetType = (PresetMovement)i;
            SetPresetEqs();
            paths[i] = new Vector3[timesteps];
            for (int step = 0; step < timesteps; step++)
            {
                paths[i][step] = positionFunc(step * Time.fixedDeltaTime);
            }
        }

        presetType = PresetMovement.Sin;
    }

    /// <summary>
    /// Pre-calculates the motion data for the point charge movement using the current set equations.
    /// </summary>
    private void PreCalcMotionData()
    {
        cycleLength = timesteps * Time.fixedDeltaTime;
        motionData = new Vector3[3, timesteps];
        omega = 2f * Mathf.PI / cycleLength;
        float time;
        for (int step = 0; step < timesteps; step++)
        {
            time = step * Time.fixedDeltaTime;
            motionData[0, step] = positionFunc(time);
            motionData[1, step] = velocityFunc(time);
            motionData[2, step] = accelerationFunc(time);
        }
    }

    /// <summary>
    /// Function for UI slider. Calculates the new motion data for the new time period length, in terms of number of FixedUpdate cycles (Time.fixedDeltaTime). 
    /// </summary>
    /// <param name="period">In terms of number of FixedUpdate cycles, not seconds.</param>
    public void OnPeriodChange(System.Single period)
    {
        timesteps = Mathf.RoundToInt(period);
        PreCalcMotionData();
    }

}
