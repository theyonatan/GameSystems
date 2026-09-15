using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in replacement footprint, applied only while building world grass batches.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class GrassCoverageArea : MonoBehaviour
{
    [Tooltip("Only roots on this owned surface are replaced. No painted source data is edited.")]
    public MeshCollider surface;
    [Tooltip("Grass sources below this transform are retained inside the replacement area.")]
    public Transform retainedRoot;
    [Min(.01f)] public float heightTolerance=.2f;

    void OnEnable() => WorldGrassManager.NotifySourcesChanged();
    void OnDisable() => WorldGrassManager.NotifySourcesChanged();
    void OnValidate() => WorldGrassManager.NotifySourcesChanged();

    public struct Snapshot
    {
        MeshCollider collider;
        Bounds bounds;
        Transform retained;
        float tolerance;
        public Snapshot(GrassCoverageArea area)
        {
            collider=area.surface; retained=area.retainedRoot;
            tolerance=Mathf.Max(.01f,area.heightTolerance);
            bounds=collider.bounds; bounds.Expand(Vector3.up*tolerance*2);
        }
        public bool AppliesTo(GrassSource source)
        {
            return collider && source && source.gameObject.scene==collider.gameObject.scene && !source.transform.IsChildOf(retained);
        }
        public bool Contains(Vector3 worldPoint)
        {
            if(!collider || !bounds.Contains(worldPoint)) return false;
            return collider.Raycast(new Ray(worldPoint+Vector3.up*tolerance,Vector3.down),out _,tolerance*2);
        }
    }

    public bool TryGetSnapshot(out Snapshot snapshot)
    {
        snapshot=default;
        if(!isActiveAndEnabled || !surface || !surface.enabled || !surface.sharedMesh || !retainedRoot ||
            !surface.transform.IsChildOf(transform) || !retainedRoot.IsChildOf(transform) ||
            !float.IsFinite(heightTolerance) || heightTolerance<=0) return false;
        snapshot=new Snapshot(this);
        return true;
    }

    // Compact only the appended world-space copy. Existing preset groups and the
    // original painted list are untouched, including points outside the footprint.
    public static void FilterAppended(GrassSource source,List<GrassData> points,int start,List<Snapshot> areas)
    {
        if(areas.Count==0) return;
        foreach(var area in areas)
        {
            // Test source ownership once, not once per painted point.
            if(!area.AppliesTo(source)) continue;
            int end=points.Count, write=start;
            for(int read=start;read<end;read++)
                if(!area.Contains(points[read].position)) points[write++]=points[read];
            if(write<end) points.RemoveRange(write,end-write);
        }
    }
}
