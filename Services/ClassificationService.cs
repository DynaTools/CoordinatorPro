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

        private static Dictionary<string, List<int>> _categoryIndex;
        private static Dictionary<string, List<int>> _keywordIndex;
        private static List<string> _uniclassStrings;

        private const int DEFAULT_CUTOFF = 30; // ✅ Reduzido de 40 para 30 (mais permissivo)
        private const int HIGH_CONFIDENCE_THRESHOLD = 80;

        private static readonly Dictionary<string, string> CategoryMapping = new Dictionary<string, string>
        {
            // ============ ARQUITETURA ============
            {"Walls", "wall"},
            {"Doors", "door"},
            {"Windows", "window"},
            {"Floors", "floor"},
            {"Roofs", "roof"},
            {"Ceilings", "ceiling"},
            {"Curtain Panels", "panel"},
            {"Curtain Wall Mullions", "mullion"},
            {"Stairs", "stair"},
            {"Railings", "railing"},
            {"Ramps", "ramp"},
            {"Columns", "column"},
            {"Structural Columns", "column"},
            {"Structural Framing", "beam"},
            {"Structural Foundations", "foundation"},
            {"Mass", "mass"},
            {"Generic Models", "model"},
            {"Casework", "casework"},
            {"Furniture", "furniture"},
            {"Furniture Systems", "furniture"},
            {"Specialty Equipment", "equipment"},
            {"Parking", "parking"},
            {"Site", "site"},
            {"Planting", "plant"},
            {"Topography", "topography"},
            
            // ============ MEP - MECÂNICO ============
            {"Mechanical Equipment", "mechanical"},
            {"Air Terminals", "air"},
            {"Ducts", "duct"},
            {"Duct Fittings", "duct"},
            {"Duct Accessories", "duct"},
            {"Duct Insulations", "insulation"},
            {"Duct Linings", "lining"},
            {"Duct Placeholders", "duct"},
            {"Flex Ducts", "duct"},
            {"Mechanical Equipment Sets", "mechanical"},
            
            // ============ MEP - HIDRÁULICO ============
            {"Plumbing Fixtures", "sanitary"},
            {"Pipes", "pipe"},
            {"Pipe Fittings", "pipe"},
            {"Pipe Accessories", "pipe"},
            {"Pipe Insulations", "insulation"},
            {"Pipe Placeholders", "pipe"},
            {"Flex Pipes", "pipe"},
            {"Sprinklers", "sprinkler"},
            
            // ============ MEP - ELÉTRICO ============
            {"Electrical Equipment", "electrical"},
            {"Electrical Fixtures", "lighting"},
            {"Lighting Fixtures", "lighting"},
            {"Lighting Devices", "lighting"},
            {"Cable Trays", "cable"},
            {"Cable Tray Fittings", "cable"},
            {"Conduits", "conduit"},
            {"Conduit Fittings", "conduit"},
            {"Communication Devices", "communication"},
            {"Data Devices", "data"},
            {"Fire Alarm Devices", "fire"},
            {"Nurse Call Devices", "nurse"},
            {"Security Devices", "security"},
            {"Telephone Devices", "telephone"},
            {"Lighting", "lighting"},
            
            // ============ EQUIPAMENTOS ESPECIALIZADOS ============
            {"Food Service Equipment", "equipment"},
            {"Medical Equipment", "equipment"},
            {"Laboratory Equipment", "equipment"},
            {"Commercial Equipment", "equipment"},
            
            // ============ ESTRUTURAL ============
            {"Structural Beam Systems", "beam"},
            {"Structural Connections", "connection"},
            {"Structural Rebar", "rebar"},
            {"Structural Stiffeners", "stiffener"},
            {"Structural Trusses", "truss"},
            {"Fabric Areas", "fabric"},
            {"Fabric Reinforcement", "reinforcement"},
            
            // ============ SISTEMAS ============
            {"Fire Protection", "fire"},
            {"HVAC Zones", "zone"},
            {"Piping Systems", "pipe"},
            
            // ============ ELEMENTOS ESPECIAIS ============
            {"Entourage", "entourage"},
            {"Curtain Systems", "curtain"},
            {"Model Groups", "group"},
            {"Parts", "part"},
            {"Assemblies", "assembly"},
            {"Areas", "area"},
            {"Rooms", "room"},
            {"Spaces", "space"},
            
            // ============ PADRÃO/FALLBACK ============
            {"Generic Model", "model"},
            {"Lines", "line"},
            {"Fascia", "fascia"},
            {"Gutter", "gutter"},
            {"Shaft Openings", "opening"},
            {"Vertical Circulation", "circulation"}
        };

        public static bool Initialize()
        {
            if (_database != null)
                return true;

            try
            {
                // ✅ LER APENAS DO JSON
                _database = LoadFromJson();

                if (_database == null || !_database.Any())
                {
                    System.Diagnostics.Debug.WriteLine("✗ ERRO: Base de dados vazia");
                    _database = new List<UniClassItem>();
                    return false;
                }

                // CONSTRUIR ÍNDICES
                BuildOptimizedIndex();

                System.Diagnostics.Debug.WriteLine($"✓ Base carregada do JSON: {_database.Count} itens");
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
        /// ✅ LÊ APENAS DO JSON
        /// </summary>
        private static List<UniClassItem> LoadFromJson()
        {
            try
            {
                // Procurar JSON na pasta da DLL
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonPath = Path.Combine(assemblyPath, "Pr_Uniclass.json");

                // Tentar nome alternativo se não encontrar
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(assemblyPath, "Uniclass2015_Pr_v1_39.json");
                }

                if (!File.Exists(jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"✗ ERRO: JSON não encontrado!");
                    System.Diagnostics.Debug.WriteLine($"   Caminho esperado: {jsonPath}");
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

                System.Diagnostics.Debug.WriteLine($"✓ JSON encontrado: {jsonPath}");

                var items = new List<UniClassItem>();

                // Ler e parsear JSON
                string jsonContent = File.ReadAllText(jsonPath);
                var jsonData = JObject.Parse(jsonContent);

                // Verificar estrutura básica
                if (!jsonData.ContainsKey("items"))
                {
                    System.Diagnostics.Debug.WriteLine("✗ ERRO: JSON não tem propriedade 'items'");
                    return null;
                }

                var itemsObject = jsonData["items"] as JObject;
                if (itemsObject == null)
                {
                    System.Diagnostics.Debug.WriteLine("✗ ERRO: 'items' não é um objeto válido");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"✓ Lendo {itemsObject.Count} itens do JSON...");

                int itemsLoaded = 0;

                // Processar cada item
                foreach (var property in itemsObject.Properties())
                {
                    try
                    {
                        var itemData = property.Value as JObject;
                        if (itemData == null)
                            continue;

                        string code = itemData["code"]?.ToString();
                        string title = itemData["title"]?.ToString();

                        // Pular itens sem code ou title
                        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(title))
                            continue;

                        var item = new UniClassItem
                        {
                            Code = code,
                            Title = title,
                            Keywords = ExtractKeywordsFromTitle(title),
                            Category = DetermineCategory(code),
                            Level = itemData["level"]?.ToObject<int>() ?? GetLevelFromCode(code),
                            Parent = itemData["parent"]?.ToString()
                        };

                        items.Add(item);
                        itemsLoaded++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Erro ao processar item '{property.Name}': {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ {itemsLoaded} itens carregados com sucesso");

                return items;
            }
            catch (FileNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO: Arquivo JSON não encontrado");
                System.Diagnostics.Debug.WriteLine($"   {ex.Message}");
                return null;
            }
            catch (JsonReaderException ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO ao ler JSON: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Linha: {ex.LineNumber}, Posição: {ex.LinePosition}");
                return null;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO ao parsear JSON: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Verifique se o arquivo JSON está bem formatado");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERRO ao ler JSON: {ex.Message}");
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

        /// <summary>
        /// ✅ MODIFICADO: Aceita nível máximo de classificação desejado (1-4)
        /// </summary>
        public static ClassificationResult Classify(Dictionary<string, string> elementData, int maxLevel = 4)
        {
            if (elementData == null || !elementData.Any())
                return CreateErrorResult("Dados vazios");

            if (_database == null || !_database.Any())
                return CreateErrorResult("Base não carregada");

            // ✅ Validar nível solicitado
            if (maxLevel < 1 || maxLevel > 4)
                maxLevel = 4;

            string cacheKey = BuildCacheKey(elementData);

            if (!string.IsNullOrEmpty(cacheKey) && _cache.TryGetValue(cacheKey, out string cachedResult))
            {
                // ✅ Verificar se cache está no nível correto
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

            // ✅ Filtrar apenas itens do nível desejado ou inferior
            List<int> targetIndices = GetTargetIndicesFast(elementData, maxLevel);

            if (!targetIndices.Any())
            {
                System.Diagnostics.Debug.WriteLine("✗ Nenhum candidato encontrado!");
                return new ClassificationResult
                {
                    Code = "NC - Sem correspondência",
                    Confidence = 0,
                    Source = "NoMatch"
                };
            }

            // ✅ Aumentado de 100 para 300 candidatos para análise
            int maxCandidates = Math.Min(300, targetIndices.Count);
            var topCandidates = targetIndices.Take(maxCandidates).ToList();
            var filteredStrings = topCandidates.Select(i => _uniclassStrings[i]).ToList();

            string searchString = BuildSimpleSearchString(elementData);

            if (string.IsNullOrEmpty(searchString))
            {
                System.Diagnostics.Debug.WriteLine("✗ ERRO: SearchString vazia!");
                System.Diagnostics.Debug.WriteLine($"   ElementData count: {elementData.Count}");
                foreach (var kvp in elementData)
                {
                    System.Diagnostics.Debug.WriteLine($"   {kvp.Key}: '{kvp.Value}'");
                }
                return CreateErrorResult("Sem dados para busca");
            }

            // ✅ DEBUG: Mostrar string de busca
            System.Diagnostics.Debug.WriteLine($"🔍 Buscando: '{searchString}' | Candidatos: {topCandidates.Count} | Nível máx: {maxLevel}");

            // ✅ Aumentado limit para retornar top 3 para análise
            var results = FuzzySharp.Process.ExtractTop(
                searchString,
                filteredStrings,
                limit: 3,
                cutoff: DEFAULT_CUTOFF
            );

            if (results != null && results.Any())
            {
                var best = results.First();

                // ✅ DEBUG: Mostrar top 3 matches
                System.Diagnostics.Debug.WriteLine($"📊 Top matches:");
                foreach (var result in results.Take(3))
                {
                    System.Diagnostics.Debug.WriteLine($"   Score {result.Score}: {result.Value}");
                }

                int localIndex = filteredStrings.IndexOf(best.Value);
                int globalIndex = topCandidates[localIndex];

                if (globalIndex >= 0 && globalIndex < _database.Count)
                {
                    var item = _database[globalIndex];

                    // ✅ Ajustar para o nível solicitado se necessário
                    var adjustedItem = AdjustToLevel(item, maxLevel);

                    string classification = $"{adjustedItem.Code} - {adjustedItem.Title}";

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

        /// <summary>
        /// ✅ NOVO: Verifica se o código cached está no nível correto
        /// </summary>
        private static bool IsCorrectLevel(string classification, int maxLevel)
        {
            if (string.IsNullOrEmpty(classification))
                return false;

            // Extrair código (antes do " - ")
            string code = classification.Split(new[] { " - " }, StringSplitOptions.None)[0];
            int level = GetLevelFromCode(code);

            return level <= maxLevel;
        }

        /// <summary>
        /// ✅ NOVO: Ajusta item para o nível desejado
        /// </summary>
        private static UniClassItem AdjustToLevel(UniClassItem item, int maxLevel)
        {
            if (item.Level <= maxLevel)
                return item; // Já está no nível correto ou inferior

            // Subir na hierarquia até atingir o nível desejado
            string targetCode = GetCodeAtLevel(item.Code, maxLevel);

            if (string.IsNullOrEmpty(targetCode))
                return item; // Fallback

            // Buscar o item pai no nível desejado
            var parentItem = _database.FirstOrDefault(i => i.Code == targetCode);

            return parentItem ?? item; // Fallback para item original se não encontrar
        }

        /// <summary>
        /// ✅ NOVO: Obtém o código no nível especificado
        /// </summary>
        private static string GetCodeAtLevel(string code, int targetLevel)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            var parts = code.Split('_');

            if (parts.Length <= targetLevel)
                return code; // Já está no nível ou inferior

            // Pegar apenas as partes até o nível desejado
            return string.Join("_", parts.Take(targetLevel));
        }

        /// <summary>
        /// ✅ MODIFICADO: Filtra por nível máximo com fallback inteligente
        /// </summary>
        private static List<int> GetTargetIndicesFast(Dictionary<string, string> elementData, int maxLevel)
        {
            var indices = new HashSet<int>();

            // ✅ ESTRATÉGIA 1: Buscar por categoria mapeada via keyword
            if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
            {
                string revitCategory = elementData["Category"];

                if (CategoryMapping.TryGetValue(revitCategory, out string mappedKeyword))
                {
                    if (_keywordIndex.TryGetValue(mappedKeyword, out var keywordIndices))
                    {
                        foreach (var idx in keywordIndices)
                        {
                            if (_database[idx].Level <= maxLevel)
                                indices.Add(idx);
                        }

                        System.Diagnostics.Debug.WriteLine($"✓ Encontrados {indices.Count} candidatos via keyword '{mappedKeyword}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Keyword '{mappedKeyword}' não encontrada no índice");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Categoria '{revitCategory}' não tem mapeamento");
                }
            }

            // ✅ ESTRATÉGIA 2: Se poucos resultados, buscar em "products" genérico
            if (indices.Count < 20)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Poucos candidatos ({indices.Count}), expandindo busca para 'products'");

                if (_categoryIndex.TryGetValue("products", out var productIndices))
                {
                    foreach (var idx in productIndices.Take(100))
                    {
                        if (_database[idx].Level <= maxLevel)
                            indices.Add(idx);
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ Total após expansão: {indices.Count} candidatos");
                }
            }

            // ✅ ESTRATÉGIA 3: Se ainda poucos, buscar palavras-chave do Family/Type
            if (indices.Count < 20 && elementData.ContainsKey("Family"))
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Ainda poucos candidatos, buscando por Family keywords");

                string family = elementData["Family"].ToLowerInvariant();
                var familyWords = family.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 3)
                    .Take(3); // Pegar primeiras 3 palavras significativas

                foreach (var word in familyWords)
                {
                    if (_keywordIndex.TryGetValue(word, out var wordIndices))
                    {
                        foreach (var idx in wordIndices.Take(30))
                        {
                            if (_database[idx].Level <= maxLevel)
                                indices.Add(idx);
                        }
                        System.Diagnostics.Debug.WriteLine($"✓ Adicionados candidatos via keyword '{word}': {indices.Count} total");
                    }
                }
            }

            // ✅ ESTRATÉGIA 4: ÚLTIMO RECURSO - buscar em TODA a base filtrada por nível
            if (indices.Count < 10)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ FALLBACK: Poucos candidatos ({indices.Count}), buscando em toda base (nível ≤ {maxLevel})");

                for (int i = 0; i < _database.Count && indices.Count < 200; i++)
                {
                    if (_database[i].Level <= maxLevel)
                        indices.Add(i);
                }

                System.Diagnostics.Debug.WriteLine($"✓ Total após busca completa: {indices.Count} candidatos");
            }

            var result = indices.ToList();
            System.Diagnostics.Debug.WriteLine($"🎯 FINAL: {result.Count} candidatos para classificação");
            return result;
        }

        private static string BuildSimpleSearchString(Dictionary<string, string> elementData)
        {
            var parts = new List<string>();

            // ✅ SEMPRE incluir Category (se disponível)
            if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
            {
                string category = elementData["Category"];
                if (CategoryMapping.TryGetValue(category, out string mapped))
                    parts.Add(mapped);
                else
                {
                    // Usar categoria original normalizada
                    string normalized = category.ToLowerInvariant()
                        .Replace("equipment", "")
                        .Replace("service", "")
                        .Trim();
                    if (!string.IsNullOrEmpty(normalized))
                        parts.Add(normalized);
                }
            }

            // ✅ SEMPRE incluir Family (se disponível)
            if (elementData.ContainsKey("Family") && !string.IsNullOrEmpty(elementData["Family"]))
            {
                parts.Add(CleanString(elementData["Family"]));
            }

            // ✅ SEMPRE incluir Type (se disponível)
            if (elementData.ContainsKey("Type") && !string.IsNullOrEmpty(elementData["Type"]))
            {
                parts.Add(CleanString(elementData["Type"]));
            }

            // ✅ Incluir outros parâmetros relevantes se disponíveis
            string[] additionalParams = new[] { "Description", "Material", "Model", "Manufacturer", "Mark", "Type Mark" };
            foreach (var param in additionalParams)
            {
                if (elementData.ContainsKey(param) && !string.IsNullOrEmpty(elementData[param]))
                {
                    parts.Add(CleanString(elementData[param]));
                }
            }

            string searchString = string.Join(" ", parts).Trim().ToLowerInvariant();

            // ✅ Limpar caracteres especiais e múltiplos espaços
            searchString = System.Text.RegularExpressions.Regex.Replace(searchString, @"[^\w\s]", " ");
            searchString = System.Text.RegularExpressions.Regex.Replace(searchString, @"\s+", " ").Trim();

            // ✅ DEBUG
            if (string.IsNullOrEmpty(searchString))
            {
                System.Diagnostics.Debug.WriteLine("⚠ AVISO: SearchString vazia!");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"📝 SearchString construída: '{searchString}'");
            }

            return searchString;
        }

        /// <summary>
        /// ✅ NOVO: Limpa e normaliza strings
        /// </summary>
        private static string CleanString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // Remover caracteres especiais comuns mas manter espaços
            return input.Replace("\"", "")
                       .Replace("\\", "")
                       .Replace("/", " ")
                       .Replace("-", " ")
                       .Replace("_", " ")
                       .Trim();
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
            List<int> targetIndices = GetTargetIndicesFast(elementData, 4);
            return $"Search: '{searchString}' | Candidates: {targetIndices.Count}";
        }
    }
}