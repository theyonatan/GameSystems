using UnityEngine;

public class ExtensionKillPlayer : MonoBehaviour
{
    public float SnowToRemove = 30f;
    public Vector3 LastCheckpoint = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StatsSingleton.Instance.GetStat(StatType.Health).OnStatChanged += ExtensionKillPlayer_OnStatChanged;
    }

    private void OnDestroy()
    {
        StatsSingleton.Instance.GetStat(StatType.Health).OnStatChanged -= ExtensionKillPlayer_OnStatChanged;
    }

    private void ExtensionKillPlayer_OnStatChanged(float newHealthValue)
    {
        if (newHealthValue <= 0f)
        {
            // reset it's stats
            StatsSingleton.Instance.SetStat(StatType.Health, 100f);

            float currentSnow = StatsSingleton.Instance.GetStat(StatType.Snow).Value;
            
            if (currentSnow - SnowToRemove <= 0f)
                StatsSingleton.Instance.SetStat(StatType.Snow, 0f);
            else if (currentSnow - SnowToRemove > 0f)
                StatsSingleton.Instance.DecreamentStat(StatType.Snow, SnowToRemove);

            // and respawn to last checkpoint
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            player.transform.position = LastCheckpoint;
        }
    }

    public void UnlockCheckpoint(Transform checkpoint)
    {
        LastCheckpoint = checkpoint.position;
    }
}
