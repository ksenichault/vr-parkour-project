using UnityEngine;
using System;
// using System.Diagnostics;
// using System.Diagnostics;

public class LocomotionTechnique : MonoBehaviour
{
    // Please implement your locomotion technique in this script.
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;

    [Range(0, 10)] public float translationGain = 0.5f;

    public GameObject hmd;
    [Header("Ground Detection")]
    public float groundCheckDistance = 5.0f;
    public float groundOffset = 0.02f;
    public LayerMask groundLayers;
    

    private bool isGrounded = false;
    private float groundY = 0f;
    [Header("Locomotion Tuning")]
    public float sensitivity = 80f;      // how strongly controller swing maps to speed (increased)
    public float speedSmooth = 6f;
    float walkingSpeed = 10.0f;

    [SerializeField] private float leftTriggerValue;
    [SerializeField] private float rightTriggerValue;
     // [SerializeField] private Vector3 startPos;
    // [SerializeField] private Vector3 offset;
    // [SerializeField] private bool isIndexTriggerDown;


    /////////////////////////////////////////////////////////
    // These are for the game mechanism.
    public ParkourCounter parkourCounter;
    public string stage;
    public SelectionTaskMeasure selectionTaskMeasure;

    // Jump physics
    private float gravity = -9.81f;
    private float jumpMagnitude= 10.0f;          
    private float verticalVelocity = 0f;

    float prevLeftY = 0.0f;
    float prevRightY = 0.0f;

    private AudioSource stepAudio;
    private AudioSource propulsionAudio;
    bool canPlayStep = true;


    [Header("Skates (Visual)")]
    public Transform leftSkate;
    public Transform rightSkate;

    // Where skates rest (offsets from body center under HMD)
    public Vector3 leftFootLocalOffset = new Vector3(-0.12f, 0.02f, 0.05f);
    public Vector3 rightFootLocalOffset = new Vector3(0.12f, 0.02f, 0.05f);

    [Header("Skate Animation")]
    public float strideLength = 0.30f;       // total front/back range in meters
    public float strideFrequency = 2.2f;     // max cycles/sec at full speed
    public float minFrequency = 0.6f;        // min cycles/sec at low speed
    public float minSpeedToAnimate = 0.05f;  // minimum velocity to start stride animation (very low)
    public float pushStrideBoost = 0.5f;     // extra stride from arm movement alone

    [Header("Skate Following (Body)")]
    public float skateFollowPos = 18f;   // how fast skates follow target position (increased)
    public float skateFollowRot = 14f;   // how fast skates follow target rotation (increased)
    public float toeOutAngle = 4f;       // subtle outward stance (degrees)

    [Header("Skate Lean (Roll)")]
    public float maxLeanAngle = 18f;
    public float leanPerDegPerSec = 0.04f;
    public float leanSmooth = 8f;

    [Header("Skate Cant (Side Tilt)")]
    public float baseCantAngle = 12f;
    public float pushCantAngle = 10f;
    public float leanBoost = 2.0f;
    public float minCantWhenMoving = 0.35f;

    [Header("Stride Gating")]
    public float pushThreshold = 0.008f; // minimum effort to consider "pushing" (lowered for sensitivity)

    [Header("Wheel Roll (Optional)")]
    public float wheelRadius = 0.04f;
    public Transform[] leftWheels;
    public Transform[] rightWheels;

    // Internal stride state
    private float skatePhase = 0f;
    private float currentStrideLeft = 0f;
    private float currentStrideRight = 0f;

    private GameObject leftFire;
    private GameObject rightFire;

    private GameObject fireEffectPrefab;
    private GameObject stepAudioPrefab;
    private GameObject propulsionAudioPrefab;

    Vector3 velocity = Vector3.zero;

    // For roomscale-follow + wheel roll + lean
    private Vector3 prevBodyCenter;
    private Vector3 prevYawDir = Vector3.forward;
    private float currentLean = 0f;

    void Awake()
    {
        if (fireEffectPrefab == null)
            fireEffectPrefab = Resources.Load<GameObject>("CartoonFire");
        if (stepAudioPrefab == null)
            stepAudioPrefab = Resources.Load<GameObject>("StepAudioPrefab");

        if (propulsionAudioPrefab == null)
            propulsionAudioPrefab = Resources.Load<GameObject>("PropulsionAudioPrefab");

        if (stepAudioPrefab == null) Debug.Log("MYLOG step audio prefab null");
        if (propulsionAudioPrefab == null) Debug.Log("MYLOG propulsion audio prefab null");

        if (stepAudioPrefab != null)
        {
            GameObject step = Instantiate(stepAudioPrefab, transform);
            stepAudio = step.GetComponent<AudioSource>();
        }

        if (propulsionAudioPrefab != null)
        {
            GameObject propObj = Instantiate(propulsionAudioPrefab, transform);
            propulsionAudio = propObj.GetComponent<AudioSource>();
        }

    }

