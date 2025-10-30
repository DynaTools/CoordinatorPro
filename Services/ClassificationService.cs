using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CoordinatorPro.Models;
using Newtonsoft.Json;

namespace CoordinatorPro.Services
{
    public static class ClassificationService
    {
   private static List<UniClassItem> _database;
     private static Dictionary<string, string> _cache = new Dictionary<string, string>();
 private const int MAX_CACHE_SIZE = 1000;
     private const int DEFAULT_CUTOFF = 60;
  private const int HIGH_CONFIDENCE_THRESHOLD = 80;
        
  // Pesos para construção da string de busca
        private const int CATEGORY_WEIGHT = 3;
 private const int FAMILY_WEIGHT = 2;
      private const int TYPE_WEIGHT = 2;
        
  /// <summary>
     /// Inicializa o serviço carregando a base de dados UniClass
        /// </summary>
 /// <returns>True se inicializado com sucesso</returns>
        public static bool Initialize()
{
if (_database != null)
     return true; // Já inicializado
       
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
  
     return true;
         }
            catch (JsonException jsonEx)
   {
    System.Diagnostics.Debug.WriteLine($"Erro ao deserializar JSON: {jsonEx.Message}");
_database = new List<UniClassItem>();
 return false;
     }
  catch (Exception ex)
 {
  System.Diagnostics.Debug.WriteLine($"Erro ao inicializar ClassificationService: {ex.Message}");
   _database = new List<UniClassItem>();
      return false;
}
        }
        
        /// <summary>
        /// Carrega JSON da base de dados (recurso embarcado ou arquivo externo)
   /// </summary>
     private static string LoadDatabaseJson()
{
// Tentar carregar como recurso embarcado primeiro
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
   catch (Exception ex)
 {
      System.Diagnostics.Debug.WriteLine($"Recurso embarcado não encontrado: {ex.Message}");
            }
   
       // Tentar carregar de arquivo externo
  try
     {
          string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
 string jsonPath = Path.Combine(assemblyPath, "uniclass2015.json");
   
    if (File.Exists(jsonPath))
      {
      return File.ReadAllText(jsonPath);
}
     }
     catch (Exception ex)
    {
   System.Diagnostics.Debug.WriteLine($"Arquivo externo não encontrado: {ex.Message}");
        }
    
 return null;
        }
        
