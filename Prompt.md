Vou revisar e melhorar os prompts, adicionando a funcionalidade de escolher o parâmetro de destino:

## **ETAPA 1: Configuração Inicial do Projeto**

### Prompt 1.1 - Criar Projeto
```
Crie um projeto C# Class Library (.NET Framework 4.8) "UniClassClassifier" para Revit 2024:
1. Target Framework: .NET Framework 4.8
2. Referências: RevitAPI.dll e RevitAPIUI.dll de C:\Program Files\Autodesk\Revit 2024\ (Copy Local = False)
3. NuGet packages: FuzzySharp (2.0.2), Newtonsoft.Json (13.0.3)
4. Estrutura de pastas: Commands, Models, Services, Utils, Forms
5. Adicione referência System.Windows.Forms para criar interface de seleção
6. Configure uniclass2015.json existente: Build Action = Embedded Resource
7. Crie pasta Config para salvar preferências do usuário
```

### Prompt 1.2 - Manifesto e Deploy
```
Crie UniClassClassifier.addin:
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Assembly>UniClassClassifier\UniClassClassifier.dll</Assembly>
    <FullClassName>UniClassClassifier.Commands.ClassifyCommand</FullClassName>
    <ClientId>a7b8c9d0-1234-5678-9abc-def012345678</ClientId>
    <Text>UniClass 2015</Text>
    <Description>Classifica elementos usando UniClass 2015 e FuzzySharp</Description>
    <VendorId>UC2015</VendorId>
    <VendorDescription>Classificador Automático UniClass</VendorDescription>
    <VisibilityMode>AlwaysVisible</VisibilityMode>
  </AddIn>
</RevitAddIns>

Post-Build Event:
xcopy "$(TargetPath)" "C:\ProgramData\Autodesk\Revit\Addins\2024\" /Y
xcopy "$(ProjectDir)*.addin" "C:\ProgramData\Autodesk\Revit\Addins\2024\" /Y
xcopy "$(TargetDir)*.dll" "C:\ProgramData\Autodesk\Revit\Addins\2024\" /Y
```

## **ETAPA 2: Interface de Seleção de Parâmetros**

### Prompt 2.1 - ParameterSelectionForm
```
Em Forms/ParameterSelectionForm.cs, crie Windows Form:

DESIGN:
- Label: "Selecione o parâmetro para receber a classificação UniClass:"
- ComboBox: cmbParameters (DropDownStyle = DropDownList)
- CheckBox: chkRememberChoice "Lembrar escolha"
- GroupBox: "Opções de Classificação"
  - RadioButton: rbSingleElement "Elemento selecionado"
  - RadioButton: rbMultipleElements "Múltiplos elementos"
  - RadioButton: rbAllVisible "Todos visíveis na vista"
- Label: lblElementCount "0 elementos selecionados"
- CheckBox: chkShowProgress "Mostrar progresso"
- Button: btnOK "Classificar"
- Button: btnCancel "Cancelar"

CÓDIGO:
1. Construtor recebe List<string> de parâmetros disponíveis e int elementCount
2. Popule ComboBox com parâmetros editáveis comuns:
   - Comments, Mark, Type Mark, Description, Assembly Description
   - Parâmetros compartilhados do projeto
   - Ordenar alfabeticamente
3. Carregue última escolha de %AppData%\UniClassClassifier\settings.json
4. Property: string SelectedParameter { get; }
5. Property: bool ShowProgress { get; }
6. Ao clicar OK, salve preferências se chkRememberChoice
```

## **ETAPA 3: Comando Principal Melhorado**

