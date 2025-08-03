// Assets/Scripts/World/OrbitAnimation.cs
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class OrbitAnimation : MonoBehaviour
{
    public float orbitalPeriodDays = 365f;
    public float timeScale = 50_000f;

    float degPerSec;

    public void Init(float realDays)
    {
        orbitalPeriodDays = realDays;
        CalcSpeed();
    }

    void Awake() => CalcSpeed();

    void Update()
    {
        if (orbitalPeriodDays <= 0) return;
        transform.RotateAround(Vector3.zero, Vector3.up, degPerSec * Time.deltaTime);
    }

    void CalcSpeed()
    {
        float realSeconds = orbitalPeriodDays * 86_400f;
        float simSeconds = realSeconds / timeScale;
        degPerSec = 360f / simSeconds;
    }
}
