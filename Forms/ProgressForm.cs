using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using CoordinatorPro.Models;
using WinForms = System.Windows.Forms;

namespace CoordinatorPro.Forms
{
    public partial class ProgressForm : WinForms.Form
    {
        private WinForms.ProgressBar progressBar1;
        private WinForms.Label lblStatus;
        private WinForms.Label lblCurrent;
        private WinForms.DataGridView dgvResults;
        private WinForms.Button btnCancel;
        private WinForms.Button btnExport;
        private WinForms.CheckBox chkAutoClose;

        private bool _cancelled = false;
        public bool Cancelled => _cancelled;

        // ✅ NOVO: Armazenar parâmetros de mapeamento
        private List<string> _mappingParameters;
        private Document _document;

        public ProgressForm(List<string> mappingParameters = null)
        {
            _mappingParameters = mappingParameters ?? new List<string>();
            InitializeComponents();
        }

        public void SetDocument(Document doc)
        {
            _document = doc;
        }

        private void InitializeComponents()
        {
            this.Text = "Progresso da Classificação";
            this.Width = 900; // ✅ Aumentado para acomodar mais colunas
            this.Height = 500;
            this.FormBorderStyle = WinForms.FormBorderStyle.Sizable;
            this.StartPosition = WinForms.FormStartPosition.CenterScreen;

            int y = 20;

            // Label status
            lblStatus = new WinForms.Label
            {
                Text = "Processando...",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(840, 20),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(lblStatus);
            y += 30;

            // ProgressBar
            progressBar1 = new WinForms.ProgressBar
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(700, 23),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar1);

            // Label contador
            lblCurrent = new WinForms.Label
            {
                Text = "0 / 0",
                Location = new System.Drawing.Point(730, y),
                Size = new System.Drawing.Size(130, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            };
            this.Controls.Add(lblCurrent);
            y += 35;

            // DataGridView resultados
            dgvResults = new WinForms.DataGridView
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(840, 280),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = WinForms.DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = WinForms.DataGridViewAutoSizeColumnsMode.Fill
            };

            // ✅ Colunas fixas
            dgvResults.Columns.Add("ElementId", "ID");
            dgvResults.Columns.Add("Name", "Nome");

            // ✅ NOVO: Adicionar colunas para parâmetros customizados
            if (_mappingParameters != null && _mappingParameters.Any())
            {
                foreach (var param in _mappingParameters)
                {
                    dgvResults.Columns.Add($"Param_{param}", param);
                }
            }

            dgvResults.Columns.Add("Classification", "Classificação");
            dgvResults.Columns.Add("Confidence", "Confiança");
            dgvResults.Columns.Add("Status", "Status");

            // ✅ Ajustar larguras
            dgvResults.Columns["ElementId"].FillWeight = 8;
            dgvResults.Columns["Name"].FillWeight = 20;

            // Parâmetros customizados
            if (_mappingParameters != null && _mappingParameters.Any())
            {
                foreach (var param in _mappingParameters)
                {
                    dgvResults.Columns[$"Param_{param}"].FillWeight = 12;
                }
            }

            dgvResults.Columns["Classification"].FillWeight = 35;
            dgvResults.Columns["Confidence"].FillWeight = 10;
            dgvResults.Columns["Status"].FillWeight = 8;

            this.Controls.Add(dgvResults);
            y += 290;

            // CheckBox auto close
            chkAutoClose = new WinForms.CheckBox
            {
                Text = "Fechar ao concluir",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(200, 20)
            };
            this.Controls.Add(chkAutoClose);

            // Botão Export
            btnExport = new WinForms.Button
            {
                Text = "Exportar Log",
                Location = new System.Drawing.Point(630, y),
                Size = new System.Drawing.Size(110, 30),
                Enabled = false
            };
            btnExport.Click += BtnExport_Click;
            this.Controls.Add(btnExport);

            // Botão Cancel
            btnCancel = new WinForms.Button
            {
                Text = "Cancelar",
                Location = new System.Drawing.Point(750, y),
                Size = new System.Drawing.Size(110, 30)
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);
        }

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

            // ✅ Criar lista de valores para a linha
            var rowValues = new List<object>
            {
                element.Id.IntegerValue,
                element.Name
            };

