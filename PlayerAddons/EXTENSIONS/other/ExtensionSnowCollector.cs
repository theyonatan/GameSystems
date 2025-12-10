using UnityEngine;
using UnityEngine.VFX;

public class ExtensionSnowCollector : MonoBehaviour
{
    public float SnowCollectionCooldown = 2f;
    public float collectionTimer = 0f;

    [SerializeField] Camera maskCamera;
    [SerializeField] RenderTexture snowMaskTexture;
    [SerializeField] LayerMask snowLayerMask;   

    [SerializeField] VisualEffect snowVFX;
    [SerializeField] Transform playerTransform;
    
    private Texture2D maskTexture2D;

    void Start()
    {
        // Convert the render texture to regular Texture2D so we can work with it
        maskTexture2D = new Texture2D(snowMaskTexture.width, snowMaskTexture.height, TextureFormat.RGB24, false);
        ReadMaskTexture();
    }

    void Update()
    {
        // if the player wants to collect snow
        if (Input.GetKey(KeyCode.E))
        {
            Debug.Log("Snowing");
            snowVFX.SetFloat("Rate", 30f);

            // shoot ray at ground, see if we black or white
            Ray ray = new(playerTransform.position + new Vector3(0f, 1f), Vector3.down);
            Debug.DrawRay(playerTransform.position, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 4f, snowLayerMask))
            {
                Renderer renderer = hit.collider.GetComponent<Renderer>();

                if (renderer != null)
                {
                    Vector2 uv = hit.textureCoord;

                    float grayScale = maskTexture2D.GetPixelBilinear(uv.x, uv.y).grayscale;
                    if (grayScale < 0.5f)
                    {
                        UpdateMask(uv);

                        collectionTimer -= Time.deltaTime;
                        if (collectionTimer > 0f)
                            return;

                        collectionTimer = SnowCollectionCooldown;

                        CollectSnow(grayScale);
                    }
                }
            }
        }
        else
        {
            snowVFX.SetFloat("Rate", 0f);
        }
    }

    private void ReadMaskTexture()
    {
        RenderTexture.active = snowMaskTexture;
        maskTexture2D.ReadPixels(new Rect(0f, 0f, snowMaskTexture.width, snowMaskTexture.height), 0, 0);
        maskTexture2D.Apply();
        RenderTexture.active = null;
    }

    private void UpdateMask(Vector2 uv)
    {
        int x = (int)(uv.x * snowMaskTexture.width);
        int y = (int)(uv.y * snowMaskTexture.height);

        maskTexture2D.SetPixel(x, y, Color.white);
        maskTexture2D.Apply();

        Graphics.Blit(maskTexture2D, snowMaskTexture);
    }

    private void CollectSnow(float snowCollected)
    {
        snowCollected = Mathf.Clamp(snowCollected, 0.5f, 1f);

        float snowToAdd = (snowCollected - 0.5f) * 4f + 1f;

        StatsSingleton.Instance.IncreamentStat(StatType.Snow, snowToAdd);
    }
}
