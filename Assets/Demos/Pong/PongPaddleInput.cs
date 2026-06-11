using UnityEngine.InputSystem;

public static class PongPaddleInput
{
    public static float ReadVerticalAxis()
    {
        if (Keyboard.current == null)
        {
            return 0f;
        }

        float axis = 0f;
        // Z AZERTY = position physique W ; Z QWERTY = zKey
        if (Keyboard.current.zKey.isPressed || Keyboard.current.wKey.isPressed)
        {
            axis += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            axis -= 1f;
        }

        return axis;
    }
}
