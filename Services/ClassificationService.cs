using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CoordinatorPro.Models;
using Newtonsoft.Json;

namespace CoordinatorPro.Services
{
    public static class ClassificationService
    {
        private static List<UniClassItem> _database;
        private static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

        // ✅ ÍNDICES PRÉ-CALCULADOS
        private static Dictionary<string, List<int>> _categoryIndex;
        private static Dictionary<string, List<int>> _keywordIndex;
        private static List<string> _uniclassStrings;

        private const int DEFAULT_CUTOFF = 40;
        private const int HIGH_CONFIDENCE_THRESHOLD = 80;

        // ✅ MAPEAMENTO SIMPLIFICADO (1 palavra-chave por categoria)
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
                string json = LoadDatabaseJson();

                if (string.IsNullOrEmpty(json))
                    return false;

                _database = JsonConvert.DeserializeObject<List<UniClassItem>>(json);

                if (_database == null || !_database.Any())
                {
                    _database = new List<UniClassItem>();
                    return false;
                }

                // ✅ CONSTRUIR ÍNDICES EM PARALELO
                BuildOptimizedIndex();

                System.Diagnostics.Debug.WriteLine($"✓ Base: {_database.Count} itens");
                System.Diagnostics.Debug.WriteLine($"✓ Cache pronto para {_cache.Count} entradas");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro: {ex.Message}");
                _database = new List<UniClassItem>();
                return false;
            }
        }

        // ✅ ÍNDICE OTIMIZADO COM HASH
        private static void BuildOptimizedIndex()
        {
            _uniclassStrings = new List<string>(_database.Count);
            _categoryIndex = new Dictionary<string, List<int>>();
            _keywordIndex = new Dictionary<string, List<int>>();

            // Processar em paralelo
            var tempStrings = new string[_database.Count];

            Parallel.For(0, _database.Count, i =>
            {
                var item = _database[i];

                // String de busca pré-processada
                string keywords = item.Keywords != null && item.Keywords.Any()
                    ? string.Join(" ", item.Keywords)
                    : "";

                tempStrings[i] = $"{item.Title} {keywords}".Trim().ToLowerInvariant();
            });

            _uniclassStrings.AddRange(tempStrings);

            // Indexar por palavras-chave
            for (int i = 0; i < _database.Count; i++)
            {
                var item = _database[i];

                // Indexar categoria
                string cat = item.Category?.ToLowerInvariant() ?? "unknown";
                if (!_categoryIndex.ContainsKey(cat))
                    _categoryIndex[cat] = new List<int>();
                _categoryIndex[cat].Add(i);

                // Indexar keywords
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

        private static string LoadDatabaseJson()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "CoordinatorPro.uniclass2015.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch { }

            try
            {
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonPath = Path.Combine(assemblyPath, "uniclass2015.json");

                if (File.Exists(jsonPath))
                {
                    return File.ReadAllText(jsonPath);
                }
            }
            catch { }

            return null;
        }

        // ✅ CLASSIFICAÇÃO ULTRA-RÁPIDA
        public static ClassificationResult Classify(Dictionary<string, string> elementData)
        {
            if (elementData == null || !elementData.Any())
                return CreateErrorResult("Dados vazios");

            if (_database == null || !_database.Any())
                return CreateErrorResult("Base não carregada");

            // ✅ CACHE COM CONCORRÊNCIA
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

            // ✅ BUSCA INTELIGENTE COM FILTRO AGRESSIVO
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

            // ✅ BUSCAR APENAS NOS TOP 100 CANDIDATOS (não todos!)
            int maxCandidates = Math.Min(100, targetIndices.Count);
            var topCandidates = targetIndices.Take(maxCandidates).ToList();
            var filteredStrings = topCandidates.Select(i => _uniclassStrings[i]).ToList();

            // Construir string de busca simplificada
            string searchString = BuildSimpleSearchString(elementData);

            if (string.IsNullOrEmpty(searchString))
                return CreateErrorResult("Sem dados");

            // ✅ FUZZY MATCH APENAS EM TOP 100
            var results = FuzzySharp.Process.ExtractTop(
                searchString,
                filteredStrings,
                limit: 1, // ✅ APENAS O MELHOR (não 3, não 5)
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

                    // Adicionar ao cache
                    if (best.Score > HIGH_CONFIDENCE_THRESHOLD && !string.IsNullOrEmpty(cacheKey))
                    {
                        _cache.TryAdd(cacheKey, classification);
                    }

                    return new ClassificationResult
                    {
                        Code = classification,
                        Confidence = best.Score,
                        Source = "FuzzyMatch",
                        Alternatives = new List<string>() // ✅ SEM ALTERNATIVAS = MAIS RÁPIDO
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

        // ✅ FILTRO ULTRA-AGRESSIVO
        private static List<int> GetTargetIndicesFast(Dictionary<string, string> elementData)
        {
            var indices = new HashSet<int>();

            // Filtrar por categoria mapeada
            if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
            {
                string revitCategory = elementData["Category"];

                if (CategoryMapping.TryGetValue(revitCategory, out string mappedKeyword))
                {
                    // Buscar diretamente no índice de keywords
                    if (_keywordIndex.TryGetValue(mappedKeyword, out var keywordIndices))
                    {
                        foreach (var idx in keywordIndices)
                            indices.Add(idx);
                    }

                    // Se encontrou muitos (>200), retornar só os primeiros
                    if (indices.Count > 200)
                        return indices.Take(200).ToList();
                }
            }

            // Se filtro muito pequeno, expandir um pouco
            if (indices.Count < 20)
            {
                // Adicionar alguns itens de categorias genéricas
                if (_categoryIndex.TryGetValue("products", out var productIndices))
                {
                    foreach (var idx in productIndices.Take(50))
                        indices.Add(idx);
                }
            }

            return indices.ToList();
        }

        // ✅ STRING DE BUSCA SIMPLIFICADA
        private static string BuildSimpleSearchString(Dictionary<string, string> elementData)
        {
            var parts = new List<string>();

            // Apenas categoria mapeada + type
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
            // Cache apenas por Category + Type (não Family)
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
                System.Diagnostics.Debug.WriteLine("Falha ao inicializar");
                return;
            }

            var testData = new Dictionary<string, string>
            {
                {"Category", "Walls"},
                {"Type", "Generic - 200mm"}
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Testar 100x para medir performance
            for (int i = 0; i < 100; i++)
            {
                var result = Classify(testData);
            }

            sw.Stop();

            System.Diagnostics.Debug.WriteLine($"100 classificações em {sw.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"Média: {sw.ElapsedMilliseconds / 100.0}ms por elemento");
        }

        public static string GetClassificationDebugInfo(Dictionary<string, string> elementData)
        {
            string searchString = BuildSimpleSearchString(elementData);
            List<int> targetIndices = GetTargetIndicesFast(elementData);
            return $"Search: '{searchString}' | Candidates: {targetIndices.Count}";
        }
    }
}