    void Start()
    {
        prevLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        prevRightY = OVRInput.GetLocalControllerPosition(rightController).y;

        prevBodyCenter = GetBodyCenter();
        prevYawDir = GetHmdYawDir();
        if (prevYawDir.sqrMagnitude < 0.0001f) prevYawDir = Vector3.forward;

        SnapSkatesToBody(prevYawDir);

        // Fire FX (children of skates)
        Vector3 leftFireOffset = new Vector3(-0.01f, -0.05f, 0f);
        Vector3 rightFireOffset = new Vector3(0.01f, -0.05f, 0f);

        if (fireEffectPrefab != null && leftSkate != null)
        {
            leftFire = Instantiate(fireEffectPrefab, leftSkate);
            leftFire.transform.localPosition = leftFireOffset;
            leftFire.transform.localRotation = Quaternion.Euler(-180f, 0f, 0f);
            leftFire.SetActive(false);
        }

        if (fireEffectPrefab != null && rightSkate != null)
        {
            rightFire = Instantiate(fireEffectPrefab, rightSkate);
            rightFire.transform.localPosition = rightFireOffset;
            rightFire.transform.localRotation = Quaternion.Euler(-180f, 0f, 0f);
            rightFire.SetActive(false);
        }
    }

    void Update()
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Inputs
        leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, leftController);
        rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, rightController);

        // -----------------------------------------------------------------------------------------------
        // JUMP (both triggers + hands above head)
        Vector3 leftPos = OVRInput.GetLocalControllerPosition(leftController);
        Vector3 rightPos = OVRInput.GetLocalControllerPosition(rightController);

        float headHeight = (hmd != null) ? hmd.transform.position.y : transform.position.y;
        bool handsUp = leftPos.y >= headHeight && rightPos.y >= headHeight;

        if (leftTriggerValue > 0.95f && rightTriggerValue > 0.95f && handsUp)
        {
            verticalVelocity = jumpMagnitude;
            playPropulsionSound();
            if (leftFire != null) leftFire.SetActive(true);
            if (rightFire != null) rightFire.SetActive(true);
        }

     // --- GROUND DETECTION ---
Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;  // Start higher
RaycastHit hit;

