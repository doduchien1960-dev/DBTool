using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using DBTool.App;
using DBTool.Cad;
using DBTool.Core;
using DBTool.IO;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.UI
{
    /// <summary>
    /// Interaction logic for MainPaletteControl.xaml
    /// Modern WPF Control hosted inside AutoCAD 2026 PaletteSet via PaletteSet.AddVisual
    /// </summary>
    public partial class MainPaletteControl : UserControl
    {
        public MainViewModel ViewModel { get; }

        public MainPaletteControl()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn tệp dữ liệu hố ga Excel",
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                ViewModel.ExcelFilePath = dlg.FileName;
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.ExcelFilePath) || !File.Exists(ViewModel.ExcelFilePath))
            {
                MessageBox.Show("Vui lòng chọn tệp Excel hợp lệ trước khi đọc dữ liệu.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ViewModel.LoadWorkbook(ViewModel.ExcelFilePath, ViewModel.SelectedSheet);
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAll(true);
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAll(false);
        }

        private void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.CurrentReadResult == null || ViewModel.Manholes.Count == 0)
                {
                    MessageBox.Show("Chưa có dữ liệu hố ga. Hãy bấm 'Đọc Dữ Liệu & Kiểm Tra' trước.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedSpecs = ViewModel.Manholes
                    .Where(m => m.IsSelected)
                    .Select(m => m.Spec)
                    .ToList();

                if (selectedSpecs.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất một hố ga để vẽ.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int blockErrors = ViewModel.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Block);
                if (blockErrors > 0)
                {
                    var res = MessageBox.Show(
                        $"Phát hiện {blockErrors} lỗi chặn (Block) trong bảng tính! Bạn có chắc chắn muốn tiếp tục vẽ không?",
                        "Cảnh Báo Chẩn Đoán",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (res != MessageBoxResult.Yes) return;
                }

                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Không tìm thấy bản vẽ AutoCAD hiện hành!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ViewModel.IsBusy = true;
                ViewModel.ProgressValue = 0;
                ViewModel.StatusMessage = "Đang tiến hành vẽ các hố ga lên ModelSpace...";

                var progress = new Progress<GenerationProgress>(p =>
                {
                    ViewModel.ProgressValue = (int)((double)p.CurrentIndex / Math.Max(1, p.TotalCount) * 100);
                    ViewModel.StatusMessage = p.StatusMessage;
                });

                // 1. Thực hiện vẽ các tờ bản vẽ lên ModelSpace
                ManholeOrchestrator.GenerateDrawings(
                    doc,
                    selectedSpecs,
                    ViewModel.CurrentReadResult,
                    ViewModel.BaseX,
                    ViewModel.BaseY,
                    progress);

                // 2. Xuất Excel chiết tính thép (nếu người dùng chọn)
                string excelExportInfo = "";
                if (ViewModel.ExportExcelSteel || ViewModel.SyncDuplicateSource)
                {
                    excelExportInfo = ExportSteelSchedules(selectedSpecs);
                }

                ViewModel.StatusMessage = $"Đã vẽ xong {selectedSpecs.Count} hố ga lên ModelSpace! {excelExportInfo}";
                doc.Editor.WriteMessage($"\n[DBTool 2026] Đã hoàn thành vẽ {selectedSpecs.Count} hố ga. {excelExportInfo}\n");
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Lỗi khi vẽ: {ex.Message}";
                MessageBox.Show($"Đã xảy ra lỗi khi tạo bản vẽ:\n{ex}", "Lỗi Thực Thi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ViewModel.IsBusy = false;
            }
        }

        private void BtnExportExcelOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.CurrentReadResult == null || ViewModel.Manholes.Count == 0)
                {
                    MessageBox.Show("Chưa có dữ liệu hố ga. Hãy bấm 'Đọc Dữ Liệu & Kiểm Tra' trước.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedSpecs = ViewModel.Manholes
                    .Where(m => m.IsSelected)
                    .Select(m => m.Spec)
                    .ToList();

                if (selectedSpecs.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất một hố ga để xuất Excel.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ViewModel.IsBusy = true;
                ViewModel.StatusMessage = "Đang chiết tính và xuất bảng thống kê cốt thép Excel...";

                string excelExportInfo = ExportSteelSchedules(selectedSpecs);

                ViewModel.StatusMessage = $"Xuất Excel thành công! {excelExportInfo}";
                MessageBox.Show($"Đã xuất bảng thống kê cốt thép thành công!\n{excelExportInfo}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Lỗi khi xuất Excel: {ex.Message}";
                MessageBox.Show($"Đã xảy ra lỗi khi xuất Excel:\n{ex}", "Lỗi Thực Thi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ViewModel.IsBusy = false;
            }
        }

        private string ExportSteelSchedules(List<NormalizedManholeSpec> selectedSpecs)
        {
            if (ViewModel.CurrentReadResult == null) return "";

            string outDir = Path.GetDirectoryName(ViewModel.ExcelFilePath) ?? Path.GetTempPath();
            var schedules = new List<(NormalizedManholeSpec Spec, SteelScheduleModel Schedule)>();

            foreach (var spec in selectedSpecs)
            {
                var geom = GeometryEngine.Build(spec);
                var bars = RebarEngine.GenerateRebarModel(spec, geom, ViewModel.CurrentReadResult.AngleCatalog);
                var sched = ScheduleEngine.CalculateSchedule(spec, bars, ViewModel.CurrentReadResult.AngleCatalog);
                schedules.Add((spec, sched));
            }

            string exportResultInfo = "";

            if (ViewModel.ExportExcelSteel)
            {
                string templateExcel = ResolveDataFormPath();
                if (File.Exists(templateExcel))
                {
                    string projectName = selectedSpecs.Count == 1 ? selectedSpecs[0].Name : $"{selectedSpecs[0].Name}_TongHop";
                    string exportedPath = ExcelSteelWriter.ExportSteelScheduleWorkbook(templateExcel, outDir, projectName, schedules);
                    exportResultInfo += $"[File: {Path.GetFileName(exportedPath)}] ";
                }
            }

            if (ViewModel.SyncDuplicateSource)
            {
                string syncPath = ExcelSteelWriter.SyncToDuplicateSourceWorkbook(ViewModel.ExcelFilePath, outDir, schedules);
                exportResultInfo += $"[Bản sao nguồn: {Path.GetFileName(syncPath)}] ";
            }

            return exportResultInfo;
        }

        private static string ResolveDataFormPath()
        {
            string[] candidates =
            {
                @"c:\Users\DucBao\Desktop\Dbtool\Ver 1\VHGpro 3.2\Template\VHG-DataForm.xlsx",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template", "VHG-DataForm.xlsx"),
                @"C:\Users\DucBao\Downloads\VHGpro 3.2\VHGpro 3.2\Template\VHG-DataForm.xlsx"
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p)) return p;
            }

            return candidates[0];
        }
    }
}
