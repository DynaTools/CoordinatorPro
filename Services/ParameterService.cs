using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using System.Text;

namespace CoordinatorPro.Services
{
    public static class ParameterService
    {
        /// <summary>
        /// Parâmetros built-in padrão que podem receber classificação
        /// </summary>
        private static readonly string[] StandardParameters = new[]
        {
            "Comments", "Mark", "Type Mark", "Description",
            "Assembly Description", "Keynote", "Type Comments",
            "Assembly Code", "Model", "Manufacturer"
        };

        /// <summary>
        /// Obtém lista de parâmetros editáveis de texto do elemento
        /// </summary>
        public static List<string> GetEditableParameters(Document doc, Element element)
        {
            if (doc == null || element == null)
                return new List<string>();

            var parameters = new HashSet<string>();

            // Adicionar parâmetros built-in padrão
            AddStandardParameters(element, parameters);

            // Adicionar parâmetros de instância do projeto
            AddInstanceParameters(element, parameters);

            // Adicionar parâmetros do Type
            AddTypeParameters(doc, element, parameters);

            return parameters.OrderBy(p => p).ToList();
        }

        /// <summary>
        /// Adiciona parâmetros built-in padrão ao conjunto
        /// </summary>
        private static void AddStandardParameters(Element element, HashSet<string> parameters)
        {
            foreach (string paramName in StandardParameters)
            {
                try
                {
                    Parameter param = element.LookupParameter(paramName);
                    if (IsEditableStringParameter(param))
                    {
                        parameters.Add(paramName);
                    }
                }
                catch
                {
                    // Ignorar parâmetros que causam erro
                }
            }
        }

