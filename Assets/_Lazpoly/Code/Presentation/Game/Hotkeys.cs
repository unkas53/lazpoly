using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>What a key press on the game screen asks for.</summary>
    public enum HotkeyAction
    {
        None,
        /// <summary>Roll the dice (Space or Enter while the roll is offered).</summary>
        Roll,
        /// <summary>End the turn (Space or Enter while the turn can end).</summary>
        EndTurn,
        /// <summary>Open the building card (B).</summary>
        BuildCard,
        /// <summary>Open the trade editor (T).</summary>
        Trade,
        /// <summary>Open the "how to play" window (H).</summary>
        HowToPlay,
        /// <summary>Close the window on top (Esc).</summary>
        Close,
        /// <summary>Open the pause menu (Esc with nothing on top).</summary>
        PauseMenu,
        /// <summary>Show or hide the frame rate and memory overlay (F3).</summary>
        PerfOverlay,
        /// <summary>Step to the next store-screenshot camera set-up (F11).</summary>
        ScreenshotPreset,
        /// <summary>Save a screenshot (F12).</summary>
        Screenshot,
    }

    /// <summary>What is on the game screen when a key is pressed; <see cref="Hotkeys.Resolve"/> reads it.</summary>
    public struct HotkeyContext
    {
        /// <summary>A text field has the keyboard, so letters are typed, not commands.</summary>
        public bool Typing;

        /// <summary>A button holds the keyboard focus (Tab), so Enter and Space press it rather than roll or end the turn.</summary>
        public bool ButtonFocused;

        /// <summary>The pause menu is open.</summary>
        public bool PauseOpen;

        /// <summary>A window that Esc may close is open: settings, how to play, trade, the end screen or the building card.</summary>
        public bool ClosableOpen;

        /// <summary>A window that must be answered is open (the city card): Esc does nothing there.</summary>
        public bool BlockingOpen;

        /// <summary>A person at this screen is on turn and the board has finished animating.</summary>
        public bool Acting;

        public GamePhase Phase;

        /// <summary>The building card can be (re)opened for the current visit.</summary>
        public bool CanBuild;

        /// <summary>A trade may be offered now.</summary>
        public bool CanTrade;
    }

    /// <summary>
    /// The game screen's keyboard shortcuts. Every shortcut repeats a button that is on screen at the
    /// same moment, so nothing can be done by key that could not be done by mouse; the host still
    /// validates the command either way.
    /// </summary>
    public static class Hotkeys
    {
        /// <summary>The keys the screen listens to.</summary>
        public static readonly KeyCode[] Keys =
        {
            KeyCode.Escape, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.B, KeyCode.T, KeyCode.H,
            KeyCode.F3, KeyCode.F11, KeyCode.F12,
        };

        public static HotkeyAction Resolve(KeyCode key, in HotkeyContext context)
        {
            if (context.Typing) return HotkeyAction.None;

            // The function keys look at the picture, not the game, so windows do not block them.
            if (key == KeyCode.F3) return HotkeyAction.PerfOverlay;
            if (key == KeyCode.F11) return HotkeyAction.ScreenshotPreset;
            if (key == KeyCode.F12) return HotkeyAction.Screenshot;

            if (key == KeyCode.Escape)
            {
                if (context.PauseOpen || context.ClosableOpen) return HotkeyAction.Close;
                return context.BlockingOpen ? HotkeyAction.None : HotkeyAction.PauseMenu;
            }

            // While a window is open, its buttons are the only way forward.
            if (context.PauseOpen || context.ClosableOpen || context.BlockingOpen) return HotkeyAction.None;

            switch (key)
            {
                case KeyCode.Space:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (!context.Acting || context.ButtonFocused) return HotkeyAction.None;
                    if (context.Phase == GamePhase.WaitingForRoll) return HotkeyAction.Roll;
                    return context.Phase == GamePhase.TurnEnd ? HotkeyAction.EndTurn : HotkeyAction.None;
                case KeyCode.B:
                    return context.Acting && context.CanBuild ? HotkeyAction.BuildCard : HotkeyAction.None;
                case KeyCode.T:
                    return context.Acting && context.CanTrade ? HotkeyAction.Trade : HotkeyAction.None;
                case KeyCode.H:
                    return HotkeyAction.HowToPlay;
                default:
                    return HotkeyAction.None;
            }
        }
    }
}
