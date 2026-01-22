using UnityEngine;
using System;

public class LocomotionTechnique : MonoBehaviour
{    
    // Please implement your locomotion technique in this script. 
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    [Range(0, 10)] public float translationGain = 0.5f;
    public GameObject hmd;
    public float sensitivity = 50f;         // how strongly controller swing maps to speed
    public float speedSmooth = 6f;          // Lerp smoothing
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

     // added
    private float gravity = -9.81f;        
    private float jumpMagnitude= 5f;          

    private float verticalVelocity = 0f;

    float prevLeftY = 0.0f;
    float prevRightY = 0.0f;

    [Header("Skates (Visual)")]
    public Transform leftSkate;
    public Transform rightSkate;

    // Where skates rest (local offsets under the rig root where this script lives)
    public Vector3 leftFootLocalOffset = new Vector3(-0.12f, 0.02f, 0.05f);
    public Vector3 rightFootLocalOffset = new Vector3(0.12f, 0.02f, 0.05f);

    [Header("Skate Animation")]
    public float strideLength = 0.25f;        // total front/back range in meters
    public float strideFrequency = 1.8f;      // cycles/sec near full speed
    public float minSpeedToAnimate = 0.05f;   // m/s
    public float skateYawFollow = 12f;        // yaw smoothing

    

    [Header("Wheel Roll (Optional)")]
    public float wheelRadius = 0.04f;         // meters
    public Transform[] leftWheels;
    public Transform[] rightWheels;

    private float skatePhase = 0f;
    private Vector3 prevRigPos;
    void Start()
    {
        prevLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        prevRightY = OVRInput.GetLocalControllerPosition(rightController).y;

        prevRigPos = transform.position;

        // Initialize skate positions to rest
        if (leftSkate != null) leftSkate.localPosition = leftFootLocalOffset;
        if (rightSkate != null) rightSkate.localPosition = rightFootLocalOffset;
    }
   float walkingSpeed = 4.5f;   // to modify maybe, kinda too slow
    float currentSpeed = 0f;      

