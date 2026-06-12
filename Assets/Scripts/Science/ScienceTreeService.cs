using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Laufzeit-Zustand des Forschungsbaums (In-Memory, später an SaveSystem anbindbar).
/// </summary>
public class ScienceTreeService
{
    private static ScienceTreeService _instance;
    public static ScienceTreeService I => _instance ??= new ScienceTreeService();

    private readonly HashSet<string> _researched = new();
    private string _activeResearchId;
    private float _researchEndTimeUnscaled;

    public event Action Changed;

    public string ActiveResearchId => _activeResearchId;

    public float GetRemainingSeconds()
    {
        if (string.IsNullOrEmpty(_activeResearchId)) return 0f;
        return Mathf.Max(0f, _researchEndTimeUnscaled - Time.unscaledTime);
    }

    public ScienceTreeService()
    {
        foreach (var tech in ScienceTreeCatalog.All)
        {
            if (tech.StartsUnlocked)
                _researched.Add(tech.Id);
        }
    }

    public bool IsResearched(string id) => _researched.Contains(id);

    public ScienceTechState GetState(ScienceTechDefinition tech)
    {
        if (tech == null) return ScienceTechState.Locked;
        if (_researched.Contains(tech.Id)) return ScienceTechState.Researched;
        if (_activeResearchId == tech.Id) return ScienceTechState.InProgress;
        if (ArePrerequisitesMet(tech)) return ScienceTechState.Available;
        return ScienceTechState.Locked;
    }

    public bool CanStartResearch(ScienceTechDefinition tech)
    {
        if (tech == null) return false;
        if (_researched.Contains(tech.Id)) return false;
        if (!string.IsNullOrEmpty(_activeResearchId)) return false;
        return ArePrerequisitesMet(tech);
    }

    public bool TryStartResearch(ScienceTechDefinition tech)
    {
        if (!CanStartResearch(tech)) return false;

        if (tech.DurationSeconds <= 0f)
        {
            _researched.Add(tech.Id);
            Changed?.Invoke();
            GameEvents.PostHUDMessage($"Forschung abgeschlossen: {tech.Title}");
            return true;
        }

        _activeResearchId = tech.Id;
        _researchEndTimeUnscaled = Time.unscaledTime + tech.DurationSeconds;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Fortschritt prüfen; true wenn sich der Zustand geändert hat.</summary>
    public bool Tick()
    {
        if (string.IsNullOrEmpty(_activeResearchId)) return false;
        if (Time.unscaledTime < _researchEndTimeUnscaled) return false;

        var tech = ScienceTreeCatalog.Get(_activeResearchId);
        _activeResearchId = null;
        if (tech != null)
        {
            _researched.Add(tech.Id);
            GameEvents.PostHUDMessage($"Forschung abgeschlossen: {tech.Title}");
        }

        Changed?.Invoke();
        return true;
    }

    private bool ArePrerequisitesMet(ScienceTechDefinition tech)
    {
        if (tech.Prerequisites == null || tech.Prerequisites.Length == 0)
            return true;

        foreach (var prereq in tech.Prerequisites)
        {
            if (string.IsNullOrWhiteSpace(prereq)) continue;
            if (!_researched.Contains(prereq))
                return false;
        }

        return true;
    }

    public IEnumerable<ScienceTechDefinition> GetByTier(int tier) =>
        ScienceTreeCatalog.All.Where(t => t.Tier == tier).OrderBy(t => t.Branch).ThenBy(t => t.Title);

    public static string FormatDuration(float seconds)
    {
        if (seconds <= 0f) return "Sofort";
        if (seconds < 60f) return $"{Mathf.CeilToInt(seconds)} Sek.";
        if (seconds < 3600f) return $"{seconds / 60f:0.#} Min.";
        return $"{seconds / 3600f:0.#} Std.";
    }
}