### Prompt 3 - ClassifyCommand.cs
```
Em Commands/ClassifyCommand.cs:

[Transaction(TransactionMode.Manual)]
public class ClassifyCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        // 1. PREPARAÇÃO
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        
        // 2. SELEÇÃO DE ELEMENTOS
        IList<Element> elementsToClassify = GetElementsToClassify(uidoc);
        if (!elementsToClassify.Any()) 
            return Result.Cancelled;
        
        // 3. COLETAR PARÂMETROS DISPONÍVEIS
        List<string> availableParams = GetEditableParameters(doc, elementsToClassify.First());
        
        // 4. MOSTRAR FORM DE SELEÇÃO
        using (var form = new ParameterSelectionForm(availableParams, elementsToClassify.Count))
        {
            if (form.ShowDialog() != DialogResult.OK)
                return Result.Cancelled;
                
            string targetParameter = form.SelectedParameter;
            bool showProgress = form.ShowProgress;
            
            // 5. PROCESSAR ELEMENTOS
            var results = ClassifyElements(doc, elementsToClassify, targetParameter, showProgress);
            
            // 6. MOSTRAR RESUMO
            ShowSummary(results);
        }
        
        return Result.Succeeded;
    }
    
    // Métodos auxiliares:
    - GetElementsToClassify(): verifica seleção atual ou permite nova seleção
    - GetEditableParameters(): lista parâmetros Instance e Type editáveis
    - ClassifyElements(): processa com transação e tratamento de erros
    - ShowSummary(): TaskDialog com estatísticas
}
```

## **ETAPA 4: Serviço de Parâmetros Inteligente**

### Prompt 4 - ParameterService.cs
```
Em Services/ParameterService.cs:

public static class ParameterService
{
    // 1. DESCOBERTA DE PARÂMETROS
    public static List<string> GetEditableParameters(Document doc, Element element)
    {
        var parameters = new HashSet<string>();
        
        // Parâmetros Built-in comuns
        var builtInParams = new[] {
            "Comments", "Mark", "Type Mark", "Description", 
            "Assembly Description", "Keynote", "Type Comments"
        };
        
        // Adicionar se existirem e forem editáveis
        foreach (string paramName in builtInParams)
        {
            Parameter param = element.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
                parameters.Add(paramName);
        }
        
        // Parâmetros do Projeto/Compartilhados
        foreach (Parameter param in element.Parameters)
        {
            if (!param.IsReadOnly && param.StorageType == StorageType.String)
                parameters.Add(param.Definition.Name);
        }
        
        // Parâmetros do Type
        if (element.GetTypeId() != ElementId.InvalidElementId)
        {
            Element type = doc.GetElement(element.GetTypeId());
            foreach (Parameter param in type.Parameters)
            {
                if (!param.IsReadOnly && param.StorageType == StorageType.String)
                    parameters.Add("[Type] " + param.Definition.Name);
            }
        }
        
        return parameters.OrderBy(p => p).ToList();
    }
    
    // 2. COLETA INTELIGENTE
    public static Dictionary<string, string> CollectElementData(Element element)
    {
        var data = new Dictionary<string, string>();
        
        // Categoria (peso máximo na busca)
        data["Category"] = element.Category?.Name ?? "";
        
        // Family e Type
        data["Family"] = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";
        data["Type"] = element.Name ?? "";
        
        // Parâmetros importantes para classificação
        string[] importantParams = {
            "Assembly Code", "Assembly Description", "Keynote",
            "Description", "Manufacturer", "Model", "Material"
        };
        
        foreach (string paramName in importantParams)
        {
            Parameter param = element.LookupParameter(paramName);
            if (param != null && param.HasValue)
                data[paramName] = param.AsValueString() ?? param.AsString() ?? "";
        }
        
        return data;
    }
    
    // 3. ATUALIZAÇÃO SEGURA
    public static bool SetParameterValue(Element element, string paramName, string value)
    {
        try
        {
            Parameter param = null;
            
            // Checar se é parâmetro do Type
            if (paramName.StartsWith("[Type] "))
            {
                string realName = paramName.Replace("[Type] ", "");
                Element type = element.Document.GetElement(element.GetTypeId());
                param = type?.LookupParameter(realName);
            }
            else
            {
                param = element.LookupParameter(paramName);
            }
            
            if (param != null && !param.IsReadOnly)
            {
                return param.Set(value);
            }
            
            return false;
        }
        catch { return false; }
    }
}
```

## **ETAPA 5: Serviço de Classificação com Cache**

