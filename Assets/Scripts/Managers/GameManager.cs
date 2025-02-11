using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameManager : Singleton<GameManager>
{
    [Header("Layer Masks")]
    [Tooltip("Layers that the characters can walk on")]
    public LayerMask groundMask;
    [Tooltip("Layers that contain enemies")]
    public LayerMask enemyMask;
    [Tooltip("Layers that contain players")]
    public LayerMask playerMask;

    private int _coinsCollected;
    public int CoinsCollected {get => _coinsCollected;
        set { 
            // Whenever we set the Coins Colected amount we should update the UI.
            if (_coinsCollected != value) {
                    _coinsCollected = value;
                    menuHelper.m_BottomLeftLabel.text = value != 0 ? $"Coins: {value}" : "";
                }
            }
    }

    MenuCreator menuHelper;
    public MenuCreator MenuHelper { get => menuHelper; }
    private PlayerInputManager playerInputManager;

    private int trackPlayerCount = 0;
    /// <summary>
    /// This ensures game manager will be created even if no one else calls Instance after the game starts.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartedWithoutGameManager()
    {
        SoundManager.IsQuittingGame = false;
        IsQuittingGame = false;
        Instance.Initialize();
    }


    public void Initialize()
    {
        Debug.Log("GameManager Initialized");
        CheckpointManager.ClearSceneHashCache();
    }

    // Start is called before the first frame update
    protected override void Awake()
    {
        base.Awake();

        if (creationFailed)
            return;
        SoundManager.Init();
        Application.quitting += OnQuitting;
        TryGetComponent(out playerInputManager);
        TryGetComponent(out menuHelper);
    }


    public void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    protected void Start()
    {
        SceneTransitioner.Instance.NewSceneLoaded.AddListener(CheckpointManager.HandleSceneLoad);
        CheckpointManager.HandleSceneLoad(SceneManager.GetActiveScene().name);
        if (playerInputManager)
        {
            //playerInputManager.joinAction = pressToJoin;
            playerInputManager.EnableJoining();
        }

        ToggleShowPressStartToJoin();

        if (PlayerController.players.Count == 0 && Camera.allCamerasCount == 0)
        {
            var newCameraGO = new GameObject("Camera");
            newCameraGO.AddComponent<Camera>();
            newCameraGO.transform.position = CheckpointManager.CurrentCheckpoint.w != 0 ?
(Vector3)CheckpointManager.CurrentCheckpoint + ((Vector3.one * 10f)) : (Vector3.one * 10f);
            newCameraGO.transform.rotation = Quaternion.Euler(Quaternion.LookRotation(newCameraGO.transform.position.directionTo(CheckpointManager.CurrentCheckpoint.w != 0 ?
(Vector3)CheckpointManager.CurrentCheckpoint : (Vector3.one))).eulerAngles.SetZ(0f));
        }
    }

    public void Update()
    {
        PositionSoundManagerInPlayerMiddle();
        if (trackPlayerCount != PlayerController.players.Count) {
            trackPlayerCount = PlayerController.players.Count;
            ToggleShowPressStartToJoin();
        }
    }

    public void PositionSoundManagerInPlayerMiddle()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < PlayerController.players.Count; i++)
        {
            sum += PlayerController.players[i].transform.position;
        }
        SoundManager.Instance.transform.position = sum / Mathf.Max(PlayerController.players.Count, 1);
    }

    private void OnQuitting()
    {
        Application.quitting -= OnQuitting;
        CheckpointManager.checkpoints.Clear();
        PlayerController.players.Clear();
        _instance = null;
    }

    public void OnPlayerJoined(PlayerInput inputJoined)
    {
        if (inputJoined.TryGetComponent(out PlayerController playerController)) {
            playerController.JoinedThroughGameManager = true;
        }
        SceneTransitioner.DisableOtherSceneCameras(true);
        ToggleShowPressStartToJoin();
    }

    public void OnPlayerLeft(PlayerInput inputLeft)
    {
        if (Camera.main != null && PlayerController.players.Count == 0)
            Camera.main.gameObject.SetActive(true);
        ToggleShowPressStartToJoin();
    }

    public void ToggleShowPressStartToJoin()
    {
        MenuHelper.m_MiddleScreenLabel.text = PlayerController.players.Count == 0 ? "Press [ A ] on gamepad\nor [ Space ] to start!" : "";
    }

    bool _isShowingOtherMenu = false;
    public bool IsShowingOtherMenu { get { return _isShowingOtherMenu; } set
        {
            _isShowingOtherMenu = value;
            if (playerInputManager != null)
            {
                if (value)
                {
                    playerInputManager.DisableJoining();
                } else
                {
                    playerInputManager.EnableJoining();
                }
            }
        }
    }
    bool _isShowingPauseMenu = false;
    public bool IsShowingPauseMenu {get => _isShowingPauseMenu; }


    public void TogglePauseMenu()
    {
        if (SceneTransitioner.Transitioning) return;

        if (!_isShowingPauseMenu)
        {
            menuHelper.StartBasicMenu("Pause Menu");
            menuHelper.AddButton(new MenuButton("Reset", ResetLevel));
            menuHelper.AddButton(new MenuButton("Unpause", TogglePauseMenu));
            _isShowingPauseMenu = true;
        } else
        {
            menuHelper.HideMenu();
            _isShowingPauseMenu = false;
        }
    }

    public void ResetLevel()
    {
        SceneTransitioner.LoadScene(SceneManager.GetActiveScene().name);
        menuHelper.HideMenu();
        _isShowingPauseMenu = false;
    }
}
