// Assets/Scripts/UI/Core/UIModule.cs
using UnityEngine;
using System;

public abstract class UIModule : MonoBehaviour
{
    [Header("Module Settings")]
    public bool startEnabled = true;
    
    protected bool isInitialized = false;
    
    public virtual void Initialize()
    {
        if (isInitialized) return;
        OnInitialize();
        isInitialized = true;
    }
    
    protected abstract void OnInitialize();
    
    public virtual void ShowModule() => gameObject.SetActive(true);
    public virtual void HideModule() => gameObject.SetActive(false);
    
    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }
}