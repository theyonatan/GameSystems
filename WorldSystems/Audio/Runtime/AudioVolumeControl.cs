using UnityEngine;

namespace GameSystems.Audio
{
    // Bind a UI slider's float event here; no dependency on a specific UI framework.
    public sealed class AudioVolumeControl : MonoBehaviour
    {
        public bool Master;
        public AudioChannel Channel;
        public void SetVolume(float value)
        {
            if (!AudioService.Instance) return;
            if (Master) AudioService.Instance.SetMasterVolume(value);
            else AudioService.Instance.SetVolume(Channel, value);
        }
    }
}
