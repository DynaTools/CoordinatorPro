using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CoordinatorPro.Forms;
using CoordinatorPro.Models;
using CoordinatorPro.Services;
using WinForms = System.Windows.Forms;

namespace CoordinatorPro.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ClassifyCommand : IExternalCommand
    {
 private const int MIN_CONFIDENCE_THRESHOLD = 70;
     
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
     try
     {
             // 1. PREPARA��O
     UIDocument uidoc = commandData.Application.ActiveUIDocument;
    if (uidoc == null)
    {
   message = "Nenhum documento ativo encontrado.";
        return Result.Failed;
  }
       
                Document doc = uidoc.Document;
                
   // Inicializar servi�o de classifica��o
          if (!ClassificationService.Initialize())
        {
     TaskDialog.Show("Erro", "Falha ao inicializar base de dados UniClass.\nVerifique se o arquivo uniclass2015.json est� presente.");
      return Result.Failed;
    }
      
       // DEBUG: Executar teste de classifica��o
       ClassificationService.TestClassification();
       
  // 2. SELE��O DE ELEMENTOS
          IList<Element> elementsToClassify = GetElementsToClassify(uidoc);
if (elementsToClassify == null || !elementsToClassify.Any())
        {
       TaskDialog.Show("UniClass 2015", "Nenhum elemento selecionado ou v�lido.");
 return Result.Cancelled;
           }
                
  // 3. COLETAR PAR�METROS DISPON�VEIS
          List<string> availableParams = ParameterService.GetEditableParameters(doc, elementsToClassify.First());
 
         if (!availableParams.Any())
       {
             TaskDialog.Show("UniClass 2015", 
      "Nenhum par�metro edit�vel de texto dispon�vel nos elementos selecionados.\n\n" +
                 "Dica: Adicione par�metros compartilhados ou use par�metros padr�o como Comments, Description, etc.");
           return Result.Cancelled;
     }
      
     // 4. MOSTRAR FORM DE SELE��O
          string targetParameter;
       bool showProgress;
  
  using (var selectionForm = new ParameterSelectionForm(availableParams, elementsToClassify.Count))
    {
      if (selectionForm.ShowDialog() != WinForms.DialogResult.OK)
   return Result.Cancelled;
     
        targetParameter = selectionForm.SelectedParameter;
         showProgress = selectionForm.ShowProgress;
 }
           
  // 5. PROCESSAR ELEMENTOS
      var results = ClassifyElements(doc, elementsToClassify, targetParameter, showProgress);
            
      // 6. MOSTRAR RESUMO
 if (results != null && results.Any())
          {
    ShowSummary(results);
   return Result.Succeeded;
        }
    
     return Result.Cancelled;
          }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
  {
                return Result.Cancelled;
        }
            catch (Exception ex)
            {
         message = ex.Message;
          TaskDialog.Show("Erro Inesperado", 
  $"Erro ao classificar elementos:\n\n{ex.Message}\n\n" +
   $"Detalhes t�cnicos:\n{ex.GetType().Name}");
           return Result.Failed;
        }
        }
        
        /// <summary>
     /// Obt�m elementos para classificar (da sele��o atual ou nova sele��o)
        /// </summary>
     private IList<Element> GetElementsToClassify(UIDocument uidoc)
      {
   try
            {
         var selectedIds = uidoc.Selection.GetElementIds();
       
      // Tentar usar sele��o atual
           if (selectedIds.Any())
          {
             var selectedElements = selectedIds
     .Select(id => uidoc.Document.GetElement(id))
       .Where(IsValidElement)
            .ToList();
   
      if (selectedElements.Any())
  return selectedElements;
 }
       
             // Pedir nova sele��o
     TaskDialog td = new TaskDialog("Sele��o de Elementos")
              {
         MainContent = "Nenhum elemento v�lido selecionado.\n\nDeseja selecionar elementos agora?",
    MainInstruction = "Sele��o de Elementos",
 CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
        DefaultButton = TaskDialogResult.Yes
      };
      
       if (td.Show() == TaskDialogResult.Yes)
     {
           var refs = uidoc.Selection.PickObjects(
        ObjectType.Element,
        new ElementSelectionFilter(),
            "Selecione os elementos para classificar (ESC para cancelar)"
        );
             
        return refs
           .Select(r => uidoc.Document.GetElement(r))
 .Where(IsValidElement)
        .ToList();
     }
     
                return new List<Element>();
   }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
           return new List<Element>();
            }
            catch (Exception)
      {
        return new List<Element>();
            }
}
        
        /// <summary>
        /// Verifica se elemento � v�lido para classifica��o
     /// </summary>
        private bool IsValidElement(Element element)
      {
      return element != null && 
          element.Category != null && 
     !element.Category.Name.Contains("View") &&
          element.Id.IntegerValue > 0;
        }
        
        /// <summary>
  /// Classifica lista de elementos com transa��o e progresso
        /// </summary>
      private Dictionary<Element, ClassificationResult> ClassifyElements(
     Document doc,
            IList<Element> elements,
            string targetParameter,
 bool showProgress)
     {
  var results = new Dictionary<Element, ClassificationResult>();
            ProgressForm progressForm = null;
         
    try
      {
   if (showProgress)
        {
  progressForm = new ProgressForm();
         progressForm.Show();
     WinForms.Application.DoEvents();
       }
          
       int current = 0;
                int total = elements.Count;
       
                using (Transaction trans = new Transaction(doc, "Classificar com UniClass 2015"))
                {
      trans.Start();
   
    try
        {
    foreach (Element element in elements)
 {
          current++;
              
               // Verificar cancelamento
          if (progressForm != null && progressForm.Cancelled)
          {
      trans.RollBack();
    return results;
           }

        // Coletar dados do elemento
         var elementData = ParameterService.CollectElementData(element);
           
     // Classificar
      var result = ClassificationService.Classify(elementData);
        results[element] = result;
       
           // DEBUG: Se n�o classificou, mostrar informa��es de debug
           if (result.Confidence == 0 && result.Source != "Error")
           {
               string debugInfo = $"=== DEBUG ELEMENTO {current}/{total} ===\n" +
                   ParameterService.GetElementDataDebugInfo(element) + "\n" +
                   ClassificationService.GetClassificationDebugInfo(elementData);
               
               System.Diagnostics.Debug.WriteLine(debugInfo);
               
               // Opcional: mostrar em TaskDialog para debug
               // TaskDialog.Show("Debug Info", debugInfo);
           }
       
           // Atualizar par�metro se confian�a suficiente
         if (result.Confidence > 0)
              {
         bool updated = ParameterService.SetParameterValue(doc, element, targetParameter, result.Code);
    
          // Se falhou ao atualizar, marcar no resultado
                 if (!updated && result.Confidence > 0)
     {
       result.Source = "ParameterError";
             }
          }

          // Atualizar progresso
            if (progressForm != null)
            {
      progressForm.UpdateProgress(current, total, element, result);
                }
       }
       
        trans.Commit();
         }
           catch (Exception ex)
         {
         trans.RollBack();
      throw new Exception($"Erro durante classifica��o: {ex.Message}", ex);
       }
   }
    
        // Mostrar resumo no form de progresso
      if (progressForm != null)
             {
      progressForm.ShowSummary();
        
       // Aguardar usu�rio fechar ou auto-close
           while (progressForm.Visible)
      {
     WinForms.Application.DoEvents();
      System.Threading.Thread.Sleep(100);
    }
           }
       
           return results;
            }
 finally
  {
  // Garantir limpeza do form
             if (progressForm != null)
      {
if (progressForm.Visible)
     progressForm.Close();
           progressForm.Dispose();
 }
   }
      }
        
    /// <summary>
        /// Mostra resumo dos resultados em TaskDialog
        /// </summary>
        private void ShowSummary(Dictionary<Element, ClassificationResult> results)
        {
  if (!results.Any())
      return;

    int total = results.Count;
        int success = results.Count(r => r.Value.Confidence > MIN_CONFIDENCE_THRESHOLD);
  int partial = results.Count(r => r.Value.Confidence > 0 && r.Value.Confidence <= MIN_CONFIDENCE_THRESHOLD);
     int failed = results.Count(r => r.Value.Confidence == 0);
  int paramErrors = results.Count(r => r.Value.Source == "ParameterError");
            
            double avgConfidence = results.Average(r => r.Value.Confidence);
     
            string summary = $"Classifica��o conclu�da!\n\n" +
    $"Total de elementos: {total}\n" +
     $"? Classificados com sucesso (>{MIN_CONFIDENCE_THRESHOLD}%): {success}\n" +
    $"? Classificados parcialmente (?{MIN_CONFIDENCE_THRESHOLD}%): {partial}\n" +
          $"? N�o classificados: {failed}\n";
            
         if (paramErrors > 0)
            {
       summary += $"? Erros ao atualizar par�metro: {paramErrors}\n";
   }
  
       summary += $"\nConfian�a m�dia: {avgConfidence:F1}%";

            // Adicionar dicas se houver muitos n�o classificados
            if (failed > total * 0.3)
 {
             summary += "\n\n?? Dica: Muitos elementos n�o foram classificados.\n" +
      "Verifique se a base UniClass est� completa e se os\n" +
    "elementos possuem informa��es de Family/Type.";
            }
   
     TaskDialog td = new TaskDialog("Resultado da Classifica��o")
     {
     MainInstruction = "Classifica��o UniClass 2015",
         MainContent = summary,
 MainIcon = success > partial + failed ? 
          TaskDialogIcon.TaskDialogIconInformation : 
         TaskDialogIcon.TaskDialogIconWarning
  };
            
       td.Show();
        }
    }
    
  /// <summary>
    /// Filtro para sele��o de elementos v�lidos
    /// </summary>
    public class ElementSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
   {
            return elem != null && 
               elem.Category != null && 
            !elem.Category.Name.Contains("View");
}
        
        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
   }
    }
}
