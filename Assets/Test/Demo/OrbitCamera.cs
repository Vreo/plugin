// ----------------------------------------------------------------------------
// Programmed by Christian Jungen
// chrisfx.jungen@gmail.com
// ----------------------------------------------------------------------------

using UnityEngine;
using System.Collections;

public class OrbitCamera: MonoBehaviour
{
	[Tooltip("This is the look at object")] public GameObject orbitTarget;
	[Tooltip("This is the camera used for this script")] public GameObject orbitCamera;
    [Tooltip("Initial distance to the camera")] public float orbitCamDistance = 10.0f;
	public float orbitCamDistanceMin = 3.0f;
	public float orbitCamDistanceMax = 100.0f;
	private float orbitCamDistanceOrg;
	public Vector2 rotation = Vector2.zero;
	private Vector2 rotationOrg;

    public enum STEERINGMODE
    {
        TWO_FINGERS_ZOOM_THREE_FINGERS_PAN,
        TWO_FINGERS_ZOOM_AND_PAN,
    }
    public STEERINGMODE steeringMode = STEERINGMODE.TWO_FINGERS_ZOOM_AND_PAN;
    public bool blocked = false;

	public float rotationSensitivity = 100.0f; // sensitivity of finger panning
	public float rotationFriction = 2.5f; // rotationFriction for deaccelerating the rotation

	public float angleLimitUpper = 87.5f;
	public float angleLimitLower = 1.0f;

	public float panningSensitivity = 0.01f;

    public float mouseZoomSensitivity = 1f;

    public float doubleClickDiffTime = 0.33f;
	private float lastClickTime = 10e37f;
	private Vector2 lastDoublClickClickPos = Vector2.zero;

	private Vector2 fingerDragPosStart;
	private Vector2 rotationPosStart;
	private Vector2 rotationPosEnd;
	private Vector2 rotationSpeed;
	private Vector2 rotationEuler;

	public Vector3 panVector = Vector3.zero;

	private float systemTime = 0.0f;
	private float fingerTouchStartTime = 0.0f;
	private float fingerTouchEndTime = 0.0f;
	private bool isMultitouchDevice = false;
	private int touchCount = 0;
	private Vector2[] touchPositions = new Vector2[ 5 ];

    // states of navigation
	private enum NAVSTATE
	{
		IDLE,
		ROTATE,
		ZOOM,
		PAN,
        ZOOM_AND_PAN,
	}
	private NAVSTATE navState = NAVSTATE.IDLE;
	private NAVSTATE lastNavState = NAVSTATE.IDLE;
	private int lastTouchCount = 0;

	private enum INPUTSTATE
	{
		RELEASED,
		JUSTPRESSED,
		KEEPPRESSING,
		JUSTRELEASED,
	}
	private INPUTSTATE inputState = INPUTSTATE.RELEASED;   

	// ==============================================================================
	// Start
	// ==============================================================================
	public void Awake()
	{      
		rotationOrg = rotation; // copy these values for a reset
		orbitCamDistance = Mathf.Clamp( orbitCamDistance, orbitCamDistanceMin, orbitCamDistanceMax );
		orbitCamDistanceOrg = orbitCamDistance;

		if ( orbitCamera == null )
			orbitCamera = Camera.main.gameObject;

		Reset();
		systemTime = 0.0f;

	}


	// ==============================================================================
	// RotationReset
	// ==============================================================================
	private void RotationReset()
	{
		fingerDragPosStart = Vector2.zero;
		rotationPosStart = Vector2.zero;
		rotationPosEnd = Vector2.zero;
		rotationSpeed = Vector2.zero;
		rotationEuler = Vector2.zero;
		fingerTouchStartTime = 0.0f;
		fingerTouchEndTime = 0.0f;

		rotationEuler.x = 0.0f;
		rotationEuler.y = 0.0f;

        mouseWheelValue = 0.0f;
	}


	// ==============================================================================
	// Reset
	// ==============================================================================
	private void Reset()
	{
		RotationReset();
		rotation = rotationOrg; // restore these values for a reset
		orbitCamDistance = orbitCamDistanceOrg;

		panVector = Vector3.zero;

		navState = NAVSTATE.IDLE;
		lastNavState = NAVSTATE.IDLE;
		lastTouchCount = 0;
		lastClickTime = -10e37f;
		inputState = INPUTSTATE.RELEASED;

	}