    void Update()
    {
          ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Please implement your LOCOMOTION TECHNIQUE in this script :D.
        leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, leftController); 
        rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, rightController); 

        // JUMP
        if (rightTriggerValue >0.95f) // i kept it like that so that we can jump as many times as we want, maybe put condition?
        {
            verticalVelocity = jumpMagnitude;
        }

    verticalVelocity += gravity * Time.deltaTime;
        transform.position += new Vector3(0, verticalVelocity * Time.deltaTime, 0); // offset

        // if below ground, stop at ground and stay there
        if (transform.position.y<=0.0f) 
        {
            transform.position = new Vector3(transform.position.x, 0f, transform.position.z );
            verticalVelocity = 0.0f;
        }


     // WALK
        // https://developers.meta.com/horizon/documentation/unity/unity-ovrinput/
        
        // here im not using 20cm detection, we're always walking no matter the distance of the swinging  
        float currentLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        float currentRightY = OVRInput.GetLocalControllerPosition(rightController).y;

          float leftDifference = Math.Abs(currentLeftY- prevLeftY);
        float rightDifference = Math.Abs(currentRightY- prevRightY);

        float effort = leftDifference + rightDifference;
        if (effort < 0.01f) effort = 0f;

        float newSpeed = walkingSpeed * Mathf.Clamp(effort * sensitivity, 0f, 1f);
        currentSpeed = Mathf.Lerp(currentSpeed, newSpeed, speedSmooth * Time.deltaTime);

        // Move in planar forward direction (prevents tilt drift)
        Vector3 flatForward = Vector3.forward;
        if (hmd != null)
        {
            flatForward = Vector3.ProjectOnPlane(hmd.transform.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        }

        transform.position += flatForward * currentSpeed * Time.deltaTime;

        prevLeftY = currentLeftY;
        prevRightY = currentRightY;

        // --- SKATE VISUALS ---
        AnimateSkates(flatForward);
        

        // works kinda; but weird effect when walking, very "buggy" effect ; with 20cm limit

        // float currentLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        // float currentRightY = OVRInput.GetLocalControllerPosition(rightController).y;

//         float thresholdWalkDetection = 0.2f; // make it bigger
//         float leftDifference = Math.Abs(currentLeftY- prevLeftY);
//         float rightDifference = Math.Abs(currentRightY- prevRightY);

//         totalLeftDist +=leftDifference;
//         totalRightDist +=rightDifference;
//         offset = Vector3.zero;
//         float stepScale = 4.0f; // meters per total threshold

//         if (totalLeftDist > thresholdWalkDetection)
//         {
//             offset += hmd.transform.forward.normalized * stepScale; // 1 step?
//             totalLeftDist =0.0f;
//         }
//         if (totalRightDist> thresholdWalkDetection)
//         {
//             offset += hmd.transform.forward.normalized * stepScale;

//             totalRightDist = 0.0f;

//         }

//         prevLeftY = currentLeftY;
//         prevRightY = currentRightY;

//         transform.position = transform.position + offset;// * translationGain;



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


        ////////////////////////////////////////////////////////////////////////////////
        // These are for the game mechanism.
        if (OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four))
        {
            if (parkourCounter.parkourStart)
            {
                transform.position = parkourCounter.currentRespawnPos;
            }
        }
    }

    private void AnimateSkates(Vector3 flatForward)
    {
        if (leftSkate == null || rightSkate == null) return;

        // distance traveled this frame (planar)
        Vector3 rigDelta = transform.position - prevRigPos;
        float planarDistance = Vector3.ProjectOnPlane(rigDelta, Vector3.up).magnitude;
        float speedApprox = planarDistance / Mathf.Max(Time.deltaTime, 0.0001f);
        prevRigPos = transform.position;

        bool moving = speedApprox > minSpeedToAnimate;

        // Yaw align skates to movement direction
        if (flatForward.sqrMagnitude > 0.0001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(flatForward, Vector3.up);
            leftSkate.rotation = Quaternion.Slerp(leftSkate.rotation, targetYaw, skateYawFollow * Time.deltaTime);
            rightSkate.rotation = Quaternion.Slerp(rightSkate.rotation, targetYaw, skateYawFollow * Time.deltaTime);
        }

        Vector3 leftBase = leftFootLocalOffset;
        Vector3 rightBase = rightFootLocalOffset;

        // If stopped, return to rest pose smoothly
        if (!moving)
        {
            leftSkate.localPosition = Vector3.Lerp(leftSkate.localPosition, leftBase, 10f * Time.deltaTime);
            rightSkate.localPosition = Vector3.Lerp(rightSkate.localPosition, rightBase, 10f * Time.deltaTime);
            return;
        }

        // Advance gait phase
        float speed01 = Mathf.Clamp01(currentSpeed / Mathf.Max(walkingSpeed, 0.001f));
        float hz = Mathf.Lerp(0.8f, strideFrequency, speed01);
        skatePhase += (hz * 2f * Mathf.PI) * Time.deltaTime;

        // back/forth slide on local Z
        float halfStride = strideLength * 0.5f;
        float slideL = Mathf.Sin(skatePhase) * halfStride;
        float slideR = Mathf.Sin(skatePhase + Mathf.PI) * halfStride;

        leftSkate.localPosition = leftBase + new Vector3(0f, 0f, slideL);
        rightSkate.localPosition = rightBase + new Vector3(0f, 0f, slideR);

        // Wheel roll (optional)
        RollWheels(leftWheels, planarDistance);
        RollWheels(rightWheels, planarDistance);
    }

    private void RollWheels(Transform[] wheels, float distance)
    {
        if (wheels == null || wheels.Length == 0) return;
        if (wheelRadius <= 0.0001f) return;

        float degrees = (distance / wheelRadius) * Mathf.Rad2Deg;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            // Adjust axis if your wheel’s roll axis is different
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