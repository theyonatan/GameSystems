using UnityEngine;

public class SegmentEntranceTrigger : MonoBehaviour
{
    private Segment _parentSegment;
    private bool _segmentActivated;

    private void Start()
    {
        _parentSegment = gameObject.GetComponentInParent<Segment>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (_segmentActivated)
            return;

        SegmentStreamer.Instance.SpawnRandomSegment(_parentSegment.ExitPoint);

        _segmentActivated = true;
        gameObject.SetActive(false);
    }
}
