using UnityEngine;

/// <summary>
/// Einheitliche Telemetrie für HUD-Anzeigen (Geschwindigkeit, Distanz).
/// </summary>
public static class ProbeTelemetry
{
    public static float GetSpeedUnitsPerSecond(Transform probeRoot)
    {
        if (probeRoot == null) return float.NaN;

        var autopilot = probeRoot.GetComponent<ProbeAutopilot>();
        if (autopilot != null && autopilot.IsAutopilotActive)
            return autopilot.CurrentSpeedUnits;

        var rb = probeRoot.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            return rb.linearVelocity.magnitude;

        if (autopilot != null)
            return autopilot.CurrentSpeedUnits;

        return rb != null ? rb.linearVelocity.magnitude : 0f;
    }
}
