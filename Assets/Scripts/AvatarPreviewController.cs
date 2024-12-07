using UnityEngine;
using UnityEngine.InputSystem;

public class AvatarPreviewController : MonoBehaviour
{
    [SerializeField] private AvatarSelectionManager selectionManager;
    private int currentPreviewIndex = 0;
    private float joystickDeadzone = 0.5f;
    private bool canSwitch = true;
    private float switchCooldown = 0.2f;
    private float lastSwitchTime;

    public void OnJoystickMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        
        if (Mathf.Abs(input.x) > joystickDeadzone && canSwitch && Time.time - lastSwitchTime > switchCooldown)
        {
            currentPreviewIndex += input.x > 0 ? 1 : -1;
            lastSwitchTime = Time.time;
            
            // 可以添加音效或其他反馈
        }
    }

    public void OnSelectButton(InputValue value)
    {
        if (value.isPressed)
        {
            selectionManager.OnAvatarSelected(currentPreviewIndex);
        }
    }
} 