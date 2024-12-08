using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonStateResetter : MonoBehaviour
{
    private Button button;

    void Awake()
    {
        // 获取按钮组件
        button = GetComponent<Button>();

        // 自动为按钮添加点击监听器
        button.onClick.AddListener(OnButtonClicked);
    }

    // 点击按钮时调用的方法
    private void OnButtonClicked()
    {
        ResetButtonState();
    }

    // 改为 public 方法
    public void ResetButtonState()
    {
        // 禁用并重新启用按钮交互，强制更新状态
        button.interactable = false;
        button.interactable = true;
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(OnButtonClicked);
    }
}
