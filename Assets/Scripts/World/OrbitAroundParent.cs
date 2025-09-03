// Assets/Scripts/World/OrbitAroundParent.cs
using UnityEngine;

public class OrbitAroundParent : MonoBehaviour
{
    public float periodDays = 27.3f;
    void Update()
    {
        if (transform.parent == null) return;
        float degPerSec = 360f / (periodDays * 24f * 3600f);
        transform.RotateAround(transform.parent.position, Vector3.up, degPerSec * Time.deltaTime);
    }
}
