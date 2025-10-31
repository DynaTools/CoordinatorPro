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

        // ✅ NOVOS: Controles para parâmetros de mapeamento
        private WinForms.CheckedListBox clbMappingParameters;
        private WinForms.Label lblMappingInfo;

        // ✅ NOVO: Controles para nível de classificação
        private WinForms.ComboBox cmbClassificationLevel;
        private WinForms.Label lblClassificationLevel;

        // Propriedades públicas do Form
        public string SelectedParameter { get; private set; }
        public bool ShowProgress { get; private set; }

        // ✅ NOVA PROPRIEDADE: Lista de parâmetros selecionados para mapeamento
        public List<string> SelectedMappingParameters { get; private set; }

        // ✅ NOVA PROPRIEDADE: Nível de classificação desejado
        public int ClassificationLevel { get; private set; }

        // ✅ Parâmetros disponíveis para mapeamento
        private static readonly string[] AvailableMappingParameters = new[]
        {
            "Mark",
            "Type Mark",
            "Description",
            "Type Comments",
            "Comments",
            "Material",
            "Model",
            "Manufacturer",
            "Assembly Code",
            "Assembly Description",
            "Keynote"
        };

        public ParameterSelectionForm(List<string> availableParameters, int elementCount)
        {
            InitializeComponents();

            // Popular ComboBox com parâmetros disponíveis
            cmbParameters.Items.AddRange(availableParameters.OrderBy(p => p).ToArray());

            // ✅ Popular CheckedListBox com parâmetros de mapeamento
            clbMappingParameters.Items.AddRange(AvailableMappingParameters);

            // ✅ Popular ComboBox de nível de classificação
            cmbClassificationLevel.Items.AddRange(new object[] {
                "Nível 1 (Pr)",
                "Nível 2 (Pr_15)",
                "Nível 3 (Pr_15_31)",
                "Nível 4 (Pr_15_31_05) - Máximo Detalhe"
            });
            cmbClassificationLevel.SelectedIndex = 3; // Padrão: Nível 4 (mais detalhado)

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

            // ✅ Restaurar parâmetros de mapeamento salvos
            if (settings.MappingParameters != null && settings.MappingParameters.Any())
            {
                for (int i = 0; i < clbMappingParameters.Items.Count; i++)
                {
                    string param = clbMappingParameters.Items[i].ToString();
                    if (settings.MappingParameters.Contains(param))
                    {
                        clbMappingParameters.SetItemChecked(i, true);
                    }
                }
            }
            else
            {
                // ✅ Padrão: Mark, Type Mark e Description
                SetDefaultMappingParameters();
            }

            // Configurar opções
            chkRememberChoice.Checked = settings.RememberChoice;
            chkShowProgress.Checked = settings.ShowProgress;

            // ✅ Restaurar nível de classificação salvo
            if (settings.ClassificationLevel >= 1 && settings.ClassificationLevel <= 4)
            {
                cmbClassificationLevel.SelectedIndex = settings.ClassificationLevel - 1;
            }

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

        private void SetDefaultMappingParameters()
        {
            string[] defaults = new[] { "Mark", "Type Mark", "Description" };

            for (int i = 0; i < clbMappingParameters.Items.Count; i++)
            {
                string param = clbMappingParameters.Items[i].ToString();
                if (defaults.Contains(param))
                {
                    clbMappingParameters.SetItemChecked(i, true);
                }
            }
        }

        private void InitializeComponents()
        {
            this.Text = "Classificação UniClass 2015";
            this.Width = 550;
            this.Height = 660; // ✅ Aumentado para acomodar ComboBox de nível
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
                Size = new System.Drawing.Size(500, 20),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(lblMain);
            y += 30;

            // ComboBox de parâmetros
            cmbParameters = new WinForms.ComboBox
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(500, 25),
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

            // ✅ NOVO: Label e ComboBox para nível de classificação
            lblClassificationLevel = new WinForms.Label
            {
                Text = "Nível de classificação desejado:",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(200, 20),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(lblClassificationLevel);
            y += 25;

            cmbClassificationLevel = new WinForms.ComboBox
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(500, 25),
                DropDownStyle = WinForms.ComboBoxStyle.DropDownList
            };
            this.Controls.Add(cmbClassificationLevel);
            y += 35;

            // ✅ NOVO: GroupBox para parâmetros de mapeamento
            var grpMapping = new WinForms.GroupBox
            {
                Text = "Parâmetros para Classificação",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(500, 200)
            };

            lblMappingInfo = new WinForms.Label
            {
                Text = "Selecione os parâmetros que serão usados para buscar a classificação:\n" +
                       "(Mínimo: Category, Family e Type são sempre incluídos)",
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(480, 35),
                ForeColor = System.Drawing.Color.DarkBlue
            };
            grpMapping.Controls.Add(lblMappingInfo);

            clbMappingParameters = new WinForms.CheckedListBox
            {
                Location = new System.Drawing.Point(10, 60),
                Size = new System.Drawing.Size(480, 130),
                CheckOnClick = true
            };
            grpMapping.Controls.Add(clbMappingParameters);

            this.Controls.Add(grpMapping);
            y += 210;

            // GroupBox opções
            var grpOptions = new WinForms.GroupBox
            {
                Text = "Opções de Classificação",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(500, 100)
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
                Location = new System.Drawing.Point(320, y),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = WinForms.DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);

            btnCancel = new WinForms.Button
            {
                Text = "Cancelar",
                Location = new System.Drawing.Point(420, y),
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

            // ✅ Coletar nível de classificação selecionado (1-4)
            ClassificationLevel = cmbClassificationLevel.SelectedIndex + 1;

            // ✅ Coletar parâmetros de mapeamento selecionados
            SelectedMappingParameters = new List<string>();
            foreach (var item in clbMappingParameters.CheckedItems)
            {
                SelectedMappingParameters.Add(item.ToString());
            }

            // Salvar preferências se solicitado
            if (chkRememberChoice.Checked)
            {
                var settings = Utils.SettingsManager.Load();
                settings.LastParameter = SelectedParameter;
                settings.ShowProgress = ShowProgress;
                settings.RememberChoice = true;
                settings.MappingParameters = SelectedMappingParameters;
                settings.ClassificationLevel = ClassificationLevel; // ✅ NOVO
                Utils.SettingsManager.Save(settings);
            }
        }
    }
}