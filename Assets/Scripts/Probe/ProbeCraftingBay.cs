using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProbeCraftingBay : MonoBehaviour
{
    [Tooltip("Welche Rezepte sind aktuell freigeschaltet?")]
    public List<CraftRecipe> unlockedRecipes = new();

    public Transform spawnPoint;          // leeres Child-Objekt vor der Sonde
    public event Action<CraftRecipe> OnCraftStarted, OnCraftFinished;

    Queue<(CraftRecipe, float endTime)> queue = new();

    /* ---------------- Public API ---------------- */
   // public bool CanCraft(CraftRecipe r) => r.costs.All(c => player.cargo.GetValueOrDefault(c.resource, 0) >= c.amount);
    public void StartCraft(CraftRecipe r)
    {
       // if (!CanCraft(r)) return;

        // 1) Ressourcen abziehen
        //foreach (var c in r.costs) player.cargo[c.resource] -= c.amount;

        // 2) Event & Build-Queue
        OnCraftStarted?.Invoke(r);
        queue.Enqueue((r, Time.time + r.buildTime));
    }

    /* ---------------- Loop ---------------- */
    void Update()
    {
        if (queue.Count == 0) return;

        var (recipe, end) = queue.Peek();
        if (Time.time < end) return;

        // Fertigstellen
        Instantiate(recipe.prefab, spawnPoint.position, spawnPoint.rotation);
        OnCraftFinished?.Invoke(recipe);
        queue.Dequeue();
    }
}