	private Vector2 mousePosition = Vector2.zero;
	private bool inputPressed = false;
	private bool inputPressedLast = false;
	private float mouseWheelValue = 0.0f;

	// ==============================================================================
	// LateUpdate
	// ==============================================================================
	public void LateUpdate()
	{
		if (
			(blocked == false && 
			UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() == false) || // blocked by UI?
		    inputState == INPUTSTATE.KEEPPRESSING // already holding
		   ) 
        {
            systemTime += Time.deltaTime;

            HandleInputDevice();
            HandleInput();

            RotationPhysics();

            orbitCamera.transform.eulerAngles = new Vector3( rotationEuler.x, rotationEuler.y, 0.0f );
            orbitCamera.transform.position = orbitTarget.transform.position - (orbitCamera.transform.forward * orbitCamDistance) + panVector;
        }
            
	}


	private float zoomStartorbitCamDistance = 0.0f;
	private float zoomStartTouchDistance = 0.0f;
	private Vector2 panStartTouch = Vector2.zero;
	private Vector2 panEndTouch = Vector2.zero;
	private Vector2 panDiffTouch = Vector2.zero;
	
	// ==============================================================================
	// HandleInput
	// ==============================================================================
	private void HandleInput()
	{
		// ---
		// --- Get the current state of the input device ----------------------
		// ---
		inputState = INPUTSTATE.RELEASED;

		if ( inputPressedLast == true )
			inputState = INPUTSTATE.KEEPPRESSING;

		if ( inputPressed == true && inputPressedLast == false )
		{
			inputPressedLast = true;
			inputState = INPUTSTATE.JUSTPRESSED;
		}

		// --- device button Released ----
		if ( inputPressed == false && inputPressedLast == true )
		{
			inputPressedLast = false;
			inputState = INPUTSTATE.JUSTRELEASED;
		}

		navState = NAVSTATE.IDLE;

        // --- handle multitouch device ----
        if ( isMultitouchDevice == true )
        {
            if ( steeringMode == STEERINGMODE.TWO_FINGERS_ZOOM_THREE_FINGERS_PAN )
            {
                if ( touchCount <= 1 )
                    navState = NAVSTATE.ROTATE;
                else if ( touchCount == 2 )
                    navState = NAVSTATE.ZOOM;
                else if ( touchCount > 2 )
                    navState = NAVSTATE.PAN;
            }
            else
            {
                if ( touchCount <= 1 )
                    navState = NAVSTATE.ROTATE;
                else if ( touchCount >= 2 )
                    navState = NAVSTATE.ZOOM_AND_PAN;
            }
        }
        // --- handle singletouch device ----
        else
        {
            if ( touchCount <= 1 )
            {
				if ( Input.GetMouseButton(1) == false ) // right mouse button clicked?
                    navState = NAVSTATE.ROTATE;
                else
                    navState = NAVSTATE.PAN;
            }
            if ( mouseWheelValue != 0.0f )
                navState = NAVSTATE.ZOOM;
        }

		// --- handle the device state ----------------------------------------

		// ---
		// --- ROTATE ---
		// ---
		if ( navState == NAVSTATE.ROTATE )
		{
			if ( inputState == INPUTSTATE.JUSTPRESSED )
				SetRotationStart( mousePosition.x, mousePosition.y );
			else if ( inputState == INPUTSTATE.KEEPPRESSING )
				SetRotationCurrent( mousePosition.x, mousePosition.y );
			else if ( inputState == INPUTSTATE.JUSTRELEASED )
				RotationEnded();
		}
		// ---
		// --- ZOOM ---
		// ---
		else if ( navState == NAVSTATE.ZOOM )
		{
            RotationStop();
            if ( isMultitouchDevice == true )
            {
                if ( inputState == INPUTSTATE.JUSTPRESSED || lastNavState != navState )
                {
                    zoomStartorbitCamDistance = orbitCamDistance;
                    zoomStartTouchDistance = (touchPositions[ 1 ] - touchPositions[ 0 ]).magnitude; // distance between the 2 touch points
                }
                else if ( inputState == INPUTSTATE.KEEPPRESSING )
                {
                    float zoomEndTouchDistance = (touchPositions[ 1 ] - touchPositions[ 0 ]).magnitude; // distance between the 2 touch points
                    float scale = (float)zoomStartTouchDistance / (float)zoomEndTouchDistance; // scale factor
                    orbitCamDistance = zoomStartorbitCamDistance * scale;
                }
            }
            else
            {
                orbitCamDistance -= mouseWheelValue * mouseZoomSensitivity;
            }
            orbitCamDistance = Mathf.Clamp( orbitCamDistance, orbitCamDistanceMin, orbitCamDistanceMax );
		}

		// ---
		// --- PAN ---
		// ---
		else if ( navState == NAVSTATE.PAN )
		{
            RotationStop();
			if ( inputState == INPUTSTATE.JUSTPRESSED || lastNavState != navState )
			{
                if ( isMultitouchDevice == true )
                    panStartTouch = GetCenterOfTouches();
                else
                    panStartTouch = mousePosition;
			}
			else if ( inputState == INPUTSTATE.KEEPPRESSING )
			{
                if ( isMultitouchDevice == true )
                    panEndTouch = GetCenterOfTouches();
                else
                    panEndTouch = mousePosition;
				panDiffTouch = panEndTouch - panStartTouch;
                panStartTouch = panEndTouch;
				panVector += orbitCamera.transform.right * (-panDiffTouch.x * panningSensitivity);
				panVector += orbitCamera.transform.up * (panDiffTouch.y * panningSensitivity);
			}
		}
        // ---
        // --- ZOOM & PAN (multitouch only!) ---
        // ---
        else if ( navState == NAVSTATE.ZOOM_AND_PAN )
        {
            RotationStop();
            if ( inputState == INPUTSTATE.JUSTPRESSED || lastNavState != navState )
            {
                zoomStartorbitCamDistance = orbitCamDistance;
                zoomStartTouchDistance = (touchPositions[ 1 ] - touchPositions[ 0 ]).magnitude; // distance between the 2 touch points
                panStartTouch = GetCenterOfTouches();
            }
            else if ( inputState == INPUTSTATE.KEEPPRESSING )
            {
                float zoomEndTouchDistance = (touchPositions[ 1 ] - touchPositions[ 0 ]).magnitude; // distance between the 2 touch points
                float scale = (float)zoomStartTouchDistance / (float)zoomEndTouchDistance; // scale factor
                orbitCamDistance = zoomStartorbitCamDistance * scale;

                panEndTouch = GetCenterOfTouches();

                panDiffTouch = panEndTouch - panStartTouch;
                panStartTouch = panEndTouch;
                panVector += orbitCamera.transform.right * (-panDiffTouch.x * panningSensitivity);
                panVector += orbitCamera.transform.up * (panDiffTouch.y * panningSensitivity);

            }
            orbitCamDistance = Mathf.Clamp( orbitCamDistance, orbitCamDistanceMin, orbitCamDistanceMax );
        }

		// ---
		// --- DOUBLE CLICK ---
		// ---

		// register double clicks
		if ( lastTouchCount == 1 && inputState == INPUTSTATE.JUSTRELEASED )
		{
			if ( systemTime <= lastClickTime + doubleClickDiffTime && // double click within defined time?
				(mousePosition-lastDoublClickClickPos).magnitude < (Screen.height / 20.0f) ) // and the click distance is within a reasonable distance
			{
				Reset();
				return; // no further processing
			}
			lastClickTime = systemTime;
			lastDoublClickClickPos = mousePosition;
		}
		
		// remember this as lastState
		lastNavState = navState;
		lastTouchCount = touchCount;

	}


