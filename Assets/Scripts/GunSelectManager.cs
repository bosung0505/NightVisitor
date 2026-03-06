using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using UnityEngine.Rendering;

public class GunSelectManager : MonoBehaviour
{
    [Header("Gun Select UI Elements")]
    public CanvasGroup gunSelectPanel; 
    
    [Header("Gun Buttons and Selection Images")]
    public Button gun1Button;
    public GameObject image1; 

    public Button gun2Button;
    public GameObject image2;

    public Button gun3Button;
    public GameObject image3;

    [Header("Countdown Settings")]
    public TextMeshProUGUI countdownText;
    public int countdownSeconds = 10;
    
    [Header("In-Game Transition Targets")]
    public CanvasGroup inGameUIPanel; 
    public Volume volumeStart; // (이제 Priority를 1 줘서 사용, Global은 항상 켜져 있음)
    public float transitionDuration = 1.0f;

    [Header("Game Elements To Pause")]
    [Tooltip("All animal AI and systems in the IN_GAME object that must wait for the countdown")]
    public GameObject inGameEnvironment;

    private int selectedGunIndex = 1; 
    private Coroutine countdownCoroutine;

    void Start()
    {
        // 1. IN_GAME 환경은 눈에 보이게 켜두되(그래야 숲이 보임), 
        // 동물이 시작 전부터 움직이지 못하게 하기 위한 초기 세팅(TimeScale/볼륨)은 이제 StartGunSelection()에서 공통 처리합니다.
        if (inGameEnvironment != null && !inGameEnvironment.activeSelf)
        {
            inGameEnvironment.SetActive(true);
        }

        // 3. Setup Buttons
        if (gun1Button != null) gun1Button.onClick.AddListener(() => SelectGun(1));
        if (gun2Button != null) gun2Button.onClick.AddListener(() => SelectGun(2));
        if (gun3Button != null) gun3Button.onClick.AddListener(() => SelectGun(3));

        SelectGun(1);
    }

    public void PrepareGunSelection()
    {
        // --- 재시작(새 스테이지) 초기화 로직 (로딩 직후 즉시 호출됨) ---
        Time.timeScale = 0f; // 시간 정지!
        
        if (inGameUIPanel != null)
        {
            inGameUIPanel.alpha = 0f;
            inGameUIPanel.gameObject.SetActive(false);
        }
        
        // Priority 1인 덮어쓰기용 볼륨을 100% 켬 (게임이 켜지자마자 번쩍이는 현상 방지)
        if (volumeStart != null) volumeStart.weight = 1f;
        // ----------------------------------------------------------------------
    }

    public void StartGunSelection()
    {
        gameObject.SetActive(true);
        if (gunSelectPanel != null)
        {
            gunSelectPanel.alpha = 1f;
            gunSelectPanel.interactable = true;
            gunSelectPanel.blocksRaycasts = true;
        }

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private void SelectGun(int gunIndex)
    {
        selectedGunIndex = gunIndex;

        if (image1 != null) image1.SetActive(gunIndex == 1);
        if (image2 != null) image2.SetActive(gunIndex == 2);
        if (image3 != null) image3.SetActive(gunIndex == 3);
        
        Debug.Log("Selected Gun: " + gunIndex);
    }

    private IEnumerator CountdownRoutine()
    {
        int currentCount = countdownSeconds;

        while (currentCount > 0)
        {
            if (countdownText != null) countdownText.text = currentCount.ToString();
            // Time.timeScale=0 이므로 시간 영향을 받지 않는 WaitForSecondsRealtime 사용
            yield return new WaitForSecondsRealtime(1f);
            currentCount--;
        }

        if (countdownText != null) countdownText.text = "0";

        StartGameTransition();
    }

    private void StartGameTransition()
    {
        if (gunSelectPanel != null)
        {
            gunSelectPanel.interactable = false;
            gunSelectPanel.blocksRaycasts = false;
            
            // 패널이 사라지는 이펙트가 '완전히 끝마친 직후' OnComplete를 통해 다음 절차를 실행합니다.
            gunSelectPanel.DOFade(0f, transitionDuration).SetUpdate(true).OnComplete(() => 
            {
                ActivateInGameLogic();
            });
        }
        else
        {
            ActivateInGameLogic();
        }
    }

    private void ActivateInGameLogic()
    {
        // 1. 멈춰둔 시간(TimeScale)을 1로 먼저 되돌려줍니다! (이때 물리/애니메이션 바로 정상화됨)
        Time.timeScale = 1f;

        // 2. InGame_Panel (탄약, 배터리 등 UI) 서서히 켜기
        if (inGameUIPanel != null)
        {
            inGameUIPanel.gameObject.SetActive(true);
            inGameUIPanel.DOFade(1f, transitionDuration);
        }

        // 3. 덮어씌워둔 Volume_Start의 껍질만 서서히 치웁니다.
        // 그러면 자연스럽게 항상 켜져있던(Priority 0) Global Volume의 원본 색감이 100% 드러납니다!
        if (volumeStart != null)
        {
            DOTween.To(() => volumeStart.weight, x => volumeStart.weight = x, 0f, transitionDuration);
        }
    }
}
