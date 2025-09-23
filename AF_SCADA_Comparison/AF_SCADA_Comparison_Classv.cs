using System.Collections.Generic;
using System;
using System.Linq;

namespace AF_SCADA_Comparison
{
    public class AF_SCADA_Comparison_Class
    {
        public string wellname { get; set; }           // key name (scada / af)
        public string welltype { get; set; }           // scada well type
        public string scadawellname { get; set; }      // scada display name
        public string aftemplate { get; set; }         // af template (for mismatch rows)
        public string scadawellstatus { get; set; }    // af "scada well status" value (for scan-off rows)
    }

    public class af_scada_comparison_result
    {
        public List<AF_SCADA_Comparison_Class> newinscada { get; } = new();
        public List<AF_SCADA_Comparison_Class> typemismatches { get; } = new();
        public List<AF_SCADA_Comparison_Class> scanoff { get; } = new();
    }

    public class WellComparer
    {
        /// <summary>
        /// scadawells: dictionary where key = scada well key name,
        /// value[0] = scada welltype, value[1] = scada display name.
        ///
        /// afwells: list of wellrow from pichildelementsclient.getwellswithscadastatusasync
        /// (wellname, welltype (template), scadawellstatus)
        /// </summary>
        public af_scada_comparison_result comparewells(
            Dictionary<string, object[]> scadawells,
            List<WellRow> afwells)
        {
            var result = new af_scada_comparison_result();

            // index af by wellname for quick lookups (case-insensitive)
            var afbyname = afwells
                .GroupBy(a => a.WellName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in scadawells)
            {
                var scadakeyname = kvp.Key;
                var scadatype = kvp.Value[0]?.ToString() ?? "";
                var scadadisplayname = kvp.Value.Length > 1 ? kvp.Value[1]?.ToString() ?? "" : "";

                if (!afbyname.TryGetValue(scadakeyname, out var af))
                {
                    // 1) in scada but not in af
                    result.newinscada.Add(new AF_SCADA_Comparison_Class
                    {
                        wellname = scadakeyname,
                        welltype = scadatype,
                        scadawellname = scadadisplayname
                    });
                    continue;
                }

                // 2) exists in both: check type (template) mismatch
                var aftemplate = af.WellType ?? "";
                if (!string.Equals(aftemplate, scadatype, StringComparison.OrdinalIgnoreCase))
                {
                    result.typemismatches.Add(new AF_SCADA_Comparison_Class
                    {
                        wellname = scadakeyname,
                        welltype = scadatype,
                        scadawellname = scadadisplayname,
                        aftemplate = aftemplate
                    });
                    continue;
                }

                // 3) exists in both and templates match: check if af says "scan off"
                var status = af.ScadaWellStatus ?? "";
                if (status.Equals("Scan Off", StringComparison.OrdinalIgnoreCase))
                {
                    result.scanoff.Add(new AF_SCADA_Comparison_Class
                    {
                        wellname = scadakeyname,
                        welltype = scadatype,
                        scadawellname = scadadisplayname,
                        scadawellstatus = status
                    });
                }
            }

            return result;
        }
    }
}
