using UnityEngine;
using System;

public class LocomotionTechnique : MonoBehaviour
{
    // Please implement your locomotion technique in this script. 
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    [Range(0, 10)] public float translationGain = 0.5f;
    public GameObject hmd;
    [SerializeField] private float leftTriggerValue;    
    [SerializeField] private float rightTriggerValue;
    [SerializeField] private Vector3 startPos;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool isIndexTriggerDown;


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

    void Start()
    {
        prevLeftY = OVRInput.GetLocalControllerPosition(leftController).y;
        prevRightY = OVRInput.GetLocalControllerPosition(rightController).y;

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

        bool isSwinging = (leftDifference+rightDifference)> 0.01f; // here it's swinging if we slightly move the controllers
        
        float newSpeed = 0.0f;
        if (isSwinging) newSpeed = walkingSpeed;

        currentSpeed = Mathf.Lerp(currentSpeed, newSpeed, 6f * Time.deltaTime); // to make it smooth, that's what i struggled with with the code below 
        // this do a linear interpolation between currentSpeed (current walking speed), new walking speed, depending on a time factor
        // this gradually move currentSpeed toward newSpeed over time
        // so that we move but it doesn't jump immediately 
        transform.position += hmd.transform.forward * currentSpeed * Time.deltaTime;

        prevLeftY = currentLeftY;
        prevRightY = currentRightY;


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