### Prompt 5 - ClassificationService.cs
```
Em Services/ClassificationService.cs:

public class ClassificationService
{
    private static List<UniClassItem> _database;
    private static Dictionary<string, string> _cache = new Dictionary<string, string>();
    
    // 1. INICIALIZAÇÃO
    public static void Initialize()
    {
        if (_database == null)
        {
            string jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "uniclass2015.json"
            );
            
            string json = File.ReadAllText(jsonPath);
            _database = JsonConvert.DeserializeObject<List<UniClassItem>>(json);
        }
    }
    
    // 2. CLASSIFICAÇÃO INTELIGENTE
    public static ClassificationResult Classify(Dictionary<string, string> elementData)
    {
        // Criar chave de cache
        string cacheKey = string.Join("|", 
            elementData["Category"], 
            elementData["Family"], 
            elementData["Type"]
        );
        
        // Verificar cache
        if (_cache.ContainsKey(cacheKey))
            return new ClassificationResult { 
                Code = _cache[cacheKey], 
                Confidence = 100,
                Source = "Cache"
            };
        
        // Construir string de busca com pesos
        var searchBuilder = new StringBuilder();
        searchBuilder.Append(elementData["Category"] + " "); // 3x peso
        searchBuilder.Append(elementData["Category"] + " ");
        searchBuilder.Append(elementData["Category"] + " ");
        searchBuilder.Append(elementData["Family"] + " ");  // 2x peso
        searchBuilder.Append(elementData["Family"] + " ");
        searchBuilder.Append(elementData["Type"] + " ");    // 2x peso
        searchBuilder.Append(elementData["Type"] + " ");
        
        // Adicionar outros dados
        foreach (var kvp in elementData.Where(x => x.Key != "Category" && x.Key != "Family" && x.Key != "Type"))
        {
            if (!string.IsNullOrEmpty(kvp.Value))
                searchBuilder.Append(kvp.Value + " ");
        }
        
        string searchString = searchBuilder.ToString().Trim();
        
        // Preparar strings do UniClass para busca
        var uniclassStrings = _database.Select(item => 
            $"{item.Code} {item.Title} {item.Parent}"
        ).ToList();
        
        // Buscar com FuzzySharp
        var results = Process.ExtractTop(searchString, uniclassStrings, limit: 5, cutoff: 60);
        
        if (results.Any())
        {
            var best = results.First();
            int index = uniclassStrings.IndexOf(best.Value);
            var item = _database[index];
            
            string classification = $"{item.Code} - {item.Title}";
            
            // Adicionar ao cache se confiança alta
            if (best.Score > 80)
                _cache[cacheKey] = classification;
            
            return new ClassificationResult
            {
                Code = classification,
                Confidence = best.Score,
                Source = "FuzzyMatch",
                Alternatives = results.Skip(1).Take(2).Select(r => 
                {
                    int idx = uniclassStrings.IndexOf(r.Value);
                    return $"{_database[idx].Code} ({r.Score}%)";
                }).ToList()
            };
        }
        
        return new ClassificationResult 
        { 
            Code = "NC - Não Classificado",
            Confidence = 0,
            Source = "NoMatch"
        };
    }
}

public class ClassificationResult
{
    public string Code { get; set; }
    public int Confidence { get; set; }
    public string Source { get; set; }
    public List<string> Alternatives { get; set; }
}
```

## **ETAPA 6: Form de Progresso**

