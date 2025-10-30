using System;
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
    
        public ProgressForm()
        {
            InitializeComponents();
        }
 
        private void InitializeComponents()
        {
            this.Text = "Progresso da Classificação";
            this.Width = 700;
            this.Height = 500;
            this.FormBorderStyle = WinForms.FormBorderStyle.Sizable;
            this.StartPosition = WinForms.FormStartPosition.CenterScreen;
        
            int y = 20;
        
            // Label status
            lblStatus = new WinForms.Label
            {
                Text = "Processando...",
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(640, 20),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(lblStatus);
            y += 30;
            
            // ProgressBar
            progressBar1 = new WinForms.ProgressBar
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(500, 23),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar1);
       
            // Label contador
            lblCurrent = new WinForms.Label
            {
                Text = "0 / 0",
                Location = new System.Drawing.Point(530, y),
                Size = new System.Drawing.Size(130, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            };
            this.Controls.Add(lblCurrent);
            y += 35;
    
            // DataGridView resultados
            dgvResults = new WinForms.DataGridView
            {
                Location = new System.Drawing.Point(20, y),
                Size = new System.Drawing.Size(640, 280),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = WinForms.DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = WinForms.DataGridViewAutoSizeColumnsMode.Fill
            };
   
            dgvResults.Columns.Add("ElementId", "ID");
            dgvResults.Columns.Add("Name", "Nome");
            dgvResults.Columns.Add("Classification", "Classificação");
            dgvResults.Columns.Add("Confidence", "Confiança");
            dgvResults.Columns.Add("Status", "Status");
       
            dgvResults.Columns[0].FillWeight = 10;
            dgvResults.Columns[1].FillWeight = 25;
            dgvResults.Columns[2].FillWeight = 40;
            dgvResults.Columns[3].FillWeight = 15;
            dgvResults.Columns[4].FillWeight = 10;
    
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
                Location = new System.Drawing.Point(430, y),
                Size = new System.Drawing.Size(110, 30),
                Enabled = false
            };
            btnExport.Click += BtnExport_Click;
            this.Controls.Add(btnExport);
            
            // Botão Cancel
            btnCancel = new WinForms.Button
            {
                Text = "Cancelar",
                Location = new System.Drawing.Point(550, y),
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
      
            // Adicionar linha ao grid
   var row = new object[] 
          {
       element.Id.IntegerValue,
     element.Name,
         result.Code,
            $"{result.Confidence}%",
         result.Confidence > 70 ? "?" : "?"
};
            dgvResults.Rows.Add(row);
      
    // Auto-scroll para última linha
          if (dgvResults.Rows.Count > 0)
       dgvResults.FirstDisplayedScrollingRowIndex = dgvResults.Rows.Count - 1;
    
            WinForms.Application.DoEvents();
 }
    
     public void ShowSummary()
        {
 int success = 0;
     foreach (WinForms.DataGridViewRow row in dgvResults.Rows)
            {
             if (row.Cells[4].Value.ToString() == "?")
      success++;
            }
        
     int total = dgvResults.Rows.Count;
     
            lblStatus.Text = $"Concluído: {success}/{total} classificados com sucesso";
          btnExport.Enabled = true;
            btnCancel.Text = "Fechar";
   
         if (chkAutoClose.Checked && success == total)
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
            // Cabeçalho
                  writer.WriteLine("ID,Nome,Classificação,Confiança,Status");
     
        // Dados
              foreach (WinForms.DataGridViewRow row in dgvResults.Rows)
        {
    writer.WriteLine($"{row.Cells[0].Value},{row.Cells[1].Value},{row.Cells[2].Value},{row.Cells[3].Value},{row.Cells[4].Value}");
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