     /// <summary>
     /// Classifica elemento baseado em seus dados
  /// </summary>
        public static ClassificationResult Classify(Dictionary<string, string> elementData)
{
    if (elementData == null || !elementData.Any())
{
       return CreateErrorResult("Dados do elemento vazios");
 }
     
            if (_database == null || !_database.Any())
            {
 return CreateErrorResult("Base de dados não carregada");
}
      
     // Criar chave de cache
        string cacheKey = BuildCacheKey(elementData);
  
       // Verificar cache
 if (!string.IsNullOrEmpty(cacheKey) && _cache.ContainsKey(cacheKey))
      {
     return new ClassificationResult
         {
 Code = _cache[cacheKey],
    Confidence = 100,
     Source = "Cache"
     };
       }
 
     // Construir string de busca com pesos
         string searchString = BuildWeightedSearchString(elementData);
 
    if (string.IsNullOrEmpty(searchString))
      {
        return CreateErrorResult("Sem dados para classificação");
            }
       
  // Preparar strings do UniClass para busca
 var uniclassStrings = _database
.Select(item => $"{item.Code} {item.Title} {item.Parent}")
    .ToList();
            
     // Buscar com FuzzySharp
            var results = FuzzySharp.Process.ExtractTop(
   searchString,
  uniclassStrings,
          limit: 5,
     cutoff: DEFAULT_CUTOFF
 );
 
        if (results != null && results.Any())
       {
    var best = results.First();
     int index = uniclassStrings.IndexOf(best.Value);
       
    if (index >= 0 && index < _database.Count)
     {
   var item = _database[index];
   string classification = $"{item.Code} - {item.Title}";
   
        // Adicionar ao cache se confiança alta
   if (best.Score > HIGH_CONFIDENCE_THRESHOLD && !string.IsNullOrEmpty(cacheKey))
   {
      AddToCache(cacheKey, classification);
   }
          
  return new ClassificationResult
      {
    Code = classification,
     Confidence = best.Score,
       Source = "FuzzyMatch",
           Alternatives = BuildAlternatives(results, uniclassStrings)
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
        /// Constrói chave de cache a partir dos dados do elemento
   /// </summary>
     private static string BuildCacheKey(Dictionary<string, string> elementData)
    {
 var keyParts = new List<string>();
      
   if (elementData.ContainsKey("Category"))
    keyParts.Add(elementData["Category"]);
 if (elementData.ContainsKey("Family"))
       keyParts.Add(elementData["Family"]);
   if (elementData.ContainsKey("Type"))
        keyParts.Add(elementData["Type"]);
       
    return keyParts.Any() ? string.Join("|", keyParts) : null;
 }
 
        /// <summary>
   /// Constrói string de busca com pesos diferenciados
  /// </summary>
  private static string BuildWeightedSearchString(Dictionary<string, string> elementData)
   {
  var searchBuilder = new StringBuilder();
 
       // Categoria com maior peso
   if (elementData.ContainsKey("Category") && !string.IsNullOrEmpty(elementData["Category"]))
  {
    for (int i = 0; i < CATEGORY_WEIGHT; i++)
           searchBuilder.Append(elementData["Category"] + " ");
   }
   
            // Family com peso médio
      if (elementData.ContainsKey("Family") && !string.IsNullOrEmpty(elementData["Family"]))
    {
         for (int i = 0; i < FAMILY_WEIGHT; i++)
searchBuilder.Append(elementData["Family"] + " ");
  }
      
 // Type com peso médio
         if (elementData.ContainsKey("Type") && !string.IsNullOrEmpty(elementData["Type"]))
            {
         for (int i = 0; i < TYPE_WEIGHT; i++)
    searchBuilder.Append(elementData["Type"] + " ");
   }
       
     // Adicionar outros dados uma vez
  foreach (var kvp in elementData
    .Where(x => x.Key != "Category" && x.Key != "Family" && x.Key != "Type")
      .Where(x => !string.IsNullOrEmpty(x.Value)))
     {
       searchBuilder.Append(kvp.Value + " ");
     }
    
  return searchBuilder.ToString().Trim();
        }
  
        /// <summary>
   /// Constrói lista de alternativas a partir dos resultados fuzzy
 /// </summary>
   private static List<string> BuildAlternatives(
   System.Collections.IEnumerable results,
  List<string> uniclassStrings)
   {
  var list = new List<string>();
 if (results == null) return list;
 foreach (var r in results)
 {
 try
 {
 dynamic dr = r;
 string val = dr.Value as string;
 int score = (int)dr.Score;
 int idx = uniclassStrings.IndexOf(val);
 if (idx >=0 && idx < _database.Count)
 list.Add($"{_database[idx].Code} ({score}%)");
 }
 catch
 {
 // ignorar problemas de reflexão/dynamic
 }
 }
 return list;
        }
        
        /// <summary>
  /// Adiciona item ao cache com controle de tamanho
     /// </summary>
private static void AddToCache(string key, string value)
 {
   if (_cache.Count >= MAX_CACHE_SIZE)
{
      // Limpar metade do cache (FIFO simples)
 var keysToRemove = _cache.Keys.Take(MAX_CACHE_SIZE / 2).ToList();
    foreach (var k in keysToRemove)
   _cache.Remove(k);
      }
     
      _cache[key] = value;
 }
        
        /// <summary>
/// Cria resultado de erro padronizado
/// </summary>
   private static ClassificationResult CreateErrorResult(string errorMessage)
  {
  return new ClassificationResult
     {
        Code = $"NC - {errorMessage}",
      Confidence = 0,
    Source = "Error"
     };
  }
        
     /// <summary>
        /// Limpa o cache de classificações
  /// </summary>
   public static void ClearCache()
        {
    _cache.Clear();
      }
        
        /// <summary>
   /// Obtém estatísticas do serviço
        /// </summary>
        public static string GetStatistics()
     {
      return $"Base de dados: {(_database?.Count ?? 0)} itens | Cache: {_cache.Count} entradas";
}
 /// <summary>
        /// Obtém informações de debug sobre o processo de classificação
        /// </summary>
        public static string GetClassificationDebugInfo(Dictionary<string, string> elementData)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DEBUG CLASSIFICAÇÃO ===");
            sb.AppendLine($"Base de dados carregada: {_database?.Count ?? 0} itens");
            sb.AppendLine($"Cache: {_cache.Count} entradas");
            sb.AppendLine();

            sb.AppendLine("Dados de entrada:");
            foreach (var kvp in elementData)
            {
                sb.AppendLine($"  {kvp.Key}: '{kvp.Value}'");
            }
            sb.AppendLine();

            // Construir string de busca
            string searchString = BuildWeightedSearchString(elementData);
            sb.AppendLine($"String de busca construída: '{searchString}'");
            sb.AppendLine();

            if (_database == null || !_database.Any())
            {
                sb.AppendLine("ERRO: Base de dados não carregada!");
                return sb.ToString();
            }

            // Preparar strings do UniClass
            var uniclassStrings = _database
                .Select(item => $"{item.Code} {item.Title} {item.Parent}")
                .ToList();

            sb.AppendLine($"Buscando em {_database.Count} itens UniClass...");
            sb.AppendLine("Itens UniClass disponíveis:");
            for (int i = 0; i < Math.Min(5, _database.Count); i++)
            {
                sb.AppendLine($"  {_database[i].Code} - {_database[i].Title}");
            }
            if (_database.Count > 5)
                sb.AppendLine($"  ... e mais {_database.Count - 5} itens");
            sb.AppendLine();

            // Executar busca fuzzy
            try
            {
                var results = FuzzySharp.Process.ExtractTop(
                    searchString,
                    uniclassStrings,
                    limit: 5,
                    cutoff: DEFAULT_CUTOFF
                );

                sb.AppendLine($"Resultados da busca fuzzy (cutoff: {DEFAULT_CUTOFF}):");
                if (results != null && results.Any())
                {
                    foreach (var result in results)
                    {
                        sb.AppendLine($"  Score: {result.Score} - '{result.Value}'");
                    }
                }
                else
                {
                    sb.AppendLine("  NENHUM RESULTADO ENCONTRADO!");
                    sb.AppendLine("  Possíveis causas:");
                    sb.AppendLine("  - String de busca muito curta ou vazia");
                    sb.AppendLine("  - Cutoff muito alto (atual: 60)");
                    sb.AppendLine("  - Dados do elemento insuficientes");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ERRO na busca fuzzy: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Testa a classificação com dados de exemplo
        /// </summary>
        public static void TestClassification()
        {
            // Inicializar se necessário
            if (!ClassificationService.Initialize())
            {
                System.Diagnostics.Debug.WriteLine("Falha ao inicializar ClassificationService");
                return;
            }

            // Dados de teste para diferentes tipos de elementos
            var testData = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "Parede",
                    new Dictionary<string, string>
                    {
                        {"Category", "Walls"},
                        {"Family", "Basic Wall"},
                        {"Type", "Generic - 200mm"},
                        {"Description", "Parede externa"}
                    }
                },
                {
                    "Porta",
                    new Dictionary<string, string>
                    {
                        {"Category", "Doors"},
                        {"Family", "Single-Flush"},
                        {"Type", "36\" x 84\""},
                        {"Description", "Porta de entrada"}
                    }
                },
                {
                    "Janela",
                    new Dictionary<string, string>
                    {
                        {"Category", "Windows"},
                        {"Family", "Fixed"},
                        {"Type", "24\" x 36\""},
                        {"Description", "Janela fixa"}
                    }
                }
            };

            System.Diagnostics.Debug.WriteLine("=== TESTE DE CLASSIFICAÇÃO ===");
            
            foreach (var testCase in testData)
            {
                System.Diagnostics.Debug.WriteLine($"\n--- Testando: {testCase.Key} ---");
                
                var result = ClassificationService.Classify(testCase.Value);
                
                System.Diagnostics.Debug.WriteLine($"Resultado: {result.Code}");
                System.Diagnostics.Debug.WriteLine($"Confiança: {result.Confidence}%");
                System.Diagnostics.Debug.WriteLine($"Fonte: {result.Source}");
                
                if (result.Alternatives != null && result.Alternatives.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Alternativas:");
                    foreach (var alt in result.Alternatives)
                        System.Diagnostics.Debug.WriteLine($"  {alt}");
                }
                
                // Mostrar debug info
                string debugInfo = ClassificationService.GetClassificationDebugInfo(testCase.Value);
                System.Diagnostics.Debug.WriteLine(debugInfo);
            }
        }
    }
}
