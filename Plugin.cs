using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using Unity.Netcode;  // For NetworkManager
using System.Collections;  // For Coroutine
using UnityEngine.InputSystem;  // For Keyboard.current

[BepInPlugin("com.yourname.pausemenu.mod", "SingleplayerPauseMod", "1.0.0")]
[MycoMod(null, ModFlags.IsClientSide)]
public class SingleplayerPauseMod : BaseUnityPlugin
{
    public static ManualLogSource LoggerInstance;  // Public static for access

    private static bool menuWasOpen = false;
    private static bool wasPaused = false;
    private static Coroutine pauseCoroutine = null;  // Track delayed pause

    void Awake()
    {
        LoggerInstance = Logger;  // Assign the BepInEx logger
        Logger.LogInfo("SingleplayerPauseMod loaded!");
    }

    void Update()
    {
        if (Menu.Instance == null) return;

        bool isOpen = Menu.Instance.IsOpen;
        bool isHub = LevelData.IsHub;  // From Menu.cs: Hubs are lobbies/startup; missions are in-game
        bool isSingleplayer = IsSingleplayer();

        //LoggerInstance.LogDebug($"Menu state check: IsOpen={isOpen}, WasOpen={menuWasOpen}, IsHub={isHub}, Singleplayer={isSingleplayer}, WasPaused={wasPaused}");

        // Handle ESC/TAB input during pause (using InputSystem low-level for compatibility)
        if (wasPaused && isSingleplayer && !isHub)
        {
            if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
            {
                //LoggerInstance.LogInfo("ESC/TAB detected during pause - temporarily resuming for input");
                Time.timeScale = 1f;  // Briefly resume to allow input processing and menu close
                AudioListener.pause = false;
                StartCoroutine(ResumeAfterInput(0.5f));  // Increased delay to 0.5s for close animation
            }
        }

        // Only pause for in-game menus (not hubs/lobbies/startup)
        if (!isHub)
        {
            // Detect open transition
            if (isOpen && !menuWasOpen && isSingleplayer && !wasPaused)
            {
                //LoggerInstance.LogInfo("Menu opened - starting delayed pause");
                if (pauseCoroutine != null) StopCoroutine(pauseCoroutine);
                pauseCoroutine = StartCoroutine(DelayedPause(1f));  // Increased delay to ~1s to ensure animation completes
            }
            // Detect close transition (only if not already handling input resume)
            else if (!isOpen && menuWasOpen && isSingleplayer && wasPaused)
            {
                Time.timeScale = 1f;  // Resume normal speed
                AudioListener.pause = false;  // Resume audio
                Cursor.lockState = CursorLockMode.Locked;  // Relock for gameplay (adjust if not FPS-style)
                wasPaused = false;
                if (pauseCoroutine != null)
                {
                    StopCoroutine(pauseCoroutine);
                    pauseCoroutine = null;
                }
                //LoggerInstance.LogInfo("Game resumed on menu close (singleplayer, in-mission)");
            }
        }
        else
        {
            //LoggerInstance.LogDebug("Skipping pause: In hub/lobby (IsHub=true)");
        }

        menuWasOpen = isOpen;
    }

    // Coroutine: Delay pause to allow menu animation to finish
    private IEnumerator DelayedPause(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);  // Unscaled wait
        if (Menu.Instance != null && Menu.Instance.IsOpen && IsSingleplayer())
        {
            Time.timeScale = 0f;  // Now pause
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            wasPaused = true;
            //LoggerInstance.LogInfo("Delayed pause applied after menu animation");
        }
        pauseCoroutine = null;
    }

    // Coroutine: Resume pause after input delay (allows ESC to process and animation to start)
    private IEnumerator ResumeAfterInput(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);  // Unscaled wait for animation/input to kick in
        if (Menu.Instance != null && Menu.Instance.IsOpen && IsSingleplayer())
        {
            Time.timeScale = 0f;  // Re-pause if menu still open
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            //LoggerInstance.LogInfo("Re-paused after input delay");
        }
        else
        {
            wasPaused = false;  // Menu closed, stay resumed
            //LoggerInstance.LogInfo("Menu closed during input resume - staying resumed");
        }
    }

    // Helper: Detect singleplayer using Netcode (from files)
    private static bool IsSingleplayer()
    {
        // Primary: Netcode client count (1 = singleplayer host; >1 = multiplayer)
        if (NetworkManager.Singleton != null)
        {
            int clientCount = NetworkManager.Singleton.ConnectedClients.Count;
            bool isSingle = clientCount <= 1;
            //LoggerInstance.LogDebug($"Netcode clients: {clientCount}, Singleplayer: {isSingle}");
            return isSingle;
        }

        // Fallback: GameManager players (from GameManager.cs)
        if (GameManager.Instance != null)
        {
            int playerCount = GameManager.players.Count;
            bool isSingle = playerCount <= 1;
            //LoggerInstance.LogDebug($"GameManager players: {playerCount}, Singleplayer: {isSingle}");
            return isSingle;
        }

        // Ultimate fallback: Assume singleplayer if no network (e.g., offline mode)
        //LoggerInstance.LogDebug("No network detected, assuming singleplayer");
        return true;
    }
}