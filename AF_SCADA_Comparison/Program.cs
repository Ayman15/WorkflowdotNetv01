using System;
using System.Linq;
using System.Threading.Tasks;

namespace AF_SCADA_Comparison
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // --- SCADA side ---
            var scadaReader = new Read_MOWT_MSSQL();
            Console.WriteLine("Fetching SCADA wells...");
            var scadaWells = scadaReader.SQL_Reader();  // Dictionary<string, object[]>

            // --- AF side ---
            string baseUrl = "https://cxpsa01/piwebapi"; // <-- set your PI Web API base URL
            string parentWebId = "F1Emn5pF4QUB9kalAO9DHiZAeAjaorWn_z7xGBqgBQVq7czAQ1hQSVNSVjAxXEFHSUJBMjAyNVxBTExfV0VMTFM"; // All_Wells WebId

            var piClient = new PiChildElementsClient(baseUrl);
            Console.WriteLine("Fetching AF wells (Name, Template, SCADA Well Status)...");
            var afRows = await piClient.GetWellsWithScadaStatusAsync(parentWebId);

            // --- Compare ---
            var comparer = new WellComparer();
            var comparison = comparer.CompareWells(scadaWells, afRows);

            // --- Print results ---

            // 1) New in SCADA
            Console.WriteLine("\n==== Wells in SCADA but NOT in AF ====");
            if (comparison.NewInScada.Any())
            {
                foreach (var w in comparison.NewInScada.OrderBy(x => x.WellName, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.WellName}, SCADA Type={w.WellType}, SCADA DisplayName={w.ScadaWellName}");
            }
            else
            {
                Console.WriteLine("None");
            }

            // 2) Type mismatches
            Console.WriteLine("\n==== Type (Template) mismatches (SCADA vs AF) ====");
            if (comparison.TypeMismatches.Any())
            {
                foreach (var w in comparison.TypeMismatches.OrderBy(x => x.WellName, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.WellName}, SCADA Type={w.WellType}, AF Template={w.AfTemplate}, SCADA DisplayName={w.ScadaWellName}");
            }
            else
            {
                Console.WriteLine("None");
            }

            // 3) Scan Off in AF (exists in both, template matches)
            Console.WriteLine("\n==== AF wells with SCADA Well Status = \"Scan Off\" ====");
            if (comparison.ScanOff.Any())
            {
                foreach (var w in comparison.ScanOff.OrderBy(x => x.WellName, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.WellName}, Type={w.WellType}, Status={w.ScadaWellStatus}, SCADA DisplayName={w.ScadaWellName}");
            }
            else
            {
                Console.WriteLine("None");
            }

            Console.WriteLine("\nComparison complete.");
        }
    }
}
