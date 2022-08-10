using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OWML.Common.Menus;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Permadeath
{
    public class CampfireMeditate : MonoBehaviour
    {
        private Campfire campfire;
        private static IInputCommands meditateCommand = null;
        private ScreenPrompt meditatePrompt;

        private void Awake()
        {
            campfire = this.GetRequiredComponent<Campfire>();

            if (Permadeath.IsEnabled && campfire._interactVolume != null && campfire._canSleepHere)
            {
                campfire._interactVolume.OnGainFocus += OnGainFocus;
                campfire._interactVolume.OnLoseFocus += OnLoseFocus;

                if (meditateCommand == null)
                {
                    InputAction inputAction = new InputAction("meditateKey");
                    inputAction.AddBinding("<Keyboard>/v", groups: "KeyboardMouse");
                    inputAction.AddBinding("<Gamepad>/leftTrigger", groups: "Gamepad");
                    meditateCommand = new InputCommands((InputConsts.InputCommandType)2600, new BasicInputAction(inputAction));

                    InputCommandManager.MappedInputActions[meditateCommand.CommandType] = meditateCommand;
                }
                meditatePrompt = new ScreenPrompt(meditateCommand, "<CMD>Meditate Until Next Loop");
            }

        }

        private void Start()
        {
            if (Permadeath.IsEnabled && campfire._canSleepHere)
            {
                meditatePrompt.SetDisplayState(campfire.CanSleepHereNow() ? ScreenPrompt.DisplayState.Normal : ScreenPrompt.DisplayState.GrayedOut);
                meditateCommand.EnableAllActions(true);
            }
        }

        private void OnDestroy()
        {
            if (campfire._interactVolume != null && campfire._canSleepHere)
            {
                campfire._interactVolume.OnGainFocus -= OnGainFocus;
                campfire._interactVolume.OnLoseFocus -= OnLoseFocus;
            }

            meditateCommand = null;
        }

        private void OnGainFocus()
        {
            if (campfire._canSleepHere)
            {
                Locator.GetPromptManager().AddScreenPrompt(meditatePrompt, PromptPosition.Center, false);
            }
        }

        private void OnLoseFocus()
        {
            if (campfire._canSleepHere)
            {
                Locator.GetPromptManager().RemoveScreenPrompt(meditatePrompt, PromptPosition.Center);
            }
        }

        private void Update()
        {
            if (Permadeath.IsEnabled && campfire._canSleepHere)
            {
                meditatePrompt.SetVisibility(false);
                if (campfire._interactVolumeFocus && !campfire._isPlayerSleeping && !campfire._isPlayerRoasting && OWInput.IsInputMode(InputMode.Character))
                {
                    meditatePrompt.SetVisibility(true);
                    meditatePrompt.SetDisplayState(campfire.CanSleepHereNow() ? ScreenPrompt.DisplayState.Normal : ScreenPrompt.DisplayState.GrayedOut);
                    if (OWInput.IsNewlyPressed(meditateCommand, InputMode.All) && campfire.CanSleepHereNow())
                    {
                        OnPopupInteract();
                    }
                }
            }
        }

        IModMessagePopup popup = null;
        private void OnPopupInteract()
        {
            if (!Permadeath.IsEnabled || Locator.GetDeathManager().IsPlayerDying() || popup != null) return;

            popup = Permadeath.SharedModHelper.Menus.PopupManager.CreateMessagePopup(UITextLibrary.GetString(UITextType.PauseMeditate), true, UITextLibrary.GetString(UITextType.MenuConfirm), UITextLibrary.GetString(UITextType.MenuCancel));
            popup.OnConfirm += OnPopupConfirm;
            popup.OnCancel += OnPopupCancel;
            OWTime.Pause(OWTime.PauseType.Menu);
            OWInput.ChangeInputMode(InputMode.Menu);
            Locator.GetSceneMenuManager().pauseMenu._isOpen = true;
            Locator.GetPauseCommandListener().AddPauseCommandLock();
        }

        private void OnPopupConfirm()
        {
            CleanupPopup();
            Locator.GetDeathManager().KillPlayer(DeathType.Meditation);
        }

        private void OnPopupCancel()
        {
            CleanupPopup();
        }

        private void CleanupPopup()
        {
            popup.OnConfirm -= OnPopupConfirm;
            popup.OnCancel -= OnPopupCancel;
            popup = null;
            OWTime.Unpause(OWTime.PauseType.Menu);
            OWInput.RestorePreviousInputs();
            Locator.GetSceneMenuManager().pauseMenu._isOpen = false;
            Locator.GetPauseCommandListener().RemovePauseCommandLock();
        }
    }
}
