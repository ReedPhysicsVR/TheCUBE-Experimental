using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Coordinates the heatmap screens and manages most settings menu interactions
/// </summary>
public class ScreenManager : MonoBehaviour
{
    private ScreenColorManager[] screenManagers;

    [Header("Screen Settings")]
    public int setScreenResolution;
    [Tooltip("The time between heat map updates, in seconds.")]
    public float updateFrequency;
    private int fixedFrameTrigger;
    private bool firstpass;
    public ColorRow[] palettes;
    private int palette_index;
    public InputActionReference swapPallette;

    [Header("Relevant Objects")]
    public GameObject sphere;
    [HideInInspector]
    public PointMovement pointMovement;
    [SerializeField] private Slider maxScaleSlider;
    [SerializeField] private Slider solSlider;
    public GameObject speedLimitSign;
    public InputActionReference openLevelMenu;
    public GameObject levelMenu;

    [Header("Vector Field Settings")]
    public FieldType fieldType;
    public bool magnitudeNotDot;
    private int fframeCount;
    private bool accelerationField;
    [HideInInspector]
    public float fieldScale;
    public float[] fieldOrderOfMag;
    [Tooltip("The max field strengths, X for magnitude, Y for dot, in order by field type enum")]
    public Vector2[] scalingMaximums;
    [HideInInspector]
    public float sliderLowValue;
    public float speedOfLight;
    public Vector2 speedOfLightMinMax;
    public float speedOfLightScaleCurvature;
    [HideInInspector]
    public bool relativistic;

    private void Awake()
    {
        // Adds functions to the controller input action profiles
        if (!swapPallette.action.enabled)
        {
            swapPallette.action.Enable();
        }
        swapPallette.action.performed += SwapColorPalette;
        if (!openLevelMenu.action.enabled)
        {
            openLevelMenu.action.Enable();
        }
        openLevelMenu.action.performed += OpenLevelMenu;

    }

    // Set initial values
    void Start()
    {
        screenManagers = GetComponentsInChildren<ScreenColorManager>();
        firstpass = true;

        palette_index = 0;

        pointMovement = sphere.GetComponent<PointMovement>();

        fframeCount = 0;
        fixedFrameTrigger = Mathf.RoundToInt(updateFrequency / Time.fixedDeltaTime);

        maxScaleSlider.minValue = sliderLowValue;
        maxScaleSlider.maxValue = scalingMaximums[(int)fieldType][Convert.ToInt16(magnitudeNotDot)];

        solSlider.minValue = -1f;
        solSlider.maxValue = 0f;
        speedOfLight = speedOfLightMinMax[1];

        magnitudeNotDot = false;
        relativistic = false;
        accelerationField = false;

        SetFieldEquation();
    }

    void Update()
    {
        // Screens are not completely initialized until after Start runs, 
        // so this sets the heatmaps for the first time.
        if (firstpass)
        {
            SetScreenColors();
            SetFieldType((int)fieldType);
            firstpass = false;
        }
    }

    void FixedUpdate()
    {
        // Enable speed limit sign if charge is moving faster than current speed of light
        if (pointMovement.velocity.magnitude > speedOfLight && !speedLimitSign.activeSelf) speedLimitSign.SetActive(true);
        else if (pointMovement.velocity.magnitude < speedOfLight && speedLimitSign.activeSelf) speedLimitSign.SetActive(false);

        // Updates the heatmaps every few cycles of FixedUpdate to cut down on frame drops
        if (fframeCount == fixedFrameTrigger)
        {
            if (pointMovement.velocity != Vector3.zero)
            {
                SetField(false);
            }
            fframeCount = 0;
        }

        fframeCount++;
    }

    private void OnDestroy()
    {
        // Remove action profiles when no longer needed
        // Need to include this to avoid errors
        swapPallette.action.Disable();
        openLevelMenu.action.Disable();
        swapPallette.action.performed -= SwapColorPalette;
        openLevelMenu.action.performed -= OpenLevelMenu;
    }

    #region UIFunctions

    /// <summary>
    /// Function to be used by a UI button. Sets the field to display the magnitude of the field or the dot product with the display surface
    /// </summary>
    /// <param name="magnotdot">Boolean, true = mag, false = dot</param>
    public void SetMagOrDot(bool magnotdot)
    {
        magnitudeNotDot = magnotdot;
        maxScaleSlider.maxValue = scalingMaximums[(int)fieldType][Convert.ToInt16(magnotdot)];
        SetField(true);
    }

