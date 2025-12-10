using UnityEngine;

public class ExtensionInventory : MonoBehaviour
{
    private InputDirector _inputDirector;
    public GameObject InventoryCanvas;
    private CameraManager _cameraManager;
    private MovementManager _movementManager;

    // Start is called before the first frame update
    void Start()
    {
        _movementManager = gameObject.GetComponent<MovementManager>();
        _cameraManager = gameObject.GetComponent<CameraManager>();
        _inputDirector = gameObject.GetComponent<InputDirector>();

        _inputDirector.OnInventoryPressed += OnInventoryToggle;

        // initiate the canvas
        InventoryCanvas.SetActive(true);
        InventoryCanvas.SetActive(false);
    }

    void OnInventoryToggle()
    {
        bool inventoryActive = InventoryCanvas.activeSelf;
        
        // Disable Inventory
        if (inventoryActive)
        {
            _movementManager.EnableMovement();
            _cameraManager.EnableCamera();
            InventoryCanvas.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else // Enable Inventory
        {
            _movementManager.DisableMovement();
            _cameraManager.DisableCamera();
            InventoryCanvas.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void OnDestroy()
    {
        _inputDirector.OnInventoryPressed -= OnInventoryToggle;
    }
}
