// Assets/Scripts/Crafting/ProductIndex.cs
using System.Collections.Generic;

public static class ProductIndex
{
    private static readonly Dictionary<string, ProductBlueprint> map = new();

    public static void Register(IEnumerable<ProductBlueprint> blueprints)
    {
        if (blueprints == null) return;
        foreach (var bp in blueprints)
            if (bp != null && !string.IsNullOrEmpty(bp.productId))
                map[bp.productId] = bp;
    }

    public static ProductBlueprint Get(string id)
        => (id != null && map.TryGetValue(id, out var bp)) ? bp : null;
}
