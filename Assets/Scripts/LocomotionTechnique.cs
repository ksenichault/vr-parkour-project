using UnityEngine;
using System;


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
    
    // for walking and jumping
    private bool isGrounded = false;
    private float groundY = 0f;
    [Header("Locomotion Tuning")]
    public float sensitivity = 80f;     
    public float speedSmooth = 6f;
    float walkingSpeed = 10.0f;

    [SerializeField] private float leftTriggerValue;
    [SerializeField] private float rightTriggerValue;



    // These are for the game mechanism.
    public ParkourCounter parkourCounter;
    public string stage;
    public SelectionTaskMeasure selectionTaskMeasure;

    // Jump physics
   private float gravity = -9.81f;
    [Header("Jump Settings")]
    public float jumpMagnitude = 13f;  
    private float verticalVelocity = 0f;
    private bool wasGrounded = true;  

    float prevLeftY = 0.0f;
    float prevRightY = 0.0f;

    private AudioSource stepAudio;
    private AudioSource propulsionAudio;
    bool canPlayStep = true;


    [Header("Skates (Visual)")]
    public Transform leftSkate;
    public Transform rightSkate;

    public Vector3 leftFootLocalOffset = new Vector3(-0.12f, 0.03f, 0.05f);
    public Vector3 rightFootLocalOffset = new Vector3(0.12f, 0.03f, 0.05f);

    [Header("Skate Animation")]
    public float strideLength = 0.30f;       
    public float strideFrequency = 2.2f;     
    public float minFrequency = 0.6f;        
    public float minSpeedToAnimate = 0.05f;  
    public float pushStrideBoost = 0.5f;     

    [Header("Skate Following (Body)")]
    public float skateFollowPos = 18f;  
    public float skateFollowRot = 14f;   
    public float toeOutAngle = 4f;       

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
    public float pushThreshold = 0.008f;


    // Internal stride state
    private float skatePhase = 0f;
    private float currentStrideLeft = 0f;
    private float currentStrideRight = 0f;

    private GameObject leftFire;
    private GameObject rightFire;

    // SOUNDS 
    private GameObject fireEffectPrefab;
    private GameObject stepAudioPrefab;
    private GameObject propulsionAudioPrefab;

    Vector3 velocity = Vector3.zero;

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

        // Initialize ground detection 
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance))
        {
            groundY = hit.point.y;
        }
        else
        {
            groundY = transform.position.y;
        }

        prevBodyCenter = GetBodyCenter();
        prevYawDir = GetHmdYawDir();
        if (prevYawDir.sqrMagnitude < 0.0001f) prevYawDir = Vector3.forward;

        SnapSkatesToBody(prevYawDir);

        // FIRE EFFECT
        // creates instances to each roller skate
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
        // Inputs
    leftTriggerValue  = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, leftController);
    rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, rightController);

     
      //  ground detection
    Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
    RaycastHit hit;
    if (Physics.SphereCast(rayOrigin, 0.2f, Vector3.down, out hit, groundCheckDistance, groundLayers))
    {
        groundY = hit.point.y;
        isGrounded = transform.position.y <= groundY + groundOffset + 0.05f;
    }
    else
    {
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
        // JUMP 
        Vector3 leftPos = OVRInput.GetLocalControllerPosition(leftController);
        Vector3 rightPos = OVRInput.GetLocalControllerPosition(rightController);

        //  local head height 
        float headLocalY = (hmd != null) ? hmd.transform.localPosition.y : 0f;

        bool handsUp = leftPos.y >= headLocalY && rightPos.y >= headLocalY;

        bool canJump = isGrounded && verticalVelocity <= 0.1f;

        // if we press buttons on both controllers + hands above headset + grounded and vertical velocity=0
        if (leftTriggerValue > 0.75f && rightTriggerValue > 0.75f && handsUp && canJump)
        {
            verticalVelocity = jumpMagnitude;
            isGrounded = false;  
            playPropulsionSound();
            if (leftFire != null) leftFire.SetActive(true);
            if (rightFire != null) rightFire.SetActive(true);
        }

        // in the air, we apply gravity
        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        // if we finished jumping, we snap the position to the ground, and stop the sound and fire effect
        else if (verticalVelocity <= 0f)
        {
            verticalVelocity = 0f;
            transform.position = new Vector3(transform.position.x, groundY + groundOffset, transform.position.z);

            if (!wasGrounded)
            {
                if (leftFire != null) leftFire.SetActive(false);
                if (rightFire != null) rightFire.SetActive(false);
                stopPropulsionSound();
            }
        }

        wasGrounded = isGrounded; 

        transform.position += new Vector3(0, verticalVelocity * Time.deltaTime, 0);

         
        // WALK        
        // we compute the difference between the controllers' position and the previous positio
        // it gives us the strength of a step 
        float currentLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        float currentRightY = OVRInput.GetLocalControllerPosition(rightController).y;

        float leftDifference = Math.Abs(currentLeftY - prevLeftY);
        float rightDifference = Math.Abs(currentRightY - prevRightY);
        float effort = leftDifference + rightDifference;

        float stepThreshold = 0.008f; // threshold to avoid walking if we move slightly the controllers
        if (effort < stepThreshold)
        {
            effort = 0f;
            canPlayStep = true; // this boolean makes sure we don't call the playStepSound every time we move, but only when the sound is done playing, to avoid overwhelming the player
        }
        // Step sound 
        if (effort > 0f && canPlayStep)
        {
            playStepSound();
            canPlayStep = false;
        }

        // we transform our effort computed above to a push variable, multiplying by our variable sensitivity
        float push01 = 0f;
        if (effort > pushThreshold)
            push01 = Mathf.Clamp01(effort * sensitivity);

        // we get the acceleration
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

        transform.position += velocity * Time.deltaTime;

        prevLeftY = currentLeftY;
        prevRightY = currentRightY;

        // Skates
        AnimateSkates(push01);


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

    // Skate helpers
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

    //  Calculate current speed 
    float planarSpeed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
    float speed01 = Mathf.Clamp01(planarSpeed / Mathf.Max(walkingSpeed, 0.001f));

    // Get directions
    Vector3 center = GetBodyCenter();
    Vector3 yawDir = GetHmdYawDir();
    if (yawDir.sqrMagnitude < 0.0001f) yawDir = Vector3.forward;
    yawDir.Normalize();

    Vector3 right = Vector3.Cross(Vector3.up, yawDir).normalized;

    // Movement direction for stride 
    Vector3 strideDir = yawDir;
    if (velocity.sqrMagnitude > 0.01f)
    {
        strideDir = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
        if (strideDir.sqrMagnitude < 0.0001f) strideDir = yawDir;
    }

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

    // Calculate BASE positions
    Vector3 leftBaseXZ = center 
        + right * leftFootLocalOffset.x 
        + yawDir * leftFootLocalOffset.z;

    Vector3 rightBaseXZ = center 
        + right * rightFootLocalOffset.x 
        + yawDir * rightFootLocalOffset.z;

    Vector3 leftTargetXZ = leftBaseXZ + strideDir * currentStrideLeft;
    Vector3 rightTargetXZ = rightBaseXZ + strideDir * currentStrideRight;

    // RAYCAST to find actual ground height for each skate 
    float leftGroundY = GetGroundHeightAt(leftTargetXZ);
    float rightGroundY = GetGroundHeightAt(rightTargetXZ);

    float debugLift = 0.2f; 

    Vector3 leftTarget = new Vector3(
        leftTargetXZ.x,
        leftGroundY + debugLift,
        leftTargetXZ.z
    );

    Vector3 rightTarget = new Vector3(
        rightTargetXZ.x,
        rightGroundY + debugLift,
        rightTargetXZ.z
    );


    // Smooth position interpolation 
    leftSkate.position = Vector3.Lerp(leftSkate.position, leftTarget, skateFollowPos * Time.deltaTime);
    rightSkate.position = Vector3.Lerp(rightSkate.position, rightTarget, skateFollowPos * Time.deltaTime);

    float activity01 = Mathf.Max(push01, speed01);
    float cantFactor = (activity01 > 0.01f)
        ? Mathf.Lerp(0f, 1f, Mathf.Max(minCantWhenMoving, activity01))
        : 0f;

    float signedTurn = Vector3.SignedAngle(prevYawDir, yawDir, Vector3.up);
    float turnRate = signedTurn / Mathf.Max(Time.deltaTime, 0.0001f);

    float targetLean = Mathf.Clamp(-turnRate * leanPerDegPerSec, -maxLeanAngle, maxLeanAngle) * speed01;
    currentLean = Mathf.Lerp(currentLean, targetLean, leanSmooth * Time.deltaTime);
    prevYawDir = yawDir;

    //  Build final rotations 
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

    prevBodyCenter = center;
}

private float GetGroundHeightAt(Vector3 position)
{
    Vector3 rayOrigin = new Vector3(position.x, transform.position.y + 2f, position.z);
    RaycastHit hit;
    
    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 3f, groundLayers))
    {
        return hit.point.y;
    }
    
    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 3f))
    {
        return hit.point.y;
    }
    
    return transform.position.y;
}

    void OnTriggerEnter(Collider other)
    {

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
    }
}