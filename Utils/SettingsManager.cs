using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CoordinatorPro.Utils
{
    /// <summary>
    /// Gerenciador de configurações persistentes do aplicativo
    /// </summary>
public static class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
         "UniClassClassifier"
);
        
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        
  private static Settings _cachedSettings;
        
        /// <summary>
     /// Classe de configurações do aplicativo
        /// </summary>
        public class Settings
        {
   /// <summary>
   /// Último parâmetro selecionado pelo usuário
/// </summary>
        public string LastParameter { get; set; } = "Comments";
            
  /// <summary>
/// Se deve mostrar janela de progresso
    /// </summary>
            public bool ShowProgress { get; set; } = true;
       
     /// <summary>
        /// Se deve lembrar a escolha do parâmetro
      /// </summary>
    public bool RememberChoice { get; set; } = true;
     
         /// <summary>
      /// Confiança mínima para classificação automática (0-100)
    /// </summary>
            public int MinConfidence { get; set; } = 70;
       
    /// <summary>
            /// Mapeamentos personalizados de categoria para código UniClass
            /// </summary>
            public Dictionary<string, string> CategoryMappings { get; set; } = new Dictionary<string, string>();
    
   /// <summary>
        /// Última vez que as configurações foram salvas
 /// </summary>
        public DateTime LastSaved { get; set; } = DateTime.Now;
         
            /// <summary>
   /// Versão das configurações
   /// </summary>
public string Version { get; set; } = "1.0";
        }
        
  /// <summary>
    /// Carrega configurações do arquivo ou retorna padrões
        /// </summary>
        public static Settings Load()
        {
            // Retornar cache se disponível
      if (_cachedSettings != null)
  return _cachedSettings;
   
      try
    {
      if (File.Exists(SettingsFilePath))
          {
           string json = File.ReadAllText(SettingsFilePath);
  
     if (!string.IsNullOrWhiteSpace(json))
            {
            var settings = JsonConvert.DeserializeObject<Settings>(json);
           
              if (settings != null)
            {
              _cachedSettings = ValidateSettings(settings);
     return _cachedSettings;
          }
  }
       }
        }
            catch (JsonException jsonEx)
            {
 System.Diagnostics.Debug.WriteLine($"Erro ao deserializar configurações: {jsonEx.Message}");
           // Fazer backup do arquivo corrompido
    BackupCorruptedSettings();
            }
         catch (IOException ioEx)
        {
         System.Diagnostics.Debug.WriteLine($"Erro de I/O ao carregar configurações: {ioEx.Message}");
    }
            catch (Exception ex)
            {
           System.Diagnostics.Debug.WriteLine($"Erro ao carregar configurações: {ex.Message}");
      }
            
            // Retornar configurações padrão
            _cachedSettings = new Settings();
      return _cachedSettings;
        }
        
   /// <summary>
    /// Salva configurações no arquivo
        /// </summary>
        public static bool Save(Settings settings)
        {
        if (settings == null)
    return false;
     
          try
     {
  // Criar diretório se não existir
            if (!Directory.Exists(AppDataFolder))
     {
          Directory.CreateDirectory(AppDataFolder);
     }
         
         // Atualizar timestamp
    settings.LastSaved = DateTime.Now;
                
      // Serializar com formatação
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            
      // Escrever arquivo
        File.WriteAllText(SettingsFilePath, json);
       
                // Atualizar cache
        _cachedSettings = settings;
        
    return true;
        }
        catch (JsonException jsonEx)
{
         System.Diagnostics.Debug.WriteLine($"Erro ao serializar configurações: {jsonEx.Message}");
  return false;
      }
          catch (IOException ioEx)
    {
                System.Diagnostics.Debug.WriteLine($"Erro de I/O ao salvar configurações: {ioEx.Message}");
     return false;
 }
    catch (Exception ex)
   {
       System.Diagnostics.Debug.WriteLine($"Erro ao salvar configurações: {ex.Message}");
  return false;
      }
        }
    
        /// <summary>
        /// Valida e corrige configurações carregadas
  /// </summary>
        private static Settings ValidateSettings(Settings settings)
     {
          // Validar MinConfidence
       if (settings.MinConfidence < 0 || settings.MinConfidence > 100)
            {
 settings.MinConfidence = 70;
            }
     
// Validar CategoryMappings
            if (settings.CategoryMappings == null)
       {
       settings.CategoryMappings = new Dictionary<string, string>();
            }
            
     // Validar LastParameter
            if (string.IsNullOrWhiteSpace(settings.LastParameter))
          {
        settings.LastParameter = "Comments";
     }
    
        return settings;
        }
    
      /// <summary>
      /// Faz backup de arquivo de configurações corrompido
        /// </summary>
        private static void BackupCorruptedSettings()
      {
          try
      {
 if (File.Exists(SettingsFilePath))
     {
  string backupPath = SettingsFilePath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(SettingsFilePath, backupPath, true);
             System.Diagnostics.Debug.WriteLine($"Backup criado em: {backupPath}");
    }
    }
     catch
   {
  // Ignorar erros de backup
       }
        }
        
 /// <summary>
     /// Reseta configurações para padrões
     /// </summary>
        public static Settings Reset()
        {
            _cachedSettings = new Settings();
            Save(_cachedSettings);
      return _cachedSettings;
        }
        
/// <summary>
     /// Limpa o cache de configurações
        /// </summary>
        public static void ClearCache()
        {
       _cachedSettings = null;
 }
        
        /// <summary>
     /// Obtém caminho do arquivo de configurações
        /// </summary>
        public static string GetSettingsPath()
        {
   return SettingsFilePath;
}
        
 /// <summary>
      /// Verifica se arquivo de configurações existe
        /// </summary>
        public static bool SettingsFileExists()
        {
  return File.Exists(SettingsFilePath);
        }
  }
}