### Prompt 6 - ProgressForm.cs
```
Em Forms/ProgressForm.cs, crie form com:

DESIGN:
- ProgressBar: progressBar1
- Label: lblStatus "Processando..."
- Label: lblCurrent "0 / 0"
- DataGridView: dgvResults
  Colunas: [ElementId, Name, Classification, Confidence, Status]
- Button: btnCancel "Cancelar"
- Button: btnExport "Exportar Log" (habilitado ao terminar)
- CheckBox: chkAutoClose "Fechar ao concluir"

CÓDIGO:
public partial class ProgressForm : Form
{
    private bool _cancelled = false;
    public bool Cancelled => _cancelled;
    
    public void UpdateProgress(int current, int total, Element element, ClassificationResult result)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => UpdateProgress(current, total, element, result)));
            return;
        }
        
        progressBar1.Maximum = total;
        progressBar1.Value = current;
        lblCurrent.Text = $"{current} / {total}";
        lblStatus.Text = $"Processando: {element.Name}";
        
        // Adicionar linha ao grid
        var row = new object[] {
            element.Id.IntegerValue,
            element.Name,
            result.Code,
            $"{result.Confidence}%",
            result.Confidence > 70 ? "✓" : "?"
        };
        dgvResults.Rows.Add(row);
        
        Application.DoEvents();
    }
    
    public void ShowSummary()
    {
        int success = dgvResults.Rows.Cast<DataGridViewRow>()
            .Count(r => r.Cells[4].Value.ToString() == "✓");
        int total = dgvResults.Rows.Count;
        
        lblStatus.Text = $"Concluído: {success}/{total} classificados com sucesso";
        btnExport.Enabled = true;
        
        if (chkAutoClose.Checked && success == total)
            Close();
    }
}
```

## **ETAPA 7: Configurações Persistentes**

### Prompt 7 - SettingsManager.cs
```
Em Utils/SettingsManager.cs:

public static class SettingsManager
{
    private static string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UniClassClassifier",
        "settings.json"
    );
    
    public class Settings
    {
        public string LastParameter { get; set; } = "Comments";
        public bool ShowProgress { get; set; } = true;
        public bool RememberChoice { get; set; } = true;
        public int MinConfidence { get; set; } = 70;
        public Dictionary<string, string> CategoryMappings { get; set; } = new();
    }
    
    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<Settings>(json);
            }
        }
        catch { }
        
        return new Settings();
    }
    
    public static void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
```

## **ETAPA 8: Build e Teste**

### Prompt 8 - Configuração Final
```
Configure o projeto para facilitar debug e distribuição:

1. DEBUG CONFIGURATION:
   <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'">
     <StartAction>Program</StartAction>
     <StartProgram>C:\Program Files\Autodesk\Revit 2024\Revit.exe</StartProgram>
   </PropertyGroup>

2. CRIAR INSTALADOR BATCH (Install.bat):
   @echo off
   set REVIT_ADDINS=C:\ProgramData\Autodesk\Revit\Addins\2024
   
   echo Instalando UniClass Classifier...
   
   if not exist "%REVIT_ADDINS%" (
       echo Erro: Pasta do Revit 2024 não encontrada
       pause
       exit /b 1
   )
   
   xcopy /Y "UniClassClassifier.dll" "%REVIT_ADDINS%\"
   xcopy /Y "UniClassClassifier.addin" "%REVIT_ADDINS%\"
   xcopy /Y "FuzzySharp.dll" "%REVIT_ADDINS%\"
   xcopy /Y "SimMetrics.Net.dll" "%REVIT_ADDINS%\"
   xcopy /Y "uniclass2015.json" "%REVIT_ADDINS%\"
   
   echo Instalação concluída!
   pause

3. CRIAR README.md com instruções de uso
```

## **PROMPT BÔNUS - Melhorias Avançadas**

### Prompt Bônus - Features Extras
```
Adicione funcionalidades avançadas ao classificador:

1. MODO APRENDIZADO:
   - Quando usuário corrigir manualmente uma classificação
   - Salve em corrections.json: {elementSignature: correctClassification}
   - Use estas correções com prioridade máxima

2. RELATÓRIO HTML:
   - Exporte resultados em HTML com gráficos
   - Mostre estatísticas por categoria
   - Liste elementos não classificados para revisão

3. CLASSIFICAÇÃO EM LOTE POR VISTA:
   - Opção de processar todas as vistas do projeto
   - Classificar apenas elementos visíveis em cada vista
   - Gerar relatório por vista

4. INTEGRAÇÃO COM EXCEL:
   - Exporte para Excel com elementos e classificações
   - Permita edição manual
   - Importe de volta as correções

5. UNDO PERSONALIZADO:
   - Mantenha histórico de classificações anteriores
   - Comando "Desfazer Última Classificação"
   - Restaure valores originais dos parâmetros
```

Esses prompts revisados agora incluem a funcionalidade de escolher o parâmetro de destino com interface amigável e configurações persistentes.