	// ==============================================================================
	// HandleInputDevice
	// ==============================================================================
	private void HandleInputDevice()
	{
        // check, if we have a multitouch device or Unity Remote running
        if ( Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android )
            isMultitouchDevice = true;
#if UNITY_EDITOR
#if UNITY_5_0
        if ( UnityEditor.EditorApplication.isRemoteConnected == true ) 
            isMultitouchDevice = true;
#endif
#endif

        if ( isMultitouchDevice == true )
        {
            inputPressed = false;

            if ( Input.multiTouchEnabled )
            {
                touchCount = Input.touchCount;
                if ( touchCount > touchPositions.Length ) // we have a limit of touches
                    touchCount = touchPositions.Length;

                if ( touchCount == 1 )
                {
                    mousePosition = Input.GetTouch( 0 ).position;
                    mousePosition.y = Screen.height - mousePosition.y; // invert y position
                    inputPressed = true;
                }
                else if ( touchCount >= 2 )
                {
                    for (int i = 0; i < touchCount; i++)
                    {
                        touchPositions[ i ] = Input.GetTouch( i ).position;
                        touchPositions[ i ].y = Screen.height - touchPositions[ i ].y; // invert y position
                    }
                    inputPressed = true;
                }
            }
        }
        else
        {
            mousePosition = Input.mousePosition;
            mousePosition.y = Screen.height - mousePosition.y; // invert y position

			inputPressed = Input.GetMouseButton( 0 ) | Input.GetMouseButton( 1 );
            touchCount = 0;
            if ( inputPressed )
                touchCount = 1;

            mouseWheelValue = Input.GetAxis( "Mouse ScrollWheel" );
        }

	}


