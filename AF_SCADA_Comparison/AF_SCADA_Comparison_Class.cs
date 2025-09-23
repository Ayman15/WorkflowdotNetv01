using System;
using System.Collections.Generic;
using System.Linq;

namespace AF_SCADA_Comparison
{
    public class AF_SCADA_Comparison_Class
    {
        public string WellName { get; set; }
        public string WellType { get; set; }
        public string ScadaWellName { get; set; }
        public string AfTemplate { get; set; } // Only for template mismatch case
        public string ScadaWellStatus { get; set; } // For "Scan Off" case (AF well status attribute)
    }

    public class WellComparer
    {
        public List<AF_SCADA_Comparison_Class> CompareWells(Dictionary<string, object[]> scadaWells, List<ChildElement> afWells)
        {
            var comparisonResults = new List<AF_SCADA_Comparison_Class>();

            // Loop through each SCADA well
            foreach (var scadaWell in scadaWells)
            {
                string scadaWellName = scadaWell.Key;
                string scadaWellType = scadaWell.Value[0].ToString();
                string scadaWellDisplayName = scadaWell.Value[1].ToString();

                // Find the corresponding AF well
                var afWell = afWells.FirstOrDefault(w => w.Name == scadaWellName);

                if (afWell == null)
                {
                    // SCADA well is not found in AF, add it to the result list
                    comparisonResults.Add(new AF_SCADA_Comparison_Class
                    {
                        WellName = scadaWellName,
                        WellType = scadaWellType,
                        ScadaWellName = scadaWellDisplayName
                    });
                }
                else
                {
                    // Check if the template of AF does not match the SCADA well type
                    if (afWell.TemplateName != scadaWellType)
                    {
                        comparisonResults.Add(new AF_SCADA_Comparison_Class
                        {
                            WellName = scadaWellName,
                            WellType = scadaWellType,
                            ScadaWellName = scadaWellDisplayName,
                            AfTemplate = afWell.TemplateName
                        });
                    }

                    // Check if AF has a "Scan Off" status attribute
                    string scadaWellStatus = ""; // Get the well status from the AF attributes
                    try
                    {
                        // Assuming the AF Well has an attribute 'Scan Off' status.
                        // You will need to modify this code to fetch actual well attributes.
                        scadaWellStatus = afWell.Links.Attributes.Contains("Scan Off") ? "Scan Off" : "";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting SCADA Well Status: {ex.Message}");
                    }

                    if (scadaWellStatus.Equals("Scan Off", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add this well to the comparison result list
                        comparisonResults.Add(new AF_SCADA_Comparison_Class
                        {
                            WellName = scadaWellName,
                            WellType = scadaWellType,
                            ScadaWellName = scadaWellDisplayName,
                            ScadaWellStatus = scadaWellStatus
                        });
                    }
                }
            }

            return comparisonResults;
        }
    }

}