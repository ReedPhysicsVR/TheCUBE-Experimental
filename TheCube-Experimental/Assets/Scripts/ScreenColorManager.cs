using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Handles the heat map texture for each screen.
/// </summary>
public class ScreenColorManager : MonoBehaviour
{
    private Texture2D screentexture;
    private int screenResolution;
    public Color[] screenColors;
    public ScreenManager screenManager;
    private float pixelScale;
    private Vector3 origin;
    private Collider col;
    private int[] axesMap;
    private int[] axesSigns;
    private Transform trans;
    private Vector3[,] screenToWorldCoord;
    public float maxFieldScale;
    private Vector3 screenNormal;
    public System.Func<Vector3, Vector3> fieldEquation;
    private PointMovement pointMovement;

    void Start()
    {
        screenResolution = screenManager.setScreenResolution;
        // instantiating the texture this way ensures the 'pixelated' look, otherwise the texture smooths itself out
        screentexture = new Texture2D(screenResolution, screenResolution, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        GetComponent<Renderer>().material.mainTexture = screentexture;
        screentexture.Apply();
        col = GetComponent<Collider>();
        trans = GetComponent<Transform>();
        origin = trans.position;
        axesMap = new int[] { 0, 1, 2 };
        axesSigns = new int[] { 1, 1, 1 };
        screenToWorldCoord = new Vector3[screenResolution, screenResolution];
        pointMovement = screenManager.pointMovement;

        MapAxes();
        GenScreenToWorldCoord();
    }

    #region InitialOrientationFunctions
    /// <summary>
    /// Finds the mapping from world axes to screen axes. 
    /// The screen texture's x and y coordinates correspond to the screens' transforms' "right" and "forward" directions respectively.
    /// </summary>
    private void MapAxes()
    {
        for (int i = 0; i < 3; i++)
        {
            if ((int)trans.right[i] != 0)
            {
                axesMap[0] = i;
                axesSigns[0] = (int)Mathf.Sign(trans.right[i]);
            }
            else if ((int)trans.forward[i] != 0)
            {
                axesMap[1] = i;
                axesSigns[1] = (int)Mathf.Sign(trans.forward[i]);
            }
            else if ((int)trans.up[i] != 0)
            {
                axesMap[2] = i;
                axesSigns[2] = (int)Mathf.Sign(trans.up[i]);
            }
        }
        screenNormal = Vector3.zero;
        screenNormal[axesMap[2]] = axesSigns[2];
    }

    /// <summary>
    /// Finds the texture's origin and the screen to world coordinate conversion array.
    /// The origin is at the top right of each screen.
    /// </summary>
    private void GenScreenToWorldCoord()
    {
        pixelScale = col.bounds.size[axesMap[0]] / screenResolution;

        for (int i = 0; i < 2; i++) origin[axesMap[i]] = trans.position[axesMap[i]] + (axesSigns[i] * col.bounds.size[axesMap[i]] / 2);
        origin[axesMap[2]] = trans.position[axesMap[2]];

        for (int x = 0; x < screenResolution; x++)
        {
            for (int y = 0; y < screenResolution; y++)
            {
                screenToWorldCoord[x, y] = new Vector3(0, 0, 0);
                screenToWorldCoord[x, y][axesMap[0]] = origin[axesMap[0]] - (x * pixelScale * axesSigns[0]);
                screenToWorldCoord[x, y][axesMap[1]] = origin[axesMap[1]] - (y * pixelScale * axesSigns[1]);
                screenToWorldCoord[x, y][axesMap[2]] = origin[axesMap[2]];
            }
        }
    }

    /// <summary>
    /// Finds the screen location of a world-space position vector.
    /// Not currently used, but may be useful to future developers.
    /// </summary>
    /// <param name="coord">World-space position vector</param>
    /// <returns>The xy coordinates of the world position in the texture.</returns>
    private int[] WorldToScreenCoord(Vector3 coord)
    {
        int[] screenCoord = new int[2];
        for (int i = 0; i < 2; i++) screenCoord[i] = Mathf.FloorToInt(axesSigns[i] * (origin[axesMap[i]] - coord[axesMap[i]]) / pixelScale);
        return screenCoord;
    }
    #endregion

    /// <summary>
    /// Sets the color of an individual pixel of the texture.
    /// </summary>
    /// <param name="x">x-coordinate of the pixel</param>
    /// <param name="y">y-coordinate of the pixel</param>
    /// <param name="mag">The value to be converted to a pixel color.</param>
    /// <param name="magNDot">True to indicate magnitude, false to indicate flux</param>
    private void SetPixelColor(int x, int y, float mag, bool magNDot)
    {
        if (magNDot) mag = (screenColors.Length - 1f) * Mathf.Clamp(mag, 0f, 1f);
        else mag = Mathf.Floor(0.5f * (screenColors.Length - 1f)) * Mathf.Clamp(mag, -1f, 1f) + Mathf.Floor(0.5f * (screenColors.Length - 1f));

        int level = Mathf.FloorToInt(Mathf.Clamp(mag, 0, screenColors.Length - 2));
        screentexture.SetPixel(x, y, Color.Lerp(screenColors[level], screenColors[level + 1], mag - level));
    }

    /// <summary>
    /// Updates the heatmap by calculating the field vector at the position of each pixel and setting the pixel color accordingly.
    /// </summary>
    /// <param name="magNotDot">True = Magnitude, False = Flux</param>
    /// <param name="allScreens">Update all screens (true) or only the visible screens (false).</param>
    public void SetHeatmap(bool magNotDot, bool allScreens)
    {
        if (!allScreens && !GetComponent<Renderer>().isVisible) return;

        float mag;
        Vector3 fieldvec;
        Vector3 curlyR;
        for (int x = 0; x < screentexture.width; x++)
        {
            for (int y = 0; y < screentexture.height; y++)
            {
                curlyR = screenToWorldCoord[x, y] - pointMovement.position;
                //The field equation here is set by ScreenManager.SetFieldEquation()
                fieldvec = screenManager.fieldOrderOfMag[(int)screenManager.fieldType] * fieldEquation(curlyR);
                mag = !magNotDot ? Vector3.Dot(fieldvec, -screenNormal) : fieldvec.magnitude;
                SetPixelColor(x, y, mag * maxFieldScale, magNotDot);
            }
        }
        screentexture.Apply();
    }

}
