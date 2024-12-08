using UnityEngine;
using System.Collections.Generic;
using System.Linq;  // 添加这行来使用LINQ
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.XR;

[System.Serializable]
public class ConditionConfig
{
    public float totalArcRange = 180f;    // 总的弧度范围
    public float displayRadius = 5f;      // Avatar到中心的距离

    [Header("Lighting")]
    public bool enableLights = true;      // 是否启用点光源
    public int lightCount = 8;            // 光源数量
    public float lightHeight = 1f;        // 光源高度偏移
    public float lightForwardOffset = 1f; // 光源前向偏移
    public float lightIntensity = 1f;     // 光源强度
    public float lightRange = 2f;         // 光源范围
}

[RequireComponent(typeof(AvatarInputController))]
[RequireComponent(typeof(AudioManager))]
public class AvatarSelectionManager : MonoBehaviour 
{
    [Header("Condition Settings")]
    [SerializeField] private int currentConditionIndex = 0;
    [SerializeField] private ConditionConfig[] conditionConfigs = new ConditionConfig[4];  // 改为4个配置

    [Header("Avatar Settings")]
    [SerializeField] private float avatarScale = 1f;
    [SerializeField] private GameObject[] avatarPrefabs;  // 只保留一个30个prefab的数
    [SerializeField] private RuntimeAnimatorController avatarAnimatorController;
    
    [Header("Layout Settings")]
    [SerializeField] private Transform centerPoint;         // Avatar圆的中心点
    [SerializeField] private float avatarHeight = 0f;      // Avatar的高度位置
    
    [Header("Animation Settings")]
    [SerializeField] private float moveToPreviewDuration = 0.5f;
    [SerializeField] private float returnDuration = 0.3f;
    [SerializeField] private float spawnDuration = 0.5f;
    [SerializeField] private Ease previewEase =Ease.OutBack;
    [SerializeField] private Ease returnEase = Ease.OutQuad;
    [SerializeField] private Ease spawnEase = Ease.OutBack;

    [Header("UI")]
    [SerializeField] private GameObject contentUI;

    private List<GameObject> currentAvatars = new List<GameObject>();
    
    private int currentSelectedIndex = -1;
    private GameObject currentPreviewAvatar;
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    
    private GameObject[] CurrentPrefabSet => conditionOrder[currentConditionIndex] switch
    {
        0 => avatarPrefabs.Take(5).ToArray(),   // 使用第一个配置
        1 => avatarPrefabs.Take(10).ToArray(),  // 使用第二个配置
        2 => avatarPrefabs.Take(15).ToArray(),  // 使用第三个配置
        3 => avatarPrefabs,                     // 使用第四个配置
        _ => avatarPrefabs.Take(5).ToArray()
    };

    [System.Serializable]
    public class SelectionData 
    {
        public int conditionSize;        // 当前条件的Avatar数量
        public float selectionTime;      // 选择用时
        public int selectedAvatarIndex;  // 选择的Avatar索引
        public string timestamp;         // 选择的时间戳
    }
    
    [System.Serializable]
    public class ExperimentSession
    {
        public string sessionId;                 // 会话ID
        public string startTime;                 // 实验开始时间
        public List<int> conditionSizes;         // 条件顺序（记录每个条件的Avatar数量：[5,15,30,10]这样）
        public List<SelectionData> selections;   // 所有选择数据
    }

    [System.Serializable]
    public class SessionListWrapper
    {
        public List<ExperimentSession> sessions = new List<ExperimentSession>();
    }

    private List<ExperimentSession> allSessions = new List<ExperimentSession>();
    private ExperimentSession currentSession;

    private float startTime;

    [Header("Selection")]
    [SerializeField] private float maxSelectDistance = 20f;
    private GameObject highlightedAvatar;
    private Camera mainCamera;

    private AvatarInputController inputController;
    private bool isPreviewAnimationPlaying = false;

    public int CurrentSelectedIndex => currentSelectedIndex;

    private List<GameObject> lightObjects = new List<GameObject>();  // 存储所有光源对象

    [Header("Audio")]
    [SerializeField] private AudioManager audioManager;

    private bool isTransitioningCondition = false;

    private int[] conditionOrder;  // 用于存储随机顺序的条件索引

