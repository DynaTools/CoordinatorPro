using System;
using System.Collections.Generic;
using System.Linq;
using WinForms = System.Windows.Forms;

namespace CoordinatorPro.Forms
{
    public partial class ParameterSelectionForm : WinForms.Form
    {
  private WinForms.ComboBox cmbParameters;
        private WinForms.CheckBox chkRememberChoice;
      private WinForms.RadioButton rbSingleElement;
      private WinForms.RadioButton rbMultipleElements;
        private WinForms.RadioButton rbAllVisible;
   private WinForms.Label lblElementCount;
        private WinForms.CheckBox chkShowProgress;
  private WinForms.Button btnOK;
        private WinForms.Button btnCancel;
        
 public string SelectedParameter { get; private set; }
    public bool ShowProgress { get; private set; }
        
        public ParameterSelectionForm(List<string> availableParameters, int elementCount)
        {
  InitializeComponents();
  
            // Popular ComboBox com parâmetros disponíveis
     cmbParameters.Items.AddRange(availableParameters.OrderBy(p => p).ToArray());
         
            // Carregar última escolha
        var settings = Utils.SettingsManager.Load();
 if (settings.RememberChoice && !string.IsNullOrEmpty(settings.LastParameter))
            {
        int index = cmbParameters.FindStringExact(settings.LastParameter);
     if (index >= 0)
    cmbParameters.SelectedIndex = index;
}
          
        if (cmbParameters.SelectedIndex < 0 && cmbParameters.Items.Count > 0)
       cmbParameters.SelectedIndex = 0;
       
      // Configurar opções
            chkRememberChoice.Checked = settings.RememberChoice;
     chkShowProgress.Checked = settings.ShowProgress;
            
    // Atualizar contagem de elementos
 lblElementCount.Text = $"{elementCount} elementos selecionados";
   
     // Selecionar modo padrão
            if (elementCount == 1)
       rbSingleElement.Checked = true;
            else if (elementCount > 1)
        rbMultipleElements.Checked = true;
            else
     rbAllVisible.Checked = true;
        }
        
        private void InitializeComponents()
        {
 this.Text = "Classificação UniClass 2015";
            this.Width = 500;
            this.Height = 400;
 this.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
     this.StartPosition = WinForms.FormStartPosition.CenterScreen;
     this.MaximizeBox = false;
  this.MinimizeBox = false;
            
  int y = 20;
        
     // Label principal
     var lblMain = new WinForms.Label
    {
  Text = "Selecione o parâmetro para receber a classificação UniClass:",
              Location = new System.Drawing.Point(20, y),
        Size = new System.Drawing.Size(440, 20),
     Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
   };
      this.Controls.Add(lblMain);
            y += 30;
          
 // ComboBox de parâmetros
            cmbParameters = new WinForms.ComboBox
   {
    Location = new System.Drawing.Point(20, y),
    Size = new System.Drawing.Size(440, 25),
             DropDownStyle = WinForms.ComboBoxStyle.DropDownList
      };
 this.Controls.Add(cmbParameters);
     y += 35;
         
            // CheckBox lembrar escolha
    chkRememberChoice = new WinForms.CheckBox
         {
  Text = "Lembrar escolha",
         Location = new System.Drawing.Point(20, y),
     Size = new System.Drawing.Size(200, 20)
       };
        this.Controls.Add(chkRememberChoice);
   y += 35;
            
     // GroupBox opções
    var grpOptions = new WinForms.GroupBox
          {
 Text = "Opções de Classificação",
     Location = new System.Drawing.Point(20, y),
            Size = new System.Drawing.Size(440, 100)
       };
         
     rbSingleElement = new WinForms.RadioButton
            {
         Text = "Elemento selecionado",
       Location = new System.Drawing.Point(15, 25),
       Size = new System.Drawing.Size(200, 20)
    };
          grpOptions.Controls.Add(rbSingleElement);
       
            rbMultipleElements = new WinForms.RadioButton
        {
    Text = "Múltiplos elementos",
        Location = new System.Drawing.Point(15, 50),
                Size = new System.Drawing.Size(200, 20)
   };
            grpOptions.Controls.Add(rbMultipleElements);
            
            rbAllVisible = new WinForms.RadioButton
    {
                Text = "Todos visíveis na vista",
    Location = new System.Drawing.Point(15, 75),
Size = new System.Drawing.Size(200, 20)
  };
        grpOptions.Controls.Add(rbAllVisible);
        
   this.Controls.Add(grpOptions);
          y += 110;
       
            // Label contagem
         lblElementCount = new WinForms.Label
      {
      Text = "0 elementos selecionados",
        Location = new System.Drawing.Point(20, y),
       Size = new System.Drawing.Size(300, 20),
        ForeColor = System.Drawing.Color.Blue
            };
            this.Controls.Add(lblElementCount);
            y += 30;
            
         // CheckBox mostrar progresso
   chkShowProgress = new WinForms.CheckBox
    {
    Text = "Mostrar progresso",
       Location = new System.Drawing.Point(20, y),
      Size = new System.Drawing.Size(200, 20),
                Checked = true
     };
   this.Controls.Add(chkShowProgress);
   y += 40;
            
    // Botões
     btnOK = new WinForms.Button
{
    Text = "Classificar",
        Location = new System.Drawing.Point(280, y),
       Size = new System.Drawing.Size(90, 30),
           DialogResult = WinForms.DialogResult.OK
       };
  btnOK.Click += BtnOK_Click;
     this.Controls.Add(btnOK);

            btnCancel = new WinForms.Button
            {
  Text = "Cancelar",
 Location = new System.Drawing.Point(380, y),
       Size = new System.Drawing.Size(90, 30),
    DialogResult = WinForms.DialogResult.Cancel
            };
   this.Controls.Add(btnCancel);
     
   this.AcceptButton = btnOK;
          this.CancelButton = btnCancel;
        }
    
        private void BtnOK_Click(object sender, EventArgs e)
     {
    if (cmbParameters.SelectedItem == null)
     {
          WinForms.MessageBox.Show("Por favor, selecione um parâmetro.", "Aviso", 
       WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
         this.DialogResult = WinForms.DialogResult.None;
     return;
        }
        
   SelectedParameter = cmbParameters.SelectedItem.ToString();
            ShowProgress = chkShowProgress.Checked;
            
            // Salvar preferências se solicitado
  if (chkRememberChoice.Checked)
    {
         var settings = Utils.SettingsManager.Load();
    settings.LastParameter = SelectedParameter;
      settings.ShowProgress = ShowProgress;
           settings.RememberChoice = true;
              Utils.SettingsManager.Save(settings);
      }
        }
    }
}
