// Assets/Scripts/UI/Core/UIModuleManager.cs
using System.Collections.Generic;
using UnityEngine;
using System;

public class UIModuleManager : MonoBehaviour
{
    [Header("UI Modules")]
    public List<UIModule> modules = new List<UIModule>();
    
    private Dictionary<Type, UIModule> moduleDict = new Dictionary<Type, UIModule>();
    
    public static UIModuleManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeModules();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void InitializeModules()
    {
        // Auto-find modules if not assigned
        if (modules.Count == 0)
        {
            modules.AddRange(GetComponentsInChildren<UIModule>());
        }
        
        foreach (var module in modules)
        {
            module.Initialize();
            moduleDict[module.GetType()] = module;
            
            if (!module.startEnabled)
                module.HideModule();
        }
    }
    
    public T GetModule<T>() where T : UIModule
    {
        moduleDict.TryGetValue(typeof(T), out UIModule module);
        return module as T;
    }
    
    public void ShowModule<T>() where T : UIModule => GetModule<T>()?.ShowModule();
    public void HideModule<T>() where T : UIModule => GetModule<T>()?.HideModule();
}