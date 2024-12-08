using UnityEngine;
using UnityEngine.InputSystem;

public class AvatarInputController : MonoBehaviour
{
    
    
    [Header("Input")]

    [SerializeField] private float inputCooldown = 0.2f;
    [SerializeField] private InputActionProperty navigateAction;
    [SerializeField] private InputActionProperty selectAction;
    [SerializeField] private InputActionProperty rotateAction;
    [SerializeField] private InputActionProperty startAction;
    [SerializeField] private float rotationSpeed = 180f;  // 每秒旋转180度

    private AvatarSelectionManager selectionManager;
    
    private float lastInputTime;
    private bool canRotatePreview = false;
    private float currentRotateValue = 0f;

    private void OnEnable()
    {
        navigateAction.action.performed += OnNavigate;
        selectAction.action.performed += OnSelect;
        rotateAction.action.performed += OnRotate;
        rotateAction.action.canceled += OnRotateEnded;
        startAction.action.performed += OnStart;
        
        // 不在这里启用动作，而是通过专门的方法控制
    }

    private void OnDisable()
    {
        navigateAction.action.performed -= OnNavigate;
        selectAction.action.performed -= OnSelect;
        rotateAction.action.performed -= OnRotate;
        rotateAction.action.canceled -= OnRotateEnded;
        startAction.action.performed -= OnStart;
        
        DisableStartAction();
        DisableGameplayActions();
    }

    private void Start()
    {
        
        selectionManager = GetComponent<AvatarSelectionManager>();
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (Time.time - lastInputTime < inputCooldown) return;
        
        Vector2 input = context.ReadValue<Vector2>();
        if (Mathf.Abs(input.x) > 0.5f)
        {
            lastInputTime = Time.time;
            if (input.x > 0)
            {
                selectionManager.SelectNextAvatar();
            }
            else
            {
                selectionManager.SelectPreviousAvatar();
            }
        }
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
        selectionManager.OnAvatarSelected(selectionManager.CurrentSelectedIndex);
    }

    public void EnableRotation()
    {
        canRotatePreview = true;
    }

    public void DisableRotation()
    {
        canRotatePreview = false;
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        if (!canRotatePreview) return;
        
        Vector2 input = context.ReadValue<Vector2>();
        currentRotateValue = input.x;
    }

    private void OnRotateEnded(InputAction.CallbackContext context)
    {
        if (!canRotatePreview) return;
        currentRotateValue = 0f;
        selectionManager.StopRotatingPreviewAvatar();
    }

    private void OnStart(InputAction.CallbackContext context)
    {
        if (selectionManager != null && selectionManager.enabled)
        {
            selectionManager.StartExperiment();
        }
    }

    private void Update()
    {
        if (canRotatePreview && currentRotateValue != 0f)
        {
            selectionManager.RotatePreviewAvatar(currentRotateValue * rotationSpeed * Time.deltaTime);
        }
    }

    // 添加方法来控制开始动作
    public void EnableStartAction()
    {
        startAction.action.Enable();
    }

    public void DisableStartAction()
    {
        startAction.action.Disable();
    }

    // 添加方法来控制游戏操作
    public void EnableGameplayActions()
    {
        navigateAction.action.Enable();
        selectAction.action.Enable();
        rotateAction.action.Enable();
    }

    public void DisableGameplayActions()
    {
        navigateAction.action.Disable();
        selectAction.action.Disable();
        rotateAction.action.Disable();
    }
} 