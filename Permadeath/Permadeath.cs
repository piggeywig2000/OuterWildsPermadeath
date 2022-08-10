using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OWML.Common;
using OWML.Common.Menus;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Permadeath
{
    public class Permadeath : ModBehaviour
    {
        public static IModHelper SharedModHelper;

        public static bool IsEnabled { get; private set; } = false;
        public static CompletionManager CompletionManager { get; private set; } = null;

        private GameObject GetRootObjectWithName(string name) => SceneManager.GetActiveScene().GetRootGameObjects().First(go => go.name == name);

        private TitleScreenManager GetTitleScreenManager() => GetRootObjectWithName("TitleMenuManagers").GetRequiredComponent<TitleScreenManager>();

        private void Awake()
        {
            GetTitleScreenManager().enabled = false;

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            Patches.TitleScreenPatches.OnInitializeProfileData += (s, e) => OnProfileChange();
            Patches.TitleScreenPatches.OnPopupReady += (s, e) => ShowClearProgressPopupIfNeeded();
            StandaloneProfileManager.SharedInstance.OnProfileSignInComplete += (r) => OnProfileChange();
            Patches.GameOverPatches.OnPermadeath += (s, e) => ClearProgress();

            Application.wantsToQuit += WantsToQuit;

            LoadManager.OnCompleteSceneLoad += (originalScene, loadScene) =>
            {
                if (loadScene == OWScene.SolarSystem || loadScene == OWScene.EyeOfTheUniverse)
                {
                    InitSolarSystemAndEye();
                    if (loadScene == OWScene.SolarSystem)
                    {
                        InitSolarSystem();
                    }
                }
                if (loadScene == OWScene.Credits_Final)
                {
                    SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
                    SaveManager.Save();
                }
            };
        }

        private void Start()
        {
            SharedModHelper = ModHelper;

            ModHelper.Menus.MainMenu.OnInit += InitMainMenu;
            ModHelper.Menus.PauseMenu.OnInit += InitPauseMenu;

            GetTitleScreenManager().enabled = true;
        }

        private void InitMainMenu()
        {
            TitleScreenManager titleScreenManager = GetTitleScreenManager();

            IModButton permadeathButton = ModHelper.Menus.MainMenu.NewExpeditionButton.Duplicate("NEW PERMADEATH EXPEDITION");

            SubmitActionLoadScene submitAction = permadeathButton.Button.gameObject.AddComponent<SubmitActionLoadScene>();
            submitAction.SetSceneToLoad(SubmitActionLoadScene.LoadableScenes.GAME);
            submitAction._confirmPopup = titleScreenManager._okCancelPopup;
            submitAction._loadingText = permadeathButton.Button.GetRequiredComponentInChildren<Text>();
            submitAction._titleScreenStreaming = GetRootObjectWithName("StreamingManager").GetComponent<TitleScreenStreaming>();

            submitAction.OnSubmitAction += () =>
            {
                PlayerData.ResetGame();
                SaveManager.Data.GetOrAddCurrentProfile().PermadeathEnabled = true;
                SaveManager.Save();
            };
            submitAction.OnPostSetupPopup += (s, popupToOpen) =>
            {
                string popupContent = $"You are about to start the game in permadeath mode. Try to complete the ship log but be warned, if you die due to something other than the supernova then your progress will be lost.\n\n{UITextLibrary.GetString(titleScreenManager._profileManager.currentProfileGameSave.loopCount > 1 ? UITextType.MenuResetGameConfirm : UITextType.MenuConfirmWarning)}";
                popupToOpen.SetUpPopup(popupContent, InputLibrary.confirm, InputLibrary.cancel, titleScreenManager._confirmActionPrompt, titleScreenManager._cancelActionPrompt, true, true);
            };
        }

        private void InitSolarSystemAndEye()
        {
            IsEnabled = SaveManager.Data.GetOrAddCurrentProfile().PermadeathEnabled;
            SaveManager.Save();
            if (IsEnabled)
            {
                ModHelper.Console.WriteLine("Permadeath mode enabled", MessageType.Info);
            }

            CompletionManager = GameObject.FindGameObjectWithTag("Global").AddComponent<CompletionManager>();
            SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = false;
            SaveManager.Save();
        }

        private void InitSolarSystem()
        {
            foreach (Campfire campfire in FindObjectsOfType<Campfire>())
            {
                campfire.gameObject.AddComponent<CampfireMeditate>();
            }
        }

        private void InitPauseMenu()
        {
            if (!IsEnabled || (LoadManager.GetCurrentScene() != OWScene.SolarSystem && LoadManager.GetCurrentScene() != OWScene.EyeOfTheUniverse)) return;

            SubmitActionLoadScene quitAction = ModHelper.Menus.PauseMenu.QuitButton.Button.GetRequiredComponent<SubmitActionLoadScene>();
            quitAction.OnPostSetupPopup += (sender, popupToOpen) =>
            {
                if (CompletionManager.Completion > 0)
                {
                    popupToOpen.SetUpPopup("You can only quit at the end of a time loop, either by being killed by the supernova or by meditating at a campfire. If you decide to quit anyway, <color=red>YOUR PROGRESS WILL BE DELETED</color>.", InputLibrary.confirm, InputLibrary.cancel,
                        new ScreenPrompt(InputLibrary.confirm, "QUIT <color=red>(DELETE ALL PROGRESS)</color>", 0, ScreenPrompt.DisplayState.Normal, false),
                        new ScreenPrompt(InputLibrary.cancel, UITextLibrary.GetString(UITextType.MenuCancel), 0, ScreenPrompt.DisplayState.Normal, false), true, true);
                }
            };
            quitAction.OnSubmitAction += () =>
            {
                ClearProgress();
                SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
                SaveManager.Save();
            };

            IModButton sleepButton = ModHelper.Menus.PauseMenu.Buttons.Find(button => button.Button.name == "Button-EndCurrentLoop");
            SubmitActionConfirm sleepAction = sleepButton.Button.GetRequiredComponent<SubmitActionSkipToNextLoop>();
            PopupMenu popup = sleepAction.GetPopupMenu();
            Destroy(sleepAction);
            sleepAction = sleepButton.Button.gameObject.AddComponent<SubmitActionConfirm>();
            sleepAction._confirmPopup = popup;
            sleepAction.OnPostSetupPopup += (sender, popupToOpen) =>
            {
                popupToOpen.SetUpPopup("To avoid cheating your way out of deaths, you can only meditate at a campfire.", InputLibrary.confirm, null,
                    new ScreenPrompt(InputLibrary.confirm, UITextLibrary.GetString(UITextType.KeyRebindingUpdatePopupContinueBtn)), null, true, false);
            };
            Locator.GetSceneMenuManager().pauseMenu._endCurrentLoopAction = sleepAction;
        }

        private void OnProfileChange()
        {
            IsEnabled = StandaloneProfileManager.SharedInstance.currentProfile == null ? false : SaveManager.Data.GetOrAddCurrentProfile().PermadeathEnabled;
            SaveManager.Save();
            ShowClearProgressPopupIfNeeded();

            if (LoadManager.GetCurrentScene() == OWScene.TitleScreen && ModHelper.Menus.MainMenu.ResumeExpeditionButton != null)
            {
                if (PlayerData.GetLastDeathType() == DeathType.BigBang || PlayerData.GetPersistentCondition("GAME_OVER_LAST_SAVE"))
                {
                    ModHelper.Menus.MainMenu.ResumeExpeditionButton.Title = IsEnabled ? "RESUME PERMADEATH EXPEDITION" : UITextLibrary.GetString(UITextType.MainMenuReload);
                }
                else
                {
                    ModHelper.Menus.MainMenu.ResumeExpeditionButton.Title = IsEnabled ? "RESUME PERMADEATH EXPEDITION" : UITextLibrary.GetString(UITextType.MainMenuResume);
                }
            }
        }

        private void ShowClearProgressPopupIfNeeded()
        {
            if (GetTitleScreenManager()._popupsToShow == StartupPopups.None && IsEnabled && !SaveManager.Data.GetOrAddCurrentProfile().SafeQuit)
            {
                ModHelper.Menus.PopupManager.CreateMessagePopup("Your last session ended unexpectedly. To prevent any cheating, your progress has been reset. I'm sorry if this happened due to a power cut.", false, UITextLibrary.GetString(UITextType.KeyRebindingUpdatePopupContinueBtn));
                ClearProgress();
                SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
                SaveManager.Save();
            }
        }

        private void ClearProgress()
        {
            if (!IsEnabled) return;

            string[] unclearableFacts = new string[]
            {
                "TH_VILLAGE_X1",
                "TH_VILLAGE_X2",
                "TH_VILLAGE_X3",
                "TM_ESKER_R1",
                "TM_EYE_LOCATOR_R1",
                "GD_GABBRO_ISLAND_R1",
            };
            int[] unclearableFrequencies = new int[]
            {
                AudioSignal.FrequencyToIndex(SignalFrequency.Default),
                AudioSignal.FrequencyToIndex(SignalFrequency.Traveler),
                AudioSignal.FrequencyToIndex(SignalFrequency.HideAndSeek)
            };
            foreach (KeyValuePair<string, ShipLogFactSave> kvp in StandaloneProfileManager.SharedInstance.currentProfileGameSave.shipLogFactSaves)
            {
                if (Array.IndexOf(unclearableFacts, kvp.Key) < 0)
                {
                    kvp.Value.revealOrder = -1;
                    kvp.Value.read = false;
                    kvp.Value.newlyRevealed = false;
                }
            }
            StandaloneProfileManager.SharedInstance.currentProfileGameSave.newlyRevealedFactIDs.RemoveAll(factId => Array.IndexOf(unclearableFacts, factId) < 0);
            Dictionary<int, bool> newKnownSignals = new Dictionary<int, bool>();
            for (int i = 0; i < StandaloneProfileManager.SharedInstance.currentProfileGameSave.knownFrequencies.Length; i++)
            {
                if (Array.IndexOf(unclearableFrequencies, i) < 0)
                {
                    StandaloneProfileManager.SharedInstance.currentProfileGameSave.knownFrequencies[i] = false;
                }
            }
            int[] signalIds = StandaloneProfileManager.SharedInstance.currentProfileGameSave.knownSignals.Keys.ToArray();
            foreach (int signalId in signalIds)
            {
                StandaloneProfileManager.SharedInstance.currentProfileGameSave.knownSignals[signalId] = false;
            }
            StandaloneProfileManager.SharedInstance.SaveGame(StandaloneProfileManager.SharedInstance.currentProfileGameSave, null, null, null);

            ModHelper.Console.WriteLine("Cleared progress", MessageType.Debug);
        }

        private bool WantsToQuit()
        {
            if (!IsEnabled ||
                (LoadManager.GetCurrentScene() != OWScene.SolarSystem && LoadManager.GetCurrentScene() != OWScene.EyeOfTheUniverse) ||
                SaveManager.Data.GetOrAddCurrentProfile().SafeQuit)
            {
                return true;
            }
            else if (CompletionManager != null && CompletionManager.Completion == 0)
            {
                SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
                SaveManager.Save();
                return true;
            }
            else
            {
                PopupMenu popup = ModHelper.Menus.PauseMenu.QuitButton.Button.GetRequiredComponent<SubmitActionConfirm>().GetPopupMenu();
                popup.SetUpPopup("You can only quit at the end of a time loop, either by being killed by the supernova or by meditating at a campfire. If you decide to quit anyway, <color=red>YOUR PROGRESS WILL BE DELETED</color>.", InputLibrary.confirm, InputLibrary.cancel,
                    new ScreenPrompt(InputLibrary.confirm, "QUIT <color=red>(DELETE ALL PROGRESS)</color>", 0, ScreenPrompt.DisplayState.Normal, false),
                    new ScreenPrompt(InputLibrary.cancel, UITextLibrary.GetString(UITextType.MenuCancel), 0, ScreenPrompt.DisplayState.Normal, false));
                popup.OnPopupConfirm += QuitConfirm;
                popup.OnPopupCancel += QuitCancel;
                popup.EnableMenu(true);
                OWTime.Pause(OWTime.PauseType.Menu);
                OWInput.ChangeInputMode(InputMode.Menu);
                if (!Locator.GetSceneMenuManager().pauseMenu.IsOpen())
                {
                    Locator.GetSceneMenuManager().pauseMenu._isOpen = true;
                    Locator.GetPauseCommandListener().AddPauseCommandLock();
                }

                return false;
            }
        }

        private void QuitConfirm()
        {
            CleanupPopup();
            ClearProgress();
            SaveManager.Data.GetOrAddCurrentProfile().SafeQuit = true;
            SaveManager.Save();
            Application.Quit();
        }

        private void QuitCancel()
        {
            CleanupPopup();
        }

        private void CleanupPopup()
        {
            PopupMenu popup = ModHelper.Menus.PauseMenu.QuitButton.Button.GetRequiredComponent<SubmitActionConfirm>().GetPopupMenu();
            popup.OnPopupConfirm -= QuitConfirm;
            popup.OnPopupCancel -= QuitCancel;
            OWTime.Unpause(OWTime.PauseType.Menu);
            OWInput.RestorePreviousInputs();
            if (!Locator.GetSceneMenuManager().pauseMenu._isPaused)
            {
                Locator.GetSceneMenuManager().pauseMenu._isOpen = false;
                Locator.GetPauseCommandListener().RemovePauseCommandLock();
            }
        }
    }
}