        /// <summary>
        /// Adiciona parâmetros de instância ao conjunto
        /// </summary>
        private static void AddInstanceParameters(Element element, HashSet<string> parameters)
        {
            try
            {
                foreach (Parameter param in element.Parameters)
                {
                    if (IsEditableStringParameter(param))
                    {
                        string name = param.Definition.Name;
                        if (!parameters.Contains(name) && !string.IsNullOrEmpty(name))
                        {
                            parameters.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao coletar parâmetros de instância: {ex.Message}");
            }
        }

        /// <summary>
        /// Adiciona parâmetros do Type ao conjunto
        /// </summary>
        private static void AddTypeParameters(Document doc, Element element, HashSet<string> parameters)
        {
            try
            {
                if (element.GetTypeId() == null || element.GetTypeId() == ElementId.InvalidElementId)
                    return;

                Element typeElement = doc.GetElement(element.GetTypeId());
                if (typeElement == null)
                    return;

                foreach (Parameter param in typeElement.Parameters)
                {
                    if (IsEditableStringParameter(param))
                    {
                        string name = "[Type] " + param.Definition.Name;
                        if (!parameters.Contains(name))
                        {
                            parameters.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao coletar parâmetros do Type: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica se parâmetro é editável e do tipo texto
        /// </summary>
        private static bool IsEditableStringParameter(Parameter param)
        {
            return param != null &&
                   !param.IsReadOnly &&
                   param.StorageType == StorageType.String &&
                   param.Definition != null;
        }

        /// <summary>
        /// ✅ NOVO: Coleta dados relevantes do elemento usando parâmetros personalizados
        /// SEMPRE inclui: Category, Family e Type (obrigatórios)
        /// </summary>
        public static Dictionary<string, string> CollectElementData(Element element, List<string> mappingParameters = null)
        {
            if (element == null)
                return new Dictionary<string, string>();

            var data = new Dictionary<string, string>();

            // ✅ SEMPRE incluir: Categoria (peso máximo na busca) - OBRIGATÓRIO
            TryAddData(data, "Category", element.Category?.Name);

            // ✅ SEMPRE incluir: Family e Type - OBRIGATÓRIOS
            TryAddData(data, "Family", GetFamilyName(element));
            TryAddData(data, "Type", element.Name);

            // ✅ Se parâmetros customizados foram especificados, coletar ADICIONALMENTE
            if (mappingParameters != null && mappingParameters.Any())
            {
                CollectCustomParameters(element, data, mappingParameters);
            }
            else
            {
                // Fallback: usar parâmetros padrão importantes ADICIONALMENTE
                CollectImportantParameters(element, data);
            }

            // ✅ DEBUG: Sempre mostrar o que foi coletado
            if (data.Count <= 3) // Apenas Category, Family, Type
            {
                System.Diagnostics.Debug.WriteLine($"⚠ AVISO: Apenas dados básicos coletados para elemento {element.Id.IntegerValue}");
                System.Diagnostics.Debug.WriteLine($"   Category: {data.GetValueOrDefault("Category", "N/A")}");
                System.Diagnostics.Debug.WriteLine($"   Family: {data.GetValueOrDefault("Family", "N/A")}");
                System.Diagnostics.Debug.WriteLine($"   Type: {data.GetValueOrDefault("Type", "N/A")}");
            }

            return data;
        }

        /// <summary>
        /// ✅ NOVO: Coleta parâmetros customizados selecionados pelo usuário
        /// </summary>
        private static void CollectCustomParameters(Element element, Dictionary<string, string> data, List<string> mappingParameters)
        {
            foreach (string paramName in mappingParameters)
            {
                try
                {
                    Parameter param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        string value = param.AsValueString() ?? param.AsString();
                        TryAddData(data, paramName, value);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao coletar parâmetro customizado '{paramName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Obtém nome da Family do elemento
        /// </summary>
        private static string GetFamilyName(Element element)
        {
            try
            {
                Parameter familyParam = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM);
                return familyParam?.AsValueString() ?? familyParam?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Coleta parâmetros importantes do elemento (FALLBACK)
        /// </summary>
        private static void CollectImportantParameters(Element element, Dictionary<string, string> data)
        {
            string[] importantParams = new[]
            {
                "Assembly Code", "Assembly Description", "Keynote",
                "Description", "Manufacturer", "Model", "Material",
                "Type Mark", "Mark", "Comments"
            };

            foreach (string paramName in importantParams)
            {
                try
                {
                    Parameter param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        string value = param.AsValueString() ?? param.AsString();
                        TryAddData(data, paramName, value);
                    }
                }
                catch
                {
                    // Ignorar parâmetros que causam erro
                }
            }
        }

        /// <summary>
        /// Adiciona dado ao dicionário se não for vazio
        /// </summary>
        private static void TryAddData(Dictionary<string, string> data, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !data.ContainsKey(key))
            {
                data[key] = value.Trim();
            }
        }

        /// <summary>
        /// Define valor do parâmetro de forma segura
        /// </summary>
        /// <returns>True se atualizado com sucesso</returns>
        public static bool SetParameterValue(Document doc, Element element, string paramName, string value)
        {
            if (doc == null || element == null || string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                Parameter param = GetTargetParameter(doc, element, paramName);

                if (param == null || param.IsReadOnly || param.StorageType != StorageType.String)
                    return false;

                // Truncar se exceder limite (geralmente 255 caracteres)
                const int maxLength = 255;
                if (value?.Length > maxLength)
                {
                    value = value.Substring(0, maxLength - 3) + "...";
                }

                return param.Set(value ?? "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao definir parâmetro {paramName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtém o parâmetro alvo (de instância ou type)
        /// </summary>
        private static Parameter GetTargetParameter(Document doc, Element element, string paramName)
        {
            // Checar se é parâmetro do Type
            if (paramName.StartsWith("[Type] "))
            {
                string realName = paramName.Replace("[Type] ", "");
                Element typeElement = doc.GetElement(element.GetTypeId());
                return typeElement?.LookupParameter(realName);
            }

            return element.LookupParameter(paramName);
        }

        /// <summary>
        /// Valida se parâmetro pode receber valores
        /// </summary>
        public static bool IsParameterValid(Document doc, Element element, string paramName)
        {
            try
            {
                Parameter param = GetTargetParameter(doc, element, paramName);
                return param != null &&
                       !param.IsReadOnly &&
                       param.StorageType == StorageType.String;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtém informações de debug sobre os dados coletados do elemento
        /// </summary>
        public static string GetElementDataDebugInfo(Element element, List<string> mappingParameters = null)
        {
            if (element == null)
                return "Elemento nulo";

            var data = CollectElementData(element, mappingParameters);
            var sb = new StringBuilder();

            sb.AppendLine($"Elemento ID: {element.Id.IntegerValue}");
            sb.AppendLine($"Nome: {element.Name}");
            sb.AppendLine($"Categoria: {element.Category?.Name ?? "N/A"}");
            sb.AppendLine();

            // ✅ NOVO: Mostrar parâmetros de mapeamento sendo usados
            if (mappingParameters != null && mappingParameters.Any())
            {
                sb.AppendLine("Parâmetros de mapeamento configurados:");
                foreach (var param in mappingParameters)
                {
                    sb.AppendLine($"  - {param}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("Dados coletados para classificação:");

            foreach (var kvp in data)
            {
                sb.AppendLine($"  {kvp.Key}: '{kvp.Value}'");
            }

            if (!data.Any())
            {
                sb.AppendLine("  NENHUM DADO COLETADO!");
                sb.AppendLine();
                sb.AppendLine("Verificando parâmetros disponíveis:");

                string[] importantParams = new[]
                {
                    "Assembly Code", "Assembly Description", "Keynote",
                    "Description", "Manufacturer", "Model", "Material",
                    "Type Mark", "Mark", "Comments"
                };

                foreach (string paramName in importantParams)
                {
                    try
                    {
                        Parameter param = element.LookupParameter(paramName);
                        if (param != null)
                        {
                            string value = param.HasValue ?
                                (param.AsValueString() ?? param.AsString() ?? "NULO") :
                                "SEM VALOR";
                            sb.AppendLine($"  {paramName}: {value} (ReadOnly: {param.IsReadOnly}, StorageType: {param.StorageType})");
                        }
                        else
                        {
                            sb.AppendLine($"  {paramName}: PARÂMETRO NÃO ENCONTRADO");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {paramName}: ERRO - {ex.Message}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// ✅ Helper: Obtém valor do dicionário ou retorna default
        /// </summary>
        private static string GetValueOrDefault(this Dictionary<string, string> dict, string key, string defaultValue = "")
        {
            return dict.TryGetValue(key, out string value) ? value : defaultValue;
        }
    }
}