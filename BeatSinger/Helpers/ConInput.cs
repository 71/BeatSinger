using UnityEngine;

namespace BeatSinger
{
    class ConInput
    {
        public enum Vive
        {
            RightMenu = KeyCode.JoystickButton0,
            LeftMenu = KeyCode.JoystickButton2,
            LeftTrackpadPress = KeyCode.JoystickButton8,
            RightTrackpadPress = KeyCode.JoystickButton9,
            LeftTrigger = KeyCode.JoystickButton14,
            RightTrigger = KeyCode.JoystickButton15,
            LeftTrackpadTouch = KeyCode.JoystickButton16,
            RightTrackpadTouch = KeyCode.JoystickButton17
        }
        public enum Oculus
        {
            AButton = KeyCode.JoystickButton0,
            BButton = KeyCode.JoystickButton1,
            XButton = KeyCode.JoystickButton2,
            YButton = KeyCode.JoystickButton3,
            Start = KeyCode.JoystickButton7,
            LeftThumbstickPress = KeyCode.JoystickButton8,
            RightThumbstickPress = KeyCode.JoystickButton9,
            LeftTrigger = KeyCode.JoystickButton14,
            RightTrigger = KeyCode.JoystickButton15,
            LeftThumbstickTouch = KeyCode.JoystickButton16,
            RightThumbstickTouch = KeyCode.JoystickButton17,
            LeftThumbRestTouch = KeyCode.JoystickButton18,
            RightThumbRestTouch = KeyCode.JoystickButton19
        }
        public enum WinMR
        {
            LeftGrip = KeyCode.JoystickButton4,
            RightGrip = KeyCode.JoystickButton5,
            LeftMenu = KeyCode.JoystickButton6,
            RightMenu = KeyCode.JoystickButton7,
            LeftThumbstickPress = KeyCode.JoystickButton8,
            RightThumbstickPress = KeyCode.JoystickButton9,
            LeftTrigger = KeyCode.JoystickButton14,
            RightTrigger = KeyCode.JoystickButton15,
            LeftTouchpadPress = KeyCode.JoystickButton16,
            RightTouchpadPress = KeyCode.JoystickButton17,
            LeftTouchpadTouch = KeyCode.JoystickButton18,
            RightTouchpadTouch = KeyCode.JoystickButton19
        }
    }
}
