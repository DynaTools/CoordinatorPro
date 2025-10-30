using System;
using Autodesk.Revit.UI;
using System.Reflection;

namespace CoordinatorPro
{
    public class CoordinatorProApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Criar painel Ribbon
                string tabName = "CoordinatorPro";
                application.CreateRibbonTab(tabName);

                // Obter caminho do assembly
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // ===== PAINEL UNICLASS =====
                RibbonPanel uniclassPanel = application.CreateRibbonPanel(tabName, "UniClass");

                // Botão UniClass 2015
                PushButtonData classifyButtonData = new PushButtonData(
                    "UniClassClassifier",
                    "UniClass\n2015",
                    assemblyPath,
                    "CoordinatorPro.Commands.ClassifyCommand"
                );

                PushButton classifyButton = uniclassPanel.AddItem(classifyButtonData) as PushButton;
                classifyButton.ToolTip = "Classificar elementos usando UniClass 2015";
                classifyButton.LongDescription = "Classifica elementos automaticamente usando " +
                    "busca fuzzy e a tabela UniClass 2015. Permite escolher o parâmetro " +
                    "de destino e acompanhar o progresso da classificação.";

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}