    /// <summary>
    /// Function to be used by a UI button. Sets the field type to display and the appropriate scaling
    /// factors and slider settings for the chosen field type.
    /// </summary>
    /// <param name="i">0 = E, 1 = B, 2 = S</param>
    public void SetFieldType(int i)
    {
        fieldType = (FieldType)i;
        SetFieldScale(sliderLowValue);
        maxScaleSlider.maxValue = scalingMaximums[accelerationField ? i + 4 : i][Convert.ToInt16(magnitudeNotDot)];
        maxScaleSlider.value = maxScaleSlider.minValue;
        SetFieldEquation();
        SetField(true);
    }

    /// <summary>
    /// Function to be used by a UI button. Toggles the display of the acceleration field vs full/velocity field
    /// </summary>
    public void ToggleAccelerationField()
    // function for UI button to manipulate
    {
        accelerationField = !accelerationField;
        SetFieldType((int)fieldType);
    }

    /// <summary>
    /// Function to be used by a UI slider. Sets the scaling of the displayed field.
    /// </summary>
    /// <param name="maxScale"></param>
    public void SetFieldScale(System.Single maxScale)
    // System.Single is the same as a float. It's written this way to allow the UI slider to use it.
    {
        foreach (ScreenColorManager scm in screenManagers) scm.maxFieldScale = maxScale;

        if (pointMovement.velocity == Vector3.zero) SetField(true);
        fieldScale = maxScale;
    }

    /// <summary>
    /// Function to be used by a UI slider. Sets the speed of light and toggles the relativistic/slow assumption equations as needed.
    /// The function sets the speed of light along an exponential curve since the most interesting effects appear at the slowest speeds.
    /// </summary>
    /// <param name="sol">Must be between -1 and 0.</param>
    public void SetSpeedOfLight(System.Single sol)
    // System.Single is the same as a float. It's written this way to allow the UI slider to use it.
    {
        speedOfLight = Mathf.Pow(speedOfLightScaleCurvature, sol) * (speedOfLightMinMax[1] - speedOfLightMinMax[0]) + speedOfLightMinMax[0];
        if (relativistic == (sol == 0))
        {
            relativistic = !relativistic;
            SetFieldEquation();
        }
    }

    public void DebugPrint()
    //Debugging function
    {
        string dbstring = "";
        dbstring += "Field Type: " + fieldType + "\n";
        dbstring += "Dot or Mag: " + (magnitudeNotDot ? "Mag" : "Dot") + "\n";
        dbstring += "Velocity: " + pointMovement.velocity + "\n";
        dbstring += "Acceleration? :" + (accelerationField ? "Yes" : "No") + "\n";
        if (accelerationField)
        {
            dbstring += "Speed of Light: " + speedOfLight + "\n";
            // dbstring += "Relative velocity: " + sphereRelVelocity + "\n";
            dbstring += "Acceleration: " + pointMovement.acceleration + "\n";
        }
        dbstring += "Field Scale Slider Val: " + maxScaleSlider.value + "\n";
        Debug.Log(dbstring);
    }

    #endregion

    #region ControllerButtonFunctions

    /// <summary>
    /// To be used as part of an input action. Allows the user to press a button and switch between color palettes.
    /// </summary>
    /// <param name="context">Required for use with input actions.</param>
    public void SwapColorPalette(InputAction.CallbackContext context)
    // function for controller button (left secondary), swaps color palette
    {
        palette_index += 1;
        if (palette_index >= palettes.Length) palette_index = 0;
        SetScreenColors();
        if (pointMovement.velocity == Vector3.zero) SetField(true);
    }

    /// <summary>
    /// To be used as part of an input action. Allows the user to press a button to open the scene select menu
    /// </summary>
    /// <param name="context"></param>
    public void OpenLevelMenu(InputAction.CallbackContext context)
    {
        levelMenu.SetActive(!levelMenu.activeInHierarchy);
    }

    #endregion

    /// <summary>
    /// Sets the color palette of the heatmaps.
    /// </summary>
    public void SetScreenColors()
    {
        foreach (ScreenColorManager scm in screenManagers)
        {
            scm.screenColors = palettes[palette_index].colors;
        }
    }

