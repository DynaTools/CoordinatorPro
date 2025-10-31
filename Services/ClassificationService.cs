// Services/ClassificationService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CoordinatorPro.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CoordinatorPro.Services
{
    public static class ClassificationService
    {
        private static List<UniClassItem> _database;
        private static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        private static Dictionary<string, float[]> _embeddingCache = new Dictionary<string, float[]>();
        private static string _loadedJsonPath; // ✅ NOVO: guardar o caminho carregado

        private const int HIGH_CONFIDENCE_THRESHOLD = 80;

        private static readonly HashSet<string> Antonyms = new HashSet<string>
        {
            "interior|exterior", "internal|external", "inside|outside",
            "upper|lower", "top|bottom", "left|right",
            "front|back", "north|south", "east|west"
        };

        public static bool Initialize()
        {
            if (_database != null)
                return true;

            try
            {
                if (!EmbeddingService.Initialize())
                {
                    System.Diagnostics.Debug.WriteLine("Falha ao inicializar serviço de embeddings");
                    return false;
                }

                _database = LoadFromJson();

                if (_database == null || !_database.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Base de dados vazia");
                    _database = new List<UniClassItem>();
                    return false;
                }

                PrecomputeEmbeddings();

                System.Diagnostics.Debug.WriteLine($"Base carregada: {_database.Count} itens de {_loadedJsonPath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao inicializar: {ex.Message}");
                _database = new List<UniClassItem>();
                return false;
            }
        }

        // ✅ NOVO: Retornar o caminho do JSON carregado
        public static string GetLoadedJsonPath()
        {
            return _loadedJsonPath;
        }

        private static void PrecomputeEmbeddings()
        {
            System.Diagnostics.Debug.WriteLine("Pré-computando embeddings...");

            Parallel.ForEach(_database, item =>
            {
                try
                {
                    string text = $"{item.Title} {string.Join(" ", item.Keywords ?? new List<string>())}";
                    var embedding = EmbeddingService.GenerateEmbedding(text);

                    lock (_embeddingCache)
                    {
                        _embeddingCache[item.Code] = embedding;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao gerar embedding para {item.Code}: {ex.Message}");
                }
            });

            System.Diagnostics.Debug.WriteLine($"Embeddings gerados: {_embeddingCache.Count}");
        }

        private static List<UniClassItem> LoadFromJson()
        {
            try
            {
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                // ✅ ORDEM DE PRIORIDADE DOS NOMES
                var candidateFileNames = new[]
                {
                    "Pr_Uniclass.json",           // Nome atual do usuário
                    "Uniclass2015_Pr_v1_39.json",
                    "uniclass_products.json",
                    "Uniclass.json"
                };

                var attemptedPaths = new List<string>();
                string foundPath = null;

                void CheckDirectory(string dir)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        return;

                    foreach (var name in candidateFileNames)
                    {
                        var p = Path.Combine(dir, name);
                        attemptedPaths.Add(p);
                        if (File.Exists(p) && foundPath == null)
                        {
                            foundPath = p;
                            return;
                        }
                    }

                    if (foundPath == null)
                    {
                        try
                        {
                            var matches = Directory.GetFiles(dir, "*uniclass*.json", SearchOption.TopDirectoryOnly)
                                .Concat(Directory.GetFiles(dir, "*Uniclass*.json", SearchOption.TopDirectoryOnly))
                                .Concat(Directory.GetFiles(dir, "Pr_*.json", SearchOption.TopDirectoryOnly));

                            foreach (var m in matches.Distinct())
                            {
                                attemptedPaths.Add(m);
                                if (foundPath == null)
                                {
                                    foundPath = m;
                                    return;
                                }
                            }
                        }
                        catch { }
                    }
                }

                CheckDirectory(assemblyPath);
                CheckDirectory(Directory.GetCurrentDirectory());
                CheckDirectory(AppDomain.CurrentDomain.BaseDirectory);

                try
                {
                    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    if (!string.IsNullOrEmpty(programData))
                    {
                        var revitAddins = Path.Combine(programData, "Autodesk", "Revit", "Addins");
                        CheckDirectory(revitAddins);

                        if (Directory.Exists(revitAddins))
                        {
                            foreach (var sub in Directory.GetDirectories(revitAddins))
                            {
                                CheckDirectory(sub);
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    var current = assemblyPath;
                    for (int i = 0; i < 3 && !string.IsNullOrEmpty(current); i++)
                    {
                        CheckDirectory(current);
                        var parent = Directory.GetParent(current);
                        if (parent == null) break;
                        current = parent.FullName;
                    }
                }
                catch { }

                if (foundPath == null)
                {
                    System.Diagnostics.Debug.WriteLine("JSON não encontrado. Caminhos verificados:");
                    foreach (var p in attemptedPaths.Distinct())
                    {
                        System.Diagnostics.Debug.WriteLine(p);
                    }
                    return null;
                }

                _loadedJsonPath = foundPath; // ✅ Guardar caminho
                System.Diagnostics.Debug.WriteLine($"Carregando JSON de: {foundPath}");

                var items = new List<UniClassItem>();
                string jsonContent = File.ReadAllText(foundPath);
                var jsonData = JObject.Parse(jsonContent);

                if (!jsonData.ContainsKey("items"))
                {
                    System.Diagnostics.Debug.WriteLine("JSON sem propriedade 'items'");
                    return null;
                }

                var itemsObject = jsonData["items"] as JObject;
                if (itemsObject == null) return null;

                foreach (var property in itemsObject.Properties())
                {
                    try
                    {
                        var itemData = property.Value as JObject;
                        if (itemData == null) continue;

                        string code = itemData["code"]?.ToString();
                        string title = itemData["title"]?.ToString();

                        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(title))
                            continue;

                        var item = new UniClassItem
                        {
                            Code = code,
                            Title = title,
                            Keywords = ExtractKeywordsFromTitle(title),
                            Category = itemData["category"]?.ToString() ?? "",
                            Level = itemData["level"]?.ToObject<int>() ?? GetLevelFromCode(code),
                            Parent = itemData["parent"]?.ToString()
                        };

                        items.Add(item);
                    }
                    catch { }
                }

                return items;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao ler JSON: {ex.Message}");
                return null;
            }
        }

        private static List<string> ExtractKeywordsFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return new List<string>();

            var separators = new[] { ' ', '-', ',', '/', '(', ')', '&', '.', ';' };
            var stopWords = new HashSet<string> { "and", "the", "for", "with", "from", "into", "are", "was", "were", "been" };

            return title.ToLowerInvariant()
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .ToList();
        }

        private static int GetLevelFromCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return 0;

            return code.Count(c => c == '_') + 1;
        }

        public static ClassificationResult Classify(Dictionary<string, string> elementData, int maxLevel = 4)
        {
            if (elementData == null || !elementData.Any())
                return CreateErrorResult("Dados vazios");

            if (_database == null || !_database.Any())
                return CreateErrorResult("Base não carregada");

            if (maxLevel < 1 || maxLevel > 4)
                maxLevel = 4;

            string cacheKey = BuildCacheKey(elementData);

            if (!string.IsNullOrEmpty(cacheKey) && _cache.TryGetValue(cacheKey, out string cachedResult))
            {
                if (IsCorrectLevel(cachedResult, maxLevel))
                {
                    return new ClassificationResult
                    {
                        Code = cachedResult,
                        Confidence = 100,
                        Source = "Cache"
                    };
                }
            }

            var results = ClassifyByLevel(elementData, maxLevel);

            if (results.Confidence > HIGH_CONFIDENCE_THRESHOLD && !string.IsNullOrEmpty(cacheKey))
            {
                _cache.TryAdd(cacheKey, results.Code);
            }

            return results;
        }

        private static ClassificationResult ClassifyByLevel(Dictionary<string, string> elementData, int targetLevel)
        {
            string searchText = BuildSearchString(elementData);

            if (string.IsNullOrEmpty(searchText))
                return CreateErrorResult("Sem dados para busca");

            var queryEmbedding = EmbeddingService.GenerateEmbedding(searchText);

            var candidates = _database
                .Where(item => item.Level <= targetLevel)
                .ToList();

            if (!candidates.Any())
                return CreateErrorResult("Nenhum candidato no nível especificado");

            System.Diagnostics.Debug.WriteLine($"Classificando: '{searchText}' | Candidatos: {candidates.Count} | Nível: {targetLevel}");

            var scored = new List<(UniClassItem item, float score)>();

            foreach (var candidate in candidates)
            {
                if (!_embeddingCache.TryGetValue(candidate.Code, out var candidateEmbedding))
                    continue;

                float similarity = EmbeddingService.CosineSimilarity(queryEmbedding, candidateEmbedding);

                if (ContainsAntonyms(searchText, candidate.Title))
                    similarity *= 0.5f;

                scored.Add((candidate, similarity));
            }

            var best = scored.OrderByDescending(x => x.score).FirstOrDefault();

            if (best.item == null || best.score < 0.3f)
            {
                return new ClassificationResult
                {
                    Code = "NC - Não Classificado",
                    Confidence = 0,
                    Source = "NoMatch"
                };
            }

            int confidence = (int)(best.score * 100);

            return new ClassificationResult
            {
                Code = $"{best.item.Code} - {best.item.Title}",
                Confidence = confidence,
                Source = "AI-Embedding",
                Alternatives = scored
                    .OrderByDescending(x => x.score)
                    .Skip(1)
                    .Take(2)
                    .Select(x => $"{x.item.Code} ({(int)(x.score * 100)}%)")
                    .ToList()
            };
        }

        private static bool ContainsAntonyms(string text1, string text2)
        {
            text1 = text1.ToLowerInvariant();
            text2 = text2.ToLowerInvariant();

            foreach (var antonymPair in Antonyms)
            {
                var parts = antonymPair.Split('|');
                if ((text1.Contains(parts[0]) && text2.Contains(parts[1])) ||
                    (text1.Contains(parts[1]) && text2.Contains(parts[0])))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSearchString(Dictionary<string, string> elementData)
        {
            var parts = new List<string>();

            if (elementData.TryGetValue("Category", out string category))
                parts.Add(category);

            if (elementData.TryGetValue("Family", out string family))
                parts.Add(family);

            if (elementData.TryGetValue("Type", out string type))
                parts.Add(type);

            string[] additionalParams = new[] { "Description", "Material", "Model", "Manufacturer", "Mark", "Type Mark" };

            foreach (var param in additionalParams)
            {
                if (elementData.TryGetValue(param, out string value) && !string.IsNullOrEmpty(value))
                    parts.Add(value);
            }

            return string.Join(" ", parts);
        }

        private static bool IsCorrectLevel(string classification, int maxLevel)
        {
            if (string.IsNullOrEmpty(classification))
                return false;

            string code = classification.Split(new[] { " - " }, StringSplitOptions.None)[0];
            int level = GetLevelFromCode(code);

            return level <= maxLevel;
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
            _embeddingCache.Clear();
        }

        public static void TestClassification()
        {
            if (!Initialize())
            {
                System.Diagnostics.Debug.WriteLine("Falha ao inicializar base de dados");
                return;
            }

            var testData = new Dictionary<string, string>
            {
                {"Category", "Walls"},
                {"Type", "Generic - 200mm"}
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = Classify(testData);
            sw.Stop();

            System.Diagnostics.Debug.WriteLine($"Teste de classificação: {result.Code}");
            System.Diagnostics.Debug.WriteLine($"Confiança: {result.Confidence}%");
            System.Diagnostics.Debug.WriteLine($"Tempo: {sw.ElapsedMilliseconds}ms");
        }

        public static string GetClassificationDebugInfo(Dictionary<string, string> elementData)
        {
            string searchString = BuildSearchString(elementData);
            return $"Search: '{searchString}'";
        }
    }
}