    private void Start()
    {
        XRSettings.eyeTextureResolutionScale = 1.2f;

        // 验证prefab数量
        if (avatarPrefabs == null || avatarPrefabs.Length < 30)
        {
            Debug.LogError("需要至少30个Avatar预制体！");
            return;
        }

        // 验证配置数量
        if (conditionConfigs.Length != 4)
        {
            Debug.LogError("需要正好4个条件配置！");
            return;
        }

        mainCamera = Camera.main;
        inputController = GetComponent<AvatarInputController>();
        audioManager = GetComponent<AudioManager>();

        // 初始状态只允许开始实验，禁用其他输入
        inputController.EnableStartAction();
        inputController.DisableGameplayActions();
    }

    private void Update()
    {
        if (currentAvatars.Count > 0)
        {
            // 使用左右箭头键选择Avatar
            float horizontalInput = Input.GetAxis("Horizontal");
            if (Input.GetKeyDown(KeyCode.RightArrow) ||Input.GetKeyDown(KeyCode.D))
            {
                SelectNextAvatar();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow)|| Input.GetKeyDown(KeyCode.A))
            {
                SelectPreviousAvatar();
            }

            // 使用空格键确认选择
            if (Input.GetKeyDown(KeyCode.Space) && currentSelectedIndex != -1)
            {
                OnAvatarSelected(currentSelectedIndex);
            }
        }
    }

    public void SelectNextAvatar()
    {
        int nextIndex = currentSelectedIndex + 1;
        if (nextIndex >= currentAvatars.Count)
        {
            nextIndex = 0;  // 回到第一个
        }
        PreviewAvatar(nextIndex);
    }

    public void SelectPreviousAvatar()
    {
        int prevIndex = currentSelectedIndex - 1;
        if (prevIndex < 0)
        {
            prevIndex = currentAvatars.Count - 1;  // 跳到最后一个
        }
        PreviewAvatar(prevIndex);
    }

    public void StartCondition(int conditionIndex)
    {
        currentPreviewAvatar = null;
        currentConditionIndex = conditionIndex;
        currentSelectedIndex = -1;
        
        ClearCurrentAvatars();
        SpawnAvatars();
        
        // 在生成Avatar完成后启用交互
        DOVirtual.DelayedCall(0.1f, () => {
            inputController.EnableGameplayActions();
            PreviewAvatar(CurrentPrefabSet.Length / 2);  // 选择中间的Avatar
        });
        
        startTime = Time.time;
    }

    private void ClearCurrentAvatars()
    {
        // 清理所有光源
        foreach (var light in lightObjects)
        {
            if (light != null)
            {
                Destroy(light);
            }
        }
        lightObjects.Clear();

        // 清���所有Avatar
        foreach (var avatar in currentAvatars)
        {
            DOTween.Kill(avatar.transform);
            Destroy(avatar);
        }
        currentAvatars.Clear();
    }

    private void SpawnAvatars()
    {
        GameObject[] prefabs = CurrentPrefabSet;
        originalPositions = new Vector3[prefabs.Length];
        originalRotations = new Quaternion[prefabs.Length];
        
        ConditionConfig config = conditionConfigs[conditionOrder[currentConditionIndex]];
        
        float effectiveArcAngle = config.totalArcRange / (prefabs.Length - 1);
        float startAngle = 90 + config.totalArcRange / 2;
        
        Vector3 centerPosition = centerPoint != null ? centerPoint.position : Vector3.zero;
        
        // 只在启用光照时创建光源
        if (config.enableLights && config.lightCount > 0)
        {
            // 计算光源之间的角度间隔
            float lightArcAngle = config.totalArcRange / (config.lightCount - 1);
            float lightStartAngle = 90 + config.totalArcRange / 2;  // 从最左侧开始
            
            // 创建均匀分布的光源
            for (int i = 0; i < config.lightCount; i++)
            {
                float lightAngle = lightStartAngle - (i * lightArcAngle);
                float radians = lightAngle * Mathf.Deg2Rad;
                float x = Mathf.Cos(radians) * config.displayRadius;
                float z = Mathf.Sin(radians) * config.displayRadius;
                
                Vector3 directionFromCenter = new Vector3(x, 0, z).normalized;
                
                GameObject lightObj = new GameObject($"Light_{i}");
                Light pointLight = lightObj.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.intensity = config.lightIntensity;
                pointLight.range = config.lightRange;
                
                Vector3 lightPosition = centerPosition + new Vector3(x, config.lightHeight, z) + directionFromCenter * config.lightForwardOffset;
                lightObj.transform.position = lightPosition;
                lightObjects.Add(lightObj);
            }
        }
        
        // 生成Avatar
        for (int i = 0; i < prefabs.Length; i++)
        {
            float angle = startAngle - (i * effectiveArcAngle);
            float radians = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(radians) * config.displayRadius;
            float z = Mathf.Sin(radians) * config.displayRadius;
            
            Vector3 position = centerPosition + new Vector3(x, avatarHeight, z);
            
            GameObject avatar = Instantiate(prefabs[i], position, Quaternion.identity, transform);
            avatar.transform.LookAt(new Vector3(centerPosition.x, centerPosition.y + avatarHeight, centerPosition.z));
            
            // 设置Animator和动画
            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                animator = avatar.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = avatarAnimatorController;
            animator.CrossFadeInFixedTime("Stand_Idle1", 0.25f);
            animator.speed = 0;  // 初始时暂停动画
            
            originalPositions[i] = position;
            originalRotations[i] = avatar.transform.rotation;
            
            // 添加效果
            avatar.transform.localScale = Vector3.zero;
            avatar.transform.DOScale(Vector3.one * avatarScale, spawnDuration).SetEase(spawnEase);
            
            currentAvatars.Add(avatar);
        }

        // 默认选择中间的Avatar
        int middleIndex = prefabs.Length / 2;
        // 确保在调用PreviewAvatar之前所有Avatar都已经正确生成
        DOVirtual.DelayedCall(0.1f, () => PreviewAvatar(middleIndex));
    }

    public void OnAvatarSelected(int index)
    {
        if (!this.enabled) return;
        
        audioManager.PlayConfirmSound();
        SelectionData data = new SelectionData
        {
            conditionSize = CurrentPrefabSet.Length,
            selectedAvatarIndex = index,
            selectionTime = Time.time - startTime,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        
        currentSession.selections.Add(data);
        SaveProgress();
        
        inputController.DisableGameplayActions();
        
        if (currentPreviewAvatar != null)
        {
            Animator animator = currentPreviewAvatar.GetComponent<Animator>();
            if (animator != null)
            {
                animator.CrossFadeInFixedTime("Emoji_Cheer", 0.25f);
            }
        }
        
        if (currentConditionIndex < 3)
        {
            DOVirtual.DelayedCall(3f, () => {
                StartCondition(currentConditionIndex + 1);
            });
        }
        else
        {
            DOVirtual.DelayedCall(3f, () => {
                SaveExperimentFile();
                ResetToInitialState();
            });
        }
    }

    private void SaveProgress()
    {
        // 从PlayerPrefs加载现有数据
        string existingData = PlayerPrefs.GetString("ExperimentData", "");
        List<ExperimentSession> sessions;
        
        if (string.IsNullOrEmpty(existingData))
        {
            sessions = new List<ExperimentSession>();
        }
        else
        {
            try
            {
                var sessionWrapper = JsonUtility.FromJson<SessionListWrapper>(existingData);
                sessions = sessionWrapper.sessions;
            }
            catch
            {
                sessions = new List<ExperimentSession>();
            }
        }

        // 更新或添加当前会话
        int existingIndex = sessions.FindIndex(s => s.sessionId == currentSession.sessionId);
        if (existingIndex >= 0)
        {
            sessions[existingIndex] = currentSession;
        }
        else
        {
            sessions.Add(currentSession);
        }

        // 保存回PlayerPrefs
        var newWrapper = new SessionListWrapper { sessions = sessions };
        string json = JsonUtility.ToJson(newWrapper, true);
        PlayerPrefs.SetString("ExperimentData", json);
        PlayerPrefs.Save();
    }

    private void SaveExperimentFile()
    {
        // 只保存当前会话的数据
        string json = JsonUtility.ToJson(currentSession, true);  // 直接序列化 currentSession
        
        if (!string.IsNullOrEmpty(json))
        {
            string folderPath = Application.persistentDataPath + "/ExperimentData";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filePath = $"{folderPath}/experiment_data_{timestamp}.json";
            
            try
            {
                System.IO.File.WriteAllText(filePath, json);
                Debug.Log($"Experiment completed! Data saved to file: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save data to file: {e.Message}");
            }
        }
    }

    private void PreviewAvatar(int index)
    {
        isPreviewAnimationPlaying = true;
        inputController.DisableRotation();

        if (currentSelectedIndex == index) return;

        GameObject previousAvatar = currentPreviewAvatar;
        int previousIndex = currentSelectedIndex;

        currentSelectedIndex = index;
        currentPreviewAvatar = currentAvatars[index];
        
        // 暂停之前Avatar的动画
        if (previousAvatar != null)
        {
            Animator previousAnimator = previousAvatar.GetComponent<Animator>();
            if (previousAnimator != null)
            {
                previousAnimator.speed = 0;
            }
        }

        // 播放当前Avatar的动画
        Animator currentAnimator = currentPreviewAvatar.GetComponent<Animator>();
        if (currentAnimator != null)
        {
            currentAnimator.speed = 1;
        }

        Vector3 centerPosition = centerPoint != null ? centerPoint.position : Vector3.zero;
        
        // 如果有之前预览的Avatar，它回到原位
        if (previousAvatar != null)
        {
            Sequence returnSequence = DOTween.Sequence();
            returnSequence.Append(previousAvatar.transform.DOMove(originalPositions[previousIndex], returnDuration).SetEase(returnEase));
            returnSequence.Join(previousAvatar.transform.DORotate(originalRotations[previousIndex].eulerAngles, returnDuration));
            returnSequence.SetId(previousAvatar.GetInstanceID());
        }

        // 移动到圆心并面向Z轴负向
        Sequence previewSequence = DOTween.Sequence();
        previewSequence.Append(currentPreviewAvatar.transform.DOMove(new Vector3(centerPosition.x, centerPosition.y + avatarHeight, centerPosition.z), moveToPreviewDuration).SetEase(previewEase));
        previewSequence.Join(currentPreviewAvatar.transform.DORotate(new Vector3(0, 180, 0), moveToPreviewDuration));
        previewSequence.SetId(currentPreviewAvatar.GetInstanceID());
        previewSequence.OnComplete(() => {
            isPreviewAnimationPlaying = false;
            inputController.EnableRotation();
        });
        audioManager.PlayMovementSound();
    }

    public void RotatePreviewAvatar(float rotateAmount)
    {
        if (isPreviewAnimationPlaying)
        {
            return;
        }
        
        if (currentPreviewAvatar == null)
        {
            return;
        }
        
        currentPreviewAvatar.transform.Rotate(0, rotateAmount, 0);
    }

    public void StopRotatingPreviewAvatar()
    {
        // 可以在这里添加停止旋转时的逻辑
    }

    private void InitializeConditionOrder()
    {
        // 创建包含4个条件的数组
        conditionOrder = new int[] { 0, 1, 2, 3 };
        
        // Fisher-Yates 洗牌算法
        for (int i = conditionOrder.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int temp = conditionOrder[i];
            conditionOrder[i] = conditionOrder[randomIndex];
            conditionOrder[randomIndex] = temp;
        }
    }

    private void ResetToInitialState()
    {
        ClearCurrentAvatars();
        
        currentPreviewAvatar = null;
        currentSelectedIndex = -1;
        currentConditionIndex = 0;

        if (contentUI != null) contentUI.SetActive(true);

        // 重置到初始状态时，重新启用开始动作，禁用游戏操作
        inputController.EnableStartAction();
        inputController.DisableGameplayActions();
    }

    public void StartExperiment()
    {
        if (this.enabled)
        {
            if (contentUI != null) contentUI.SetActive(false);
            audioManager.PlayButtonClickSound();
            InitializeConditionOrder();
            
            // 创建新的实验会话，记录条件顺序对应的Avatar数量
            currentSession = new ExperimentSession
            {
                sessionId = System.Guid.NewGuid().ToString(),
                startTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                conditionSizes = conditionOrder.Select(index => {
                    return index switch
                    {
                        0 => 5,
                        1 => 10,
                        2 => 15,
                        3 => 30,
                        _ => 5
                    };
                }).ToList(),
                selections = new List<SelectionData>()
            };
            
            inputController.DisableStartAction();
            StartCondition(0);
        }
    }
} 