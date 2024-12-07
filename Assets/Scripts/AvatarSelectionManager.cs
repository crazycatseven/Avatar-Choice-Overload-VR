using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class AvatarSelectionManager : MonoBehaviour 
{
    [Header("Avatar Prefabs")]
    [SerializeField] private GameObject[] avatarPrefabs5;
    [SerializeField] private GameObject[] avatarPrefabs15;
    [SerializeField] private GameObject[] avatarPrefabs30;
    
    [Header("Layout Settings")]
    [SerializeField] private float arcAngle = 30f;          // Avatar之间的角度间隔
    [SerializeField] private float displayRadius = 5f;      // Avatar到中心的距离
    [SerializeField] private float avatarHeight = 0f;       // Avatar的高度位置
    
    [Header("Animation Settings")]
    [SerializeField] private float moveToPreviewDuration = 0.5f;
    [SerializeField] private float returnDuration = 0.3f;
    [SerializeField] private float spawnDuration = 0.5f;
    [SerializeField] private Ease previewEase =Ease.OutBack;
    [SerializeField] private Ease returnEase = Ease.OutQuad;
    [SerializeField] private Ease spawnEase = Ease.OutBack;

    [Header("UI")]
    [SerializeField] private Button startButton;

    private List<GameObject> currentAvatars = new List<GameObject>();
    private int currentConditionIndex = 0;
    private int currentSelectedIndex = -1;
    private GameObject currentPreviewAvatar;
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    
    private GameObject[] CurrentPrefabSet => currentConditionIndex switch
    {
        0 => avatarPrefabs5,
        1 => avatarPrefabs15,
        2 => avatarPrefabs30,
        _ => avatarPrefabs5
    };

    [System.Serializable]
    public class SelectionData 
    {
        public int conditionSize;
        public float selectionTime;
        public int selectedAvatarIndex;
    }
    
    private List<SelectionData> selectionHistory = new List<SelectionData>();
    private float startTime;

    [Header("Selection")]
    [SerializeField] private float maxSelectDistance = 20f;
    private GameObject highlightedAvatar;
    private Camera mainCamera;

    private void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(() => StartCondition(0));
        }
        mainCamera = Camera.main;
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

    private void SelectNextAvatar()
    {
        int nextIndex = (currentSelectedIndex + 1) % currentAvatars.Count;
        PreviewAvatar(nextIndex);
    }

    private void SelectPreviousAvatar()
    {
        int prevIndex = currentSelectedIndex - 1;
        if (prevIndex < 0) prevIndex = currentAvatars.Count - 1;
        PreviewAvatar(prevIndex);
    }

    public void StartCondition(int conditionIndex)
    {
        if (startButton != null) startButton.gameObject.SetActive(false);
        
        currentConditionIndex = conditionIndex;
        currentSelectedIndex = -1;
        ClearCurrentAvatars();
        SpawnAvatars();
        startTime = Time.time;
    }

    private void ClearCurrentAvatars()
    {
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
        
        // 计算总角度范围，使Avatar组居中
        float totalArcAngle = (prefabs.Length - 1) * arcAngle;
        float centerOffset = totalArcAngle / 2;
        float currentAngle = 90 - centerOffset;  // 从90度开始（左侧），这样0度就是正前方
        
        for (int i = 0; i < prefabs.Length; i++)
        {
            // 计算圆弧上的位置
            float angle = currentAngle + (i * arcAngle);
            float radians = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(radians) * displayRadius;
            float z = Mathf.Sin(radians) * displayRadius;
            
            // 当i是中间索引时，强制位置在Z轴正方向（0点钟位置）
            if (i == prefabs.Length / 2)
            {
                x = 0;
                z = displayRadius;
            }
            Vector3 position = new Vector3(x, avatarHeight, z);
            
            GameObject avatar = Instantiate(prefabs[i], position, Quaternion.identity, transform);
            // 让Avatar面向圆心
            avatar.transform.LookAt(new Vector3(0, avatarHeight, 0));
            
            originalPositions[i] = position;
            originalRotations[i] = avatar.transform.rotation;
            
            // 添加淡入效果
            avatar.transform.localScale = Vector3.zero;
            avatar.transform.DOScale(Vector3.one, spawnDuration).SetEase(spawnEase);
            
            currentAvatars.Add(avatar);
        }
    }

    public void OnAvatarSelected(int index)
    {
        SelectionData data = new SelectionData
        {
            conditionSize = CurrentPrefabSet.Length,
            selectedAvatarIndex = index,
            selectionTime = Time.time - startTime
        };
        
        selectionHistory.Add(data);
        SaveProgress();
        
        // 显示选择结果的UI提示
        Debug.Log($"Selected Avatar {index} in condition {currentConditionIndex}");
        
        // 如果还有下一个条件，延迟一段时间开始
        if (currentConditionIndex < 2)
        {
            DOVirtual.DelayedCall(2f, () => StartCondition(currentConditionIndex + 1));
        }
    }

    private void SaveProgress()
    {
        string json = JsonUtility.ToJson(new { selections = selectionHistory });
        PlayerPrefs.SetString("SelectionProgress", json);
        PlayerPrefs.Save();
    }

    private void PreviewAvatar(int index)
    {
        if (currentSelectedIndex == index) return;

        // 如果有当前预览的Avatar，先让它回到原位
        if (currentPreviewAvatar != null)
        {
            ReturnAvatarToPosition(currentPreviewAvatar, currentSelectedIndex);
        }

        currentSelectedIndex = index;
        GameObject avatar = currentAvatars[index];
        currentPreviewAvatar = avatar;
        
        // 移动到圆心并面向Z轴负方向
        Sequence previewSequence = DOTween.Sequence();
        previewSequence.Append(avatar.transform.DOMove(new Vector3(0, avatarHeight, 0), moveToPreviewDuration).SetEase(previewEase));
        previewSequence.Join(avatar.transform.DORotate(new Vector3(0, 180, 0), moveToPreviewDuration));
    }

    private void ReturnAvatarToPosition(GameObject avatar, int index)
    {
        Sequence returnSequence = DOTween.Sequence();
        returnSequence.Append(avatar.transform.DOMove(originalPositions[index], returnDuration).SetEase(returnEase));
        returnSequence.Join(avatar.transform.DORotate(originalRotations[index].eulerAngles, returnDuration));
    }
} 