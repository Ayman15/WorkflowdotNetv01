//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace AF_SCADA_Comparison
//{
//    public class AF_SCADA_Comparison_Class
//    {
//        public string WellName { get; set; }           // key name (SCADA / AF)
//        public string WellType { get; set; }           // SCADA well type
//        public string ScadaWellName { get; set; }      // SCADA display name
//        public string AfTemplate { get; set; }         // AF template (for mismatch rows)
//        public string ScadaWellStatus { get; set; }    // AF "SCADA Well Status" value (for scan-off rows)
//    }

//    public class AF_SCADA_Comparison_Result
//    {
//        public List<AF_SCADA_Comparison_Class> NewInScada { get; } = new();
//        public List<AF_SCADA_Comparison_Class> TypeMismatches { get; } = new();
//        public List<AF_SCADA_Comparison_Class> ScanOff { get; } = new();
//    }

//    public class WellComparer
//    {
//        /// <summary>
//        /// scadaWells: Dictionary where Key = SCADA well key name,
//        /// Value[0] = SCADA WellType, Value[1] = SCADA Display Name.
//        ///
//        /// afWells: List of WellRow from PIChildElementsClient.GetWellsWithScadaStatusAsync
//        /// (WellName, WellType (template), ScadaWellStatus)
//        /// </summary>
//        public AF_SCADA_Comparison_Result CompareWells(
//            Dictionary<string, object[]> scadaWells,
//            List<WellRow> afWells)
//        {
//            var result = new AF_SCADA_Comparison_Result();

//            // Index AF by WellName for quick lookups (case-insensitive)
//            var afByName = afWells
//                .GroupBy(a => a.WellName, StringComparer.OrdinalIgnoreCase)
//                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

//            foreach (var kvp in scadaWells)
//            {
//                var scadaKeyName = kvp.Key;
//                var scadaType = kvp.Value[0]?.ToString() ?? "";
//                var scadaDisplayName = kvp.Value.Length > 1 ? kvp.Value[1]?.ToString() ?? "" : "";

//                if (!afByName.TryGetValue(scadaKeyName, out var af))
//                {
//                    // 1) In SCADA but NOT in AF
//                    result.NewInScada.Add(new AF_SCADA_Comparison_Class
//                    {
//                        WellName = scadaKeyName,
//                        WellType = scadaType,
//                        ScadaWellName = scadaDisplayName
//                    });
//                    continue;
//                }

//                // 2) Exists in both: check type (template) mismatch
//                var afTemplate = af.WellType ?? "";
//                if (!string.Equals(afTemplate, scadaType, StringComparison.OrdinalIgnoreCase))
//                {
//                    result.TypeMismatches.Add(new AF_SCADA_Comparison_Class
//                    {
//                        WellName = scadaKeyName,
//                        WellType = scadaType,
//                        ScadaWellName = scadaDisplayName,
//                        AfTemplate = afTemplate
//                    });
//                    continue;
//                }

//                // 3) Exists in both and templates MATCH: check if AF says "Scan Off"
//                var status = af.ScadaWellStatus ?? "";
//                if (status.Equals("Scan Off", StringComparison.OrdinalIgnoreCase))
//                {
//                    result.ScanOff.Add(new AF_SCADA_Comparison_Class
//                    {
//                        WellName = scadaKeyName,
//                        WellType = scadaType,
//                        ScadaWellName = scadaDisplayName,
//                        ScadaWellStatus = status
//                    });
//                }
//            }

//            return result;
//        }
//    }
//}
