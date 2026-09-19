namespace Yggdrasilnet.Server.Utils;

public static class AIUtility {
    public static float Clamp01(float v) => MathF.Min(1f, MathF.Max(0f, v));

    public static float Multiply(params float[] values) {
        float r = 1f;
        for (int i = 0; i < values.Length; i++)
            r *= Clamp01(values[i]);
        return r;
    }
}