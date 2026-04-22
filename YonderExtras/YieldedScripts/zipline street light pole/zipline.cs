using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class zipline : MonoBehaviour, Interactable
{
    [SerializeField] private zipline targetZip;
    [SerializeField] private float zipSpeed = 5f;
    [SerializeField] private float zipScale = 0.2f;

    [SerializeField] private float arrivalThreshold = 0.4f;
    [SerializeField] private LineRenderer cable;

    public Transform ZipTransform;

    private bool zipping = false;
    private GameObject localZip;

    private void Awake()
    {
        cable.SetPosition(0, ZipTransform.position);
        if (!targetZip)
            return;
        cable.SetPosition(1, targetZip.ZipTransform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (!targetZip) return;
        if (!zipping || !localZip) return;

        localZip.transform.position = Vector3.MoveTowards(
            localZip.transform.position,
            targetZip.ZipTransform.position,
            zipSpeed * Time.deltaTime
        );

        if (HasPlayerPassedZipline())
            ResetZipLine();
    }

    public void StartZipLine(GameObject player)
    {
        if (zipping) return;

        // create gameobject that will carry the player
        localZip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        localZip.transform.position = ZipTransform.position;
        localZip.transform.localScale = new Vector3(zipScale, zipScale, zipScale);

        var rb = localZip.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        localZip.GetComponent<Collider>().isTrigger = true;
        localZip.GetComponent<MeshRenderer>().enabled = false;

        // disable player gravity
        player.GetComponent<Player>().SwapPlayerState<cc_ZiplineState, TP_CameraState>();
        
        Quaternion savedRotation = player.transform.rotation;
        player.transform.SetParent(localZip.transform, true);
        player.transform.rotation = savedRotation;

        // start zip
        zipping = true;
    }

    public bool HasPlayerPassedZipline()
    {
        Vector3 startZip = ZipTransform.position;
        Vector3 endZip = targetZip.ZipTransform.position;
        Vector3 playerPosition = localZip.transform.position;

        Vector3 zipDirection = (endZip - startZip).normalized;
        Vector3 endToPlayer = playerPosition - endZip;

        return Vector3.Dot(zipDirection, endToPlayer) > 0f - arrivalThreshold;
    }

    private void ResetZipLine()
    {
        if (!zipping) return;

        GameObject player = localZip.transform.GetChild(0).gameObject;
        player.GetComponent<Player>().SwapPlayerState<cc_tpState, TP_CameraState>();
        localZip.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;

        player.transform.SetParent(null, true);
        Destroy(localZip);
        localZip = null;
        zipping = false;
    }

    public string InteractableType => "NPC Interact";

    public void Interact()
    {
        if (!targetZip)
            return;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        StartZipLine(player);
    }

    public void MarkAsInteractable()
    {
        Debug.Log("Wanna Ride?");
    }
}
