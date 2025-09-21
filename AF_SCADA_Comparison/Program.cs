using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AF_SCADA_Comparison
{
    //    class Program
    //    {
    //        static async Task Main(string[] args)
    //        {
    //            // Initialize the SCADA reader and fetch the SCADA wells
    //            Read_MOWT_MSSQL scadaReader = new Read_MOWT_MSSQL();
    //            Console.WriteLine("Fetching SCADA wells...");
    //            var scadaWells = scadaReader.SQL_Reader();  // Returns a Dictionary<string, object[]>

    //            // Define the base URL for AF (PI Web API)
    //            string baseUrl = "https://localhost/piwebapi";  // Your AF base URL (make sure it's correct)
    //            PiChildElementsClient piClient = new PiChildElementsClient(baseUrl);

    //            // Fetch AF wells from PI Web API (using a known Parent WebId)
    //            Console.WriteLine("Fetching AF wells...");
    //            List<ChildElement> afWells = await piClient.GetChildElementsAsync("F1Emn5pF4QUB9kalAO9DHiZAeAjaorWn_z7xGBqgBQVq7czAQ1hQSVNSVjAxXEFHSUJBMjAyNVxBTExfV0VMTFM");

    //            // Perform the comparison between SCADA and AF wells
    //            WellComparer comparer = new WellComparer();
    //            Console.WriteLine("Comparing SCADA wells with AF wells...");
    //            List<AF_SCADA_Comparison_Class> comparisonResults = comparer.CompareWells(scadaWells, afWells);

    //            // Output the results
    //            Console.WriteLine("\nComparison Results:");

    //            // If there are new wells in SCADA that don't exist in AF
    //            var newWellsInScada = comparisonResults.Where(r => string.IsNullOrEmpty(r.AfTemplate) && string.IsNullOrEmpty(r.ScadaWellStatus)).ToList();
    //            if (newWellsInScada.Any())
    //            {
    //                Console.WriteLine("\nWells found in SCADA but not in AF:");
    //                foreach (var result in newWellsInScada)
    //                {
    //                    Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}");
    //                }
    //            }
    //            else
    //            {
    //                Console.WriteLine("\nNo new wells found in SCADA that are missing from AF.");
    //            }

    //            // If there are wells with template mismatches
    //            var templateMismatches = comparisonResults.Where(r => !string.IsNullOrEmpty(r.AfTemplate) && string.IsNullOrEmpty(r.ScadaWellStatus)).ToList();
    //            if (templateMismatches.Any())
    //            {
    //                Console.WriteLine("\nTemplate mismatches (AF template vs SCADA well type):");
    //                foreach (var result in templateMismatches)
    //                {
    //                    Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}, AF Template: {result.AfTemplate}");
    //                }
    //            }
    //            else
    //            {
    //                Console.WriteLine("\nNo template mismatches found between SCADA and AF.");
    //            }

    //            // If there are wells with Scan Off status in SCADA
    //            var scanOffWells = comparisonResults.Where(r => !string.IsNullOrEmpty(r.ScadaWellStatus) && r.ScadaWellStatus.Equals("Scan Off", StringComparison.OrdinalIgnoreCase)).ToList();
    //            if (scanOffWells.Any())
    //            {
    //                Console.WriteLine("\nWells with Scan Off status in SCADA:");
    //                foreach (var result in scanOffWells)
    //                {
    //                    Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}, SCADA Well Status: {result.ScadaWellStatus}");
    //                }
    //            }
    //            else
    //            {
    //                Console.WriteLine("\nNo SCADA wells with Scan Off status found.");
    //            }

    //            Console.WriteLine("\nComparison complete.");
    //        }
    //    }
    //}



class Program
{
    static async Task Main(string[] args)
    {
        // Initialize the SCADA reader and fetch the SCADA wells
        Read_MOWT_MSSQL scadaReader = new Read_MOWT_MSSQL();
        Console.WriteLine("Fetching SCADA wells...");
        var scadaWells = scadaReader.SQL_Reader();  // Returns a Dictionary<string, object[]>

        // Define the base URL for AF (PI Web API)
        string baseUrl = "https://localhost/piwebapi";  // Your AF base URL (make sure it's correct)
        PiChildElementsClient piClient = new PiChildElementsClient(baseUrl);

        // Fetch AF wells from PI Web API (using a known Parent WebId)
        Console.WriteLine("Fetching AF wells...");
        List<ChildElement> afWells = await piClient.GetChildElementsAsync("F1Emn5pF4QUB9kalAO9DHiZAeAjaorWn_z7xGBqgBQVq7czAQ1hQSVNSVjAxXEFHSUJBMjAyNVxBTExfV0VMTFM");

        // Perform the comparison between SCADA and AF wells
        WellComparer comparer = new WellComparer();
        Console.WriteLine("Comparing SCADA wells with AF wells...");
        List<AF_SCADA_Comparison_Class> comparisonResults = comparer.CompareWells(scadaWells, afWells);

        // Output the results
        Console.WriteLine("\nComparison Results:");

        // If there are new wells in SCADA that don't exist in AF
        var newWellsInScada = comparisonResults.Where(r => string.IsNullOrEmpty(r.AfTemplate) && string.IsNullOrEmpty(r.ScadaWellStatus)).ToList();
        if (newWellsInScada.Any())
        {
            Console.WriteLine("\nWells found in SCADA but not in AF:");
            foreach (var result in newWellsInScada)
            {
                Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}");
            }
        }
        else
        {
            Console.WriteLine("\nNo new wells found in SCADA that are missing from AF.");
        }

        // If there are wells with template mismatches
        var templateMismatches = comparisonResults.Where(r => !string.IsNullOrEmpty(r.AfTemplate) && string.IsNullOrEmpty(r.ScadaWellStatus)).ToList();
        if (templateMismatches.Any())
        {
            Console.WriteLine("\nTemplate mismatches (AF template vs SCADA well type):");
            foreach (var result in templateMismatches)
            {
                Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}, AF Template: {result.AfTemplate}");
            }
        }
        else
        {
            Console.WriteLine("\nNo template mismatches found between SCADA and AF.");
        }

        // If there are wells with Scan Off status in SCADA
        var scanOffWells = comparisonResults.Where(r => !string.IsNullOrEmpty(r.ScadaWellStatus) && r.ScadaWellStatus.Equals("Scan Off", StringComparison.OrdinalIgnoreCase)).ToList();
        if (scanOffWells.Any())
        {
            Console.WriteLine("\nWells with Scan Off status in SCADA:");
            foreach (var result in scanOffWells)
            {
                Console.WriteLine($"Well Name: {result.WellName}, SCADA Well Type: {result.WellType}, SCADA Well Name: {result.ScadaWellName}, SCADA Well Status: {result.ScadaWellStatus}");
            }
        }
        else
        {
            Console.WriteLine("\nNo SCADA wells with Scan Off status found.");
        }

        Console.WriteLine("\nComparison complete.");
    }
}
}