	// ==============================================================================
	// RotationPhysics
	// ==============================================================================
	private void RotationPhysics()
	{
		float deltaTime = Time.deltaTime;
		rotation.x += rotationSpeed.x * deltaTime; // accelerate by speed
		rotation.y += rotationSpeed.y * deltaTime;
		rotationSpeed.x *= 1.0f - rotationFriction * deltaTime; // apply rotationFriction (slowdown speed useing deltaTime)
		rotationSpeed.y *= 1.0f - rotationFriction * deltaTime;

		// limit the angles at the poles to avoid the gimbal lock
        float limitAngle, limit;
		limitAngle = angleLimitUpper;
		limit = limitAngle * (float)Screen.height / rotationSensitivity;
		if ( rotation.y > limit )
		{
			rotation.y = limit;
			rotationSpeed.y = 0.0f; // stop moving
		}
        limitAngle = angleLimitLower;
        limit = limitAngle * (float)Screen.height / rotationSensitivity;
		if ( rotation.y < limit )
		{
			rotation.y = limit;
			rotationSpeed.y = 0.0f; // stop moving
		}

		// update angles
		rotationEuler.y = rotation.x * rotationSensitivity;
		rotationEuler.x = rotation.y * rotationSensitivity;

	}

	// ==============================================================================
	// SetRotationStart
	// Set rotation start position
	// ==============================================================================
	private void SetRotationStart( float xPos, float yPos )
	{
		xPos /= (float)Screen.width; // convert to normalized coords
        yPos /= (float)Screen.height;

		rotationPosStart = new Vector2( xPos, yPos );
		fingerDragPosStart = rotation;
		rotationSpeed = Vector2.zero;

		fingerTouchStartTime = systemTime;

	}

	// ==============================================================================
	// SetRotationCurrent
	// Set rotation current position
	// ==============================================================================
	private void SetRotationCurrent( float xPos, float yPos )
	{
        xPos /= (float)Screen.width; // convert to normalized coords
        yPos /= (float)Screen.height;

		rotationPosEnd = new Vector2( xPos, yPos );
		rotation.x = (xPos - rotationPosStart.x) + fingerDragPosStart.x;
		rotation.y = (yPos - rotationPosStart.y) + fingerDragPosStart.y;

		// speed is zero while dragging
		rotationSpeed = Vector2.zero;

	}


	// ==============================================================================
	// RotationEnded
	// ==============================================================================
	private void RotationEnded()
	{
		fingerTouchEndTime = systemTime;

		rotationSpeed.x = 0.0f;
		rotationSpeed.y = 0.0f;

		if ( fingerTouchEndTime - fingerTouchStartTime > 0.0f )
		{
			rotationSpeed.x = ( rotationPosEnd.x - rotationPosStart.x ) / ( fingerTouchEndTime - fingerTouchStartTime );
			rotationSpeed.y = ( rotationPosEnd.y - rotationPosStart.y ) / ( fingerTouchEndTime - fingerTouchStartTime );
		}

	}

	// ==============================================================================
	// RotationStop
	// ==============================================================================
	public void RotationStop()
	{
		rotationSpeed.x = 0.0f;
		rotationSpeed.y = 0.0f;

	}


	// ==============================================================================
	// GetCenterOfTouches
	// ==============================================================================
	private Vector2 GetCenterOfTouches()
	{
		Vector2 centre = Vector2.zero; // calc center of the touches
		for ( int i=0; i<touchCount; i++ )
			centre += touchPositions[ i ];
		centre /= (float)touchCount;

		return centre;

	}

}



