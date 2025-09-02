// Assets/Scripts/World/HygStarRecord.cs
using System.Globalization;

public class HygStarRecord
{
    public string proper, spect;
    public float lum;  // L/L☉
    public float ci;   // B-V

    public static bool TryParse(string line, out HygStarRecord rec)
    {
        rec = null;
        // sehr einfacher Parser; bei komplexem CSV ggf. ersetzen
        // Annahme: Felder sind durch Komma getrennt, Quotes selten.
        var parts = SplitCsv(line);
        if (parts == null || parts.Length < 35) return false;

        string proper = parts[6];
        string spect = parts[15];
        float.TryParse(parts[33], NumberStyles.Float, CultureInfo.InvariantCulture, out float lum); // "lum"
        float.TryParse(parts[16], NumberStyles.Float, CultureInfo.InvariantCulture, out float ci);  // "ci"

        if (string.IsNullOrWhiteSpace(spect)) spect = "G2V";
        if (float.IsNaN(lum) || lum <= 0) lum = 1f;

        rec = new HygStarRecord
        {
            proper = string.IsNullOrWhiteSpace(proper) ? "Unbenannter Stern" : proper,
            spect = spect,
            lum = lum,
            ci = ci
        };
        return true;
    }

    static string[] SplitCsv(string s)
    {
        var list = new System.Collections.Generic.List<string>();
        bool inQ = false; var cur = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            if (c == '\"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { list.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        list.Add(cur.ToString());
        return list.ToArray();
    }
}