    /// <summary>
    /// Updates the heatmaps on either all screens or only the visible screens.
    /// For the sake of performance, if the field is relativistic or acceleration only, only 3 of the screens will update
    /// even if allScreens is set to true.
    /// </summary>
    /// <param name="allScreens">True = Update all screens, False = Update only visible screens</param>
    public void SetField(bool allScreens)
    {
        if (accelerationField || speedOfLight < speedOfLightMinMax[1] || !allScreens)
        {
            for (int i = 0; i < 3; i++)
            {
                screenManagers[i].SetHeatmap(magnitudeNotDot, allScreens);
            }
            return;
        }
        foreach (ScreenColorManager scm in screenManagers)
        {
            scm.SetHeatmap(magnitudeNotDot, allScreens);
        }
    }

    /// <summary>
    /// Selects the appropriate equation to calculate the field vector given the settings and 
    /// sends it to the ScreenColorManagers. For details, see the developer manual.
    /// </summary>
    private void SetFieldEquation()
    {
        Func<Vector3, Vector3> fieldeq = curlyR => Vector3.zero;

        if (!relativistic && !accelerationField)
        {
            switch (fieldType)
            {
                case FieldType.E:
                    fieldeq = curlyR => curlyR.normalized / curlyR.sqrMagnitude;
                    break;
                case FieldType.B:
                    fieldeq = curlyR => -Vector3.Cross(pointMovement.velocity, curlyR.normalized) / curlyR.sqrMagnitude;
                    break;
                case FieldType.Poynting:
                    fieldeq = curlyR =>
                    {
                        return (curlyR.sqrMagnitude * pointMovement.velocity - Vector3.Dot(curlyR, pointMovement.velocity) * curlyR) / Mathf.Pow(curlyR.sqrMagnitude, 3);
                    };
                    break;
            }
        }
        else if (!relativistic)
        {
            Vector3 EaccSlow(Vector3 curlyR)
            {
                return (Vector3.Dot(curlyR.normalized, pointMovement.acceleration) * curlyR.normalized - pointMovement.acceleration) / curlyR.magnitude;
            }
            switch (fieldType)
            {
                case FieldType.E:
                    fieldeq = EaccSlow;
                    break;
                case FieldType.B:
                    fieldeq = curlyR => Vector3.Cross(curlyR.normalized, EaccSlow(curlyR));
                    break;
                case FieldType.Poynting:
                    fieldeq = curlyR => EaccSlow(curlyR).sqrMagnitude * curlyR.normalized - EaccSlow(curlyR) * Vector3.Dot(EaccSlow(curlyR), curlyR.normalized);
                    break;
            }
        }
        else if (!accelerationField)
        {
            Vector3 EFast(Vector3 curlyR)
            {
                Vector3 uvec = speedOfLight * curlyR.normalized - pointMovement.velocity;
                float udotr = Mathf.Pow(1f / Vector3.Dot(uvec, curlyR), 3);
                return (speedOfLight * speedOfLight - pointMovement.velocity.sqrMagnitude) * udotr * curlyR.magnitude * uvec;
            }
            switch (fieldType)
            {
                case FieldType.E:
                    fieldeq = EFast;
                    break;
                case FieldType.B:
                    fieldeq = curlyR => -speedOfLight * Vector3.Cross(curlyR.normalized, EFast(curlyR));
                    break;
                case FieldType.Poynting:
                    fieldeq = curlyR =>
                    {
                        Vector3 efast = EFast(curlyR);
                        return speedOfLight * (-efast.sqrMagnitude * curlyR.normalized + Vector3.Dot(efast, curlyR.normalized) * efast);
                    };
                    break;
            }
        }
        else
        {
            Vector3 EaccFast(Vector3 curlyR)
            {
                Vector3 uvec = speedOfLight * curlyR.normalized - pointMovement.velocity;
                float udotr = Mathf.Pow(1f / Vector3.Dot(uvec, curlyR), 3);
                return udotr * curlyR.magnitude * speedOfLight * speedOfLight * Vector3.Cross(curlyR, Vector3.Cross(uvec, pointMovement.acceleration));

            }
            switch (fieldType)
            {
                case FieldType.E:
                    fieldeq = EaccFast;
                    break;
                case FieldType.B:
                    fieldeq = curlyR => Vector3.Cross(curlyR.normalized, EaccFast(curlyR));
                    break;
                case FieldType.Poynting:
                    fieldeq = curlyR =>
                    {
                        Vector3 Eacc = EaccFast(curlyR);
                        return Eacc.sqrMagnitude * curlyR.normalized - Eacc * Vector3.Dot(Eacc, curlyR.normalized);
                    };
                    break;
            }
        }

        foreach (ScreenColorManager scm in screenManagers) scm.fieldEquation = fieldeq;
    }
}