// Use SphereCast for reliability
if (Physics.SphereCast(rayOrigin, 0.2f, Vector3.down, out hit, groundCheckDistance, groundLayers))
{
    groundY = hit.point.y;
    isGrounded = transform.position.y <= groundY + groundOffset + 0.05f;
}
else
{
    // Fallback: try without layer mask (detects everything)
    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance))
    {
        groundY = hit.point.y;
        isGrounded = transform.position.y <= groundY + groundOffset + 0.05f;
    }
    else
    {
        isGrounded = false;
    }
}

        // --- GRAVITY AND LANDING ---
        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        else if (verticalVelocity <= 0f)
        {
            verticalVelocity = 0f;
            transform.position = new Vector3(transform.position.x, groundY + groundOffset, transform.position.z);

            if (leftFire != null) leftFire.SetActive(false);
            if (rightFire != null) rightFire.SetActive(false);
            stopPropulsionSound();
        }

        transform.position += new Vector3(0, verticalVelocity * Time.deltaTime, 0);



           // float currentLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        // float currentRightY = OVRInput.GetLocalControllerPosition(rightController).y;

        // float leftDifference = Math.Abs(currentLeftY- prevLeftY);
        // float rightDifference = Math.Abs(currentRightY- prevRightY);

        // float effort = leftDifference + rightDifference;
        // if (effort < 0.01f) effort = 0f;

        // float newSpeed = walkingSpeed * Mathf.Clamp(effort * sensitivity, 0f, 1f);
        // currentSpeed = Mathf.Lerp(currentSpeed, newSpeed, speedSmooth * Time.deltaTime);
         
        // WALK        
        // here im not using 20cm detection, we're always walking no matter the distance of the swinging  
        float currentLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        float currentRightY = OVRInput.GetLocalControllerPosition(rightController).y;

        float leftDifference = Math.Abs(currentLeftY - prevLeftY);
        float rightDifference = Math.Abs(currentRightY - prevRightY);
        float effort = leftDifference + rightDifference;

        // Step sound gating
        float stepThreshold = 0.008f;
        if (effort < stepThreshold)
        {
            effort = 0f;
            canPlayStep = true;
        }
        if (effort > 0f && canPlayStep)
        {
            playStepSound();
            canPlayStep = false;
        }

        // Push gating
        float push01 = 0f;
        if (effort > pushThreshold)
            push01 = Mathf.Clamp01(effort * sensitivity);

        float acceleration = walkingSpeed * push01;

        // Inertia + friction
        float friction = 0.65f;
           // inertia: velocity*= (1-friction * delta t) so that velocity reduces over time
        // we then add the acceleration * delta t * direction of head 
        // which is the current acceleration 
        float damp = Mathf.Clamp01(1f - friction * Time.deltaTime);
        velocity *= damp;

        Vector3 flatForward = GetHmdYawDir();
        velocity += flatForward * acceleration * Time.deltaTime;

        // Apply horizontal movement
        transform.position += velocity * Time.deltaTime;

        prevLeftY = currentLeftY;
        prevRightY = currentRightY;

        // -----------------------------------------------------------------------------------------------
        // SKATE VISUALS
        AnimateSkates(push01);





       // PROF'S CODE
        // if (leftTriggerValue > 0.95f && rightTriggerValue > 0.95f)
        // {
        //     if (!isIndexTriggerDown)
        //     {
        //         isIndexTriggerDown = true;
        //         startPos = (OVRInput.GetLocalControllerPosition(leftController) + OVRInput.GetLocalControllerPosition(rightController)) / 2;
        //     }
        //     offset = hmd.transform.forward.normalized *
        //             (OVRInput.GetLocalControllerPosition(leftController) - startPos +
        //             (OVRInput.GetLocalControllerPosition(rightController) - startPos)).magnitude;
        //     Debug.DrawRay(startPos, offset, Color.red, 0.2f);
        // }
        // else if (leftTriggerValue > 0.95f && rightTriggerValue < 0.95f)
        // {
        //     if (!isIndexTriggerDown)
        //     {
        //         isIndexTriggerDown = true;
        //         startPos = OVRInput.GetLocalControllerPosition(leftController);
        //     }
        //     offset = hmd.transform.forward.normalized *
        //              (OVRInput.GetLocalControllerPosition(leftController) - startPos).magnitude;
        //     Debug.DrawRay(startPos, offset, Color.red, 0.2f);
        // }
        // else if (leftTriggerValue < 0.95f && rightTriggerValue > 0.95f)
        // {
        //     if (!isIndexTriggerDown)
        //     {
        //         isIndexTriggerDown = true;
        //         startPos = OVRInput.GetLocalControllerPosition(rightController);
        //     }
        //    offset = hmd.transform.forward.normalized *
        //             (OVRInput.GetLocalControllerPosition(rightController) - startPos).magnitude;
        //     Debug.DrawRay(startPos, offset, Color.red, 0.2f);
        // }
        // else
        // {
        //     if (isIndexTriggerDown)
        //     {
        //         isIndexTriggerDown = false;
        //         offset = Vector3.zero;
        //     }
        // }

        if (OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four))
        {
            if (parkourCounter != null && parkourCounter.parkourStart)
                transform.position = parkourCounter.currentRespawnPos;
        }
    }

    // Audio helpers
    void playStepSound()
    {
        if (stepAudio != null) stepAudio.Play();
    }

    void playPropulsionSound()
    {
        if (propulsionAudio != null && !propulsionAudio.isPlaying)
            propulsionAudio.Play();
    }

    void stopPropulsionSound()
    {
        if (propulsionAudio != null)
            propulsionAudio.Stop();
    }

    // ==========================
    // Skate helpers
    // ==========================
    private Vector3 GetHmdYawDir()
    {
        Vector3 dir = Vector3.forward;
        if (hmd != null)
        {
            dir = Vector3.ProjectOnPlane(hmd.transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        }
        return dir;
    }

    private Vector3 GetBodyCenter()
    {
        Vector3 center = transform.position;

        if (hmd != null)
        {
            Vector3 hmdOffset = Vector3.ProjectOnPlane(hmd.transform.position - transform.position, Vector3.up);
            center += hmdOffset;
        }

        center.y = transform.position.y;
        return center;
    }

 private void SnapSkatesToBody(Vector3 yawDir)
{
    if (leftSkate == null || rightSkate == null) return;

    Vector3 center = GetBodyCenter();
    Vector3 right = Vector3.Cross(Vector3.up, yawDir).normalized;

    Vector3 leftTargetXZ = center + right * leftFootLocalOffset.x + yawDir * leftFootLocalOffset.z;
    Vector3 rightTargetXZ = center + right * rightFootLocalOffset.x + yawDir * rightFootLocalOffset.z;

    // Raycast for actual ground height
    float leftGroundY = GetGroundHeightAt(leftTargetXZ);
    float rightGroundY = GetGroundHeightAt(rightTargetXZ);

    leftSkate.position = new Vector3(leftTargetXZ.x, leftGroundY + leftFootLocalOffset.y, leftTargetXZ.z);
    rightSkate.position = new Vector3(rightTargetXZ.x, rightGroundY + rightFootLocalOffset.y, rightTargetXZ.z);

    Quaternion yawQ = Quaternion.LookRotation(yawDir, Vector3.up);
    Quaternion toeL = Quaternion.AngleAxis(-toeOutAngle, Vector3.up);
    Quaternion toeR = Quaternion.AngleAxis(+toeOutAngle, Vector3.up);

    leftSkate.rotation = yawQ * toeL;
    rightSkate.rotation = yawQ * toeR;
}

  private void AnimateSkates(float push01)
{
    if (leftSkate == null || rightSkate == null) return;

    // --- 1) Calculate current speed ---
    float planarSpeed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
    float speed01 = Mathf.Clamp01(planarSpeed / Mathf.Max(walkingSpeed, 0.001f));

    // --- 2) Get directions ---
    Vector3 center = GetBodyCenter();
    Vector3 yawDir = GetHmdYawDir();
    if (yawDir.sqrMagnitude < 0.0001f) yawDir = Vector3.forward;
    yawDir.Normalize();

    Vector3 right = Vector3.Cross(Vector3.up, yawDir).normalized;

    // Movement direction for stride (use velocity if moving, else yaw)
    Vector3 strideDir = yawDir;
    if (velocity.sqrMagnitude > 0.01f)
    {
        strideDir = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
        if (strideDir.sqrMagnitude < 0.0001f) strideDir = yawDir;
    }

    // --- 3) Compute stride offsets based on VELOCITY + PUSH EFFORT ---
    float targetStrideLeft = 0f;
    float targetStrideRight = 0f;

    float activity = Mathf.Max(speed01, push01 * pushStrideBoost);
    
    if (planarSpeed > minSpeedToAnimate || push01 > 0.01f)
    {
        float hz = Mathf.Lerp(minFrequency, strideFrequency, activity);
        skatePhase += hz * Mathf.PI * 2f * Time.deltaTime;
        
        if (skatePhase > Mathf.PI * 100f)
            skatePhase -= Mathf.PI * 100f;

        float speedAmplitude = strideLength * 0.5f * speed01;
        float pushAmplitude = strideLength * 0.5f * push01 * pushStrideBoost;
        float amplitude = Mathf.Max(speedAmplitude, pushAmplitude);
        
        if (activity > 0.01f)
            amplitude = Mathf.Max(amplitude, strideLength * 0.15f * activity);

        targetStrideLeft = Mathf.Sin(skatePhase) * amplitude;
        targetStrideRight = Mathf.Sin(skatePhase + Mathf.PI) * amplitude;
    }
    else
    {
        skatePhase = Mathf.Lerp(skatePhase, 0f, 3f * Time.deltaTime);
    }

    float strideSmooth = 12f;
    currentStrideLeft = Mathf.Lerp(currentStrideLeft, targetStrideLeft, strideSmooth * Time.deltaTime);
    currentStrideRight = Mathf.Lerp(currentStrideRight, targetStrideRight, strideSmooth * Time.deltaTime);

    // --- 4) Calculate BASE positions (without Y - we'll raycast for that) ---
    Vector3 leftBaseXZ = center 
        + right * leftFootLocalOffset.x 
        + yawDir * leftFootLocalOffset.z;

    Vector3 rightBaseXZ = center 
        + right * rightFootLocalOffset.x 
        + yawDir * rightFootLocalOffset.z;

    // Add stride offset along movement direction
    Vector3 leftTargetXZ = leftBaseXZ + strideDir * currentStrideLeft;
    Vector3 rightTargetXZ = rightBaseXZ + strideDir * currentStrideRight;

    // --- 5) RAYCAST to find actual ground height for each skate ---
    float leftGroundY = GetGroundHeightAt(leftTargetXZ);
    float rightGroundY = GetGroundHeightAt(rightTargetXZ);

    // Position skates ON TOP of the detected ground
    Vector3 leftTarget = new Vector3(leftTargetXZ.x, leftGroundY + leftFootLocalOffset.y, leftTargetXZ.z);
    Vector3 rightTarget = new Vector3(rightTargetXZ.x, rightGroundY + rightFootLocalOffset.y, rightTargetXZ.z);

    // --- 6) Smooth position interpolation ---
    leftSkate.position = Vector3.Lerp(leftSkate.position, leftTarget, skateFollowPos * Time.deltaTime);
    rightSkate.position = Vector3.Lerp(rightSkate.position, rightTarget, skateFollowPos * Time.deltaTime);

    // --- 7) Activity factor for cant ---
    float activity01 = Mathf.Max(push01, speed01);
    float cantFactor = (activity01 > 0.01f)
        ? Mathf.Lerp(0f, 1f, Mathf.Max(minCantWhenMoving, activity01))
        : 0f;

    // --- 8) Turning lean ---
    float signedTurn = Vector3.SignedAngle(prevYawDir, yawDir, Vector3.up);
    float turnRate = signedTurn / Mathf.Max(Time.deltaTime, 0.0001f);

    float targetLean = Mathf.Clamp(-turnRate * leanPerDegPerSec, -maxLeanAngle, maxLeanAngle) * speed01;
    currentLean = Mathf.Lerp(currentLean, targetLean, leanSmooth * Time.deltaTime);
    prevYawDir = yawDir;

    // --- 9) Build final rotations ---
    Quaternion yawQ = Quaternion.LookRotation(yawDir, Vector3.up);

    Quaternion toeL = Quaternion.AngleAxis(-toeOutAngle, Vector3.up);
    Quaternion toeR = Quaternion.AngleAxis(+toeOutAngle, Vector3.up);

    float turningLean = currentLean * leanBoost;
    float cant = (baseCantAngle + pushCantAngle * push01) * cantFactor;

    Quaternion rollLeft = Quaternion.AngleAxis(-cant + turningLean, Vector3.forward);
    Quaternion rollRight = Quaternion.AngleAxis(+cant + turningLean, Vector3.forward);

    Quaternion leftRotTarget = yawQ * toeL * rollLeft;
    Quaternion rightRotTarget = yawQ * toeR * rollRight;

    leftSkate.rotation = Quaternion.Slerp(leftSkate.rotation, leftRotTarget, skateFollowRot * Time.deltaTime);
    rightSkate.rotation = Quaternion.Slerp(rightSkate.rotation, rightRotTarget, skateFollowRot * Time.deltaTime);

    // --- 10) Wheel roll ---
    Vector3 delta = center - prevBodyCenter;
    float planarDistance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
    prevBodyCenter = center;

    RollWheels(leftWheels, planarDistance);
    RollWheels(rightWheels, planarDistance);
}

// NEW HELPER METHOD - Add this to your class
private float GetGroundHeightAt(Vector3 position)
{
    Vector3 rayOrigin = new Vector3(position.x, transform.position.y + 1f, position.z);
    RaycastHit hit;
    
    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 2f, groundLayers))
    {
        return hit.point.y;
    }
    
    // Fallback to current ground level if raycast misses
    return groundY;
}
    private void RollWheels(Transform[] wheels, float distance)
    {
        if (wheels == null || wheels.Length == 0) return;
        if (wheelRadius <= 0.0001f) return;

        float degrees = (distance / wheelRadius) * Mathf.Rad2Deg;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            wheels[i].Rotate(Vector3.right, degrees, Space.Self);
        }
    }

    void OnTriggerEnter(Collider other)
    {

        // These are for the game mechanism.
        if (other.CompareTag("banner"))
        {
            stage = other.gameObject.name;
            parkourCounter.isStageChange = true;
        }
        else if (other.CompareTag("objectInteractionTask"))
        {
            selectionTaskMeasure.isTaskStart = true;
            selectionTaskMeasure.scoreText.text = "";
            selectionTaskMeasure.partSumErr = 0f;
            selectionTaskMeasure.partSumTime = 0f;
            // rotation: facing the user's entering direction
            float tempValueY = other.transform.position.y > 0 ? 12 : 0;
            Vector3 tmpTarget = new(hmd.transform.position.x, tempValueY, hmd.transform.position.z);
            selectionTaskMeasure.taskUI.transform.LookAt(tmpTarget);
            selectionTaskMeasure.taskUI.transform.Rotate(new Vector3(0, 180f, 0));
            selectionTaskMeasure.taskStartPanel.SetActive(true);
        }
        else if (other.CompareTag("coin"))
        {
            parkourCounter.coinCount += 1;
            GetComponent<AudioSource>().Play();
            other.gameObject.SetActive(false);
        }
                // These are for the game mechanism.
    }
}