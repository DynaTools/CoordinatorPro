using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CoordinatorPro.Models;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace CoordinatorPro.Services
{
    public static class ClassificationService
    {
        private static List<UniClassItem> _database;
        private static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

        private static Dictionary<string, List<int>> _categoryIndex;
        private static Dictionary<string, List<int>> _keywordIndex;
        private static List<string> _uniclassStrings;

        private const int DEFAULT_CUTOFF = 40;
        private const int HIGH_CONFIDENCE_THRESHOLD = 80;

        private static readonly Dictionary<string, string> CategoryMapping = new Dictionary<string, string>
        {
            {"Walls", "wall"},
            {"Doors", "door"},
            {"Windows", "window"},
            {"Floors", "floor"},
            {"Roofs", "roof"},
            {"Stairs", "stair"},
            {"Railings", "railing"},
            {"Columns", "column"},
            {"Structural Framing", "beam"},
            {"Structural Foundations", "foundation"},
            {"Mechanical Equipment", "mechanical"},
            {"Plumbing Fixtures", "sanitary"},
            {"Lighting Fixtures", "lighting"},
            {"Furniture", "furniture"},
            {"Casework", "casework"},
            {"Ceilings", "ceiling"},
            {"Curtain Panels", "panel"},
            {"Pipes", "pipe"},
            {"Ducts", "duct"},
            {"Cable Trays", "cable"}
        };

        public static bool Initialize()
        {
            if (_database != null)
                return true;

            try
            {
                // ✅ LER APENAS DO EXCEL (SEM FALLBACK)
                _database = LoadFromExcel();

                if (_database == null || !_database.Any())
                {
                    System.Diagnostics.Debug.WriteLine("✗ ERRO: Base de dados vazia");
                    _database = new List<UniClassItem>();
                    return false;
                }

                // CONSTRUIR ÍNDICES
                BuildOptimizedIndex();

                System.Diagnostics.Debug.WriteLine($"✓ Base carregada do Excel: {_database.Count} itens");
                System.Diagnostics.Debug.WriteLine($"✓ Índice Keywords: {_keywordIndex?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"✓ Cache pronto");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO ao inicializar: {ex.Message}");
                _database = new List<UniClassItem>();
                return false;
            }
        }

        /// <summary>
        /// ✅ LÊ APENAS DO EXCEL - SEM FALLBACK JSON
        /// </summary>
        private static List<UniClassItem> LoadFromExcel()
        {
            try
            {
                // Procurar Excel na pasta da DLL
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string excelPath = Path.Combine(assemblyPath, "Uniclass2015_Pr_v1_39.xlsx");

                if (!File.Exists(excelPath))
                {
                    System.Diagnostics.Debug.WriteLine($"✗ ERRO: Excel não encontrado!");
                    System.Diagnostics.Debug.WriteLine($"   Caminho esperado: {excelPath}");
                    System.Diagnostics.Debug.WriteLine($"   Pasta atual: {assemblyPath}");

                    // Listar arquivos na pasta para debug
                    if (Directory.Exists(assemblyPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"   Arquivos na pasta:");
                        foreach (var file in Directory.GetFiles(assemblyPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"     - {Path.GetFileName(file)}");
                        }
                    }

                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"✓ Excel encontrado: {excelPath}");

                // Configurar licença EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var items = new List<UniClassItem>();

                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    if (package.Workbook.Worksheets.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("✗ ERRO: Excel não tem abas");
                        return null;
                    }

                    var worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension?.Rows ?? 0;

                    if (rowCount < 2)
                    {
                        System.Diagnostics.Debug.WriteLine("✗ ERRO: Excel vazio ou sem dados");
                        return null;
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Lendo {rowCount} linhas do Excel...");

                    // Ler dados (linha 1 = header, dados começam na linha 2)
                    int itemsLoaded = 0;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        try
                        {
                            string code = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            string title = worksheet.Cells[row, 7].Value?.ToString()?.Trim();

                            // Pular linhas vazias
                            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(title))
                                continue;

                            var item = new UniClassItem
                            {
                                Code = code,
                                Title = title,
                                Keywords = ExtractKeywordsFromTitle(title),
                                Category = DetermineCategory(code),
                                Level = GetLevelFromCode(code),
                                Parent = GetParentCode(code)
                            };

                            items.Add(item);
                            itemsLoaded++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Erro na linha {row}: {ex.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ {itemsLoaded} itens carregados com sucesso");
                }

                return items;
            }
            catch (FileNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO: Arquivo Excel não encontrado");
                System.Diagnostics.Debug.WriteLine($"   {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO ao ler Excel: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Tipo: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
                return null;
            }
        }

        private static List<string> ExtractKeywordsFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return new List<string>();

            var separators = new[] { ' ', '-', ',', '/', '(', ')', '&', '.', ';' };

            var words = title.ToLowerInvariant()
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .Where(w => !IsStopWord(w))
                .Distinct()
                .ToList();

            return words;
        }

        private static bool IsStopWord(string word)
        {
            var stopWords = new HashSet<string> { "and", "the", "for", "with", "from", "into", "are", "was", "were", "been" };
            return stopWords.Contains(word);
        }

        private static string DetermineCategory(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "Unknown";

            if (code.StartsWith("Pr_15_31"))
                return "Applied cleaning and treatment products";
            else if (code.StartsWith("Pr_15"))
                return "Preparatory products";
            else if (code.StartsWith("Pr_"))
                return "Products";
            else
                return "Other";
        }

        private static int GetLevelFromCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return 0;

            return code.Count(c => c == '_') + 1;
        }

        private static string GetParentCode(string code)
        {
            if (string.IsNullOrEmpty(code) || !code.Contains("_"))
                return null;

            int lastUnderscore = code.LastIndexOf('_');
            return code.Substring(0, lastUnderscore);
        }

        private static void BuildOptimizedIndex()
        {
            _uniclassStrings = new List<string>(_database.Count);
            _categoryIndex = new Dictionary<string, List<int>>();
            _keywordIndex = new Dictionary<string, List<int>>();

            var tempStrings = new string[_database.Count];

            Parallel.For(0, _database.Count, i =>
            {
                var item = _database[i];

                string keywords = item.Keywords != null && item.Keywords.Any()
                    ? string.Join(" ", item.Keywords)
                    : "";

                tempStrings[i] = $"{item.Title} {keywords}".Trim().ToLowerInvariant();
            });

            _uniclassStrings.AddRange(tempStrings);

            for (int i = 0; i < _database.Count; i++)
            {
                var item = _database[i];

                string cat = item.Category?.ToLowerInvariant() ?? "unknown";
                if (!_categoryIndex.ContainsKey(cat))
                    _categoryIndex[cat] = new List<int>();
                _categoryIndex[cat].Add(i);

                if (item.Keywords != null)
                {
                    foreach (var keyword in item.Keywords)
                    {
                        string key = keyword.ToLowerInvariant();
                        if (!_keywordIndex.ContainsKey(key))
                            _keywordIndex[key] = new List<int>();
                        _keywordIndex[key].Add(i);
                    }
                }
            }
        }

        public static ClassificationResult Classify(Dictionary<string, string> elementData)
        {
            if (elementData == null || !elementData.Any())
                return CreateErrorResult("Dados vazios");

            if (_database == null || !_database.Any())
                return CreateErrorResult("Base não carregada");

            string cacheKey = BuildCacheKey(elementData);

            if (!string.IsNullOrEmpty(cacheKey) && _cache.TryGetValue(cacheKey, out string cachedResult))
            {
                return new ClassificationResult
                {
                    Code = cachedResult,
                    Confidence = 100,
                    Source = "Cache"
                };
            }

            List<int> targetIndices = GetTargetIndicesFast(elementData);

            if (!targetIndices.Any())
            {
                return new ClassificationResult
                {
                    Code = "NC - Sem correspondência",
                    Confidence = 0,
                    Source = "NoMatch"
                };
            }

            int maxCandidates = Math.Min(100, targetIndices.Count);
            var topCandidates = targetIndices.Take(maxCandidates).ToList();
            var filteredStrings = topCandidates.Select(i => _uniclassStrings[i]).ToList();

            string searchString = BuildSimpleSearchString(elementData);

            if (string.IsNullOrEmpty(searchString))
                return CreateErrorResult("Sem dados");

            var results = FuzzySharp.Process.ExtractTop(
                searchString,
                filteredStrings,
                limit: 1,
                cutoff: DEFAULT_CUTOFF
            );

            if (results != null && results.Any())
            {
                var best = results.First();
                int localIndex = filteredStrings.IndexOf(best.Value);
                int globalIndex = topCandidates[localIndex];

                if (globalIndex >= 0 && globalIndex < _database.Count)
                {
                    var item = _database[globalIndex];
                    string classification = $"{item.Code} - {item.Title}";

                    if (best.Score > HIGH_CONFIDENCE_THRESHOLD && !string.IsNullOrEmpty(cacheKey))
                    {
                        _cache.TryAdd(cacheKey, classification);
                    }

                    return new ClassificationResult
                    {
                        Code = classification,
                        Confidence = best.Score,
                        Source = "FuzzyMatch",
                        Alternatives = new List<string>()
                    };
                }
            }

            return new ClassificationResult
            {
                Code = "NC - Não Classificado",
                Confidence = 0,
                Source = "NoMatch"
            };
        }

        private static List<int> GetTargetIndicesFast(Dictionary<string, string> elementData)
        {
            var indices = new HashSet<int>();

            if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
            {
                string revitCategory = elementData["Category"];

                if (CategoryMapping.TryGetValue(revitCategory, out string mappedKeyword))
                {
                    if (_keywordIndex.TryGetValue(mappedKeyword, out var keywordIndices))
                    {
                        foreach (var idx in keywordIndices)
                            indices.Add(idx);
                    }

                    if (indices.Count > 200)
                        return indices.Take(200).ToList();
                }
            }

            if (indices.Count < 20)
            {
                if (_categoryIndex.TryGetValue("products", out var productIndices))
                {
                    foreach (var idx in productIndices.Take(50))
                        indices.Add(idx);
                }
            }

            return indices.ToList();
        }

        private static string BuildSimpleSearchString(Dictionary<string, string> elementData)
        {
            var parts = new List<string>();

            if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
            {
                string category = elementData["Category"];
                if (CategoryMapping.TryGetValue(category, out string mapped))
                    parts.Add(mapped);
            }

            if (elementData.ContainsKey("Type") && !string.IsNullOrEmpty(elementData["Type"]))
            {
                parts.Add(elementData["Type"]);
            }

            return string.Join(" ", parts).Trim().ToLowerInvariant();
        }

        private static string BuildCacheKey(Dictionary<string, string> elementData)
        {
            var parts = new List<string>();

            if (elementData.TryGetValue("Category", out string cat))
                parts.Add(cat);
            if (elementData.TryGetValue("Type", out string type))
                parts.Add(type);

            return parts.Any() ? string.Join("|", parts) : null;
        }

        private static ClassificationResult CreateErrorResult(string errorMessage)
        {
            return new ClassificationResult
            {
                Code = $"NC - {errorMessage}",
                Confidence = 0,
                Source = "Error"
            };
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        public static string GetStatistics()
        {
            return $"Base: {(_database?.Count ?? 0)} | Cache: {_cache.Count} | Índice Keywords: {_keywordIndex?.Count ?? 0}";
        }

        public static void TestClassification()
        {
            if (!Initialize())
            {
                System.Diagnostics.Debug.WriteLine("✗ Falha ao inicializar base de dados");
                return;
            }

            var testData = new Dictionary<string, string>
            {
                {"Category", "Walls"},
                {"Type", "Generic - 200mm"}
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                var result = Classify(testData);
            }

            sw.Stop();

            System.Diagnostics.Debug.WriteLine($"✓ 100 classificações em {sw.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"✓ Média: {sw.ElapsedMilliseconds / 100.0}ms por elemento");
        }

        public static string GetClassificationDebugInfo(Dictionary<string, string> elementData)
        {
            string searchString = BuildSimpleSearchString(elementData);
            List<int> targetIndices = GetTargetIndicesFast(elementData);
            return $"Search: '{searchString}' | Candidates: {targetIndices.Count}";
        }
    }
}