using System;
using System.Linq;
using System.Threading.Tasks;
using AF_SCADA_ComparisonLib;

namespace AF_SCADA_Comparison1
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
            string baseUrl = "https://CXPIPSA01.agiba.local/PIWebAPI"; // <-- set your PI Web API base URL
            string parentWebId = "F1Emn5pF4QUB9kalAO9DHiZAeAjaorWn_z7xGBqgBQVq7czAQ1hQSVNSVjAxXEFHSUJBMjAyNVxBTExfV0VMTFM"; // All_Wells WebId

            var piClient = new PiChildElementsClient(baseUrl);
            Console.WriteLine("Fetching AF wells (Name, Template, SCADA Well Status)...");
            var afRows = await piClient.GetWellsWithScadaStatusAsync(parentWebId);

            // --- Compare ---
            var comparer = new WellComparer();
            var comparison = comparer.comparewells(scadaWells, afRows);

            // --- Print results ---

            // 1) New in SCADA
            Console.WriteLine("\n==== Wells in SCADA but NOT in AF ====");
            if (comparison.newinscada.Any())
            {
                foreach (var w in comparison.newinscada.OrderBy(x => x.wellname, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.wellname}, SCADA Type={w.welltype}, SCADA DisplayName={w.scadawellname}, SCADA Well Status ={w.scadawellstatus}");
            }
            else
            {
                Console.WriteLine("None");
            }

            // 2) Type mismatches
            Console.WriteLine("\n==== Type (Template) mismatches (SCADA vs AF) ====");
            if (comparison.typemismatches.Any())
            {
                foreach (var w in comparison.typemismatches.OrderBy(x => x.wellname, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.wellname}, SCADA Type={w.welltype}, AF Template={w.aftemplate}, SCADA DisplayName={w.scadawellname}, SCADA Well Status ={w.scadawellstatus}");
            }
            else
            {
                Console.WriteLine("None");
            }

            // 3) Scan Off in AF (exists in both, template matches)
            Console.WriteLine("\n==== AF wells with SCADA Well Status = \"Scan Off\" ====");
            if (comparison.scanoff.Any())
            {
                foreach (var w in comparison.scanoff.OrderBy(x => x.wellname, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"WellName={w.wellname}, Type={w.welltype}, SCADA Well Status ={w.scadawellstatus}, SCADA DisplayName={w.scadawellname}");
            }
            else
            {
                Console.WriteLine("None");
            }

            Console.WriteLine("\nComparison complete.");
            Console.ReadLine();
        }
    }
}