            // ✅ NOVO: Adicionar valores dos parâmetros customizados
            if (_mappingParameters != null && _mappingParameters.Any())
            {
                foreach (var paramName in _mappingParameters)
                {
                    string paramValue = GetParameterValue(element, paramName);
                    rowValues.Add(string.IsNullOrEmpty(paramValue) ? "-" : paramValue);
                }
            }

            // Adicionar classificação, confiança e status
            rowValues.Add(result.Code);
            rowValues.Add($"{result.Confidence}%");

            // ✅ CORRIGIDO: Status baseado na confiança
            string status;
            if (result.Confidence >= 70)
                status = "✓"; // Sucesso
            else if (result.Confidence > 0)
                status = "⚠"; // Parcial
            else
                status = "✗"; // Falha

            rowValues.Add(status);

            dgvResults.Rows.Add(rowValues.ToArray());

            // Auto-scroll para última linha
            if (dgvResults.Rows.Count > 0)
                dgvResults.FirstDisplayedScrollingRowIndex = dgvResults.Rows.Count - 1;

            WinForms.Application.DoEvents();
        }

        /// <summary>
        /// ✅ NOVO: Obtém valor de parâmetro do elemento
        /// </summary>
        private string GetParameterValue(Element element, string paramName)
        {
            try
            {
                // Verificar se é parâmetro do Type
                if (paramName.StartsWith("[Type] "))
                {
                    string realName = paramName.Replace("[Type] ", "");
                    if (_document != null)
                    {
                        Element typeElement = _document.GetElement(element.GetTypeId());
                        if (typeElement != null)
                        {
                            Parameter param = typeElement.LookupParameter(realName);
                            if (param != null && param.HasValue)
                            {
                                return param.AsValueString() ?? param.AsString() ?? "";
                            }
                        }
                    }
                }
                else
                {
                    // Parâmetro de instância
                    Parameter param = element.LookupParameter(paramName);
                    if (param != null && param.HasValue)
                    {
                        return param.AsValueString() ?? param.AsString() ?? "";
                    }
                }
            }
            catch
            {
                // Ignorar erros
            }

            return "";
        }

        public void ShowSummary()
        {
            // ✅ CORRIGIDO: Contar baseado em status correto
            int success = 0;
            int partial = 0;
            int failed = 0;

            foreach (WinForms.DataGridViewRow row in dgvResults.Rows)
            {
                string status = row.Cells[row.Cells.Count - 1].Value?.ToString() ?? "";

                if (status == "✓")
                    success++;
                else if (status == "⚠")
                    partial++;
                else if (status == "✗")
                    failed++;
            }

            int total = dgvResults.Rows.Count;

            lblStatus.Text = $"Concluído: {success} sucesso, {partial} parcial, {failed} falha (Total: {total})";
            btnExport.Enabled = true;
            btnCancel.Text = "Fechar";

            if (chkAutoClose.Checked && failed == 0)
            {
                System.Threading.Thread.Sleep(1000);
                Close();
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (btnCancel.Text == "Cancelar")
            {
                _cancelled = true;
                lblStatus.Text = "Cancelando...";
            }
            else
            {
                Close();
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                var sfd = new WinForms.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"UniClass_Classification_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == WinForms.DialogResult.OK)
                {
                    using (var writer = new System.IO.StreamWriter(sfd.FileName))
                    {
                        // ✅ Cabeçalho dinâmico
                        var headers = new List<string> { "ID", "Nome" };

                        if (_mappingParameters != null && _mappingParameters.Any())
                        {
                            headers.AddRange(_mappingParameters);
                        }

                        headers.AddRange(new[] { "Classificação", "Confiança", "Status" });
                        writer.WriteLine(string.Join(",", headers));

                        // Dados
                        foreach (WinForms.DataGridViewRow row in dgvResults.Rows)
                        {
                            var values = new List<string>();
                            foreach (WinForms.DataGridViewCell cell in row.Cells)
                            {
                                string value = cell.Value?.ToString() ?? "";
                                // Escapar vírgulas e aspas
                                if (value.Contains(",") || value.Contains("\""))
                                {
                                    value = $"\"{value.Replace("\"", "\"\"")}\"";
                                }
                                values.Add(value);
                            }
                            writer.WriteLine(string.Join(",", values));
                        }
                    }

                    WinForms.MessageBox.Show("Log exportado com sucesso!", "Sucesso",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show($"Erro ao exportar: {ex.Message}", "Erro",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }
    }
}